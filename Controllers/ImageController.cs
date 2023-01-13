using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

using AppImageGenerator.Models;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;

using Microsoft.AspNetCore.Mvc;

using System.Net.Http.Headers;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Net.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AppImageGenerator.Controllers
{
    [ApiController]
    [Route("api/")]
    
    public class ImageController : ControllerBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ImageController (IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet("getImagesZipById", Name = "getImagesZipById")]
        public async Task<ActionResult> Get(string id)
        {
            try
            {
                // Create path from the id and return the file...
                string zipFilePath = CreateFilePathFromId(new Guid(id));
                if (string.IsNullOrEmpty(zipFilePath))
                {
                    var response = new NotFoundResult();
                    return new NotFoundResult();
                }

                var archive = File(await System.IO.File.ReadAllBytesAsync(zipFilePath), "application/octet-stream", fileDownloadName: "AppImages.zip");

                System.IO.File.Delete(zipFilePath);

                return archive;
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex.ToString()) { StatusCode = (int?)HttpStatusCode.InternalServerError };
            }
        }

        /// <summary>
        /// Generates a list of images from the specified base image.
        /// Expected arguments:
        /// - baseImage: the image as a multipart form POST. This is the image from which all other images will be generated. Should generally be 512x512 or larger, ideally in PNG format.
        /// - platform: a list of values specifying the platform(s) for which the images are being generated, e.g. "windows10"
        /// - padding: a value between 0 and 1 specifying the padding for the generated images.
        /// - color: a hex color value to use as the background color of the generated images. If null, a best guess -- the color of pixel (0,0) -- will be used.
        /// </summary>
        /// <returns></returns>
        [HttpPost("generateImagesZip")]
        public async Task<ActionResult> Post([FromForm] ImageFormData Form)
        {
            var zipId = Guid.NewGuid();

            try
            {
                using (var args = ImageGenerationModel.FromFormData(HttpContext.Request.Form, HttpContext.Request.Form.Files))
                {
                    // Punt if we have invalid arguments.
                    if (!string.IsNullOrEmpty(args.ErrorMessage))
                    {
                        return new ObjectResult(args.ErrorMessage) { StatusCode = (int?)HttpStatusCode.BadRequest };
                    }

                    var profiles = GetProfilesFromPlatforms(args.Platforms);
                    var imageStreams = new List<Stream>(profiles.Count);

                    using (var zip = ZipFile.Open(CreateFilePathFromId(zipId), ZipArchiveMode.Create))
                    {
                        var iconObject = new IconRootObject();
                        foreach (var profile in profiles)
                        {
                            var stream = CreateImageStream(args, profile);
                            imageStreams.Add(stream);
                            var fmt = string.IsNullOrEmpty(profile.Format) ? "png" : profile.Format;
                            var iconEntry = zip.CreateEntry(profile.Name + "." + fmt, CompressionLevel.Fastest);
                            var iconStream = iconEntry.Open();
                            await stream.CopyToAsync(iconStream);
                            iconStream.Close();
                            stream.Close();
        
                            iconObject.icons.Add(new IconObject(profile.Folder + profile.Name + "." + fmt, profile.Width + "x" + profile.Height));
                        }

                        var iconStr = JsonConvert.SerializeObject(iconObject, Formatting.Indented);

                        using (StreamWriter writer = new StreamWriter(zip.CreateEntry("icons.json", CompressionLevel.Optimal).Open()))
                        {
                            writer.Write(iconStr);
                        }

                        string zipFilePath = CreateFilePathFromId(zipId);
                        imageStreams.ForEach(s => s.Dispose());
                    }
                }
            }
            catch (OutOfMemoryException ex)
            {
                return new ObjectResult(ex.ToString()) { StatusCode = (int?)HttpStatusCode.UnsupportedMediaType };
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex.ToString()) { StatusCode = (int?)HttpStatusCode.InternalServerError };
            }

            // Send back a route to download the zip file.
            var url = Url.RouteUrl("getImagesZipById", new { id = zipId.ToString() });
            //var uri = new Uri(url, UriKind.Relative);
            //var responseMessage = new HttpResponseMessage(HttpStatusCode.Created);
            ////responseMessage.Content = new StringContent(JsonConvert.SerializeObject(new ImageResponse { Uri = uri }));
            //responseMessage.Headers.Location = uri;
            //responseMessage.Headers.Add("X-Zip-Id", zipId.ToString());

            return new RedirectResult(url);
        }

        // Same as Post, but additionally downloads the file
        //[HttpPost("generateAndDownloadImagesZip")]
        //public async Task<ActionResult> Download([FromForm] ImageFormData Form)
        //{
        //    var postResponse = await Post(Form);
        //    if (postResponse.StatusCode == HttpStatusCode.Created)
        //    {
        //        if (postResponse.Headers.TryGetValues("X-Zip-Id", out var vals))
        //        {
        //            var zipId = vals.FirstOrDefault();
        //            if (zipId != null)
        //            {
        //                return Get(zipId);
        //            }
        //        }
        //    }

        //    return new StatusCodeResult((int)postResponse.StatusCode);
        //}

        
        [HttpPost("generateBase64Image")]
        public async Task<ActionResult> Base64([FromForm] ImageFormData Form)
        {

            using (var args = ImageGenerationModel.FromFormData(HttpContext.Request.Form, HttpContext.Request.Form.Files))
            {
                if (!string.IsNullOrEmpty(args.ErrorMessage))
                {
                    return new ObjectResult(args.ErrorMessage) { StatusCode = (int?)HttpStatusCode.BadRequest };
                }

                var imgs = GetProfilesFromPlatforms(args.Platforms)
                    .Select(profile => new WebManifestIcon
                    {
                        Purpose = "any",
                        Sizes = $"{profile.Width}x{profile.Height}",
                        Src = CreateBase64Image(args, profile),
                        Type = string.IsNullOrEmpty(profile.Format) ? "image/png" : profile.Format
                    });

                var response = new ObjectResult(JsonConvert.SerializeObject(imgs));
                return response;
            }
        }

        private static string ReadStringFromConfigFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            {
                return sr.ReadToEnd();
            }
        }

        private IEnumerable<string> GetConfig(string platformId)
        {
            List<string> config = new List<string>();
            string webRootPath = _webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath;
            var root = Path.Combine(webRootPath, "App_Data");

            string filePath = Path.Combine(root, platformId + "Images.json");
            config.Add(ReadStringFromConfigFile(filePath));
            return config;
        }

        private IReadOnlyList<Profile> GetProfilesFromPlatforms(IEnumerable<string> platforms)
        {
            List<Profile> profiles = null;
            foreach (var platform in platforms)
            {
                // Get the platform and profiles
                var config = GetConfig(platform);
                if (config.Count() < 1)
                {
                    throw new HttpRequestException(HttpStatusCode.BadRequest.ToString());
                }

                foreach (var cfg in config)
                {
                    if (profiles == null)
                    {
                        profiles = JsonConvert.DeserializeObject<List<Profile>>(cfg);
                    }
                    else
                    {
                        profiles.AddRange(JsonConvert.DeserializeObject<List<Profile>>(cfg));
                    }
                }
            }

            return profiles;
        }

        private string CreateFilePathFromId(Guid id)
        {
            string webRootPath = _webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath;
            var root = Path.Combine(webRootPath, "App_Data");
      /*      string root = HttpContextHelper.Current.Server.MapPath("~/App_Data");*/
            string zipFilePath = Path.Combine(root, id + ".zip");
            return zipFilePath;
        }

        private static IImageEncoder getEncoderFromType(string type)
        {
            if (!string.IsNullOrEmpty(type))
            {
                if (new Regex(type).IsMatch("png"))
                    return new PngEncoder();
                if (new Regex(type).IsMatch("jpeg") || new Regex(type).IsMatch("jpg"))
                    return new JpegEncoder();
                if (new Regex(type).IsMatch("webp"))
                    return new WebpEncoder();
                if (new Regex(type).IsMatch("bmp"))
                    return new BmpEncoder();
                if (new Regex(type).IsMatch("tiff"))
                    return new TiffEncoder();
            }

            return new PngEncoder();
        }

        private static MemoryStream CreateImageStream(ImageGenerationModel model, Profile profile)
        {
            // We the individual image has padding specified, used that.
            // Otherwise, use the general padding passed into the model.
            var padding = profile.Padding ?? model.Padding;

            var imageEncoder = getEncoderFromType(profile.Format);

            if (model.SvgFormData != null)
            {
                return ImageGenerationModel.ProcessSvgToStream(model.SvgFormData, profile.Width, profile.Height, imageEncoder, padding, model.BackgroundColor);
            }
            else if (model.BaseImage != null)
            {
                return  ImageGenerationModel.ProcessImageToStream(model.BaseImage, profile.Width, profile.Height, imageEncoder, padding, model.BackgroundColor);
            }

            return null;
        }

        private static string CreateBase64Image(ImageGenerationModel model, Profile profile)
        {
            var formatOrPng = string.IsNullOrEmpty(profile.Format) ? "image/png" : profile.Format;
            using (var imgStream = CreateImageStream(model, profile))
            {
                var base64 = Convert.ToBase64String(imgStream.ToArray());
                return $"data:{formatOrPng};base64,{base64}";
            }
        }
    }

    public class ImageResponse
    {
        public Uri Uri { get; set; }
    }


    public class JsonResponse
    {
        public string Json { get; set; }
    }

    public class IconObject
    {
        public IconObject(string src, string size)
        {
            this.src = src;
            this.sizes = size;
        }

        public string src { get; set; }

        public string sizes { get; set; }
    }

    public class IconRootObject
    {
        public List<IconObject> icons { get; set; } = new List<IconObject>();
    }

    public class ImageFormData
    {
        [Required]
        public IFormFile fileName { get; set; }

        [Required]
        [DefaultValue(Platform.android)]
        public Platform platform { get; set; } /* "android" | "chrome "| "firefox" | "ios" | "msteams" | "windows10" | "windows11" */
        public string? padding { get; set; }

        public string? color { get; set; }
    }

    public enum Platform {
        android,
        chrome, firefox, ios, msteams, windows10, windows11 }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using Ionic.Zip;
using Newtonsoft.Json;

using Svg;
using Svg.Transforms;
using WWA.WebUI.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SkiaSharp;
using Svg.Skia;
using SKSvg = Svg.Skia.SKSvg;

using System.Windows;
using Microsoft.SqlServer.Server;
using System.Windows.Interop;
using ShimSkiaSharp;
using SKImage = SkiaSharp.SKImage;
using SKSizeI = SkiaSharp.SKSizeI;
using System.Runtime.InteropServices.ComTypes;
using SixLabors.ImageSharp.Formats;
using Size = SixLabors.ImageSharp.Size;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;

namespace WWA.WebUI.Controllers
{
    public class ImageController : ApiController
    {
        public HttpResponseMessage Get(string id)
        {
            HttpResponseMessage httpResponseMessage;
            try
            {
                // Create path from the id and return the file...
                string zipFilePath = CreateFilePathFromId(new Guid(id));
                if (string.IsNullOrEmpty(zipFilePath))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("Can't find {0}", id));
                }

                httpResponseMessage = Request.CreateResponse();
                httpResponseMessage.Content = new ByteArrayContent(File.ReadAllBytes(zipFilePath));
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                httpResponseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "AppImages.zip"
                };

                File.Delete(zipFilePath);
            }
            catch (Exception ex)
            {
                httpResponseMessage = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
                return httpResponseMessage;
            }

            return httpResponseMessage;
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
        public async Task<HttpResponseMessage> Post()
        {
            var root = HttpContext.Current.Server.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);
            var zipId = Guid.NewGuid();

            try
            {
                // Read the arguments.
                await Request.Content.ReadAsMultipartAsync(provider);
                using (var args = ImageGenerationModel.FromFormData(provider.FormData, provider.FileData))
                {
                    // Punt if we have invalid arguments.
                    if (!string.IsNullOrEmpty(args.ErrorMessage))
                    {
                        var request = Request.CreateErrorResponse(HttpStatusCode.BadRequest, args.ErrorMessage);
                        request.ReasonPhrase = args.ErrorMessage;
                        return request;
                    }

                    var profiles = GetProfilesFromPlatforms(args.Platforms);
                    var imageStreams = new List<Stream>(profiles.Count);
                    using (var zip = new ZipFile())
                    {
                        var iconObject = new IconRootObject();
                        foreach (var profile in profiles)
                        {
                            var stream = CreateImageStream(args, profile);
                            imageStreams.Add(stream);
                            var fmt = string.IsNullOrEmpty(profile.Format) ? "png" : profile.Format;
                            zip.AddEntry(profile.Folder + profile.Name + "." + fmt, stream);
                            stream.Flush();
                            iconObject.icons.Add(new IconObject(profile.Folder + profile.Name + "." + fmt, profile.Width + "x" + profile.Height));
                        }

                        var iconStr = JsonConvert.SerializeObject(iconObject, Formatting.Indented);

                        zip.AddEntry("icons.json", iconStr);

                        string zipFilePath = CreateFilePathFromId(zipId);
                        zip.Save(zipFilePath);
                        imageStreams.ForEach(s => s.Dispose());
                    }
                }
            }
            catch (OutOfMemoryException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.UnsupportedMediaType, ex);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            // Send back a route to download the zip file.
            var url = Url.Route("DefaultApi", new { controller = "image", id = zipId.ToString() });
            var uri = new Uri(url, UriKind.Relative);
            var responseMessage = Request.CreateResponse(HttpStatusCode.Created, new ImageResponse { Uri = uri });
            responseMessage.Headers.Location = uri;
            responseMessage.Headers.Add("X-Zip-Id", zipId.ToString());
            return responseMessage;
        }

        // Same as Post, but additionally downloads the file
        public async Task<HttpResponseMessage> Download()
        {
            var postResponse = await Post();
            if (postResponse.StatusCode == HttpStatusCode.Created)
            {
                if (postResponse.Headers.TryGetValues("X-Zip-Id", out var vals))
                {
                    var zipId = vals.FirstOrDefault();
                    if (zipId != null)
                    {
                        return Get(zipId);
                    }
                }
            }

            return postResponse;
        }

        public async Task<HttpResponseMessage> Base64()
        {
            var root = HttpContext.Current.Server.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);

            // Grab the args.
            await Request.Content.ReadAsMultipartAsync(provider);
            using (var args = ImageGenerationModel.FromFormData(provider.FormData, provider.FileData))
            {
                if (!string.IsNullOrEmpty(args.ErrorMessage))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, args.ErrorMessage);
                }

                var imgs = GetProfilesFromPlatforms(args.Platforms)
                    .Select(profile => new WebManifestIcon
                    {
                        Purpose = "any",
                        Sizes = $"{profile.Width}x{profile.Height}",
                        Src = CreateBase64Image(args, profile),
                        Type = string.IsNullOrEmpty(profile.Format) ? "image/png" : profile.Format
                    });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(imgs))
                };
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
            string root = HttpContext.Current.Server.MapPath("~/App_Data");
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
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
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
            string root = HttpContext.Current.Server.MapPath("~/App_Data");
            string zipFilePath = Path.Combine(root, id + ".zip");
            return zipFilePath;
        }

        private static IImageEncoder getEncoderFromType(string type)
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

            return new PngEncoder();
        }

        private static MemoryStream CreateImageStream(ImageGenerationModel model, Profile profile)
        {
            // We the individual image has padding specified, used that.
            // Otherwise, use the general padding passed into the model.
            var padding = profile.Padding ?? model.Padding;

            var imageEncoder = getEncoderFromType(profile.Format);

            if (model.SvgFileName != null)
            {
                return RenderSvgToStream(model.SvgFileName, profile.Width, profile.Height, imageEncoder, padding, model.BackgroundColor);
            }
            else
            {
                return ResizeImage(model.BaseImage, profile.Width, profile.Height, imageEncoder, padding, model.BackgroundColor);
            }
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


        public static MemoryStream RenderSvgToStream(string filePath, int width, int height, IImageEncoder imageEncoder, double? padding, Color? backgroundColor = null)
        {
            using (var svg = new SKSvg( ))
            {
                if (svg.Load(filePath) != null)
                {
                    using (SKImage image = SKImage.FromPicture(svg.Picture, new SKSizeI((int)(Convert.ToDouble(width) - padding * 2), (int)(Convert.ToDouble(height) - padding * 2))))
                    {
                        var stream = new MemoryStream();

                        // Save the image to the stream in the specified format
                        using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            data.SaveTo(stream);
                        }

                        // Reset the stream position to the beginning
                        stream.Position = 0;

                        Image image2 = Image.Load(stream);
                        image2.Mutate(x => x.Pad(width, height));

                        if (backgroundColor != null)
                            image2.Mutate(x => x.BackgroundColor((Color)backgroundColor));

                        stream.Position = 0;
                        image2.Save(stream, imageEncoder);


                        return stream;
                    }
                }
                else
                    return null;
            }
        }

        private static MemoryStream ResizeImage(Image image, int newWidth, int newHeight, IImageEncoder imageEncoder, double paddingProp = 0.3, Color? bg = null)
        {
            int adjustWidth;
            int adjustedHeight;
            int paddingW;
            int paddingH;
            if (paddingProp > 0)
            {
                paddingW = (int)(paddingProp * newWidth * 0.5);
                adjustWidth = newWidth - paddingW;
                paddingH = (int)(paddingProp * newHeight * 0.5);
                adjustedHeight = newHeight - paddingH;
            }
            else
            {
                paddingW = paddingH = 0;
                adjustWidth = newWidth;
                adjustedHeight = newHeight;
            }

            image.Mutate(x => x.Resize(adjustWidth, adjustedHeight, KnownResamplers.Lanczos3));

            if (paddingProp > 0)
                image.Mutate(x => x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        Mode = ResizeMode.Pad
                    }).BackgroundColor((Color)bg));

            var memoryStream = new MemoryStream();
            image.Save(memoryStream, imageEncoder);
     

            return memoryStream;
        }
    }

    public class ImageResponse
    {
        public Uri Uri { get; set; }
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
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, args.ErrorMessage);
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

        private static MemoryStream CreateImageStream(ImageGenerationModel model, Profile profile)
        {
            // We the individual image has padding specified, used that.
            // Otherwise, use the general padding passed into the model.
            var padding = profile.Padding ?? model.Padding;

            if (model.SvgFileName != null)
            {
                return RenderSvgToStream(model.SvgFileName, profile.Width, profile.Height, profile.Format, padding, model.BackgroundColor);
            }
            else
            {
                return ResizeImage(model.BaseImage, profile.Width, profile.Height, profile.Format, padding, model.BackgroundColor);
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

        private static MemoryStream RenderSvgToStream(string filename, int width, int height, string fmt, double paddingProp = 0.3, Color? bg = null)
        {
            var displaySize = new Size(width, height);

            SvgDocument svgDoc = SvgDocument.Open(filename);
            RectangleF svgSize = RectangleF.Empty;
            try
            {
                svgSize.Width = svgDoc.GetDimensions().Width;
                svgSize.Height = svgDoc.GetDimensions().Height;
            }
            catch (Exception)
            { }

            if (svgSize == RectangleF.Empty)
            {
                svgSize = new RectangleF(0, 0, svgDoc.ViewBox.Width, svgDoc.ViewBox.Height);
            }

            if (svgSize.Width == 0)
            {
                throw new Exception("SVG does not have size specified. Cannot work with it.");
            }

            var displayProportion = (displaySize.Height * 1.0f) / displaySize.Width;
            var svgProportion = svgSize.Height / svgSize.Width;

            float scalingFactor = 0f;
            int padding = 0; 

            // if display is proportionally narrower than svg 
            if (displayProportion > svgProportion)
            {
                padding = (int)(paddingProp * width * 0.5);
                // we pick the width of display as max and compute the scaling against that. 
                scalingFactor = ((displaySize.Width - padding * 2) * 1.0f) / svgSize.Width;
            }
            else
            {
                padding = (int)(paddingProp * height * 0.5);
                // we pick the height of display as max and compute the scaling against that. 
                scalingFactor = ((displaySize.Height - padding * 2) * 1.0f) / svgSize.Height;
            }

            if (scalingFactor < 0)
            {
                throw new Exception("Viewing area is too small to render the image");
            }

            // When proportions of drawing do not match viewing area, it's nice to center the drawing within the viewing area. 
            int centeringX = Convert.ToInt16((displaySize.Width - (padding + svgDoc.Width * scalingFactor)) / 2);
            int centeringY = Convert.ToInt16((displaySize.Height - (padding + svgDoc.Height * scalingFactor)) / 2);

            // Remove the "+ centering*" to avoid growing and padding the Bitmap with transparent fill. 
            svgDoc.Transforms = new SvgTransformCollection();
            svgDoc.Transforms.Add(new SvgTranslate(padding + centeringX, padding + centeringY));
            svgDoc.Transforms.Add(new SvgScale(scalingFactor));

            // This keeps the size of bitmap fixed to stated viewing area. Image is padded with transparent areas. 
            svgDoc.Width = new SvgUnit(svgDoc.Width.Type, displaySize.Width);
            svgDoc.Height = new SvgUnit(svgDoc.Height.Type, displaySize.Height);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (bg != null)
                g.Clear((Color)bg);

            svgDoc.Draw(g);

            var memoryStream = new MemoryStream();
            ImageFormat imgFmt = (fmt == "jpg") ? ImageFormat.Jpeg : ImageFormat.Png;
            bitmap.Save(memoryStream, imgFmt);
            memoryStream.Position = 0;

            return memoryStream;
        }

        private static MemoryStream ResizeImage(Image image, int newWidth, int newHeight, string fmt, double paddingProp = 0.3, Color? bg = null)
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

            int width = image.Size.Width;
            int height = image.Size.Height;

            double ratioW = (double)adjustWidth / width;
            double ratioH = (double)adjustedHeight / height;

            double scaleFactor = ratioH > ratioW ? ratioW : ratioH;

            var scaledHeight = (int)(height * scaleFactor);
            var scaledWidth = (int)(width * scaleFactor);

            double originX = ratioH > ratioW ? paddingW * 0.5 : newWidth * 0.5 - scaledWidth * 0.5;
            double originY = ratioH > ratioW ? newHeight * 0.5 - scaledHeight * 0.5 : paddingH * 0.5;

            var srcBmp = new Bitmap(image);
            Color pixel = bg != null ? (Color)bg : srcBmp.GetPixel(0, 0);

            var bitmap = new Bitmap(newWidth, newHeight, srcBmp.PixelFormat);
            Graphics g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            g.Clear(pixel);

            var dstRect = new Rectangle((int)originX, (int)originY, scaledWidth, scaledHeight);

            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, dstRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, ia);
            }

            var memoryStream = new MemoryStream();
            ImageFormat imgFmt = (fmt == "jpg") ? ImageFormat.Jpeg : ImageFormat.Png;
            bitmap.Save(memoryStream, imgFmt);
            memoryStream.Position = 0;

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

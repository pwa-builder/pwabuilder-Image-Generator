using CsPotrace;
using Fizzler;
using Ionic.Zip;
using Newtonsoft.Json;
using Svg;
using Svg.Transforms;
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
using System.Xml;

namespace WWA.WebUI.Controllers
{
    #region input classes
    public class Profile
    {
        [DataMember(Name = "width")]
        public int Width { get; set; }

        [DataMember(Name = "height")]
        public int Height { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "desc")]
        public string Desc { get; set; }

        [DataMember(Name = "folder")]
        public string Folder { get; set; }

        [DataMember(Name = "format")]
        public string Format { get; set; }

        [DataMember(Name = "silhouette")]
        public bool Silhouette { get; set; }
    }
    #endregion

    public class ImageController : ApiController
    {
        #region GET api/image
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
        #endregion


        #region POST api/image
        public async Task<HttpResponseMessage> Post()
        {
            string root = HttpContext.Current.Server.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);
            Guid zipId = Guid.NewGuid();

            try
            {
                // Read the form data.
                await Request.Content.ReadAsMultipartAsync(provider);

                MultipartFileData multipartFileData = provider.FileData.First();

                using (var model = new IconModel())
                {
                    var ct = multipartFileData.Headers.ContentType.MediaType;
                    if (ct != null && ct.Contains("svg"))
                    {
                        model.SvgFile = multipartFileData.LocalFileName;
                    }
                    else
                    {
                        model.InputImage = Image.FromFile(multipartFileData.LocalFileName);
                    }
                    model.Padding = Convert.ToDouble(provider.FormData.GetValues("padding")[0]);
                    if (model.Padding < 0 || model.Padding > 1.0)
                    {
                        // Throw out as user has supplied invalid hex string..
                        HttpResponseMessage httpResponseMessage =
                            Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Padding value invalid. Please input a number between 0 and 1");
                        return httpResponseMessage;
                    }

                    var colorStr = provider.FormData.GetValues("color")?[0];
                    var colorChanged = provider.FormData.GetValues("colorChanged")?[0] == "1";

                    if (!string.IsNullOrEmpty(colorStr) && colorChanged)
                    {
                        try
                        {
                            var colorConverter = new ColorConverter();
                            model.Background = (Color)colorConverter.ConvertFromString(colorStr);
                        }
                        catch (Exception ex)
                        {
                            // Throw out as user has supplied invalid hex string..
                            HttpResponseMessage httpResponseMessage =
                                Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Background Color value invalid. Please input a valid hex color.", ex);
                            return httpResponseMessage;
                        }
                    }

                    var platforms = provider.FormData.GetValues("platform");

                    if (platforms == null)
                    {
                        // Throw out as user has supplied no platforms..
                        HttpResponseMessage httpResponseMessage =
                            Request.CreateErrorResponse(HttpStatusCode.BadRequest, "No platform has been specified.");
                        return httpResponseMessage;
                    }

                    model.Platforms = platforms;

                    List<Profile> profiles = null;

                    foreach (var platform in model.Platforms)
                    {
                        // Get the platform and profiles
                        IEnumerable<string> config = GetConfig(platform);
                        if (config.Count() < 1)
                        {
                            throw new HttpResponseException(HttpStatusCode.BadRequest);
                        }

                        foreach (var cfg in config)
                        {
                            if (profiles == null)
                                profiles = JsonConvert.DeserializeObject<List<Profile>>(cfg);
                            else
                                profiles.AddRange(JsonConvert.DeserializeObject<List<Profile>>(cfg));
                        }
                    }

                    using (var zip = new ZipFile())
                    {
                        var iconObject = new IconRootObject();
                        foreach (var profile in profiles)
                        {

                            var stream = CreateImageStream(model, profile);

                            string fmt = string.IsNullOrEmpty(profile.Format) ? "png" : profile.Format;
                            zip.AddEntry(profile.Folder + profile.Name + "." + fmt, stream);
                            stream.Flush();

                            iconObject.icons.Add(new IconObject(profile.Folder + profile.Name + "." + fmt, profile.Width + "x" + profile.Height));
                        }

                        var iconStr = JsonConvert.SerializeObject(iconObject, Newtonsoft.Json.Formatting.Indented);

                        zip.AddEntry("icons.json", iconStr);

                        string zipFilePath = CreateFilePathFromId(zipId);

                        zip.Save(zipFilePath);
                    }
                }

                // Delete source image file from local disk
                File.Delete(multipartFileData.LocalFileName);
            }
            catch (OutOfMemoryException ex)
            {
                HttpResponseMessage httpResponseMessage = Request.CreateErrorResponse(HttpStatusCode.UnsupportedMediaType, ex);
                return httpResponseMessage;
            }
            catch (Exception ex)
            {
                HttpResponseMessage httpResponseMessage = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
                return httpResponseMessage;
            }

            string url = Url.Route("DefaultApi", new { controller = "image", id = zipId.ToString() });

            var uri = new Uri(url, UriKind.Relative);
            var responseMessage = Request.CreateResponse(HttpStatusCode.Created,
                new ImageResponse { Uri = uri });

            responseMessage.Headers.Location = uri;

            return responseMessage;
        }
        #endregion

        private string CreateFilePathFromId(Guid id)
        {
            string root = HttpContext.Current.Server.MapPath("~/App_Data");
            string zipFilePath = Path.Combine(root, id + ".zip");
            return zipFilePath;
        }

        #region image conversion
        private static Stream CreateImageStream(IconModel model, Profile profile)
        {
            if (profile.Silhouette == true)
            {
                return RenderSilhouetteSvg(model, profile);
            }
            else if (model.SvgFile != null)
            {
                return RenderSvgToStream(model.SvgFile, profile.Width, profile.Height, profile.Format, model.Padding, model.Background);
            }
            else
            {
                return ResizeImage(model.InputImage, profile.Width, profile.Height, profile.Format, model.Padding, model.Background);
            }
        }

        private static Stream RenderSvgToStream(string filename, int width, int height, string fmt, double paddingProp = 0.3, Color? bg = null)
        {
            var displaySize = new Size(width, height);

            SvgDocument svgDoc = SvgDocument.Open(filename);
            RectangleF svgSize = RectangleF.Empty;
            try
            {
                svgSize.Width = svgDoc.GetDimensions().Width;
                svgSize.Height = svgDoc.GetDimensions().Height;
            }
            catch (Exception ex)
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

        private static Stream ResizeImage(Image image, int newWidth, int newHeight, string fmt, double paddingProp = 0.3, Color? bg = null)
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

        /*
            Handles paths concerning if the image is a raster or an svg.
            Part 1
            Raster:
            - Convert image to bitmap
            - Bitmap is iterated by pixel, background becomes white, all else becomes black
            - Bitmap then is sent into Potrace and the svg is a text file that has been outputted.

            SVG:
            - Background color turned white, all else turned black.

            Part 2
            - white svg elements are set to transparent
            - black svg elements are set to black

         */
        private static Stream RenderSilhouetteSvg(IconModel model, Profile profile)
        {
            XmlDocument document = new XmlDocument(); // In Memory representation of the SVG document, will be the output to the stream.
            string svgFile = (model.SvgFile != null) ? model.SvgFile : null;
            double paddingProp = (model.Padding == 0) ? model.Padding : 0.3;
            int width = profile.Width;
            int height = profile.Height;
            // Prep the bit map by making background white and the rest of the colors black.

            // Part 1
            if (svgFile == null)
            {
                
                width = model.InputImage.Size.Width;
                height = model.InputImage.Size.Height;
                int adjustWidth;
                int adjustedHeight;
                int paddingW;
                int paddingH;
                if (paddingProp > 0)
                {
                    paddingW = (int)(paddingProp * profile.Width * 0.5);
                    adjustWidth = profile.Width - paddingW;
                    paddingH = (int)(paddingProp * profile.Height * 0.5);
                    adjustedHeight = profile.Height - paddingH;
                }
                else
                {
                    paddingW = paddingH = 0;
                    adjustWidth = profile.Width;
                    adjustedHeight = profile.Height;
                }

                double ratioW = (double)adjustWidth / width;
                double ratioH = (double)adjustedHeight / height;

                double scaleFactor = ratioH > ratioW ? ratioW : ratioH;

                var scaledHeight = (int)(height * scaleFactor);
                var scaledWidth = (int)(width * scaleFactor);

                double originX = ratioH > ratioW ? paddingW * 0.5 : profile.Width * 0.5 - scaledWidth * 0.5;
                double originY = ratioH > ratioW ? profile.Height * 0.5 - scaledHeight * 0.5 : paddingH * 0.5;

                // 1. Convert to bitmap
                Bitmap original = new Bitmap(model.InputImage);
                Bitmap bitmap = new Bitmap(profile.Width, profile.Height, original.PixelFormat);

                Color toWhite = model.Background != null ? (Color)model.Background : original.GetPixel(0, 0);

                Graphics g = Graphics.FromImage(bitmap);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                g.Clear(toWhite);

                var dstRect = new Rectangle((int)originX, (int)originY, scaledWidth, scaledHeight);

                using (var ia = new ImageAttributes())
                {
                    using (var image = model.InputImage)
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(image, dstRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, ia);
                    }
                }

                // 2. Convert background color and white to white, all else to black
                Color c;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        c = bitmap.GetPixel(x, y);

                        // assumption built in, where background needs to be white for potrace, white is kept white, not sure about light grays atm.
                        if (c.Equals(toWhite) || c.Equals(Color.White))
                        {
                            bitmap.SetPixel(x, y, Color.White);
                        } else
                        {
                            bitmap.SetPixel(x, y, Color.Black);
                        }
                    }
                }

                List<List<Curve>> traces = new List<List<Curve>>(); //ignore this output, intended for the GUI
                Potrace.Potrace_Trace(bitmap, traces);

                //SVG
                string svgFileContent = Potrace.getSVG();
                document.LoadXml(svgFileContent);
            
                // The way it CSPotrace is set up the internal representations need to be manually cleared.
                Potrace.Clear();

            } else
            {
                /*
                    SVG Path
                 */
                // 1. Resize document nodes send this output to stream into the XMLDocument.
                SvgDocument svgDoc = SvgDocument.Open(model.SvgFile);
                float oldWidth = 0f;
                float oldHeight = 0f;

                try
                {
                    oldWidth = svgDoc.GetDimensions().Width;
                    oldHeight = svgDoc.GetDimensions().Height;
                } catch (Exception ex) { }

                if (oldWidth == 0f)
                {
                    XmlNode firstRect = document.SelectSingleNode("rect");
                    oldWidth = float.Parse(firstRect.Attributes["width"].Value);
                    oldHeight = float.Parse(firstRect.Attributes["height"].Value);

                    if (oldWidth == 0f)
                    {
                        throw new Exception("SVG does not have size specified. Cannot work with it.");
                    }
                }

                float newRatio = (height * 1.0f) / width;
                float oldRatio = oldHeight / oldWidth;
                float scalingFactor = 1f;
                int padding = 0;
                if (newRatio > oldRatio)
                {
                    padding = (int)(paddingProp * oldWidth * 0.5);
                    scalingFactor = ((width - padding * 2) * 1.0f) / oldWidth;
                } else
                {
                    padding = (int)(paddingProp * oldHeight * 0.5);
                    scalingFactor = ((height - padding * 2) * 1.0f) / oldHeight;
                }

                /*
                    1. Traverse XML and build queue of elements to convert to black and white.
                 */
                // Read the SVG document as XML in memory
                Color toWhite = model.Background != null ? (Color)model.Background : Color.White;

                document.LoadXml(svgDoc.GetXML());

                // DFS
                Queue<XmlNode> queue = traverseSvgNodes(ref document, (XmlNode node) => { return isGraphicalElement(node) || isContainerElement(node); });

                // grab background element, change to white
                foreach (XmlNode graphicalElement in queue)
                {
                    string fillColor = graphicalElement.Attributes["fill"].Value;

                    if (fillColor.Equals(ColorTranslator.ToHtml(toWhite)) || fillColor.Equals(ColorTranslator.ToHtml(Color.White))) {
                        graphicalElement.Attributes["fill"].Value = "#ffffff";
                    }
                    else
                    {
                        graphicalElement.Attributes["fill"].Value = "#000000";
                    }
                }
            }

            /*
                Part 2 - change background to transparent and the silhouette nodes to white.
             */
            //convert white nodes to transparent, convert black nodes to white (silhouette color).
            var stream = new MemoryStream();

            Queue<XmlNode> toTransparent = traverseSvgNodes(ref document, 
                (XmlNode node) => { return node.Attributes["fill"] != null && (node.Attributes["fill"].Value == "#ffffff" || node.Attributes["fill"].Value == "white"); });
            Queue<XmlNode> silhouetteQueue = traverseSvgNodes(ref document, 
                (XmlNode node) => { return node.Attributes["fill"] != null && (node.Attributes["fill"].Value == "#000000" || node.Attributes["fill"].Value == "black"); });

            foreach (XmlNode node in toTransparent)
            {
                node.Attributes["fill"].Value = "transparent";
            }

            foreach (XmlNode node in silhouetteQueue)
            {
                node.Attributes["fill"].Value = "#ffffff";
            }

            document.Save(stream);

            return stream;
        }

        #region SvgAsXml
        static private HashSet<string> graphicalElements = new HashSet<string>(new string[] {
                    "circle",
                    "ellipse",
                    "line",
                    "path",
                    "polygon",
                    "polyline",
                    "rect",
                    "text",
                    "use"
        });

        static private HashSet<string> containerElements = new HashSet<string>(new string[] {
                    "defs",
                    "g",
                    "marker",
                    "mask",
                    "pattern",
                    "switch",
                    "symbol"
        });

        private static bool isGraphicalElement(XmlNode node)
        {
            return graphicalElements.Contains(node.Name);
        }

        private static bool isContainerElement(XmlNode node)
        {
            return containerElements.Contains(node.Name);
        }

        private static Queue<XmlNode> traverseSvgNodes(ref XmlDocument document, Func<XmlNode, bool> condition)
        {
            Queue<XmlNode> edits = new Queue<XmlNode>();

            foreach (XmlNode svg in document.GetElementsByTagName("svg"))
            {
                traverseSvgNodesRecursive(ref edits, in svg, condition);
            }

            return edits;
        }

        private static void traverseSvgNodesRecursive(ref Queue<XmlNode> queue, in XmlNode node, Func<XmlNode, bool> condition)
        {
            if (condition(node))
            {
                queue.Enqueue(node);
            }

            if (node.HasChildNodes)
            {
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    traverseSvgNodesRecursive(ref queue, childNode, condition);
                }
            }
        }
      
        #endregion
    }
    #endregion


    #region output classes
    public class IconModel: IDisposable
    {
        private bool disposed = false;

        public string SvgFile { get; set; }

        public Image InputImage { get; set; }

        public double Padding { get; set; }

        public Color? Background { get; set; }

        public string[] Platforms { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (InputImage != null)
                {
                    InputImage.Dispose();
                }
            }

            disposed = true;
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
    #endregion
}

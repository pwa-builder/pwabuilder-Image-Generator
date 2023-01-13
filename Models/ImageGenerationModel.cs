using System.Globalization;
using Microsoft.Extensions.Primitives;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using SKSvg = Svg.Skia.SKSvg;

namespace AppImageGenerator.Models
{
    public class ImageGenerationModel : IDisposable
    {
        private bool disposed = false;

        public double Padding { get; set; }
        public Color? BackgroundColor { get; set; }
        public string[]? Platforms { get; set; }
        public IFormFile? BaseImageData { get; set; }
        public Image? BaseImage { get; set; }
        public string? ErrorMessage { get; set; }
        public IFormFile? SvgFormData { get; set; }

        public static ImageGenerationModel FromFormData(IFormCollection form, IFormFileCollection files)
        {
            // Validate base image data.
            var baseImageData = files.FirstOrDefault();
            if (baseImageData == null)
            {
                return new ImageGenerationModel
                {
                    ErrorMessage = "No base image specified. Request must contain image."
                };
            }

            // Get the image, or SVG file name if it's an SVG image.
            var svgFormData = default(IFormFile);
            var baseImage = default(Image);
            if (baseImageData.Headers.ContentType.ToString().Contains("svg") == true)
            {
                svgFormData = baseImageData;
            }
            else
            {
                //baseImage = Image.FromFile(baseImageData.LocalFileName);

                FileStream? fs = null;
                try
                {
                    /*  fs = new FileStream(baseImageData.FileName, FileMode.Open, FileAccess.Read);*/
                    var rs = baseImageData.OpenReadStream();
                    baseImage = Image.Load(rs);
                    rs.Close();
                }
                catch (Exception)
                {

                    throw;
                }
                finally
                {
                    fs?.Close();
                }
                
            }

            // Validate platforms.
            StringValues platforms;
            form.TryGetValue("platform", out platforms);

            if (platforms.Count <= 0)
            {
                return new ImageGenerationModel
                {
                    ErrorMessage = "No platform has been specified."
                };
            }

            // Validate the padding.
            StringValues paddings;
            form.TryGetValue("padding", out paddings);
            var hasPadding = paddings.Count > 0 ? true : false;

            double.TryParse(paddings.First()?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var padding);

            if (!hasPadding || padding < 0 || padding > 1.0)
            {
                padding = 0;
            }

            // Validate the color.
            StringValues colorStrings;
            form.TryGetValue("color", out colorStrings);

            var colorStr = colorStrings.First();
            var color = Color.FromRgba(0, 0, 0, 0);

            if (!string.IsNullOrEmpty(colorStr))
            {
                try
                {
                    if (!Color.TryParse(colorStr, out color))
                        if (!Color.TryParseHex(colorStr, out color))
                            throw new ArgumentException("Parsing color unsucessful");
                }
                catch
                {
                    return new ImageGenerationModel
                    {
                        ErrorMessage = "Background Color value invalid. Please input a valid hex color."
                    };
                }
            }

            return new ImageGenerationModel
            {
                BaseImageData = baseImageData,
                BaseImage = baseImage,
                SvgFormData = svgFormData,
                BackgroundColor = color,
                Padding = padding,
                Platforms = platforms
            };
        }

        public static MemoryStream ProcessSvgToStream(IFormFile inputSvg, int newWidth, int newHeight, IImageEncoder imageEncoder, double? paddingProp, Color? backgroundColor = null)
        {
            using (var svg = new SKSvg())
            {
                var svgStream = inputSvg.OpenReadStream();
                if (svg.Load(svgStream) != null && svg.Picture != null)
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

                    // Conver and scale SVG to Image
                    var svgMax = Math.Max(svg.Picture.CullRect.Height, svg.Picture.CullRect.Width);
                    var imageMin = Math.Min(adjustWidth, adjustedHeight);
                    float scale = imageMin / svgMax;
                    var scaleMatrix = SKMatrix.CreateScale(scale, scale);
                    SKImage SkiaImage = SKImage.FromPicture(svg.Picture, new SKSizeI(adjustWidth, adjustedHeight), scaleMatrix);

                    // Save the image to the stream in the specified format
                    var outputImage = new MemoryStream();
                    using (SKData data = SkiaImage.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        data.SaveTo(outputImage);
                    }
                    outputImage.Position = 0;

                    // Conver to ImageSharp and Resize with padding
                    Image processedImage = Image.Load(outputImage);
                    outputImage.Position = 0;

                    if (backgroundColor != null)
                        processedImage.Mutate(x => x.BackgroundColor((Color)backgroundColor));
                    if (paddingProp > 0)
                        processedImage.Mutate(x => x.Resize(
                            new ResizeOptions
                            {
                                Size = new Size(newWidth, newHeight),
                                Mode = ResizeMode.BoxPad,
                                PadColor = backgroundColor ?? Color.Transparent
                            }));


                    processedImage.Save(outputImage, imageEncoder);
                    outputImage.Position = 0;

                    svgStream.Close();

                    return outputImage;

                }
                else
                {
                    svgStream.Close();

                    return null;
                }

            }
        }

        public static MemoryStream ProcessImageToStream(Image inputImage, int newWidth, int newHeight, IImageEncoder imageEncoder, double paddingProp = 0.3, Color? backgroundColor = null)
        {
            int adjustWidth;
            int adjustedHeight;
            int paddingW;
            int paddingH;
            Image processedImage = inputImage.Clone(x => { });

            if (paddingProp > 0)
            {
                paddingW = (int)(paddingProp * newWidth * 0.5);
                adjustWidth = newWidth - paddingW;
                paddingH = (int)(paddingProp * newHeight * 0.5);
                adjustedHeight = newHeight - paddingH;
            }
            else
            {
                // paddingW = paddingH = 0;
                adjustWidth = newWidth;
                adjustedHeight = newHeight;
            }

            processedImage.Mutate(x => x.Resize(adjustWidth, adjustedHeight, KnownResamplers.Lanczos3));

            if (backgroundColor != null)
                processedImage.Mutate(x => x.BackgroundColor((Color)backgroundColor));

            if (paddingProp > 0)
                processedImage.Mutate(x => x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        Mode = ResizeMode.BoxPad,
                        PadColor = backgroundColor ?? Color.Transparent
                    }));

            var outputImage = new MemoryStream();
            processedImage.Save(outputImage, imageEncoder);
            outputImage.Position = 0;


            return outputImage;
        }
    
    public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (BaseImage != null)
                {
                    BaseImage.Dispose();
                }

                if (BaseImageData != null && !string.IsNullOrEmpty(BaseImageData.FileName))
                {
                    File.Delete(BaseImageData.FileName);
                }
            }

            disposed = true;
        }
    }
}
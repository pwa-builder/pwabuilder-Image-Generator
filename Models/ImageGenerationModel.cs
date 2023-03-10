using System.Globalization;

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
                using var rs = baseImageData.OpenReadStream();
                baseImage = Image.Load(rs);               
            }

            // Validate platforms.
            form.TryGetValue("platform", out var platforms);

            if (platforms.Count <= 0)
            {
                return new ImageGenerationModel
                {
                    ErrorMessage = "No platform has been specified."
                };
            }

            // Validate the padding.
            form.TryGetValue("padding", out var paddings);
            var hasPadding = paddings.Count > 0 ? true : false;
            double padding = 0;

            if (hasPadding)
            {
                double.TryParse(paddings.First()?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out padding);
            }

            if (!hasPadding || padding < 0 || padding > 1.0)
            {
                padding = 0;
            }

            // Validate the color.
            form.TryGetValue("color", out var colorStrings);

            var colorStr = colorStrings.First();
            var color = Color.FromRgba(0, 0, 0, 0);

            if (!string.IsNullOrEmpty(colorStr))
            {
                try
                {
                    if (!Color.TryParse(colorStr, out color) && !Color.TryParseHex(colorStr, out color))
                    {
                        throw new ArgumentException("Parsing color unsucessful");
                    }
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

        public static MemoryStream? ProcessSvgToStream(IFormFile inputSvg, int newWidth, int newHeight, IImageEncoder imageEncoder, double? paddingProp, Color? backgroundColor = null)
        {
            using (var svg = new SKSvg())
            {
                using var svgStream = inputSvg.OpenReadStream();
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

                    // Translate, scale and convert SVG to Image
                    var svgWidth = svg.Picture.CullRect.Width;
                    var svgHeight = svg.Picture.CullRect.Height;
                    var svgMax = Math.Max(svgWidth, svgHeight);
                    var imageMin = Math.Min(adjustWidth, adjustedHeight);
                    var scale = imageMin / svgMax;
                    var scaleMatrix = SKMatrix.CreateIdentity();
                    SKMatrix.Concat(ref scaleMatrix,
                                    SKMatrix.CreateTranslation(adjustWidth / 2 - svgWidth * scale / 2, adjustedHeight/2 - svgHeight * scale / 2),
                                    SKMatrix.CreateScale(scale, scale));

                    var SkiaImage = SKImage.FromPicture(svg.Picture, new SKSizeI(adjustWidth, adjustedHeight), scaleMatrix);

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
                    {
                        processedImage.Mutate(x => x.BackgroundColor((Color)backgroundColor));
                    }
                    if (paddingProp > 0)
                    {
                        processedImage.Mutate(x => x.Resize(
                            new ResizeOptions
                            {
                                Size = new Size(newWidth, newHeight),
                                Mode = ResizeMode.BoxPad,
                                PadColor = backgroundColor ?? Color.Transparent
                            }));
                    }


                    processedImage.Save(outputImage, imageEncoder);
                    outputImage.Position = 0;

                    return outputImage;

                }
                else
                {
                    return null;
                }

            }
        }

        public static MemoryStream ProcessImageToStream(Image inputImage, int newWidth, int newHeight, IImageEncoder imageEncoder, double paddingProp = 0, Color? backgroundColor = null)
        {
            int adjustWidth;
            int adjustedHeight;
            var processedImage = inputImage.Clone(x => { });

            if (paddingProp > 0)
            {
                adjustWidth = newWidth - (int)(paddingProp * newWidth * 0.5);
                adjustedHeight = newHeight - (int)(paddingProp * newHeight * 0.5);
            }
            else
            {
                adjustWidth = newWidth;
                adjustedHeight = newHeight;
            }

            processedImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(adjustWidth, adjustedHeight),
                Mode = ResizeMode.Pad,
                Sampler = KnownResamplers.Lanczos3
            }));

            if (backgroundColor != null)
            {
                processedImage.Mutate(x => x.BackgroundColor((Color)backgroundColor));
            }

            if (paddingProp > 0)
            {
                processedImage.Mutate(x => x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        Mode = ResizeMode.BoxPad,
                        PadColor = backgroundColor ?? Color.Transparent
                    })
                );
            }

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

                if (!string.IsNullOrEmpty(BaseImageData?.FileName))
                {
                    File.Delete(BaseImageData.FileName);
                }
            }

            disposed = true;
        }
    }
}
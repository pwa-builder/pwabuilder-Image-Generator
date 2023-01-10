using System.Globalization;
using Microsoft.Extensions.Primitives;
using SixLabors.ImageSharp;

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
                // No padding? Default to 0.3
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
                        Color.TryParseHex(colorStr, out color);
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
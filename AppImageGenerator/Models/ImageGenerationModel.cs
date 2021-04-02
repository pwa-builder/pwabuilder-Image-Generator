using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace WWA.WebUI.Models
{
    public class ImageGenerationModel : IDisposable
    {
        private bool disposed = false;

        public double Padding { get; set; }
        public Color? BackgroundColor { get; set; }
        public bool ColorChanged { get; set; }
        public string[] Platforms { get; set; }
        public MultipartFileData BaseImageData { get; set; }
        public Image BaseImage { get; set; }
        public string ErrorMessage { get; set; }
        public string SvgFileName { get; set; }

        public static ImageGenerationModel FromFormData(NameValueCollection form, Collection<MultipartFileData> files)
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
            var svgFileName = default(string);
            var baseImage = default(Image);
            if (baseImageData.Headers.ContentType?.MediaType?.Contains("svg") == true)
            {
                svgFileName = baseImageData.LocalFileName;
            }
            else
            {
                baseImage = Image.FromFile(baseImageData.LocalFileName);
            }

            // Validate platforms.
            var platforms = form.GetValues("platform");
            if (platforms == null)
            {
                return new ImageGenerationModel
                {
                    ErrorMessage = "No platform has been specified."
                };
            }

            // Validate the padding.
            var hasPadding = double.TryParse(form.GetValues("padding").FirstOrDefault(), out var padding);
            if (!hasPadding || padding < 0 || padding > 1.0)
            {
                return new ImageGenerationModel
                {
                    ErrorMessage = "Padding value invalid. Please input a number between 0 and 1"
                };
            }

            // Validate the color.
            var colorStr = form.GetValues("color")?.FirstOrDefault();
            var colorChanged = form.GetValues("colorChanged")?.FirstOrDefault() == "1";
            Color? color = null;
            if (!string.IsNullOrEmpty(colorStr) && colorChanged)
            {
                try
                {
                    var colorConverter = new ColorConverter();
                    color = (Color)colorConverter.ConvertFromString(colorStr);
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
                SvgFileName = svgFileName,
                BackgroundColor = color,
                ColorChanged = colorChanged,
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
                if (this.BaseImage != null)
                {
                    this.BaseImage.Dispose();
                }

                if (this.BaseImageData != null && !string.IsNullOrEmpty(this.BaseImageData.LocalFileName))
                {
                    System.IO.File.Delete(this.BaseImageData.LocalFileName);
                }
            }

            disposed = true;
        }
    }
}
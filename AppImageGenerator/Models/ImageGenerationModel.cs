using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using SixLabors.ImageSharp;

namespace WWA.WebUI.Models
{
    public class ImageGenerationModel : IDisposable
    {
        private bool disposed = false;

        public double Padding { get; set; }
        public Color? BackgroundColor { get; set; }
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
                //baseImage = Image.FromFile(baseImageData.LocalFileName);

                FileStream fs = null;
                try
                {
                    fs = new FileStream(baseImageData.LocalFileName, FileMode.Open, FileAccess.Read);
                    baseImage = Image.Load(fs);
                }
                catch (Exception)
                {

                    throw;
                }
                finally
                {
                    fs.Close();
                }
                
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
                // No padding? Default to 0.3
                padding = 0.3;
            }

            // Validate the color.
            var colorStr = form.GetValues("color")?.FirstOrDefault();
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
                SvgFileName = svgFileName,
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
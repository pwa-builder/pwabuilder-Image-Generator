using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WWA.WebUI.Models
{
    /// <summary>
    /// A web manifest icon.
    /// </summary>
    public class WebManifestIcon
    {
        [JsonProperty("src")]
        public string Src { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("sizes")]
        public string Sizes { get; set; }
        [JsonProperty("purpose")]
        public string Purpose { get; set; } // "any" | "maskable" | "monochrome";

        public bool IsSquare()
        {
            if (this.Sizes == null)
            {
                return false;
            }

            return this.GetAllDimensions()
                .Any(d => d.width == d.height);
        }

        /// <summary>
        /// Gets the largest dimension for the image.
        /// </summary>
        /// <returns></returns>
        public (int width, int height)? GetLargestDimension()
        {
            var largest = GetAllDimensions()
                .OrderByDescending(i => i.width + i.height)
                .FirstOrDefault();
            if (largest.height == 0 && largest.width == 0)
            {
                return null;
            }

            return largest;
        }

        /// <summary>
        /// Finds the largest dimension from the <see cref="Sizes"/> property
        /// </summary>
        /// <returns>The largest dimension from the <see cref="Sizes"/> string. If no valid size could be found, null.</returns>
        public List<(int width, int height)> GetAllDimensions()
        {
            if (this.Sizes == null)
            {
                return new List<(int width, int height)>(0);
            }

            return this.Sizes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(size => size.Split(new[] { 'x' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(widthAndHeight =>
                {
                    if (int.TryParse(widthAndHeight.ElementAtOrDefault(0), out var width) &&
                        int.TryParse(widthAndHeight.ElementAtOrDefault(1), out var height))
                    {
                        return (width, height);
                    }
                    return (width: 0, height: 0);
                })
                .Where(d => d.width != 0 && d.height != 0)
                .ToList();
        }
    }
}
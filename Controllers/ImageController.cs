using System.IO.Compression;
using System.Net;
using System.Text.Json;

using AppImageGenerator.Models;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;

using Microsoft.AspNetCore.Mvc;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AppImageGenerator.Controllers;

[ApiController]
[Route("api/")]
public class ImageController : ControllerBase
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger _logger;

    private const string FileDownloadName = "AppImages.zip";
    private const string FileDownloadType = "application/octet-stream";
    private const string FileIconsJsonName = "icons.json";
    private const string PlatformNameCommonPart = "Images.json";
    private const string AppDataFolderName = "App_Data";
    private const string GetZipById = "getIconsZipById";
    private const string GenerateIconsZip = "generateIconsZip";

    public ImageController (IWebHostEnvironment webHostEnvironment, ILogger<ImageController> logger)
    {
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet(GetZipById, Name = GetZipById)]
    public async Task<ActionResult> Get(string id)
    {
        try
        {
            // Create path from the id and return the file...
            var zipFilePath = CreateFilePathFromId(new Guid(id));
            if (string.IsNullOrEmpty(zipFilePath))
            {
                return new NotFoundResult();
            }

            var archive = File(await System.IO.File.ReadAllBytesAsync(zipFilePath), FileDownloadType, fileDownloadName: FileDownloadName);

            System.IO.File.Delete(zipFilePath);

            return archive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, string.Format("{{GetZipById}}: Couldn't get generated zip due to exception", GetZipById));
            return StatusCode((int)HttpStatusCode.InternalServerError, ex.ToString());
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
    [HttpPost(GenerateIconsZip)]
    public async Task<ActionResult> Post([FromForm] ImageFormData Form)
    {
        var zipId = Guid.NewGuid();

        try
        {
            using var args = ImageGenerationModel.FromFormData(HttpContext.Request.Form, HttpContext.Request.Form.Files);
            
            // Punt if we have invalid arguments.
            if (!string.IsNullOrEmpty(args.ErrorMessage))
            {
                return new ObjectResult(args.ErrorMessage) { StatusCode = (int?)HttpStatusCode.BadRequest };
            }

            var profiles = GetProfilesFromPlatforms(args.Platforms);
            if (profiles == null)
                throw new Exception(string.Format("No platforms found in config: {{PLATFORMS}}", args.Platforms != null? args.Platforms : "no param"));

            var imageStreams = new List<Stream>(profiles.Count);

            using (var zip = ZipFile.Open(CreateFilePathFromId(zipId), ZipArchiveMode.Create))
            {
                var iconObject = new IconRootObject();
                foreach (var profile in profiles)
                {
                    var stream = CreateImageStream(args, profile);
                    if (stream != null)
                    {
                        imageStreams.Add(stream);
                        var fmt = string.IsNullOrEmpty(profile.Format) ? "png" : profile.Format;
                        var iconEntry = zip.CreateEntry(profile.Folder + profile.Name + "." + fmt, CompressionLevel.Fastest);
                        var iconStream = iconEntry.Open();
                        await stream.CopyToAsync(iconStream);
                        iconStream.Close();
                        stream.Close();

                        iconObject.Icons.Add(new IconObject(profile.Folder + profile.Name + "." + fmt, profile.Width + "x" + profile.Height));
                    }
                }

                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var iconStr = JsonSerializer.Serialize(iconObject, options);

                using (StreamWriter writer = new(zip.CreateEntry(FileIconsJsonName, CompressionLevel.Optimal).Open()))
                {
                    writer.Write(iconStr);
                }

                var zipFilePath = CreateFilePathFromId(zipId);
                imageStreams.ForEach(s => s.Dispose());
            }

            // Send back a route to download the zip file.
            var url = Url.RouteUrl(GetZipById, new { id = zipId.ToString() });

            if (url == null)
                throw new Exception(string.Format("Couldn't generate RouteUrl for ID: {{ID}}", zipId.ToString()));

            _logger.LogInformation(string.Format("{{GenerateIconsZip}}: icons generated for platforms: {{PLATFORMS}}", GenerateIconsZip,
              args.Platforms != null ? args.Platforms : "no param"));

            return new RedirectResult(url);

        }
        //catch (OutOfMemoryException ex)
        //{
        //    _logger.LogError(ex, string.Format("{{GenerateIconsZip}}: Couldn't generate images due to exception" , GenerateIconsZip));
        //    return StatusCode((int)HttpStatusCode.UnsupportedMediaType, ex.ToString());
        //}
        catch (Exception ex)
        {
            _logger.LogError(ex, string.Format("{{GenerateIconsZip}}: Couldn't generate images due to exception", GenerateIconsZip));
            return StatusCode((int)HttpStatusCode.UnsupportedMediaType, ex.ToString());
        }
    }

    // Legacy wrapper for new generateIconsZip method
    [Obsolete]
    [HttpPost("image")]
    public async Task<ActionResult> LegacyGetGeneratedImagesZip([FromForm] MultipartFormDataContent Form)
    {
        var formData = new ImageFormData();

        // Convert from MultipartFormDataContent to ImageFormData
        if (Request.Form.Files.Count > 0)
        {
            formData.FileName = Request.Form.Files[0];
        }
        if (Request.Form.TryGetValue("Platform", out var platformValue))
        {
            if (Enum.TryParse<Platform>(platformValue, out var platform))
            {
                formData.Platform = platform;
            }
        }
        if (Request.Form.TryGetValue("Padding", out var padding))
        {
            formData.Padding = padding;
        }
        if (Request.Form.TryGetValue("Color", out var color))
        {
            formData.Color = color;
        }

        if (formData.FileName == null)
        {
            _logger.LogError(null, "legacyGenerateIconsZip: Couldn't generate images due to bad FormData");
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var postResponse = await Post(formData);
        if (postResponse is RedirectResult redirectResult)
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.Redirect);
            responseMessage.Content = new StringContent(JsonSerializer.Serialize(new ImageResponse { Uri = redirectResult.Url }));

            HttpContext.Request.Form.TryGetValue("platform", out var platforms);
            _logger.LogInformation(string.Format("legacyGenerateIconsZip: icons generated for platforms: {{PLATFORMS}}", platforms.ToString()));

            return new ObjectResult(new ImageResponse { Uri = redirectResult.Url });
        }
        else
        {
            if (postResponse is ObjectResult redirectFail)
            {
                if (postResponse.GetType().GetProperty("Value") != null && postResponse.GetType().GetProperty("StatusCode") != null)
                {
                    _logger.LogError(redirectFail.Value?.ToString(), "legacyGenerateIconsZip: Couldn't generate images due to exception");
                    return StatusCode((int)redirectFail.StatusCode!, redirectFail.Value!.ToString());
                }

            }
        }
        _logger.LogError(null, "legacyGenerateIconsZip: Couldn't generate images due to exception");
        return StatusCode((int)HttpStatusCode.BadRequest);
    }

    [HttpPost("generateBase64Icons")]
    public ActionResult Base64([FromForm] ImageFormData Form)
    {
        try
        {
            using var args = ImageGenerationModel.FromFormData(HttpContext.Request.Form, HttpContext.Request.Form.Files);
            if (!string.IsNullOrEmpty(args.ErrorMessage))
            {
                return new ObjectResult(args.ErrorMessage) { StatusCode = (int?)HttpStatusCode.BadRequest };
            }

            var profiles = GetProfilesFromPlatforms(args.Platforms);
            if (profiles == null)
                throw new Exception(string.Format("No platforms found in config: {{PLATFORMS}}", args.Platforms != null ? args.Platforms : "no param"));

            var imgs = profiles
                .Select(profile => new WebManifestIcon
                {
                    Purpose = "any",
                    Sizes = $"{profile.Width}x{profile.Height}",
                    Src = CreateBase64Image(args, profile),
                    Type = string.IsNullOrEmpty(profile.Format) ? "image/png" : profile.Format
                });

            var options = new JsonSerializerOptions { WriteIndented = true };
            var response = new ObjectResult(JsonSerializer.Serialize(imgs, options));

            _logger.LogInformation(string.Format("generateBase64Icons: icons generated for platforms: {{PLATFORMS}}",
              args.Platforms != null ? args.Platforms : "no param"));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "generateBase64Icons: Couldn't generate images due to exception");
            return StatusCode((int)HttpStatusCode.InternalServerError, ex.ToString());
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
        var config = new List<string>();
        var webRootPath = _webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath;
        var root = Path.Combine(webRootPath, AppDataFolderName);

        var filePath = Path.Combine(root, platformId + PlatformNameCommonPart);
        config.Add(ReadStringFromConfigFile(filePath));
        return config;
    }

    private IReadOnlyList<Profile>? GetProfilesFromPlatforms(IEnumerable<string>? platforms)
    {
        List<Profile>? profiles = null;
        if (platforms != null)
        {
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
                    if (cfg != null)
                    {
                        var profile = JsonSerializer.Deserialize<List<Profile>>(cfg);
                        if (profile != null)
                        {
                            //profiles == null ? profiles = profile : profiles.AddRange(profile);
                            if (profiles == null)
                            {
                                profiles = profile;
                            }
                            else
                            {
                                profiles.AddRange(profile);
                            }
                        }
                    }
                }
            }
        }

        return profiles;
    }

    private string CreateFilePathFromId(Guid id)
    {
        var webRootPath = _webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath;
        var root = Path.Combine(webRootPath, AppDataFolderName);
        var zipFilePath = Path.Combine(root, id + ".zip");
        return zipFilePath;
    }

    private static IImageEncoder GetEncoderFromType(string? type)
    {
        if (!string.IsNullOrEmpty(type))
        {
            string ImageType = type.ToLower();

            if (ImageType.EndsWith("png"))
                return new PngEncoder();
            if (ImageType.EndsWith("jpeg")|| ImageType.EndsWith("jpg"))
                return new JpegEncoder();
            if (ImageType.EndsWith("webp"))
                return new WebpEncoder();
            if (ImageType.EndsWith("bmp"))
                return new BmpEncoder();
            if (ImageType.EndsWith("tiff"))
                return new TiffEncoder();
        }

        return new PngEncoder();
    }

    private static MemoryStream? CreateImageStream(ImageGenerationModel model, Profile profile)
    {
        // We the individual image has padding specified, used that.
        // Otherwise, use the general padding passed into the model.
        var padding = profile.Padding ?? model.Padding;

        var imageEncoder = GetEncoderFromType(profile.Format);

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

    private static string? CreateBase64Image(ImageGenerationModel model, Profile profile)
    {
        var formatOrPng = string.IsNullOrEmpty(profile.Format) ? "image/png" : profile.Format;
        using (var imgStream = CreateImageStream(model, profile))
        {   if (imgStream != null)
            {
                var base64 = Convert.ToBase64String(imgStream.ToArray());
                return $"data:{formatOrPng};base64,{base64}";
            }
        }
        return null;
    }
}

public class IconObject
{
    public IconObject(string src, string size)
    {
        this.Src = src;
        this.Sizes = size;
    }

    public string Src { get; set; }

    public string Sizes { get; set; }
}

public class IconRootObject
{
    public List<IconObject> Icons { get; set; } = new List<IconObject>();
}

public class ImageFormData
{
    [Required]
    public IFormFile? FileName { get; set; }

    [Required]
    [DefaultValue(Platform.android)]
    public Platform Platform { get; set; } /* "android" | "chrome "| "firefox" | "ios" | "msteams" | "windows10" | "windows11" */
    public string? Padding { get; set; }

    public string? Color { get; set; }
}

public enum Platform {
    android,
    chrome, firefox, ios, msteams, windows10, windows11 }

// Legacy
public class ImageResponse
{
    [JsonPropertyName("Uri")]
    public string? Uri { get; set; }
}

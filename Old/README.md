Please use our [main repository for any issues/bugs/features suggestion](https://github.com/pwa-builder/PWABuilder/issues/new/choose).

# App Image Generator
A web tool that generates images for various app platforms. Used by PWABuilder.com.

See a version of the website at [http://appimagegenerator-prod.azurewebsites.net/](http://appimagegenerator-prod.azurewebsites.net/).

Written in C# and ASP.net 4.5.2.

Origionally written by [Peter Daukintis](https://github.com/peted70)

## Usage

POST to /api/image with the following Form Data:

| Option         | Type     | Description |
|--------------|-----------|------------|
| fileName | bytes (Blob or File in JS)    | The bytes of the file from which to generate all the image of the target platform(s)        |
| platform      | "android" \| "chrome "\| "firefox" \| "ios" \| "msteams" \| "windows10" \| "windows11" | The platforms to generate the images for. To use multiple platforms, add this field multiple times to the form data       |
| padding      | number  | How much padding to add to generated images. Must be between 0 and 1, where 0 is no padding and 1 is maximum. 0.3 is a reasonable value. Some platforms, such as Windows 11, control padding per-image. In such cases, this value will be ignored.       |
| color      | string  | Optional. The background color to fill in the images' padding and non-square areas. The string can be a well-known color ("blue"), a hex color ("#ffeedd"), a hex color with alpha (#2828ab73), or "transparent". If omitted or null, the background color will be chosen from pixel (0,0) in the source image.      |

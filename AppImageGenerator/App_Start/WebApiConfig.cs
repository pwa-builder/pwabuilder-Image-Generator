using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;

namespace WWA.WebUI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            var pwabuilderUrl = ConfigurationManager.AppSettings["pwabuilderUrl"];

            EnableCorsAttribute cors = new EnableCorsAttribute(pwabuilderUrl, "*", "GET,POST");
            config.EnableCors(cors);

            config.Routes.MapHttpRoute(
                name: "ImagePost",
                routeTemplate: "api/image",
                defaults: new { controller = "Image", action = "Post" });

            config.Routes.MapHttpRoute(
                name: "ImageBase64",
                routeTemplate: "api/image/base64",
                defaults: new { controller = "Image", action = "Base64" });

            config.Routes.MapHttpRoute(
                name: "ImageDownload",
                routeTemplate: "api/image/download",
                defaults: new { controller = "Image", action = "Download" });

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}

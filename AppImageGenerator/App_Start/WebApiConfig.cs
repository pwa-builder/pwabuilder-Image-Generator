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
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}

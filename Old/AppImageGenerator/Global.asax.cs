using System;
using System.Collections.Generic;
using System.Linq;
using WWA.WebUI;

namespace WAT.WebUI
{
    // Note: For instructions on enabling IIS7 classic mode, 
    // visit http://go.microsoft.com/fwlink/?LinkId=301868
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            GlobalConfiguration.Configuration.EnsureInitialized();

            //Needed to add the following line in order to return XML or else it always returns JSON.
            //var formatters = GlobalConfiguration.Configuration.Formatters;
            //formatters.Remove(formatters.JsonFormatter);
        }
    }
}

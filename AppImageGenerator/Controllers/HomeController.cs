using System.Configuration;
using System.Web.Mvc;

namespace WAT.WebUI.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        public ActionResult Index()
        {
            ViewBag.HomeLink = ConfigurationManager.AppSettings["homeLink"];
            ViewBag.GenerateLink = ConfigurationManager.AppSettings["generateLink"];
            ViewBag.DeployLink = ConfigurationManager.AppSettings["deployLink"];
            ViewBag.LicenseLink = ConfigurationManager.AppSettings["licenseLink"];

            return View();
        }
	}
}
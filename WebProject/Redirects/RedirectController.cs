using System.Web.Mvc;

namespace WebProject.Redirects
{
    [Authorize(Roles = "WebAdmins, Administrators")]
    public class RedirectController : Controller
    {
        [Route("Admin/RedirectManager")]
        public ActionResult Index()
        {
            return View("/Views/Admin/RedirectManager.cshtml");
        }
    }
}
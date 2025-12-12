using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Areas.admin.Controllers
{
    [Area("admin")]
    public class MainController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

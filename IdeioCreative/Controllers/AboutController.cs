using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Controllers
{
    public class AboutController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

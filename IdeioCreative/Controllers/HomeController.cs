using System.Diagnostics;
using IdeioCreative.Data;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DatabaseContext _context;
        public HomeController(ILogger<HomeController> logger, DatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var services = _context.Services.ToList();
            var references = _context.References.ToList();
            var model = new HomePageViewModel
            {
                Services = services,
                References = references
            };
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }
        [Route("404")]
        public IActionResult NotFound()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

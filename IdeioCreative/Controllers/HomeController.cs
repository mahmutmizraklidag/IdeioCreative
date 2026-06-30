using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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
            var services = _context.Services.Where(x => x.Language == Language.TR)
                .OrderBy(s => s.Title).ToList();
            var references = _context.References.ToList();
            var blogs = _context.Blogs.OrderByDescending(b => b.CreatedAt).Take(3).ToList();
            var model = new HomePageViewModel
            {
                Services = services,
                References = references,
                Blog = blogs
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

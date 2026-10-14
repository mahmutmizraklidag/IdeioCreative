using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
            var services = _context.Services.Where(x => x.Language == Language.TR && x.PageType == ServicePageType.Service)
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
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public new IActionResult NotFound()
        {
            // Preserve the server-error status when the exception handler uses this view.
            Response.StatusCode = HttpContext.Features.Get<IExceptionHandlerFeature>() is null
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status500InternalServerError;

            var model = new NotFoundPageViewModel
            {
                Services = _context.Services.AsNoTracking()
                    .Where(service => service.Language == Language.TR && service.PageType == ServicePageType.Service)
                    .OrderBy(service => service.Title).ToList(),
                Phone = _context.SiteSettings.AsNoTracking()
                    .Where(settings => settings.Language == Language.TR)
                    .OrderBy(settings => settings.Id)
                    .Select(settings => settings.Phone).FirstOrDefault()
            };
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

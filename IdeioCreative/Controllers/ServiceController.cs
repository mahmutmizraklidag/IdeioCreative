using IdeioCreative.Data;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioCreative.Controllers
{
    public class ServiceController : Controller
    {
        private readonly DatabaseContext _context;

        public ServiceController(DatabaseContext context)
        {
            _context = context;
        }

        [Route("hizmetlerimiz")]
        public IActionResult Index()
        {
            ViewData["IsSectorPage"] = false;
            var services = _context.Services
                .Where(s => s.Language == Language.TR && s.PageType == ServicePageType.Service)
                .OrderBy(s => s.Title) // alfabetik sıralama
                .ToList();

            return View(services);
        }

        [Route("hizmetlerimiz/{slug}")]
        public IActionResult Detail(string slug)
        {
            var service = _context.Services.Include(s => s.Faqs)
                .FirstOrDefault(s => s.Slug == slug && s.Language == Language.TR && s.PageType == ServicePageType.Service);
            if (service is null)
                return NotFound();
            ViewData["IsSectorPage"] = false;
            return View(service);
        }
    }
}

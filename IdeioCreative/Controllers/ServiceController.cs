using IdeioCreative.Data;
using Microsoft.AspNetCore.Mvc;

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
            var services = _context.Services
                .Where(s => s.Language.ToString() == "Tr")
                .OrderBy(s => s.Title) // alfabetik sıralama
                .ToList();

            return View(services);
        }

        [Route("hizmetlerimiz/{slug}")]
        public IActionResult Detail(string slug)
        {
            var service = _context.Services.FirstOrDefault(s => s.Slug == slug && s.Language.ToString() == "Tr");
            return View(service);
        }
    }
}

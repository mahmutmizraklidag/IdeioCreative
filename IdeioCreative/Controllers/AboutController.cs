using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Controllers
{
    public class AboutController : Controller
    {
        private readonly DatabaseContext _context;

        public AboutController(DatabaseContext context)
        {
            _context = context;
        }

        [Route("hakkimizda")]
        public IActionResult Index()
        {
            var AboutViewModel = new AboutViewModel()
            {
                Services = _context.Services.Where(x => x.Language == Language.TR).ToList(),
                About = _context.Abouts.FirstOrDefault(a => a.Language == Language.TR)
            };
            return View(AboutViewModel);
        }
    }
}

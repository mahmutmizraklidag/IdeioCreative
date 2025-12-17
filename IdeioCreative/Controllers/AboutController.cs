using IdeioCreative.Data;
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
                Services = _context.Services.Where(s => s.Language.ToString() == "Tr").ToList(),
                About = _context.Abouts.FirstOrDefault(a => a.Language.ToString() == "Tr")
            };
            return View();
        }
    }
}

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
            var services = _context.Services.Where(s => s.Language.ToString() == "Tr").ToList();
            return View();
        }
        public IActionResult Detail()
        {
            return View();
        }
    }
}

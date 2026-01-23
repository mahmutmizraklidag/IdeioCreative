using IdeioCreative.Data;
using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Controllers
{
    public class TeamController : Controller
    {
        private readonly DatabaseContext _context;

        public TeamController(DatabaseContext context)
        {
            _context = context;
        }

        [Route("ekibimiz")]
        public IActionResult Index()
        {
            var teams = _context.Teams
        .OrderBy(x => x.OrderNo)
        .ToList();

            return View(teams);
        }
        public IActionResult Detail()
        {
            return View();
        }
    }
}

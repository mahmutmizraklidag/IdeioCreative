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
            var model=_context.Teams.ToList();
            return View(model);
        }
        public IActionResult Detail()
        {
            return View();
        }
    }
}

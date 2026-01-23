using IdeioCreative.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Areas.admin.Controllers
{
    [Area("admin"),Authorize]
    public class MainController : Controller
    {
        private readonly DatabaseContext _context;

        public MainController(DatabaseContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var servicesCount = _context.Services.Count();
            ViewBag.ServicesCount = servicesCount;
            var teamsCount = _context.Teams.Count();
            ViewBag.TeamsCount = teamsCount;
            var referencesCount = _context.References.Count();
            ViewBag.ReferencesCount = referencesCount;
            var contactsCount = _context.ContactForms.Count();
            ViewBag.ContactsCount = contactsCount;
            return View();
        }
    }
}

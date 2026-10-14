using IdeioCreative.Data;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioCreative.Controllers;

public class SectorController : Controller
{
    private readonly DatabaseContext _context;

    public SectorController(DatabaseContext context)
    {
        _context = context;
    }

    [Route("sektorler")]
    public IActionResult Index()
    {
        ViewData["IsSectorPage"] = true;
        var sectors = _context.Services
            .Where(item => item.Language == Language.TR && item.PageType == ServicePageType.Sector)
            .OrderBy(item => item.Title)
            .ToList();
        if (sectors.Count == 0)
            ViewData["Robots"] = "noindex, follow";

        return View("~/Views/Service/Index.cshtml", sectors);
    }

    [Route("sektorler/{slug}")]
    public IActionResult Detail(string slug)
    {
        var sector = _context.Services.Include(item => item.Faqs)
            .FirstOrDefault(item => item.Slug == slug && item.Language == Language.TR && item.PageType == ServicePageType.Sector);
        if (sector is null)
            return NotFound();

        ViewData["IsSectorPage"] = true;
        return View("~/Views/Service/Detail.cshtml", sector);
    }
}

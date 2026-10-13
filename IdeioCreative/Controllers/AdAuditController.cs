using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioCreative.Controllers;

public sealed class AdAuditController(DatabaseContext context) : Controller
{
    [HttpGet("ucretsiz-reklam-denetimi")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await context.SiteSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Language == Language.TR, cancellationToken);
        var about = await context.Abouts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Language == Language.TR, cancellationToken);
        var references = await context.References.AsNoTracking()
            .Where(x => x.Language == Language.TR && x.Image != null && x.Image != "")
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var services = await context.Services.AsNoTracking()
            .Where(x => x.Language == Language.TR)
            .OrderBy(x => x.Title).ToListAsync(cancellationToken);

        return View(new AdAuditViewModel
        {
            SiteSetting = settings ?? new SiteSetting(),
            About = about ?? new About(),
            References = references,
            Services = services
        });
    }
}

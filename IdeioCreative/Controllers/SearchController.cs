using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioCreative.Controllers;

public sealed class SearchController : Controller
{
    private readonly DatabaseContext _context;

    public SearchController(DatabaseContext context)
    {
        _context = context;
    }

    [HttpGet("arama")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length > SearchPageViewModel.MaxQueryLength)
            query = query[..SearchPageViewModel.MaxQueryLength];

        var results = new List<SearchResultViewModel>();
        if (!string.IsNullOrEmpty(query))
        {
            var services = await _context.Services.AsNoTracking()
                .Where(service => service.Language == Language.TR &&
                    (service.Title.Contains(query) || service.Slug.Contains(query) ||
                     (service.Description != null && service.Description.Contains(query)) ||
                     (service.HomeDescription != null && service.HomeDescription.Contains(query)) ||
                     (service.MetaDescription != null && service.MetaDescription.Contains(query)) ||
                     (service.Keywords != null && service.Keywords.Contains(query))))
                .OrderBy(service => service.Title).Take(30)
                .Select(service => new SearchResultViewModel(service.Title,
                    service.MetaDescription ?? service.HomeDescription,
                    service.Slug,
                    service.PageType == ServicePageType.Sector ? "Sector" : "Service",
                    service.PageType == ServicePageType.Sector ? "Sektör" : "Hizmet"))
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;
            var blogs = await _context.Blogs.AsNoTracking()
                .Where(blog => blog.CreatedAt <= now &&
                    (blog.Title.Contains(query) || blog.Slug.Contains(query) ||
                     blog.Description.Contains(query) ||
                     (blog.MetaDescription != null && blog.MetaDescription.Contains(query)) ||
                     (blog.Keywords != null && blog.Keywords.Contains(query))))
                .OrderByDescending(blog => blog.CreatedAt).Take(30)
                .Select(blog => new SearchResultViewModel(blog.Title,
                    blog.MetaDescription ?? blog.Description, blog.Slug, "Blog", "Blog"))
                .ToListAsync(cancellationToken);

            results.AddRange(services);
            results.AddRange(blogs);
        }

        return View(new SearchPageViewModel { Query = query, Results = results });
    }
}

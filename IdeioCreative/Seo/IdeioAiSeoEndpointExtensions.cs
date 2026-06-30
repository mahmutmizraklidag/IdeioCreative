using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Xml.Linq;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdeioCreative.Seo;

public static class IdeioAiSeoEndpointExtensions
{
    public static IServiceCollection AddIdeioAiSeo(
        this IServiceCollection services,
        Action<IdeioSeoOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<SeoTextFormatter>();
        return services;
    }

    public static WebApplication MapIdeioAiSeo<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        app.MapGet("/robots.txt", (IOptions<IdeioSeoOptions> options) =>
            PlainText(BuildRobots(options.Value)))
            .ExcludeFromDescription();

        app.MapGet("/llms.txt", async (
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var data = await LoadData(db, options.Value.MaxBlogCountInLlms, cancellationToken);
            return PlainText(BuildLlms(data, options.Value, formatter));
        }).ExcludeFromDescription();

        app.MapGet("/llms-full.txt", async (
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var data = await LoadData(db, int.MaxValue, cancellationToken);
            return PlainText(BuildLlmsFull(data, options.Value, formatter));
        }).ExcludeFromDescription();

        app.MapGet("/sitemap.xml", async (
            TContext db,
            IOptions<IdeioSeoOptions> options,
            CancellationToken cancellationToken) =>
        {
            var data = await LoadData(db, int.MaxValue, cancellationToken);
            return Xml(BuildSitemap(data, options.Value));
        }).ExcludeFromDescription();

        app.MapGet("/ai/home.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var data = await LoadData(db, options.Value.MaxBlogCountInLlms, cancellationToken);
            return Markdown(http, BuildHomeMarkdown(data, options.Value, formatter));
        }).ExcludeFromDescription();

        app.MapGet("/ai/about.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var abouts = await db.Set<About>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var sites = await db.Set<SiteSetting>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var about = Preferred(abouts, x => x.Language, options.Value).FirstOrDefault();
            var site = Preferred(sites, x => x.Language, options.Value).FirstOrDefault();

            if (about is null)
                return Results.NotFound();

            var sb = Header(about.Title ?? "İdeio Creative Hakkında", options.Value.Absolute(options.Value.AboutPath));
            sb.AppendLine(formatter.ToPlainText(about.Description));
            AppendContact(sb, site);
            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/services.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var all = await db.Set<Service>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var services = Preferred(all, x => x.Language, options.Value);

            var sb = Header("İdeio Creative Hizmetleri", options.Value.Absolute(options.Value.ServicesPath));
            foreach (var service in services)
            {
                sb.AppendLine($"## {service.Title}");
                sb.AppendLine();
                sb.AppendLine(formatter.OneLine(service.HomeDescription ?? service.Description, 600));
                sb.AppendLine();
                sb.AppendLine($"- Dil: {service.Language}");
                sb.AppendLine($"- Web sayfası: {options.Value.ServiceUrl(service)}");
                sb.AppendLine($"- Markdown: {options.Value.Absolute($"/ai/services/{Uri.EscapeDataString(service.Slug)}.md")}");
                sb.AppendLine();
            }

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/services/{slug}.md", async (
            string slug,
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var matches = await db.Set<Service>().AsNoTracking()
                .Where(x => x.Slug == slug)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var service = Preferred(matches, x => x.Language, options.Value).FirstOrDefault();
            if (service is null)
                return Results.NotFound();

            var sb = Header(service.Title, options.Value.ServiceUrl(service));
            sb.AppendLine(service.MetaDescription ?? formatter.OneLine(service.HomeDescription ?? service.Description));
            sb.AppendLine();
            sb.AppendLine($"- Dil: {service.Language}");
            if (!string.IsNullOrWhiteSpace(service.Keywords))
                sb.AppendLine($"- Anahtar kelimeler: {service.Keywords}");
            sb.AppendLine();
            sb.AppendLine("## Hizmet açıklaması");
            sb.AppendLine();
            sb.AppendLine(formatter.ToPlainText(service.Description));

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/blog.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var blogs = await db.Set<Blog>().AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            var sb = Header("İdeio Creative Blog", options.Value.Absolute(options.Value.BlogPath));
            foreach (var blog in blogs)
            {
                sb.AppendLine($"## {blog.Title}");
                sb.AppendLine();
                sb.AppendLine(formatter.OneLine(blog.MetaDescription ?? blog.Description, 600));
                sb.AppendLine();
                sb.AppendLine($"- Tarih: {blog.CreatedAt:yyyy-MM-dd}");
                sb.AppendLine($"- Web sayfası: {options.Value.BlogUrl(blog)}");
                sb.AppendLine($"- Markdown: {options.Value.Absolute($"/ai/blog/{Uri.EscapeDataString(blog.Slug)}.md")}");
                sb.AppendLine();
            }

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/blog/{slug}.md", async (
            string slug,
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var blog = await db.Set<Blog>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

            if (blog is null)
                return Results.NotFound();

            var sb = Header(blog.Title, options.Value.BlogUrl(blog));
            sb.AppendLine($"- Yayın tarihi: {blog.CreatedAt:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(blog.Keywords))
                sb.AppendLine($"- Anahtar kelimeler: {blog.Keywords}");
            sb.AppendLine();
            sb.AppendLine(blog.MetaDescription ?? formatter.OneLine(blog.Description));
            sb.AppendLine();
            sb.AppendLine("## İçerik");
            sb.AppendLine();
            sb.AppendLine(formatter.ToPlainText(blog.Description));

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/team.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            SeoTextFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            var all = await db.Set<Team>().AsNoTracking()
                .OrderBy(x => x.OrderNo)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            var members = Preferred(all, x => x.Language, options.Value);

            var sb = Header("İdeio Creative Ekibi", options.Value.Absolute(options.Value.TeamPath));
            foreach (var member in members)
            {
                sb.AppendLine($"## {member.Name}");
                sb.AppendLine();
                sb.AppendLine($"- Pozisyon: {member.Position}");
                sb.AppendLine($"- Dil: {member.Language}");
                sb.AppendLine($"- Web sayfası: {options.Value.TeamUrl(member)}");
                if (!string.IsNullOrWhiteSpace(member.Description))
                    sb.AppendLine($"- Hakkında: {formatter.OneLine(member.Description, 600)}");
                sb.AppendLine();
            }

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/ai/references.md", async (
            HttpContext http,
            TContext db,
            IOptions<IdeioSeoOptions> options,
            CancellationToken cancellationToken) =>
        {
            var all = await db.Set<Reference>().AsNoTracking()
                .OrderByDescending(x => x.IsHome)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
            var references = Preferred(all, x => x.Language, options.Value);

            var sb = Header("İdeio Creative Referansları", options.Value.Absolute("/#referanslar"));
            foreach (var reference in references)
                sb.AppendLine($"- {reference.Name}");

            return Markdown(http, sb.ToString());
        }).ExcludeFromDescription();

        return app;
    }

    private static async Task<SeoData> LoadData<TContext>(
        TContext db,
        int maxBlogs,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var sites = await db.Set<SiteSetting>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var abouts = await db.Set<About>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var services = await db.Set<Service>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var teams = await db.Set<Team>().AsNoTracking().OrderBy(x => x.OrderNo).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var references = await db.Set<Reference>().AsNoTracking().OrderByDescending(x => x.IsHome).ThenBy(x => x.Name).ToListAsync(cancellationToken);

        var blogQuery = db.Set<Blog>().AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var blogs = maxBlogs == int.MaxValue
            ? await blogQuery.ToListAsync(cancellationToken)
            : await blogQuery.Take(Math.Max(maxBlogs, 0)).ToListAsync(cancellationToken);

        return new SeoData(sites, abouts, services, teams, references, blogs);
    }

    private static string BuildRobots(IdeioSeoOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /admin/");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /account/");
        sb.AppendLine("Disallow: /Account/");
        sb.AppendLine();
        sb.AppendLine("User-agent: OAI-SearchBot");
        sb.AppendLine("Allow: /");
        sb.AppendLine();
        sb.AppendLine("User-agent: ChatGPT-User");
        sb.AppendLine("Allow: /");
        sb.AppendLine();
        sb.AppendLine("User-agent: ClaudeBot");
        sb.AppendLine("Allow: /");
        sb.AppendLine();
        sb.AppendLine("User-agent: PerplexityBot");
        sb.AppendLine("Allow: /");
        sb.AppendLine();
        sb.AppendLine("User-agent: GPTBot");
        sb.AppendLine(options.AllowGptBotForTraining ? "Allow: /" : "Disallow: /");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {options.Absolute("/sitemap.xml")}");
        return sb.ToString();
    }

    private static string BuildLlms(SeoData data, IdeioSeoOptions options, SeoTextFormatter formatter)
    {
        var site = Preferred(data.Sites, x => x.Language, options).FirstOrDefault();
        var about = Preferred(data.Abouts, x => x.Language, options).FirstOrDefault();
        var services = Preferred(data.Services, x => x.Language, options);
        var members = Preferred(data.Teams, x => x.Language, options);
        var references = Preferred(data.References, x => x.Language, options);

        var sb = new StringBuilder();
        sb.AppendLine($"# {options.BrandName}");
        sb.AppendLine();
        sb.AppendLine($"> {site?.MetaDescription ?? formatter.OneLine(about?.HomeDescription ?? about?.Description, 500)}");
        sb.AppendLine();
        var aboutText = formatter.OneLine(about?.HomeDescription ?? about?.Description, 900);
        if (!string.IsNullOrWhiteSpace(aboutText))
        {
            sb.AppendLine(aboutText);
            sb.AppendLine();
        }
        sb.AppendLine("## Resmî sayfalar");
        sb.AppendLine();
        sb.AppendLine($"- [Anasayfa]({options.Absolute("/")})");
        sb.AppendLine($"- [Hakkımızda]({options.Absolute(options.AboutPath)})");
        sb.AppendLine($"- [Hizmetler]({options.Absolute(options.ServicesPath)})");
        sb.AppendLine($"- [Ekip]({options.Absolute(options.TeamPath)})");
        sb.AppendLine($"- [Blog]({options.Absolute(options.BlogPath)})");
        sb.AppendLine($"- [İletişim]({options.Absolute(options.ContactPath)})");
        sb.AppendLine();
        sb.AppendLine("## Hizmetler");
        sb.AppendLine();
        foreach (var service in services)
        {
            sb.AppendLine($"- [{service.Title}]({options.ServiceUrl(service)}): {formatter.OneLine(service.HomeDescription ?? service.Description, 260)}");
        }

        if (data.Blogs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Son blog yazıları");
            sb.AppendLine();
            foreach (var blog in data.Blogs)
                sb.AppendLine($"- [{blog.Title}]({options.BlogUrl(blog)}): {formatter.OneLine(blog.MetaDescription ?? blog.Description, 220)}");
        }

        if (members.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Ekip");
            sb.AppendLine();
            foreach (var member in members)
                sb.AppendLine($"- {member.Name} — {member.Position}");
        }

        if (references.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Referanslar");
            sb.AppendLine();
            sb.AppendLine(string.Join(", ", references.Select(x => x.Name)));
        }

        AppendContact(sb, site);
        sb.AppendLine();
        sb.AppendLine("## Makine tarafından okunabilir içerikler");
        sb.AppendLine();
        sb.AppendLine($"- [Tam içerik özeti]({options.Absolute("/llms-full.txt")})");
        sb.AppendLine($"- [Anasayfa Markdown]({options.Absolute("/ai/home.md")})");
        sb.AppendLine($"- [Hizmetler Markdown]({options.Absolute("/ai/services.md")})");
        sb.AppendLine($"- [Blog Markdown]({options.Absolute("/ai/blog.md")})");
        sb.AppendLine($"- [Ekip Markdown]({options.Absolute("/ai/team.md")})");
        sb.AppendLine($"- [Referanslar Markdown]({options.Absolute("/ai/references.md")})");

        return sb.ToString();
    }

    private static string BuildLlmsFull(SeoData data, IdeioSeoOptions options, SeoTextFormatter formatter)
    {
        var site = Preferred(data.Sites, x => x.Language, options).FirstOrDefault();
        var abouts = Preferred(data.Abouts, x => x.Language, options);
        var services = Preferred(data.Services, x => x.Language, options);
        var teams = Preferred(data.Teams, x => x.Language, options);
        var references = Preferred(data.References, x => x.Language, options);

        var sb = new StringBuilder(BuildLlms(data, options, formatter));

        foreach (var about in abouts)
        {
            sb.AppendLine();
            sb.AppendLine($"# {about.Title ?? "Hakkımızda"}");
            sb.AppendLine();
            sb.AppendLine(formatter.ToPlainText(about.Description));
        }

        sb.AppendLine();
        sb.AppendLine("# Hizmet ayrıntıları");
        foreach (var service in services)
        {
            sb.AppendLine();
            sb.AppendLine($"## {service.Title}");
            sb.AppendLine();
            sb.AppendLine($"Kaynak: {options.ServiceUrl(service)}");
            sb.AppendLine();
            sb.AppendLine(formatter.ToPlainText(service.Description));
        }

        if (data.Blogs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Blog içerikleri");
            foreach (var blog in data.Blogs)
            {
                sb.AppendLine();
                sb.AppendLine($"## {blog.Title}");
                sb.AppendLine();
                sb.AppendLine($"Yayın tarihi: {blog.CreatedAt:yyyy-MM-dd}");
                sb.AppendLine($"Kaynak: {options.BlogUrl(blog)}");
                sb.AppendLine();
                sb.AppendLine(formatter.ToPlainText(blog.Description));
            }
        }

        if (teams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Ekip ayrıntıları");
            foreach (var member in teams)
            {
                sb.AppendLine();
                sb.AppendLine($"## {member.Name} — {member.Position}");
                if (!string.IsNullOrWhiteSpace(member.Description))
                    sb.AppendLine(formatter.ToPlainText(member.Description));
            }
        }

        if (references.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Referanslar");
            foreach (var reference in references)
                sb.AppendLine($"- {reference.Name}");
        }

        AppendContact(sb, site);
        return sb.ToString();
    }

    private static string BuildHomeMarkdown(SeoData data, IdeioSeoOptions options, SeoTextFormatter formatter)
    {
        var site = Preferred(data.Sites, x => x.Language, options).FirstOrDefault();
        var about = Preferred(data.Abouts, x => x.Language, options).FirstOrDefault();
        var services = Preferred(data.Services, x => x.Language, options);
        var teams = Preferred(data.Teams, x => x.Language, options)
            .Where(x => x.IsHomePage)
            .ToList();
        var references = Preferred(data.References, x => x.Language, options)
            .Where(x => x.IsHome)
            .ToList();

        var sb = Header(options.BrandName, options.Absolute("/"));
        sb.AppendLine(site?.MetaDescription ?? formatter.OneLine(about?.HomeDescription ?? about?.Description, 600));

        if (about is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"## {about.HomeTitle ?? about.Title ?? "Hakkımızda"}");
            sb.AppendLine();
            sb.AppendLine(formatter.ToPlainText(about.HomeDescription));
        }

        sb.AppendLine();
        sb.AppendLine("## Hizmetler");
        foreach (var service in services)
        {
            sb.AppendLine();
            sb.AppendLine($"### {service.Title}");
            sb.AppendLine(formatter.OneLine(service.HomeDescription ?? service.Description, 600));
            sb.AppendLine($"Kaynak: {options.ServiceUrl(service)}");
        }

        if (references.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Referanslar");
            sb.AppendLine(string.Join(", ", references.Select(x => x.Name)));
        }

        if (teams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Ekip");
            foreach (var member in teams)
                sb.AppendLine($"- {member.Name} — {member.Position}");
        }

        AppendContact(sb, site);
        return sb.ToString();
    }

    private static string BuildSitemap(SeoData data, IdeioSeoOptions options)
    {
        var entries = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);

        AddSitemapEntry(entries, options.Absolute("/"), null);
        AddSitemapEntry(entries, options.Absolute(options.AboutPath), null);
        AddSitemapEntry(entries, options.Absolute(options.ServicesPath), null);
        AddSitemapEntry(entries, options.Absolute(options.TeamPath), null);
        AddSitemapEntry(entries, options.Absolute(options.BlogPath), null);
        AddSitemapEntry(entries, options.Absolute(options.ContactPath), null);

        foreach (var path in options.AdditionalStaticPaths)
            AddSitemapEntry(entries, options.Absolute(path), null);

        foreach (var service in data.Services)
            AddSitemapEntry(entries, options.ServiceUrl(service), null);

        if (options.IncludeTeamDetailPages)
        {
            foreach (var member in data.Teams)
                AddSitemapEntry(entries, options.TeamUrl(member), null);
        }

        foreach (var blog in data.Blogs)
            AddSitemapEntry(entries, options.BlogUrl(blog), blog.CreatedAt);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                entries.Select(entry =>
                    new XElement(ns + "url",
                        new XElement(ns + "loc", entry.Key),
                        entry.Value.HasValue
                            ? new XElement(ns + "lastmod", entry.Value.Value.ToString("yyyy-MM-dd"))
                            : null))));

        return document.ToString();
    }

    private static List<T> Preferred<T>(
        IEnumerable<T> values,
        Func<T, Language> languageSelector,
        IdeioSeoOptions options)
    {
        var list = values.ToList();
        var preferred = list.Where(x => options.IsPreferredLanguage(languageSelector(x))).ToList();
        return preferred.Count > 0 ? preferred : list;
    }

    private static void AddSitemapEntry(
        IDictionary<string, DateTime?> entries,
        string url,
        DateTime? lastModified)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!entries.TryGetValue(url, out var current) ||
            (lastModified.HasValue && (!current.HasValue || lastModified > current)))
        {
            entries[url] = lastModified;
        }
    }

    private static StringBuilder Header(string title, string canonicalUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"> Kaynak: {canonicalUrl}");
        sb.AppendLine();
        return sb;
    }

    private static void AppendContact(StringBuilder sb, SiteSetting? site)
    {
        if (site is null)
            return;

        if (string.IsNullOrWhiteSpace(site.Phone) &&
            string.IsNullOrWhiteSpace(site.Email) &&
            string.IsNullOrWhiteSpace(site.Address))
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("## İletişim");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(site.Phone))
            sb.AppendLine($"- Telefon: {site.Phone}");
        if (!string.IsNullOrWhiteSpace(site.Email))
            sb.AppendLine($"- E-posta: {site.Email}");
        if (!string.IsNullOrWhiteSpace(site.Address))
            sb.AppendLine($"- Adres: {site.Address}");
    }

    private static IResult PlainText(string content) =>
        Results.Text(content, "text/plain; charset=utf-8", Encoding.UTF8);

    private static IResult Xml(string content) =>
        Results.Text(content, "application/xml; charset=utf-8", Encoding.UTF8);

    private static IResult Markdown(HttpContext http, string content)
    {
        http.Response.Headers["X-Robots-Tag"] = "noindex, follow";
        http.Response.Headers["Cache-Control"] = "public, max-age=300";
        return Results.Text(content, "text/markdown; charset=utf-8", Encoding.UTF8);
    }

    private sealed record SeoData(
        List<SiteSetting> Sites,
        List<About> Abouts,
        List<Service> Services,
        List<Team> Teams,
        List<Reference> References,
        List<Blog> Blogs);
}

using System.Xml.Linq;
using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Seo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class SitemapTests
{
    [Fact]
    public async Task RealEditsAndDeletionsAreRecordedButNoOpSavesAndContactSubmissionsAreNot()
    {
        var clock = new ManualClock();
        await using var db = CreateContext(clock);
        var service = NewService("seo");
        db.Services.Add(service);
        db.SaveChanges();
        var originalRevisionCount = await db.SeoPageModifications.CountAsync();

        clock.Advance();
        db.Update(service);
        await db.SaveChangesAsync();
        Assert.Equal(originalRevisionCount, await db.SeoPageModifications.CountAsync());

        db.ContactForms.Add(new ContactForm
        {
            Name = "Contact form", Email = "contact@example.test", Message = "A message"
        });
        await db.SaveChangesAsync();
        Assert.Equal(originalRevisionCount, await db.SeoPageModifications.CountAsync());

        service.Description = "New content";
        await db.SaveChangesAsync();
        Assert.Contains(await db.SeoPageModifications.ToListAsync(), revision =>
            revision.PagePath == "/hizmetlerimiz/seo" && revision.LastModifiedUtc == clock.GetUtcNow().UtcDateTime);

        clock.Advance();
        db.Services.Remove(service);
        db.SaveChanges();
        Assert.Contains(await db.SeoPageModifications.ToListAsync(), revision =>
            revision.PagePath == "/hizmetlerimiz" && revision.LastModifiedUtc == clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task PrivateSmtpEditsDoNotChangePageDates()
    {
        var clock = new ManualClock();
        await using var db = CreateContext(clock);
        var settings = new SiteSetting { Language = Language.TR, Phone = "123" };
        db.SiteSettings.Add(settings);
        await db.SaveChangesAsync();
        var originalCount = await db.SeoPageModifications.CountAsync();

        clock.Advance();
        settings.SmtpServer = "smtp.example.test";
        await db.SaveChangesAsync();
        Assert.Equal(originalCount, await db.SeoPageModifications.CountAsync());

        settings.Phone = "456";
        await db.SaveChangesAsync();
        Assert.Equal(originalCount + 1, await db.SeoPageModifications.CountAsync());
    }

    [Fact]
    public void FailedSavesDoNotPersistRevisionDatesAndRetryDoesNotDuplicateThem()
    {
        var clock = new ManualClock();
        var failure = new FailFirstSave();
        var databaseName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName).AddInterceptors(failure).Options;
        using var db = new DatabaseContext(dbOptions, timeProvider: clock);
        db.Services.Add(NewService("seo"));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        using (var persisted = new DatabaseContext(dbOptions, timeProvider: clock))
            Assert.Empty(persisted.SeoPageModifications);

        var pendingCount = db.SeoPageModifications.Local.Count;
        clock.Advance();
        db.SaveChanges();
        Assert.Equal(pendingCount, db.SeoPageModifications.Count());
        Assert.All(db.SeoPageModifications, revision =>
            Assert.Equal(clock.GetUtcNow().UtcDateTime, revision.LastModifiedUtc));
    }

    [Fact]
    public async Task SitemapHasSixteenDatedCanonicalUrlsAndReflectsContentChanges()
    {
        var clock = new ManualClock();
        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddIdeioAiSeo(options =>
        {
            options.PageTemplatesLastModified = new DateTime(2026, 1, 1);
            options.AdditionalStaticPaths.Add("/ucretsiz-reklam-denetimi");
        });
        await using var app = builder.Build();
        app.MapIdeioAiSeo<DatabaseContext>();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Services.AddRange(Enumerable.Range(1, 9).Select(index => NewService($"service-{index}")));
            db.Services.Add(new Service { Title = "English", Slug = "english", Language = Language.EN });
            db.Blogs.Add(new Blog
            {
                Title = "Scheduled", Description = "Not public yet", Slug = "scheduled",
                CreatedAt = DateTime.Now.AddDays(7)
            });
            await db.SaveChangesAsync();
        }

        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var initial = await ReadSitemap(http);
        Assert.Equal(16, initial.Count);
        Assert.All(initial, entry => Assert.Equal("2026-10-01", entry.Value));
        Assert.All(initial.Keys, url => Assert.StartsWith("https://ideiocreative.com/", url));
        Assert.DoesNotContain(initial.Keys, url => url.Contains("scheduled") || url.Contains("english"));
        Assert.Equal(initial.OrderBy(entry => entry.Key), (await ReadSitemap(http)).OrderBy(entry => entry.Key));

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            (await db.Services.SingleAsync(service => service.Slug == "service-1")).Description = "Edited";
            await db.SaveChangesAsync();
        }
        var edited = await ReadSitemap(http);
        Assert.Equal("2026-10-02", edited["https://ideiocreative.com/hizmetlerimiz/service-1"]);
        Assert.Equal("2026-10-02", edited["https://ideiocreative.com/hizmetlerimiz"]);
        Assert.Equal("2026-10-01", edited["https://ideiocreative.com/hizmetlerimiz/service-2"]);

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Services.Remove(await db.Services.SingleAsync(service => service.Slug == "service-1"));
            await db.SaveChangesAsync();
        }
        var deleted = await ReadSitemap(http);
        Assert.Equal(15, deleted.Count);
        Assert.DoesNotContain("https://ideiocreative.com/hizmetlerimiz/service-1", deleted.Keys);
        Assert.Equal("2026-10-03", deleted["https://ideiocreative.com/hizmetlerimiz"]);

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Blogs.Add(new Blog
            {
                Title = "Published", Description = "Original", Slug = "published",
                CreatedAt = new DateTime(2026, 9, 1)
            });
            await db.SaveChangesAsync();
        }
        Assert.Equal(16, (await ReadSitemap(http)).Count);

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            (await db.Blogs.SingleAsync(blog => blog.Slug == "published")).Description = "Blog edit";
            await db.SaveChangesAsync();
        }
        var blogEdited = await ReadSitemap(http);
        Assert.Equal("2026-10-05", blogEdited["https://ideiocreative.com/blog/published"]);
        Assert.Equal("2026-10-05", blogEdited["https://ideiocreative.com/blog"]);
        Assert.Equal("2026-10-03", blogEdited["https://ideiocreative.com/hizmetlerimiz/service-2"]);
    }

    [Fact]
    public async Task SectorUrlsEnterTheSitemapWhenPublishedContentExistsAndFaqDatesFollowTheirPageType()
    {
        var clock = new ManualClock();
        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddIdeioAiSeo(options => options.PageTemplatesLastModified = new DateTime(2026, 1, 1));
        await using var app = builder.Build();
        app.MapIdeioAiSeo<DatabaseContext>();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Services.Add(NewService("legacy-service"));
            db.Services.Add(new Service
            {
                Title = "English sector", Slug = "english-sector", Language = Language.EN, PageType = ServicePageType.Sector
            });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string sectorIndex = "https://ideiocreative.com/sektorler";
        const string sectorUrl = sectorIndex + "/sector-fixture";
        var empty = await ReadSitemap(http);
        Assert.DoesNotContain(sectorIndex, empty.Keys);
        Assert.DoesNotContain(empty.Keys, url => url.Contains("english-sector"));

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Services.Add(new Service
            {
                Title = "Sector fixture", Slug = "sector-fixture", Language = Language.TR, PageType = ServicePageType.Sector,
                Faqs = { new ServiceFaq { Question = "Sector question?", Answer = "Sector answer." } }
            });
            await db.SaveChangesAsync();
        }
        var published = await ReadSitemap(http);
        Assert.Contains(sectorIndex, published.Keys);
        Assert.Contains(sectorUrl, published.Keys);
        Assert.Contains("https://ideiocreative.com/hizmetlerimiz/legacy-service", published.Keys);
        Assert.DoesNotContain("https://ideiocreative.com/hizmetlerimiz/sector-fixture", published.Keys);

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            (await db.Set<ServiceFaq>().SingleAsync()).Answer = "Edited sector answer.";
            await db.SaveChangesAsync();
        }
        var edited = await ReadSitemap(http);
        Assert.Equal("2026-10-03", edited[sectorUrl]);
        Assert.Equal(published[sectorIndex], edited[sectorIndex]);
        Assert.Equal(published["https://ideiocreative.com/hizmetlerimiz/legacy-service"],
            edited["https://ideiocreative.com/hizmetlerimiz/legacy-service"]);

        clock.Advance();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            (await db.Services.SingleAsync(service => service.Slug == "sector-fixture")).PageType = ServicePageType.Service;
            await db.SaveChangesAsync();
            var revisions = await db.SeoPageModifications.Where(revision => revision.LastModifiedUtc == clock.GetUtcNow().UtcDateTime)
                .Select(revision => revision.PagePath).ToListAsync();
            Assert.Contains("/sektorler", revisions);
            Assert.Contains("/hizmetlerimiz", revisions);
            Assert.Contains("/sektorler/sector-fixture", revisions);
            Assert.Contains("/hizmetlerimiz/sector-fixture", revisions);
        }
        var moved = await ReadSitemap(http);
        Assert.DoesNotContain(sectorIndex, moved.Keys);
        Assert.DoesNotContain(sectorUrl, moved.Keys);
        Assert.Contains("https://ideiocreative.com/hizmetlerimiz/sector-fixture", moved.Keys);
        Assert.Contains("https://ideiocreative.com/hizmetlerimiz/legacy-service", moved.Keys);
    }

    [Fact]
    public void PageTypeMigrationOnlyAddsAColumnWithServiceAsTheDefault()
    {
        using var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer("Server=localhost;Database=SitemapTests;Trusted_Connection=True;").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var entry = Assert.Single(assembly.Migrations, migration => migration.Key.EndsWith("_ServicePageTypes"));
        var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Services", column.Table);
        Assert.Equal("PageType", column.Name);
        Assert.False(column.IsNullable);
        Assert.Equal(0, column.DefaultValue);
        Assert.Equal(typeof(ServicePageType), db.Model.FindEntityType(typeof(Service))!.FindProperty(nameof(Service.PageType))!.ClrType);
    }

    [Fact]
    public async Task PublishedBlogUsesItsActualEditDateAndQueryParametersAreExcluded()
    {
        var clock = new ManualClock();
        await using var db = CreateContext(clock);
        var blog = new Blog
        {
            Title = "Published", Description = "Original", Slug = "published",
            CreatedAt = new DateTime(2026, 9, 1)
        };
        db.Blogs.Add(blog);
        await db.SaveChangesAsync();
        clock.Advance();
        blog.Description = "Edited";
        await db.SaveChangesAsync();
        Assert.Contains(await db.SeoPageModifications.ToListAsync(), revision =>
            revision.PagePath == "/blog/published" && revision.LastModifiedUtc == clock.GetUtcNow().UtcDateTime);

        var options = new IdeioSeoOptions { AdditionalStaticPaths = { "/example?utm=x&gclid=y" } };
        var paths = SeoPageModificationTracker.ChangedPagePaths(db.ChangeTracker, options, DateTime.Now);
        Assert.Empty(paths);
        db.Services.Add(NewService("another-service"));
        paths = SeoPageModificationTracker.ChangedPagePaths(db.ChangeTracker, options, DateTime.Now);
        Assert.Contains("/example", paths);
        Assert.DoesNotContain(paths, path => path.Contains('?'));

        db.Services.Add(NewService("Case-Service"));
        paths = SeoPageModificationTracker.ChangedPagePaths(db.ChangeTracker, options, DateTime.Now);
        Assert.Contains("/hizmetlerimiz/case-service", paths);
    }

    [Fact]
    public void SqlServerCanTranslateTheRevisionAggregationAndContainsTheMigration()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer("Server=localhost;Database=SitemapTests;Trusted_Connection=True;").Options;
        using var db = new DatabaseContext(options);
        var sql = db.SeoPageModifications.GroupBy(revision => revision.PagePath)
            .Select(group => new { PagePath = group.Key, LastModifiedUtc = group.Max(item => item.LastModifiedUtc) })
            .ToQueryString();
        Assert.Contains("MAX(", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains(db.Database.GetMigrations(), migration => migration.EndsWith("_SeoPageModifications"));
    }

    private static DatabaseContext CreateContext(ManualClock clock) => new(
        new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        timeProvider: clock);

    private static Service NewService(string slug) => new()
    {
        Title = slug, Slug = slug, Language = Language.TR
    };

    private static async Task<Dictionary<string, string>> ReadSitemap(HttpClient http)
    {
        using var response = await http.GetAsync("/sitemap.xml?utm=example&gclid=example");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var entries = document.Root!.Elements(ns + "url").ToDictionary(
            url => url.Element(ns + "loc")!.Value, url => url.Element(ns + "lastmod")!.Value);
        Assert.DoesNotContain(entries.Keys, url => url.Contains('?') || url.Contains('#'));
        return entries;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public void Advance() => _now = _now.AddDays(1);
    }

    private sealed class FailFirstSave : SaveChangesInterceptor
    {
        private bool _fail = true;
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (_fail)
            {
                _fail = false;
                throw new InvalidOperationException("Simulated save failure");
            }
            return result;
        }
    }
}

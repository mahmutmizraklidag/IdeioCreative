using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using IdeioCreative.Controllers;
using IdeioCreative.Data;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class FaqTests
{
    [Fact]
    public async Task AdminCanCreateThenAddRemoveReorderAndUnpublishServiceFaqs()
    {
        await using var host = await FaqAdminHost.Start();
        host.Authenticate();
        var form = ServiceForm("New service", "new-service");
        AddFaq(form, 0, 0, "  First question?  ", "  First answer.  ", true);
        AddFaq(form, 1, 0, "Second question?", "Second answer.", true);
        AddFaq(form, 2, 0, "Draft question?", "Draft answer.", false);
        AddFaq(form, 3, 0, "   ", "\t", true);
        using (var created = await host.PostForm("/admin/Services/Create", form))
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var service = await host.ReadService("new-service");
        var initial = service.Faqs.OrderBy(faq => faq.SortOrder).ToList();
        Assert.Equal(3, initial.Count);
        Assert.Equal(new[] { "First question?", "Second question?", "Draft question?" }, initial.Select(faq => faq.Question));
        Assert.Equal("First answer.", initial[0].Answer);
        Assert.False(initial[2].IsPublished);

        var edit = ServiceForm("New service", "new-service", service.Id);
        AddFaq(edit, 0, initial[1].Id, "Second question?", "Second answer.", false);
        AddFaq(edit, 1, 0, "Added question?", "Added answer.", true);
        AddFaq(edit, 2, initial[0].Id, "First question?", "Updated first answer.", true);
        using (var edited = await host.PostForm($"/admin/Services/Edit/{service.Id}", edit))
            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        var updated = (await host.ReadService("new-service")).Faqs.OrderBy(faq => faq.SortOrder).ToList();
        Assert.Equal(new[] { "Second question?", "Added question?", "First question?" }, updated.Select(faq => faq.Question));
        Assert.Equal(new[] { 0, 1, 2 }, updated.Select(faq => faq.SortOrder));
        Assert.Equal(initial[1].Id, updated[0].Id);
        Assert.False(updated[0].IsPublished);
        Assert.Equal(initial[0].Id, updated[2].Id);
        Assert.Equal("Updated first answer.", updated[2].Answer);
        using var scope = host.App.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<DatabaseContext>().Set<ServiceFaq>()
            .AnyAsync(faq => faq.Id == initial[2].Id));
    }

    [Fact]
    public async Task AdminFaqValidationPreservesInputAndRejectsUnauthorizedOrForeignIdsWithoutMutation()
    {
        await using var host = await FaqAdminHost.Start();
        using (var anonymous = await host.Http.GetAsync("/admin/Services/Create"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Authenticate();
        using (var noToken = await host.Http.PostAsync("/admin/Services/Create", new FormUrlEncodedContent(ServiceForm("Rejected", "rejected"))))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var partial = ServiceForm("Should not persist", "owned", 1);
        const string enteredQuestion = "  Kept <question>?  ";
        AddFaq(partial, 0, 41, enteredQuestion, "", true);
        using (var invalid = await host.PostForm("/admin/Services/Edit/1", partial))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            var html = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("data-valmsg-for=\"Faqs[0].Answer\"", html);
            var input = Assert.Single(Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>(),
                match => Attribute(match.Value, "name") == "Faqs[0].Question");
            Assert.Equal(enteredQuestion, Attribute(input.Value, "value"));
        }

        var foreign = ServiceForm("Should not persist", "owned", 1);
        AddFaq(foreign, 0, 42, "Hijacked question?", "Hijacked answer.", true);
        using (var invalid = await host.PostForm("/admin/Services/Edit/1", foreign))
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var duplicate = ServiceForm("Should not persist", "owned", 1);
        AddFaq(duplicate, 0, 41, "Duplicate one?", "One answer.", true);
        AddFaq(duplicate, 1, 41, "Duplicate two?", "Two answer.", true);
        using (var invalid = await host.PostForm("/admin/Services/Edit/1", duplicate))
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var createWithForeignId = ServiceForm("Should not be created", "foreign-create");
        AddFaq(createWithForeignId, 0, 42, "Hijacked question?", "Hijacked answer.", true);
        using (var invalid = await host.PostForm("/admin/Services/Create", createWithForeignId))
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

        var owned = await host.ReadService("owned");
        Assert.Equal("Owned service", owned.Title);
        Assert.Equal("Owned question?", Assert.Single(owned.Faqs).Question);
        var protectedFaq = Assert.Single((await host.ReadService("other")).Faqs);
        Assert.Equal(42, protectedFaq.Id);
        Assert.Equal(2, protectedFaq.ServiceId);
        Assert.Equal("Protected question?", protectedFaq.Question);
        using var scope = host.App.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DatabaseContext>().Services.CountAsync());
    }

    [Fact]
    public async Task AdminCanSelectPageTypeAndSwitchItWithoutLosingFaqsWhileInvalidTypesAreRejected()
    {
        await using var host = await FaqAdminHost.Start();
        host.Authenticate();
        var create = ServiceForm("Sector admin fixture", "sector-admin");
        create["PageType"] = "1";
        AddFaq(create, 0, 0, "Sector admin question?", "Sector admin answer.", true);
        using (var created = await host.PostForm("/admin/Services/Create", create))
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var sector = await host.ReadService("sector-admin");
        Assert.Equal(ServicePageType.Sector, sector.PageType);
        var faqId = Assert.Single(sector.Faqs).Id;
        using (var editPage = await host.Http.GetAsync($"/admin/Services/Edit/{sector.Id}"))
        {
            var html = await editPage.Content.ReadAsStringAsync();
            Assert.Contains("name=\"PageType\"", html);
            var typeSelect = Regex.Match(html, "<select\\b[^>]*name=\"PageType\"[^>]*>([\\s\\S]*?)</select>", RegexOptions.IgnoreCase);
            var selectedSector = Assert.Single(Regex.Matches(typeSelect.Groups[1].Value, "<option\\b[^>]*>").Cast<Match>(),
                option => Attribute(option.Value, "value") == "1" && Regex.IsMatch(option.Value, "\\sselected(?:\\s|=|>)"));
            Assert.NotEmpty(selectedSector.Value);
        }
        var invalid = ServiceForm("Should not persist", "sector-admin", sector.Id);
        invalid["PageType"] = "999";
        AddFaq(invalid, 0, faqId, "Sector admin question?", "Sector admin answer.", true);
        using (var rejected = await host.PostForm($"/admin/Services/Edit/{sector.Id}", invalid))
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(ServicePageType.Sector, (await host.ReadService("sector-admin")).PageType);
        Assert.Equal("Sector admin fixture", (await host.ReadService("sector-admin")).Title);

        var switchType = ServiceForm("Sector admin fixture", "sector-admin", sector.Id);
        switchType["PageType"] = "0";
        AddFaq(switchType, 0, faqId, "Sector admin question?", "Sector admin answer.", true);
        using (var updated = await host.PostForm($"/admin/Services/Edit/{sector.Id}", switchType))
            Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        var service = await host.ReadService("sector-admin");
        Assert.Equal(ServicePageType.Service, service.PageType);
        Assert.Equal(faqId, Assert.Single(service.Faqs).Id);
        Assert.Equal("Sector admin answer.", Assert.Single(service.Faqs).Answer);
    }

    [Fact]
    public async Task AdminCanSaveMoreThanTheDefaultMvcCollectionLimit()
    {
        await using var host = await FaqAdminHost.Start();
        host.Authenticate();
        var form = ServiceForm("Large FAQ service", "large-faq-service");
        for (var index = 0; index < 1025; index++)
            AddFaq(form, index, 0, $"Question {index}?", $"Answer {index}.", true);
        using var response = await host.PostForm("/admin/Services/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var faqs = (await host.ReadService("large-faq-service")).Faqs.OrderBy(faq => faq.SortOrder).ToList();
        Assert.Equal(1025, faqs.Count);
        Assert.Equal("Question 0?", faqs[0].Question);
        Assert.Equal("Question 1024?", faqs[^1].Question);
    }

    [Fact]
    public async Task OnlyVisibleFaqChangesReviseTheParentDetailDateAndTheRelationshipCascades()
    {
        var clock = new ManualClock();
        var options = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new DatabaseContext(options, timeProvider: clock))
        {
            seed.Services.AddRange(new Service { Id = 1, Title = "Primary", Slug = "primary", Language = Language.TR },
                new Service { Id = 2, Title = "Other", Slug = "other", Language = Language.TR });
            await seed.SaveChangesAsync();
        }
        await using var db = new DatabaseContext(options, timeProvider: clock);
        var latestRevisionId = await db.SeoPageModifications.MaxAsync(revision => revision.Id);
        async Task SaveAndCheck(bool visibleChange)
        {
            clock.Advance();
            await db.SaveChangesAsync();
            var revisions = await db.SeoPageModifications.Where(revision => revision.Id > latestRevisionId).ToListAsync();
            if (visibleChange)
            {
                var revision = Assert.Single(revisions);
                Assert.Equal("/hizmetlerimiz/primary", revision.PagePath);
                Assert.Equal(clock.GetUtcNow().UtcDateTime, revision.LastModifiedUtc);
                latestRevisionId = revision.Id;
            }
            else
                Assert.Empty(revisions);
        }

        // The parent remains unloaded while FAQ changes are saved.
        var faq = new ServiceFaq { ServiceId = 1, Question = "Question?", Answer = "Answer.", IsPublished = false };
        db.Set<ServiceFaq>().Add(faq);
        await SaveAndCheck(false);
        faq.Answer = "Draft edit.";
        await SaveAndCheck(false);
        faq.IsPublished = true;
        await SaveAndCheck(true);
        faq.Question = "  Question?  ";
        await SaveAndCheck(false);
        faq.Answer = "Published edit.";
        await SaveAndCheck(true);
        var second = new ServiceFaq { ServiceId = 1, Question = "Second?", Answer = "Second answer.", SortOrder = 1 };
        db.Set<ServiceFaq>().Add(second);
        await SaveAndCheck(true);
        faq.SortOrder = 2;
        await SaveAndCheck(true);
        faq.IsPublished = false;
        await SaveAndCheck(true);
        faq.Answer = "Another hidden draft edit.";
        await SaveAndCheck(false);
        faq.IsPublished = true;
        await SaveAndCheck(true);
        db.Remove(faq);
        await SaveAndCheck(true);
        await SaveAndCheck(false);

        var service = await db.Services.Include(item => item.Faqs).SingleAsync(item => item.Id == 1);
        db.Services.Remove(service);
        await db.SaveChangesAsync();
        Assert.Empty(await db.Set<ServiceFaq>().Where(item => item.ServiceId == 1).ToListAsync());
        Assert.True(await db.Services.AnyAsync(item => item.Id == 2));
        var foreignKey = Assert.Single(db.Model.FindEntityType(typeof(ServiceFaq))!.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);

        using var sqlDb = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer("Server=localhost;Database=FaqTests;Trusted_Connection=True;").Options);
        Assert.Contains(sqlDb.Database.GetMigrations(), migration => migration.EndsWith("_ServiceFaqs"));
        Assert.Contains("ON DELETE CASCADE", sqlDb.Database.GenerateCreateScript());
    }

    private static Dictionary<string, string> ServiceForm(string title, string slug, int id = 0) => new()
    {
        ["Id"] = id.ToString(CultureInfo.InvariantCulture), ["Title"] = title, ["Slug"] = slug,
        ["Language"] = ((int)Language.TR).ToString(CultureInfo.InvariantCulture), ["Description"] = "Service content"
    };

    private static void AddFaq(Dictionary<string, string> form, int index, int id, string question, string answer, bool published)
    {
        var prefix = $"Faqs[{index}]";
        form[prefix + ".Id"] = id.ToString(CultureInfo.InvariantCulture);
        form[prefix + ".Question"] = question;
        form[prefix + ".Answer"] = answer;
        form[prefix + ".IsPublished"] = published ? "true" : "false";
    }

    private static string Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, "\\s" + Regex.Escape(name) + "=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class FaqAdminHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Http { get; }

        private FaqAdminHost(WebApplication app)
        {
            App = app;
            Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        }

        public void Authenticate() => Http.DefaultRequestHeaders.Add("X-Test-User", "admin");

        public async Task<HttpResponseMessage> PostForm(string path, Dictionary<string, string> form)
        {
            using var get = await Http.GetAsync(path);
            get.EnsureSuccessStatusCode();
            var html = await get.Content.ReadAsStringAsync();
            Assert.Contains("data-faq-editor", html);
            var input = Assert.Single(Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>(),
                match => Attribute(match.Value, "name") == "__RequestVerificationToken");
            form["__RequestVerificationToken"] = Attribute(input.Value, "value");
            return await Http.PostAsync(path, new FormUrlEncodedContent(form));
        }

        public async Task<Service> ReadService(string slug)
        {
            using var scope = App.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<DatabaseContext>().Services.AsNoTracking()
                .Include(service => service.Faqs).SingleAsync(service => service.Slug == slug);
        }

        public static async Task<FaqAdminHost> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(HomeController).Assembly.FullName,
                ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../IdeioCreative")),
                EnvironmentName = "Development"
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllersWithViews(options => options.MaxModelBindingCollectionSize = int.MaxValue);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                db.Services.AddRange(
                    new Service
                    {
                        Id = 1, Title = "Owned service", Slug = "owned", Language = Language.TR,
                        Faqs = { new ServiceFaq { Id = 41, Question = "Owned question?", Answer = "Owned answer." } }
                    },
                    new Service
                    {
                        Id = 2, Title = "Other service", Slug = "other", Language = Language.TR,
                        Faqs = { new ServiceFaq { Id = 42, Question = "Protected question?", Answer = "Protected answer." } }
                    });
                await db.SaveChangesAsync();
            }
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllerRoute("areas", "{area:exists}/{controller=Main}/{action=Index}/{id?}");
            await app.StartAsync();
            return new FaqAdminHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-User"] != "admin")
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test administrator") }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public void Advance() => _now = _now.AddDays(1);
    }
}

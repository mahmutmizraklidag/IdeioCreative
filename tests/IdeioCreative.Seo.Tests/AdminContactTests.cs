using IdeioCreative.Tools;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

public sealed class AdminContactTests
{
    [Fact]
    public async Task AdminContactPagesShowStoredDatesAndHandleLegacyMissingDates()
    {
        await using var host = await AdminHost.Start();
        using (var anonymous = await host.Http.GetAsync("/admin/ContactForms"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Http.DefaultRequestHeaders.Add("X-Test-User", "admin");

        using (var listResponse = await host.Http.GetAsync("/admin/ContactForms"))
        {
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var html = WebUtility.HtmlDecode(await listResponse.Content.ReadAsStringAsync());
            AssertMainHeading(html);
            Assert.Contains("G\u00f6nderim tarihi", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("createDate", html);
        }

        using (var response = await host.Http.PostAsync("/admin/ContactForms/GetContactMessages",
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       ["draw"] = "1", ["start"] = "0", ["length"] = "10", ["search[value]"] = ""
                   })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.GetProperty("recordsTotal").GetInt32());
            var rows = json.RootElement.GetProperty("data").EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("17.05.2024 09:43", rows.Single(row => row.GetProperty("id").GetInt32() == 1)
                .GetProperty("createDate").GetString());
            Assert.Equal("Tarih bilgisi yok", rows.Single(row => row.GetProperty("id").GetInt32() == 2)
                .GetProperty("createDate").GetString());
        }

        foreach (var action in new[] { "Details", "Delete" })
        {
            foreach (var entry in new[] { (Id: 1, Date: "17.05.2024 09:43"), (Id: 2, Date: "Tarih bilgisi yok") })
            {
                using var response = await host.Http.GetAsync($"/admin/ContactForms/{action}/{entry.Id}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                AssertMainHeading(html);
                Assert.Contains(entry.Date, html);
            }
        }

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.Equal(2, await db.ContactForms.CountAsync());
        Assert.Equal(new DateTime(2024, 5, 17, 9, 43, 12), (await db.ContactForms.SingleAsync(entry => entry.Id == 1)).CreateDate);
        Assert.Null((await db.ContactForms.SingleAsync(entry => entry.Id == 2)).CreateDate);
    }

    private static void AssertMainHeading(string html)
    {
        var headings = Regex.Matches(Regex.Replace(html, "<!--[\\s\\S]*?-->", string.Empty),
            "<h1\\b[^>]*>([\\s\\S]*?)</h1\\s*>", RegexOptions.IgnoreCase);
        var heading = Assert.Single(headings.Cast<Match>());
        Assert.False(string.IsNullOrWhiteSpace(Regex.Replace(heading.Groups[1].Value, "<[^>]+>", string.Empty)));
    }

    private sealed class AdminHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Http { get; }

        private AdminHost(WebApplication app)
        {
            App = app;
            Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Urls.Single())
            };
        }

        public static async Task<AdminHost> Start()
        {
            var projectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../../IdeioCreative"));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(HomeController).Assembly.FullName,
                ContentRootPath = projectDirectory,
                EnvironmentName = "Development"
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSiteDelivery();
            builder.Services.AddControllersWithViews();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                db.ContactForms.AddRange(
                    new ContactForm
                    {
                        Id = 1, Name = "Dated message", Email = "dated@example.test", Message = "Stored submission",
                        CreateDate = new DateTime(2024, 5, 17, 9, 43, 12)
                    },
                    new ContactForm
                    {
                        Id = 2, Name = "Legacy message", Email = "legacy@example.test", Message = "Missing historical date",
                        CreateDate = null
                    });
                await db.SaveChangesAsync();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllerRoute("areas", "{area:exists}/{controller=Main}/{action=Index}/{id?}");
            await app.StartAsync();
            return new AdminHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-User"] != "admin")
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test administrator") }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}

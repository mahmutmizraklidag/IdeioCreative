using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using IdeioCreative.Seo;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<DatabaseContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(x =>
    {
        x.LoginPath = "/Admin/Login";
        x.AccessDeniedPath = "/AccessDenied";
        x.LogoutPath = "/Admin/Logout";
        x.Cookie.Name = "Admin";
        x.Cookie.MaxAge = TimeSpan.FromDays(10);
        x.ExpireTimeSpan = TimeSpan.FromHours(3); // ?? 1 saat oturum süresi
        x.SlidingExpiration = true; // ?? Her iþlemde süre yenilenir
        x.Cookie.IsEssential = true;
        x.Cookie.SameSite = SameSiteMode.Lax; // ?? None yerine Lax önerilir
        x.Cookie.HttpOnly = true;
    });
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("contact-form", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,                 // 1 dakikada max 5 istek
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddIdeioAiSeo(options =>
{
    options.BaseUrl = "https://www.ideiocreative.com";
    options.BrandName = "Ýdeio Creative";
    options.ImageBasePath = "/img/";

    options.AboutPath = "/hakkimizda";
    options.ServicesPath = "/hizmetlerimiz";
    options.TeamPath = "/ekibimiz";
    options.BlogPath = "/blog";
    options.ContactPath = "/iletisim";

    options.PreferredLanguageName = "Turkish";
    options.AllowGptBotForTraining = false;

    // Ekip üyesi detay sayfanýz bulunmuyorsa false kalmalý.
    options.IncludeTeamDetailPages = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/NotFound");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    var dbcontext =
        context.RequestServices.GetRequiredService<DatabaseContext>();

    DataRequestModel.ClearData();

    var language = context.Request.Path.StartsWithSegments("/en")
        ? Language.EN
        : Language.TR;

    DataRequestModel.SiteSetting =
        dbcontext.SiteSettings
            .AsNoTracking()
            .FirstOrDefault(x => x.Language == language)
        ?? new SiteSetting();

    DataRequestModel.Teams =
        dbcontext.Teams
            .AsNoTracking()
            .Where(x =>
                x.Language == language &&
                x.IsHomePage)
            .OrderBy(x => x.OrderNo)
            .ToList();

    DataRequestModel.About =
        dbcontext.Abouts
            .AsNoTracking()
            .FirstOrDefault(x => x.Language == language)
        ?? new About();

    DataRequestModel.Services =
        dbcontext.Services
            .AsNoTracking()
            .Where(x => x.Language == language)
            .OrderBy(x => x.Title)
            .ToList();

    DataRequestModel.References =
        dbcontext.References
            .AsNoTracking()
            .Where(x => x.Language == language)
            .ToList();

    await next();
});
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/Home/NotFound");
app.MapControllerRoute(
           name: "areas",
           pattern: "{area:exists}/{controller=Main}/{action=Index}/{id?}"
         );
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapFallbackToController("NotFound", "Home");
app.MapIdeioAiSeo<DatabaseContext>();
app.Run();

using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

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
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/NotFound");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.Use((context, next) =>
{
    var dbcontext = context.RequestServices.GetRequiredService<DatabaseContext>();

    DataRequestModel.ClearData();

    var lang = context.Request.Path.StartsWithSegments("/en") ? "En" : "Tr";

    DataRequestModel.SiteSetting =
        dbcontext.SiteSettings.FirstOrDefault(x => x.Language.ToString() == lang)
        ?? new SiteSetting(); // null kalmasýn
    DataRequestModel.Teams =
        dbcontext.Teams.Where(x => x.Language.ToString() == lang && x.IsHomePage).ToList();
    DataRequestModel.About =
        dbcontext.Abouts.FirstOrDefault(x => x.Language.ToString() == lang)
        ?? new About();
    DataRequestModel.Services =
        dbcontext.Services.Where(x => x.Language.ToString() == lang)
                .OrderBy(s => s.Title).ToList();
    DataRequestModel.References =dbcontext.References
        .Where(x => x.Language.ToString() == lang && x.IsHome).ToList();
    return next();
});
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

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
app.Run();

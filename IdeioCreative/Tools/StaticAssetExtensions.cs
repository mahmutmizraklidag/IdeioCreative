using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace IdeioCreative.Tools;

public static class StaticAssetExtensions
{
    private static readonly HashSet<string> CacheableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".svg", ".ico",
        ".woff", ".woff2", ".ttf", ".otf", ".eot", ".mp4", ".webm", ".webmanifest", ".json", ".mjs", ".wasm"
    };

    public static IServiceCollection AddSiteDelivery(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ContentHashedAssets>();
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        return services;
    }

    public static IApplicationBuilder UseCachedStaticFiles(this IApplicationBuilder app) =>
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = app.ApplicationServices.GetRequiredService<ContentHashedAssets>(),
            OnPrepareResponse = SetCacheHeaders
        });

    public static IApplicationBuilder UseSiteDelivery(this IApplicationBuilder app)
    {
        app.UseResponseCompression();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var response = context.Response;
                if (response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) ||
                        context.User.Identity?.IsAuthenticated == true ||
                        context.Request.Headers.ContainsKey("Authorization") ||
                        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) ||
                        response.Headers.ContainsKey("Set-Cookie"))
                        response.Headers.CacheControl = "no-cache, no-store";
                    else if (response.StatusCode == 200 && !response.Headers.ContainsKey("Cache-Control"))
                        response.Headers.CacheControl = "public, max-age=60, must-revalidate";
                }
                return Task.CompletedTask;
            });
            await next();
        });
        return app.UseCachedStaticFiles();
    }

    private static void SetCacheHeaders(StaticFileResponseContext context)
    {
        if (!CacheableExtensions.Contains(Path.GetExtension(context.File.Name)))
            return;

        var request = context.Context.Request;
        context.Context.Response.Headers.CacheControl = ContentHashedAssets.TryOriginalPath(request.Path.Value!, out _, out _)
            ? "public, max-age=31536000, immutable"
            : "public, max-age=300, must-revalidate";
    }
}

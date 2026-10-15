using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.IO.Compression;
using IdeioCreative.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers.Binary;
using IdeioCreative.Seo;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class StaticAssetTests
{
    [Theory]
    [InlineData("png", 1200, 630, true)]
    [InlineData("jpeg", 1200, 630, true)]
    [InlineData("vp8x", 1200, 630, true)]
    [InlineData("vp8", 1200, 630, true)]
    [InlineData("vp8l", 1200, 630, true)]
    [InlineData("png", 600, 315, false)]
    [InlineData("jpeg", 1200, 600, false)]
    [InlineData("vp8x", 630, 1200, false)]
    public async Task SocialImagesUsePageAssetsOnlyAtTheDeclaredDimensions(string format, int width, int height, bool eligible)
    {
        await using var host = await StaticHost.Start();
        var bytes = new byte[30];
        if (format == "png")
        {
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        }
        else if (format == "jpeg")
        {
            new byte[] { 255, 216, 255, 194, 0, 17, 8 }.CopyTo(bytes, 0);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(7), (ushort)height);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(9), (ushort)width);
        }
        else
        {
            "RIFF"u8.CopyTo(bytes);
            "WEBP"u8.CopyTo(bytes.AsSpan(8));
            if (format == "vp8x")
            {
                "VP8X"u8.CopyTo(bytes.AsSpan(12));
                bytes[24] = (byte)(width - 1);
                bytes[25] = (byte)((width - 1) >> 8);
                bytes[27] = (byte)(height - 1);
                bytes[28] = (byte)((height - 1) >> 8);
            }
            else if (format == "vp8")
            {
                "VP8 "u8.CopyTo(bytes.AsSpan(12));
                new byte[] { 157, 1, 42 }.CopyTo(bytes, 23);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), (ushort)width);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), (ushort)height);
            }
            else
            {
                "VP8L"u8.CopyTo(bytes.AsSpan(12));
                bytes[20] = 47;
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21), (uint)((width - 1) | (height - 1) << 14));
            }
        }
        var extension = format == "jpeg" ? "jpg" : format == "png" ? "png" : "webp";
        var imagePath = "/assets/social-" + format + "." + extension;
        await File.WriteAllBytesAsync(Path.Combine(host.DirectoryPath, "wwwroot" + imagePath), bytes);
        var builder = new SocialMetadataBuilder(Options.Create(new IdeioSeoOptions()), new SeoTextFormatter(), host.Assets);
        var view = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = new Service { Title = "Service", Slug = "service", Image = imagePath }
        };
        view["Title"] = "Page & Title";
        view["MetaDescription"] = "<p>Description &amp; details</p>";
        view["CanonicalPath"] = "/hizmetlerimiz/service?utm_source=fixture#fragment";
        var metadata = builder.Create(view, new SiteSetting(), "/ignored");
        Assert.Equal("https://ideiocreative.com/hizmetlerimiz/service", metadata.Url);
        Assert.Equal("Description & details", metadata.Description);
        Assert.Equal("website", metadata.Type);
        if (eligible)
        {
            Assert.Contains(host.Assets.Url(imagePath), metadata.ImageUrl);
            Assert.Equal(format == "png" ? "image/png" : format == "jpeg" ? "image/jpeg" : "image/webp", metadata.ImageType);
        }
        else Assert.Contains("/assets/img/social/ideio-creative.png", metadata.ImageUrl);
    }

    [Theory]
    [InlineData("/css/site.css?v=content-hash", 300, false)]
    [InlineData("/img/47b561c4-3241-4a69-99df-3bbc647a199c_logo.png", 300, false)]
    [InlineData("/css/site.css", 300, false)]
    [InlineData("/css/site.css?v=", 300, false)]
    [InlineData("/css/site.css?v=one&v=two", 300, false)]
    [InlineData("/assets/image.webp", 300, false)]
    [InlineData("/assets/font.woff2", 300, false)]
    [InlineData("/assets/media.webm", 300, false)]
    public async Task PublicAssetsGetExplicitLifetimesAndBypassDynamicMiddleware(string path, int seconds, bool immutable)
    {
        await using var host = await StaticHost.Start();
        using var response = await host.Http.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Dynamic-Pipeline"));
        var cache = Assert.IsType<CacheControlHeaderValue>(response.Headers.CacheControl);
        Assert.True(cache.Public);
        Assert.Equal(TimeSpan.FromSeconds(seconds), cache.MaxAge);
        Assert.Equal(immutable, cache.Extensions.Any(value => value.Name == "immutable"));
        Assert.NotNull(response.Headers.ETag);
        Assert.NotNull(response.Content.Headers.LastModified);
    }

    [Fact]
    public async Task ConditionalAndRangeRequestsKeepCachePolicy()
    {
        await using var host = await StaticHost.Start();
        var url = host.Assets.Url("/css/site.css");
        using var original = await host.Http.GetAsync(url);
        using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
        conditional.Headers.IfNoneMatch.Add(original.Headers.ETag!);
        using var notModified = await host.Http.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(original.Headers.CacheControl!.ToString(), notModified.Headers.CacheControl!.ToString());

        using var range = new HttpRequestMessage(HttpMethod.Get, "/assets/media.webm");
        range.Headers.Range = new RangeHeaderValue(0, 3);
        using var partial = await host.Http.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(5), partial.Headers.CacheControl!.MaxAge);
        Assert.Equal(4, (await partial.Content.ReadAsByteArrayAsync()).Length);
    }

    [Theory]
    [InlineData("/missing.png", HttpStatusCode.NotFound)]
    [InlineData("/assets/readme.txt", HttpStatusCode.OK)]
    public async Task DynamicPagesMissingAssetsAndOtherFilesDoNotGetLongCacheHeaders(string path, HttpStatusCode status)
    {
        await using var host = await StaticHost.Start();
        using var response = await host.Http.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/assets/image.webp")]
    [InlineData("/assets/font.woff2")]
    [InlineData("/assets/media.webm")]
    public async Task HashFilenamesMatchActualContentAndReceiveOneYearImmutable(string path)
    {
        await using var host = await StaticHost.Start();
        var expectedHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(StaticHost.Payload))).ToLowerInvariant();
        var slash = path.LastIndexOf('/');
        var expectedUrl = path[..(slash + 1)] + expectedHash + "." + path[(slash + 1)..];
        Assert.Equal(expectedUrl, host.Assets.Url(path));
        using var response = await host.Http.GetAsync(expectedUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl!.ToString());
        Assert.Equal(StaticHost.Payload, await response.Content.ReadAsStringAsync());
        using var incorrect = await host.Http.GetAsync(expectedUrl.Replace(expectedHash, new string('0', 64)));
        Assert.Equal(HttpStatusCode.NotFound, incorrect.StatusCode);
        Assert.Null(incorrect.Headers.CacheControl);
    }

    [Fact]
    public async Task ChangedContentGetsANewFilenameAndOldHashesNeverReturnNewBytes()
    {
        await using var host = await StaticHost.Start();
        var previous = host.Assets.Url("/css/site.css");
        await File.WriteAllTextAsync(Path.Combine(host.DirectoryPath, "wwwroot/css/site.css"), StaticHost.Payload + "changed");
        var current = host.Assets.Url("/css/site.css");
        Assert.NotEqual(previous, current);
        using var response = await host.Http.GetAsync(current);
        Assert.Equal(StaticHost.Payload + "changed", await response.Content.ReadAsStringAsync());
        using var stale = await host.Http.GetAsync(previous);
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task BrotliAndGzipAreNegotiatedAndDecodeToTheOriginalContent(string encoding)
    {
        await using var host = await StaticHost.Start();
        using var request = new HttpRequestMessage(HttpMethod.Get, host.Assets.Url("/css/site.css"));
        request.Headers.AcceptEncoding.ParseAdd(encoding);
        using var response = await host.Http.SendAsync(request);
        Assert.Equal(encoding, Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        var compressed = await response.Content.ReadAsByteArrayAsync();
        Assert.True(compressed.Length < System.Text.Encoding.UTF8.GetByteCount(StaticHost.Payload));
        using var input = new MemoryStream(compressed);
        using Stream decoder = encoding == "br" ? new BrotliStream(input, CompressionMode.Decompress) : new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(decoder);
        Assert.Equal(StaticHost.Payload, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task HtmlGetsOneMinuteWhileFormsCookiesAndAdminPagesRemainNoStore()
    {
        await using var host = await StaticHost.Start();
        using var page = await host.Http.GetAsync("/page");
        Assert.Equal(TimeSpan.FromSeconds(60), page.Headers.CacheControl!.MaxAge);
        Assert.True(page.Headers.CacheControl.Public);
        Assert.True(page.Headers.CacheControl.MustRevalidate);
        foreach (var path in new[] { "/form", "/cookie", "/admin/page" })
        {
            using var response = await host.Http.GetAsync(path);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Fact]
    public async Task CmsImagesAndSourceSetsUseHashesWhileRemoteUrlsStayUnchanged()
    {
        await using var host = await StaticHost.Start();
        var local = host.Assets.Url("/assets/image.webp");
        const string remote = "https://example.test/remote.webp";
        var html = host.Assets.PrepareContent($"<img src='/assets/image.webp?x=1&amp;y=2' srcset='/assets/image.webp 1x, {remote} 2x' alt='Brand'>");
        Assert.Contains($"src=\"{local}?x=1&amp;y=2\"", html);
        Assert.Contains($"srcset=\"{local} 1x, {remote} 2x\"", html);
        Assert.Equal(remote, host.Assets.Url(remote));
        var picture = host.Assets.PrepareContent("<picture><source srcset='/assets/image.webp 1x'></picture>");
        Assert.Equal($"<picture><source srcset=\"{local} 1x\"></picture>", picture);
    }

    private sealed class StaticHost(WebApplication app, string directory) : IAsyncDisposable
    {
        public static readonly string Payload = string.Concat(Enumerable.Repeat("Static asset fixture content.\n", 500));
        public HttpClient Http { get; } = new() { BaseAddress = new Uri(app.Urls.Single()) };
        public ContentHashedAssets Assets => app.Services.GetRequiredService<ContentHashedAssets>();
        public string DirectoryPath => directory;

        public static async Task<StaticHost> Start()
        {
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"static-cache-fixture-{Guid.NewGuid():N}"));
            var webRoot = Path.Combine(directory, "wwwroot");
            foreach (var path in new[]
            {
                "css/site.css", "assets/image.webp", "assets/font.woff2", "assets/media.webm", "assets/readme.txt",
                "img/47b561c4-3241-4a69-99df-3bbc647a199c_logo.png"
            })
            {
                var file = Path.Combine(webRoot, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllTextAsync(file, Payload);
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = directory,
                WebRootPath = webRoot,
                EnvironmentName = "Production"
            });
            builder.Logging.ClearProviders();
            builder.Services.AddSiteDelivery();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Use(async (context, next) => { context.Request.Scheme = "https"; await next(); });
            app.UseSiteDelivery();
            app.Run(async context =>
            {
                context.Response.Headers["X-Dynamic-Pipeline"] = "reached";
                context.Response.ContentType = "text/html";
                context.Response.StatusCode = context.Request.Path is var path &&
                    (path == "/page" || path == "/form" || path == "/cookie" || path == "/admin/page") ? 200 : 404;
                if (path == "/form") context.Response.Headers.CacheControl = "no-cache, no-store";
                if (path == "/cookie") context.Response.Cookies.Append("session", "fixture");
                await context.Response.WriteAsync("Dynamic response");
            });
            await app.StartAsync();
            return new StaticHost(app, directory);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await app.DisposeAsync();
            var expectedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
            if (!string.Equals(Path.GetDirectoryName(directory), expectedParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture directory must stay inside the test output directory.");
            Directory.Delete(directory, recursive: true);
        }
    }
}

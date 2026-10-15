using System.Net;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdeioCreative.Controllers;
using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using IdeioCreative.Seo;
using IdeioCreative.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class RecoveryTests
{
    [Theory]
    [InlineData("GET", "/Blog", "/blog")]
    [InlineData("HEAD", "/BLOG", "/blog")]
    [InlineData("GET", "/BLOG/WEB-REHBERI?utm_source=Google&token=AbC%2FDeF", "/blog/web-rehberi?utm_source=Google&token=AbC%2FDeF")]
    [InlineData("GET", "/HIZMETLERIMIZ/WEB-TASARIMI", "/hizmetlerimiz/web-tasarimi")]
    [InlineData("GET", "/ARAMA?q=Web", "/arama?q=Web")]
    public async Task UppercasePageUrlsPermanentlyRedirectToWorkingLowercasePages(string method, string path, string expected)
    {
        await using var host = await RecoveryHost.Start();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var redirect = await host.Http.SendAsync(request);
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal(new Uri(host.Http.BaseAddress!, expected), redirect.Headers.Location);

        using var followUp = new HttpRequestMessage(new HttpMethod(method), redirect.Headers.Location);
        using var page = await host.Http.SendAsync(followUp);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Null(page.Headers.Location);
    }

    [Fact]
    public async Task UppercaseFormActionsKeepTheirPostBody()
    {
        await using var host = await RecoveryHost.Start();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["message"] = "Hello World" });
        using var response = await host.Http.PostAsync("/TEST/SUBMIT", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("Hello World", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ExistingMixedCaseImageFilenamesAreServedWithoutRedirecting()
    {
        await using var host = await RecoveryHost.Start();
        using var response = await host.Http.GetAsync("/assets/img/team/DSCF5769.jpg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PublicPagesHaveUniqueSocialTagsCanonicalUrlsAndWorkingLargeImages()
    {
        await using var host = await RecoveryHost.Start();
        foreach (var path in new[]
        {
            "/?utm_source=fixture", "/iletisim", "/hakkimizda", "/hizmetlerimiz",
            "/hizmetlerimiz/web-tasarimi?utm_source=fixture", "/blog", "/blog/web-rehberi?gclid=fixture",
            "/ekibimiz", "/ucretsiz-reklam-denetimi?utm_source=fixture", "/arama?q=Web", "/404"
        })
        {
            using var page = await host.Http.GetAsync(path);
            var html = await page.Content.ReadAsStringAsync();
            var head = Regex.Match(html, "<head\\b[^>]*>([\\s\\S]*?)</head>", RegexOptions.IgnoreCase).Groups[1].Value;
            var tags = HtmlTags(head, "meta");
            string Meta(string key)
            {
                var tag = Assert.Single(tags, tag => HtmlAttributeValues(tag, key.StartsWith("og:") ? "property" : "name").Contains(key));
                return Assert.Single(HtmlAttributeValues(tag, "content"));
            }
            Assert.NotEmpty(Meta("og:title"));
            Assert.NotEmpty(Meta("og:description"));
            Assert.Equal("tr_TR", Meta("og:locale"));
            Assert.Equal("summary_large_image", Meta("twitter:card"));
            Assert.Equal(Meta("og:title"), Meta("twitter:title"));
            Assert.Equal(Meta("og:description"), Meta("twitter:description"));
            Assert.Equal(Meta("og:image"), Meta("twitter:image"));
            Assert.Equal("1200", Meta("og:image:width"));
            Assert.Equal("630", Meta("og:image:height"));
            Assert.NotEmpty(Meta("og:image:alt"));
            var canonical = new Uri(Meta("og:url"));
            Assert.Equal("https", canonical.Scheme);
            Assert.Equal("ideiocreative.com", canonical.Host);
            Assert.Empty(canonical.Query);
            Assert.Equal(path.StartsWith("/blog/web-rehberi") ? "article" : "website", Meta("og:type"));
            if (path.StartsWith("/blog/web-rehberi")) Assert.Contains("Web Rehberi", Meta("og:title"));
            var image = new Uri(Meta("og:image"));
            Assert.Equal("https", image.Scheme);
            using var response = await host.Http.GetAsync(image.PathAndQuery);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(1200, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
            Assert.Equal(630, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        }
    }

    [Fact]
    public async Task PublicPagesServeMinifiedAssetsInDependencyOrder()
    {
        await using var host = await RecoveryHost.Start();
        foreach (var path in new[]
        {
            "/", "/iletisim", "/hakkimizda", "/hizmetlerimiz", "/hizmetlerimiz/web-tasarimi",
            "/blog", "/blog/web-rehberi", "/ekibimiz", "/arama?q=Web", "/404", "/ucretsiz-reklam-denetimi"
        })
        {
            using var response = await host.Http.GetAsync(path);
            Assert.Equal(path == "/404" ? HttpStatusCode.NotFound : HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var scripts = Regex.Matches(html, "<script\\b[^>]*\\bsrc=\"([^\"]+)\"[^>]*>", RegexOptions.IgnoreCase);
            var localScripts = scripts.Cast<Match>().Where(match => match.Groups[1].Value.StartsWith('/')).ToList();
            Assert.NotEmpty(localScripts);
            foreach (var script in localScripts)
            {
                Assert.Contains(".min.js", script.Groups[1].Value);
                Assert.Matches("\\sdefer(?:\\s|=|>)", script.Value);
                using var asset = await host.Http.GetAsync(WebUtility.HtmlDecode(script.Groups[1].Value));
                Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
                Assert.Equal(TimeSpan.FromDays(365), asset.Headers.CacheControl!.MaxAge);
                Assert.Contains(asset.Headers.CacheControl.Extensions, extension => extension.Name == "immutable");
            }
            if (path != "/ucretsiz-reklam-denetimi")
                Assert.Contains("site.bundle.min.js", localScripts[0].Groups[1].Value);
            Assert.DoesNotContain("$(document)", html);
            Assert.DoesNotContain("flag-icons", html);
            Assert.DoesNotContain("cdn.jsdelivr.net/npm/bootstrap-icons", html);

            var stylesheets = Regex.Matches(html, "<link\\b[^>]*rel=\"stylesheet\"[^>]*href=\"([^\"]+)\"[^>]*>");
            Assert.InRange(stylesheets.Count, 1, 2);
            foreach (Match stylesheet in stylesheets)
            {
                Assert.Contains(".min.css", stylesheet.Groups[1].Value);
                using var asset = await host.Http.GetAsync(WebUtility.HtmlDecode(stylesheet.Groups[1].Value));
                Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
                Assert.Equal(TimeSpan.FromDays(365), asset.Headers.CacheControl!.MaxAge);
            }
        }
        using var font = await host.Http.GetAsync("/assets/vendor/bootstrap-icons/fonts/bootstrap-icons.woff2");
        Assert.Equal(HttpStatusCode.OK, font.StatusCode);
    }

    [Fact]
    public async Task BundledCssUsesWorkingImmutableFontAndBackgroundFilenames()
    {
        await using var host = await RecoveryHost.Start();
        var cssUrl = host.AssetUrl("/assets/css/site.bundle.min.css");
        var css = await host.Http.GetStringAsync(cssUrl);
        var urls = Regex.Matches(css, "url\\(\"([^\"]+)\"\\)")
            .Select(match => match.Groups[1].Value).ToList();
        foreach (var name in new[] { "fa-solid-900.woff2", "bootstrap-icons.woff2", "footer-version1bg.png", "ekip.webp" })
        {
            var relative = urls.First(url => url.Contains(name, StringComparison.Ordinal));
            Assert.Matches("/[0-9a-f]{64}\\.", relative);
            var absolute = new Uri(new Uri(host.Http.BaseAddress!, cssUrl), relative);
            using var response = await host.Http.GetAsync(absolute);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl!.MaxAge);
            Assert.Contains(response.Headers.CacheControl.Extensions, extension => extension.Name == "immutable");
        }
    }

    [Fact]
    public async Task PublicImagesLoadLazilyAndBannerBackgroundsArePreloaded()
    {
        await using var host = await RecoveryHost.Start();
        var detailImages = new Dictionary<string, string>
        {
            ["/hizmetlerimiz/web-tasarimi"] = "/img/service-main.png",
            ["/blog/web-rehberi"] = "/img/blog-main.png"
        };
        var cmsImages = new Dictionary<string, (string Fixture, string Source, string SourceSet)>
        {
            ["/"] = ("cms-home", "/img/service-main.png", "/img/service-main.png 1x, /img/service-main-2x.png 2x"),
            ["/hakkimizda"] = ("cms-about", "/img/cms-about.png?variant=1&mode=2", "/img/cms-about.png 1x, /img/cms-about-2x.png 2x"),
            ["/hizmetlerimiz/web-tasarimi"] = ("cms-service", "/img/cms-service.png?variant=1&mode=2", "/img/cms-service.png 1x, /img/cms-service-2x.png 2x"),
            ["/blog/web-rehberi"] = ("cms-blog", "/img/cms-blog.png?variant=1&mode=2", "/img/cms-blog.png 1x, /img/cms-blog-2x.png 2x")
        };

        foreach (var path in new[]
        {
            "/", "/iletisim", "/hakkimizda", "/hizmetlerimiz", "/hizmetlerimiz/web-tasarimi",
            "/blog", "/blog/web-rehberi", "/ekibimiz", "/arama?q=Web", "/404", "/ucretsiz-reklam-denetimi"
        })
        {
            using var response = await host.Http.GetAsync(path);
            Assert.Equal(path == "/404" ? HttpStatusCode.NotFound : HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var images = HtmlTags(html, "img");
            Assert.NotEmpty(images);
            foreach (var image in images)
            {
                var source = Assert.Single(HtmlAttributeValues(image, "src"));
                var decoding = HtmlAttributeValues(image, "decoding");
                Assert.True(decoding.Length == 1, $"Expected one decoding attribute at {path}: {image}");
                Assert.Equal("async", decoding[0]);
                var priority = HtmlAttributeValues(image, "fetchpriority");
                Assert.InRange(priority.Length, 0, 1);
                var isTrackingPixel = source.StartsWith("https://www.facebook.com/tr?", StringComparison.Ordinal);
                var isHeroImage = path == "/" && source == host.AssetUrl("/assets/img/banner/hero-bg1.png");
                Assert.Equal(isTrackingPixel || isHeroImage ? "eager" : "lazy",
                    Assert.Single(HtmlAttributeValues(image, "loading")));
                if (isHeroImage)
                    Assert.Equal("high", Assert.Single(priority));
                else
                    Assert.Empty(priority);
            }

            if (path == "/")
                Assert.Single(images, image => HtmlAttributeValues(image, "fetchpriority").Contains("high"));

            if (detailImages.TryGetValue(path, out var detailSource))
                Assert.Contains(images, image => HtmlAttributeValues(image, "src").Contains(detailSource));

            var preloadedImages = HtmlTags(html, "link").Where(link =>
                HtmlAttributeValues(link, "rel").Contains("preload") &&
                HtmlAttributeValues(link, "as").Contains("image")).ToList();
            if (path is "/" or "/ucretsiz-reklam-denetimi")
                Assert.Empty(preloadedImages);
            else
            {
                var preload = Assert.Single(preloadedImages);
                Assert.Equal("high", Assert.Single(HtmlAttributeValues(preload, "fetchpriority")));
                Assert.Equal(host.AssetUrl("/assets/img/banner/breadcrumnd-bg.png"), Assert.Single(HtmlAttributeValues(preload, "href")));
            }

            var logos = images.Where(image => HtmlAttributeValues(image, "src").Contains(host.AssetUrl("/img/ideio-logo.png"))).ToList();
            Assert.True(logos.Count >= 2, $"Header and footer logos must render at {path}.");
            Assert.All(logos, logo => Assert.Equal("lazy", Assert.Single(HtmlAttributeValues(logo, "loading"))));

            if (cmsImages.TryGetValue(path, out var cms))
            {
                var image = Assert.Single(images, tag => HtmlAttributeValues(tag, "data-fixture").Contains(cms.Fixture));
                Assert.Equal(cms.Source, Assert.Single(HtmlAttributeValues(image, "src")));
                Assert.Equal(cms.SourceSet, Assert.Single(HtmlAttributeValues(image, "srcset")));
                Assert.Equal("quoted > value", Assert.Single(HtmlAttributeValues(image, "title")));
            }

            if (path is "/" or "/ucretsiz-reklam-denetimi")
            {
                Assert.Empty(HtmlTags(html, "video"));
                var player = Assert.Single(HtmlTags(html, "iframe"),
                    tag => HtmlAttributeValues(tag, "class").Any(value => value.Split(' ').Contains("youtube-player")));
                Assert.Empty(HtmlAttributeValues(player, "src"));
                var source = new Uri(Assert.Single(HtmlAttributeValues(player, "data-src")));
                Assert.Equal("https://www.youtube.com/embed/vYmVAC5Gsc0", source.GetLeftPart(UriPartial.Path));
                var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(source.Query);
                foreach (var parameter in new[] { "enablejsapi", "autoplay", "mute", "loop", "playsinline" })
                    Assert.Equal("1", parameters[parameter].ToString());
                Assert.Equal("vYmVAC5Gsc0", parameters["playlist"].ToString());
                Assert.Equal("0", parameters["controls"].ToString());
                Assert.Equal("0", parameters["cc_load_policy"].ToString());
                Assert.DoesNotContain("/video/", html);
                Assert.Contains(images, image => HtmlAttributeValues(image, "src").Contains("/img/about-home.png"));
            }
            if (path is "/" or "/ekibimiz")
                Assert.Contains(images, image => HtmlAttributeValues(image, "src").Contains("/img/team-fixture.png"));
            if (path is "/" or "/ucretsiz-reklam-denetimi")
                Assert.Contains(images, image => HtmlAttributeValues(image, "src").Contains("/img/reference-fixture.png"));
        }

        using var banner = await host.Http.GetAsync("/assets/img/banner/breadcrumnd-bg.png");
        Assert.Equal(HttpStatusCode.OK, banner.StatusCode);
    }

    [Fact]
    public async Task PublicPagesHaveOneMainHeadingAndDescribeEveryImage()
    {
        await using var host = await RecoveryHost.Start();
        var cmsContexts = new Dictionary<string, (string Fixture, string Description)>
        {
            ["/"] = ("cms-home", "Web Tasar\u0131m\u0131"),
            ["/hakkimizda"] = ("cms-about", "About"),
            ["/hizmetlerimiz/web-tasarimi"] = ("cms-service", "Web Tasar\u0131m\u0131"),
            ["/blog/web-rehberi"] = ("cms-blog", "Web Rehberi")
        };

        foreach (var path in new[]
        {
            "/", "/iletisim", "/hakkimizda", "/hizmetlerimiz", "/hizmetlerimiz/web-tasarimi",
            "/blog", "/blog/web-rehberi", "/ekibimiz", "/arama?q=Web", "/404", "/ucretsiz-reklam-denetimi"
        })
        {
            using var response = await host.Http.GetAsync(path);
            Assert.Equal(path == "/404" ? HttpStatusCode.NotFound : HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Single(HtmlTags(html, "h1"));
            var headings = Regex.Matches(Regex.Replace(html, "<!--[\\s\\S]*?-->", string.Empty),
                "<h1\\b(?:\"[^\"]*\"|'[^']*'|[^'\">])*>([\\s\\S]*?)</h1\\s*>", RegexOptions.IgnoreCase);
            var heading = Assert.Single(headings.Cast<Match>());
            var text = WebUtility.HtmlDecode(Regex.Replace(heading.Groups[1].Value, "<[^>]+>", string.Empty));
            Assert.False(string.IsNullOrWhiteSpace(text), $"The main heading must have text at {path}.");

            var images = HtmlTags(html, "img");
            Assert.NotEmpty(images);
            foreach (var image in images)
            {
                var descriptions = HtmlAttributeValues(image, "alt");
                Assert.True(descriptions.Length == 1, $"Expected one alt attribute at {path}: {image}");
                Assert.False(string.IsNullOrWhiteSpace(descriptions[0]), $"Image alt must have text at {path}: {image}");
            }

            var logos = images.Where(image => HtmlAttributeValues(image, "src").Contains("/img/ideio-logo.png"));
            Assert.All(logos, logo => Assert.Contains("Creative", Assert.Single(HtmlAttributeValues(logo, "alt")),
                StringComparison.OrdinalIgnoreCase));

            if (cmsContexts.TryGetValue(path, out var cms))
            {
                var image = Assert.Single(images, tag => HtmlAttributeValues(tag, "data-fixture").Contains(cms.Fixture));
                Assert.Equal(cms.Description, Assert.Single(HtmlAttributeValues(image, "alt")));
                var contentHeadings = HtmlTags(html, "h2");
                Assert.Single(contentHeadings, tag => HtmlAttributeValues(tag, "data-fixture").Contains(cms.Fixture + "-heading"));
                Assert.Single(contentHeadings, tag => HtmlAttributeValues(tag, "data-fixture").Contains(cms.Fixture + "-heading-second"));
            }

            if (path == "/hakkimizda")
            {
                var preserved = Assert.Single(images, tag => HtmlAttributeValues(tag, "data-fixture").Contains("cms-preserved"));
                Assert.Equal("Existing illustration", Assert.Single(HtmlAttributeValues(preserved, "alt")));
            }
        }
    }

    [Fact]
    public async Task LocalBusinessMarkupAppearsOnlyOnHomeAndContactWithConsistentPublicDetails()
    {
        await using var host = await RecoveryHost.Start();
        const string organizationId = "https://ideiocreative.com/#organization";
        const string address = "Test Sokak No: 12, Bayrakli / Izmir";
        var expectedProfiles = new[]
        {
            "https://www.instagram.com/ideio-fixture/", "https://www.linkedin.com/company/ideio-fixture/"
        };

        foreach (var path in new[]
        {
            "/", "/iletisim", "/hakkimizda", "/hizmetlerimiz", "/hizmetlerimiz/web-tasarimi",
            "/blog", "/blog/web-rehberi", "/ekibimiz", "/arama?q=Web", "/404", "/ucretsiz-reklam-denetimi"
        })
        {
            using var response = await host.Http.GetAsync(path);
            Assert.Equal(path == "/404" ? HttpStatusCode.NotFound : HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var nodes = ReadJsonLd(html);
            var organization = Assert.Single(nodes, node => node.TryGetProperty("@id", out var id) && id.GetString() == organizationId);
            var expectedLocalBusiness = path is "/" or "/iletisim";
            if (expectedLocalBusiness)
            {
                Assert.Equal(new[] { "Organization", "LocalBusiness", "ProfessionalService" }, SchemaTypes(organization));
                var postalAddress = organization.GetProperty("address");
                Assert.Equal("PostalAddress", postalAddress.GetProperty("@type").GetString());
                Assert.Equal(address, postalAddress.GetProperty("streetAddress").GetString());
                Assert.Equal("Bayrakl\u0131", postalAddress.GetProperty("addressLocality").GetString());
                Assert.Equal("\u0130zmir", postalAddress.GetProperty("addressRegion").GetString());
                Assert.Equal("35530", postalAddress.GetProperty("postalCode").GetString());
                Assert.Equal("TR", postalAddress.GetProperty("addressCountry").GetString());
                Assert.True(host.SeoOptions.LocalBusiness.Latitude.HasValue);
                Assert.True(host.SeoOptions.LocalBusiness.Longitude.HasValue);
                var geo = organization.GetProperty("geo");
                Assert.Equal("GeoCoordinates", geo.GetProperty("@type").GetString());
                Assert.Equal(host.SeoOptions.LocalBusiness.Latitude.GetValueOrDefault(), geo.GetProperty("latitude").GetDouble());
                Assert.Equal(host.SeoOptions.LocalBusiness.Longitude.GetValueOrDefault(), geo.GetProperty("longitude").GetDouble());
                Assert.Equal(host.SeoOptions.LocalBusiness.MapUrl, organization.GetProperty("hasMap").GetString());
                Assert.Equal(host.SeoOptions.LocalBusiness.Name, organization.GetProperty("name").GetString());
                Assert.Equal(host.SeoOptions.BrandName, organization.GetProperty("alternateName").GetString());

                var hours = organization.GetProperty("openingHoursSpecification").EnumerateArray().ToList();
                var weekdays = Assert.Single(hours, entry => entry.GetProperty("opens").GetString() == "09:00");
                Assert.Equal("18:00", weekdays.GetProperty("closes").GetString());
                Assert.Equal(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    weekdays.GetProperty("dayOfWeek").EnumerateArray().Select(day => day.GetString()!.Split('/').Last()));
                var closed = Assert.Single(hours, entry => entry.GetProperty("opens").GetString() == "00:00");
                Assert.Equal("00:00", closed.GetProperty("closes").GetString());
                Assert.Equal(new[] { "Saturday", "Sunday" },
                    closed.GetProperty("dayOfWeek").EnumerateArray().Select(day => day.GetString()!.Split('/').Last()));
            }
            else
            {
                Assert.Equal(new[] { "Organization" }, SchemaTypes(organization));
            }
            Assert.Equal(expectedLocalBusiness ? 1 : 0, nodes.Count(node => SchemaTypes(node).Contains("LocalBusiness")));
            Assert.Equal(expectedLocalBusiness ? 1 : 0, nodes.Count(node => SchemaTypes(node).Contains("ProfessionalService")));
            Assert.Equal("+90 (232) 123 45 67", organization.GetProperty("telephone").GetString());
            Assert.Equal("agency@example.test", organization.GetProperty("email").GetString());
            Assert.Equal(expectedProfiles.Order(), organization.GetProperty("sameAs").EnumerateArray()
                .Select(profile => profile.GetString()!).Order());
            foreach (var website in nodes.Where(node => SchemaTypes(node).Contains("WebSite")))
                Assert.Equal(organizationId, website.GetProperty("publisher").GetProperty("@id").GetString());
            if (path == "/hizmetlerimiz/web-tasarimi")
                Assert.Equal(organizationId, Assert.Single(nodes, node => SchemaTypes(node).Contains("Service"))
                    .GetProperty("provider").GetProperty("@id").GetString());
            if (path == "/blog/web-rehberi")
                Assert.Equal(organizationId, Assert.Single(nodes, node => SchemaTypes(node).Contains("BlogPosting"))
                    .GetProperty("publisher").GetProperty("@id").GetString());

            if (expectedLocalBusiness)
            {
                var body = Regex.Match(html, "<body\\b[^>]*>([\\s\\S]*?)</body>", RegexOptions.IgnoreCase).Groups[1].Value;
                body = Regex.Replace(body, "<(script|style)\\b[^>]*>[\\s\\S]*?</\\1>|<!--[\\s\\S]*?-->", "", RegexOptions.IgnoreCase);
                var visibleText = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(body, "<[^>]+>", " ")), "\\s+", " ");
                Assert.Contains(address, visibleText);
                Assert.Contains("+90 (232) 123 45 67", visibleText);
                Assert.Contains("agency@example.test", visibleText);
                if (path == "/iletisim")
                    Assert.All(host.SeoOptions.LocalBusiness.OpeningHours.Where(hours => hours.IsValid),
                        hours => Assert.Contains(hours.DisplayText, visibleText));
            }
        }
    }

    [Fact]
    public async Task ServiceFaqsShowOnlyPublishedCompleteItemsAndMatchTheirSchema()
    {
        await using var host = await RecoveryHost.Start();
        const string path = "/hizmetlerimiz/web-tasarimi?utm=fixture&gclid=fixture";
        const string canonical = "https://ideiocreative.com/hizmetlerimiz/web-tasarimi";
        var empty = await host.Http.GetStringAsync(path);
        Assert.DoesNotContain("id=\"faq\"", empty);
        Assert.DoesNotContain(ReadJsonLd(empty), node => SchemaTypes(node).Contains("FAQPage"));

        const string firstQuestion = "First & <question>?";
        const string firstAnswer = "A literal <b>answer</b> & </script><script>alert('fixture')</script>.";
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var service = await db.Services.SingleAsync(item => item.Slug == "web-tasarimi");
            service.Faqs.Add(new ServiceFaq { Question = "Later question?", Answer = "Later answer.", SortOrder = 30, IsPublished = true });
            service.Faqs.Add(new ServiceFaq
            {
                Question = "  " + firstQuestion + "  ", Answer = "  " + firstAnswer + "  ", SortOrder = 10, IsPublished = true
            });
            service.Faqs.Add(new ServiceFaq { Question = "Draft-only question?", Answer = "Draft-only answer.", SortOrder = 0, IsPublished = false });
            service.Faqs.Add(new ServiceFaq { Question = "   ", Answer = "Ignored blank question.", SortOrder = 1, IsPublished = true });
            service.Faqs.Add(new ServiceFaq { Question = "Ignored blank answer?", Answer = "\t", SortOrder = 2, IsPublished = true });
            await db.SaveChangesAsync();
        }

        var html = await host.Http.GetStringAsync(path);
        var section = Assert.Single(HtmlTags(html, "section"), tag => HtmlAttributeValues(tag, "id").Contains("faq"));
        Assert.Contains("faq-section", Assert.Single(HtmlAttributeValues(section, "class")));
        var details = Regex.Matches(html, "<details\\b[^>]*class=\"faq-item\"[^>]*>([\\s\\S]*?)</details>", RegexOptions.IgnoreCase)
            .Cast<Match>().ToList();
        Assert.Equal(2, details.Count);
        var visible = details.Select(item => (
            Question: WebUtility.HtmlDecode(Regex.Match(item.Groups[1].Value, "<h3\\b[^>]*>([\\s\\S]*?)</h3>", RegexOptions.IgnoreCase).Groups[1].Value).Trim(),
            Answer: WebUtility.HtmlDecode(Regex.Match(item.Groups[1].Value, "<p\\b[^>]*class=\"faq-answer\"[^>]*>([\\s\\S]*?)</p>", RegexOptions.IgnoreCase).Groups[1].Value).Trim()))
            .ToList();
        Assert.Equal(new[] { (firstQuestion, firstAnswer), ("Later question?", "Later answer.") }, visible);
        var schema = Assert.Single(ReadJsonLd(html), node => SchemaTypes(node).Contains("FAQPage"));
        Assert.Equal(canonical + "#faq", schema.GetProperty("@id").GetString());
        Assert.Equal(canonical, schema.GetProperty("url").GetString());
        var questions = schema.GetProperty("mainEntity").EnumerateArray().ToList();
        Assert.Equal(visible.Count, questions.Count);
        for (var index = 0; index < questions.Count; index++)
        {
            Assert.Equal("Question", questions[index].GetProperty("@type").GetString());
            Assert.Equal(visible[index].Question, questions[index].GetProperty("name").GetString());
            var answer = questions[index].GetProperty("acceptedAnswer");
            Assert.Equal("Answer", answer.GetProperty("@type").GetString());
            Assert.Equal(visible[index].Answer, answer.GetProperty("text").GetString());
        }
        var canonicalLink = Assert.Single(HtmlTags(html, "link"), tag => HtmlAttributeValues(tag, "rel").Contains("canonical"));
        Assert.Equal(canonical, Assert.Single(HtmlAttributeValues(canonicalLink, "href")));
        Assert.DoesNotContain("Draft-only", html);
        Assert.DoesNotContain("Ignored blank", html);
        Assert.DoesNotContain("<script>alert('fixture')</script>", html);
        Assert.DoesNotContain(ReadJsonLd(await host.Http.GetStringAsync("/hizmetlerimiz")), node => SchemaTypes(node).Contains("FAQPage"));

        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            foreach (var faq in await db.Set<ServiceFaq>().ToListAsync())
                faq.IsPublished = false;
            await db.SaveChangesAsync();
        }
        var unpublished = await host.Http.GetStringAsync(path);
        Assert.DoesNotContain("id=\"faq\"", unpublished);
        Assert.DoesNotContain(ReadJsonLd(unpublished), node => SchemaTypes(node).Contains("FAQPage"));
    }

    [Fact]
    public async Task SectorPagesKeepTheirOwnRoutesFaqsAndNavigationWithoutMixingServiceContent()
    {
        await using var host = await RecoveryHost.Start();
        var empty = await host.Http.GetStringAsync("/sektorler");
        Assert.Contains("name=\"robots\" content=\"noindex, follow\"", empty);
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.Services.Add(new Service
            {
                Title = "Sector Fixture", Slug = "sector-fixture", Language = Language.TR, PageType = ServicePageType.Sector,
                Image = "sector-fixture.png", Description = "Sector description",
                HomeDescription = "<div data-fixture=\"sector-home-marker\">Sector-only home content</div>",
                Faqs = { new ServiceFaq { Question = "Sector question?", Answer = "Sector answer." } }
            });
            db.Services.Add(new Service
            {
                Title = "English Sector Secret", Slug = "english-sector", Language = Language.EN, PageType = ServicePageType.Sector
            });
            await db.SaveChangesAsync();
        }

        var sectors = await host.Http.GetStringAsync("/sektorler");
        var sectorMain = Regex.Match(sectors, "<main\\b[^>]*>([\\s\\S]*?)</main>", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.Contains("Sector Fixture", sectorMain);
        Assert.Contains("href=\"/sektorler/sector-fixture\"", sectorMain);
        Assert.DoesNotContain("Web Tasar", sectorMain);
        Assert.DoesNotContain("English Sector Secret", sectorMain);
        Assert.DoesNotContain("name=\"robots\" content=\"noindex, follow\"", sectors);
        var services = await host.Http.GetStringAsync("/hizmetlerimiz");
        var serviceMain = Regex.Match(services, "<main\\b[^>]*>([\\s\\S]*?)</main>", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.DoesNotContain("Sector Fixture", serviceMain);
        var home = await host.Http.GetStringAsync("/");
        Assert.DoesNotContain("sector-home-marker", home);
        Assert.Contains("href=\"/sektorler\"", home);
        var search = await host.Http.GetStringAsync("/arama?q=Sector%20Fixture");
        Assert.Contains("href=\"/sektorler/sector-fixture\"", search);

        var detail = await host.Http.GetStringAsync("/sektorler/sector-fixture?utm=fixture&gclid=fixture");
        Assert.Contains("Sector Fixture", Regex.Match(detail, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value);
        const string canonical = "https://ideiocreative.com/sektorler/sector-fixture";
        var link = Assert.Single(HtmlTags(detail, "link"), tag => HtmlAttributeValues(tag, "rel").Contains("canonical"));
        Assert.Equal(canonical, Assert.Single(HtmlAttributeValues(link, "href")));
        var schema = Assert.Single(ReadJsonLd(detail), node => SchemaTypes(node).Contains("FAQPage"));
        Assert.Equal(canonical + "#faq", schema.GetProperty("@id").GetString());
        Assert.Equal(canonical, schema.GetProperty("url").GetString());
        Assert.Contains("Sector question?", detail);
        Assert.Equal("Sector question?", Assert.Single(schema.GetProperty("mainEntity").EnumerateArray())
            .GetProperty("name").GetString());
        Assert.Single(HtmlTags(detail, "h1"));
        foreach (var path in new[] { "/hizmetlerimiz/sector-fixture", "/sektorler/web-tasarimi", "/sektorler/english-sector" })
        {
            using var missing = await host.Http.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        int faqId;
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var sector = await db.Services.Include(item => item.Faqs).SingleAsync(item => item.Slug == "sector-fixture");
            faqId = Assert.Single(sector.Faqs).Id;
            sector.PageType = ServicePageType.Service;
            await db.SaveChangesAsync();
        }
        using (var oldRoute = await host.Http.GetAsync("/sektorler/sector-fixture"))
            Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
        var moved = await host.Http.GetStringAsync("/hizmetlerimiz/sector-fixture?utm=fixture");
        var movedSchema = Assert.Single(ReadJsonLd(moved), node => SchemaTypes(node).Contains("FAQPage"));
        Assert.Equal("https://ideiocreative.com/hizmetlerimiz/sector-fixture#faq", movedSchema.GetProperty("@id").GetString());
        using var finalScope = host.App.Services.CreateScope();
        Assert.Equal(faqId, await finalScope.ServiceProvider.GetRequiredService<DatabaseContext>().Set<ServiceFaq>()
            .Where(faq => faq.Question == "Sector question?").Select(faq => faq.Id).SingleAsync());
    }

    [Fact]
    public async Task MissingUrlsRenderRecoveryToolsWith404AndServerErrorsKeep500()
    {
        await using var host = await RecoveryHost.Start();
        foreach (var path in new[]
        {
            "/404", "/home/notfound", "/missing/deep/page", "/missing.css",
            "/hizmetlerimiz/missing-service", "/blog/missing-post", "/test/plain-404"
        })
        {
            using var response = await host.Http.GetAsync(path);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("role=\"search\"", html);
            Assert.Contains("action=\"/arama\"", html);
            Assert.Contains("name=\"q\"", html);
            Assert.Contains("href=\"/hizmetlerimiz/web-tasarimi\"", html);
            Assert.Contains("href=\"tel:+902321234567\"", html);
            Assert.Contains("+90 (232) 123 45 67", html);
            Assert.Contains("name=\"robots\" content=\"noindex, follow\"", html);
            Assert.Contains(host.AssetUrl("/assets/img/error/opps.png"), html);
            Assert.DoesNotContain("Web English Secret", html);
        }

        using var serverError = await host.Http.GetAsync("/test/server-error");
        Assert.Equal(HttpStatusCode.InternalServerError, serverError.StatusCode);
        using var stylesheet = await host.Http.GetAsync("/css/recovery.css");
        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
    }

    [Fact]
    public async Task SearchFindsServicesAndPublishedPostsAndOffersAnEmptyState()
    {
        await using var host = await RecoveryHost.Start();
        using var response = await host.Http.GetAsync("/arama?q=%20Web%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("“Web” için 2 sonuç bulundu.", html);
        Assert.Contains("Web Tasarımı", html);
        Assert.Contains("Web Rehberi", html);
        Assert.Contains("href=\"/hizmetlerimiz/web-tasarimi\"", html);
        Assert.Contains("href=\"/blog/web-rehberi\"", html);
        Assert.Contains("value=\"Web\"", html);
        Assert.DoesNotContain("Web Future Secret", html);
        Assert.DoesNotContain("Web English Secret", html);
        Assert.Contains("name=\"robots\" content=\"noindex, follow\"", html);

        var bodySearch = WebUtility.HtmlDecode(await host.Http.GetStringAsync("/arama?q=Kurumsal"));
        Assert.Contains("“Kurumsal” için 1 sonuç bulundu.", bodySearch);

        var empty = WebUtility.HtmlDecode(await host.Http.GetStringAsync("/arama?q=unmatched-word"));
        Assert.Contains("Bir sonuç bulamadık", empty);
        Assert.Contains("href=\"/hizmetlerimiz\"", empty);
        var blank = await host.Http.GetStringAsync("/arama");
        Assert.DoesNotContain("search-results", blank);

        var malicious = await host.Http.GetStringAsync("/arama?q=" + Uri.EscapeDataString("<script>alert(1)</script>"));
        Assert.DoesNotContain("<script>alert(1)</script>", malicious);
    }

    private static List<JsonElement> ReadJsonLd(string html)
    {
        var scripts = Regex.Matches(html,
            "<script\\b[^>]*\\btype=\"application/ld\\+json\"[^>]*>([\\s\\S]*?)</script>", RegexOptions.IgnoreCase);
        Assert.NotEmpty(scripts.Cast<Match>());
        var nodes = new List<JsonElement>();
        foreach (Match script in scripts)
        {
            using var json = JsonDocument.Parse(script.Groups[1].Value);
            nodes.Add(json.RootElement.Clone());
        }
        return nodes;
    }

    private static string[] SchemaTypes(JsonElement node)
    {
        if (!node.TryGetProperty("@type", out var type))
            return Array.Empty<string>();
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : new[] { type.GetString()! };
    }

    // Images inside template comments are not rendered DOM elements.
    private static List<string> HtmlTags(string html, string name) => Regex.Matches(
            Regex.Replace(html, "<!--[\\s\\S]*?-->", string.Empty),
            "<" + Regex.Escape(name) + "\\b(?:\"[^\"]*\"|'[^']*'|[^'\">])*>", RegexOptions.IgnoreCase)
        .Cast<Match>().Select(match => match.Value).ToList();

    private static string[] HtmlAttributeValues(string tag, string name) => Regex.Matches(tag,
            "\\s" + Regex.Escape(name) + "\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase)
        .Cast<Match>().Select(match => WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value :
            match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value)).ToArray();

    private static string CmsImage(string fixture, string source, string sourceSet, string? alt = null) =>
        $"<h1 data-fixture=\"{fixture}-heading\">CMS section</h1>" +
        $"<H1 data-fixture=\"{fixture}-heading-second\"><span>Another CMS section</span></H1>" +
        $"<img data-fixture=\"{fixture}\" src=\"{source}\" srcset=\"{sourceSet}\" title=\"quoted > value\" loading='eager' decoding=\"sync\"" +
        (alt is null ? "" : $" alt=\"{WebUtility.HtmlEncode(alt)}\"") + ">";

    private sealed class RecoveryHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public WebApplication App => _app;
        public HttpClient Http { get; }
        public IdeioSeoOptions SeoOptions { get; }
        public string AssetUrl(string path) => _app.Services.GetRequiredService<ContentHashedAssets>().Url(path);

        private RecoveryHost(WebApplication app)
        {
            _app = app;
            SeoOptions = app.Services.GetRequiredService<IOptions<IdeioSeoOptions>>().Value;
            Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Urls.Single())
            };
        }

        public static async Task<RecoveryHost> Start()
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
            builder.Configuration.AddJsonFile(Path.Combine(projectDirectory, "local-business.json"), optional: false);
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSiteDelivery();
            builder.Services.AddControllersWithViews();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddSingleton<ContactSecurityQuestionService>();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
            builder.Services.AddIdeioAiSeo(options => builder.Configuration.GetSection("LocalBusiness").Bind(options.LocalBusiness));
            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                db.Services.AddRange(
                    new Service
                    {
                        Title = "Web Tasarımı", Slug = "web-tasarimi", Language = Language.TR,
                        Description = "Kurumsal web siteleri" + CmsImage("cms-service", "/img/cms-service.png?variant=1&amp;mode=2",
                            "/img/cms-service.png 1x, /img/cms-service-2x.png 2x"),
                        Image = "service-main.png",
                        HomeDescription = CmsImage("cms-home", "{{SERVICE_IMAGE}}", "{{SERVICE_IMAGE}} 1x, /img/service-main-2x.png 2x")
                    },
                    new Service { Title = "Web English Secret", Slug = "english-secret", Language = Language.EN });
                db.SiteSettings.Add(new SiteSetting
                {
                    Phone = "+90 (232) 123 45 67", Language = Language.TR, Logo = "ideio-logo.png",
                    Email = "agency@example.test", Address = "Test Sokak No: 12, Bayrakli / Izmir",
                    Facebook = "https://www.instagram.com/ideio-fixture/",
                    Instagram = " https://www.instagram.com/ideio-fixture/ ",
                    LinkedIn = "https://www.linkedin.com/company/ideio-fixture/", Twitter = "#", YouTube = "   "
                });
                db.Abouts.Add(new About
                {
                    Language = Language.TR, Title = "About",
                    Description = "About description" + CmsImage("cms-about", "/img/cms-about.png?variant=1&amp;mode=2",
                        "/img/cms-about.png 1x, /img/cms-about-2x.png 2x", "   ") +
                        "<img data-fixture=\"cms-preserved\" src=\"/img/preserved.png\" alt=\"Existing illustration\" loading=\"eager\" decoding=\"sync\">",
                    HomeDescription = "Home description", HomeImage = "about-home.png",
                    Image = "ideio-about.png", Image2 = "ideio-about.png"
                });
                db.Blogs.AddRange(
                    new Blog
                    {
                        Title = "Web Rehberi", Description = "Web hakkında içerik", Slug = "web-rehberi",
                        CreatedAt = DateTime.Now.AddDays(-1), Image = "blog-main.png"
                    },
                    new Blog
                    {
                        Title = "Web Future Secret", Description = "Future", Slug = "future-secret",
                        CreatedAt = DateTime.Now.AddDays(7)
                    });
                db.Blogs.Local.Single(blog => blog.Slug == "web-rehberi").Description +=
                    CmsImage("cms-blog", "/img/cms-blog.png?variant=1&amp;mode=2", "/img/cms-blog.png 1x, /img/cms-blog-2x.png 2x", "");
                db.Teams.Add(new Team
                {
                    Name = "Team Fixture", Position = "Designer", Slug = "team-fixture",
                    Image = "team-fixture.png", Language = Language.TR, IsHomePage = true
                });
                db.References.Add(new Reference
                {
                    Name = "Reference Fixture", Image = "reference-fixture.png", Language = Language.TR, IsHome = true
                });
                await db.SaveChangesAsync();
            }

            app.UseExceptionHandler("/Home/NotFound");
            app.UseSiteDelivery();
            app.UseLowercaseUrlRedirects();
            app.UseRouting();
            app.Use(async (http, next) =>
            {
                var db = http.RequestServices.GetRequiredService<DatabaseContext>();
                DataRequestModel.ClearData();
                DataRequestModel.SiteSetting = (await db.SiteSettings.FirstOrDefaultAsync()) ?? new SiteSetting();
                DataRequestModel.Services = await db.Services.Where(service => service.Language == Language.TR &&
                    service.PageType == ServicePageType.Service).ToListAsync();
                DataRequestModel.Sectors = await db.Services.Where(service => service.Language == Language.TR &&
                    service.PageType == ServicePageType.Sector).ToListAsync();
                DataRequestModel.About = (await db.Abouts.FirstOrDefaultAsync()) ?? new About();
                DataRequestModel.Teams = await db.Teams.Where(team => team.IsHomePage).ToListAsync();
                DataRequestModel.References = await db.References.Where(reference => reference.IsHome).ToListAsync();
                await next();
            });
            app.UseStatusCodePagesWithReExecute("/Home/NotFound");
            app.MapControllerRoute("areas", "{area:exists}/{controller=Main}/{action=Index}/{id?}");
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            app.MapFallbackToController("NotFound", "Home");
            app.MapGet("/test/plain-404", () => Results.NotFound());
            app.MapPost("/test/submit", async (HttpRequest request) =>
                Results.Text((await request.ReadFormAsync())["message"].ToString()));
            app.MapGet("/test/server-error", () =>
                Results.Problem(statusCode: ThrowServerError()));
            await app.StartAsync();
            return new RecoveryHost(app);
        }

        private static int ThrowServerError() => throw new InvalidOperationException("Test server error");

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await _app.DisposeAsync();
        }
    }
}

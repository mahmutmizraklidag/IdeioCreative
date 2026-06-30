using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdeioCreative.Entities;

namespace IdeioCreative.Seo;

public static class SeoJsonLdBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Organization(SiteSetting? site, IdeioSeoOptions options)
    {
        var sameAs = site is null
            ? Array.Empty<string>()
            : new[]
            {
                site.Facebook,
                site.Instagram,
                site.Twitter,
                site.LinkedIn,
                site.YouTube
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["@id"] = options.Absolute("/#organization"),
            ["name"] = options.BrandName,
            ["url"] = options.Absolute("/"),
            ["logo"] = string.IsNullOrWhiteSpace(site?.Logo) ? null : options.ImageUrl(site.Logo),
            ["description"] = site?.MetaDescription,
            ["email"] = site?.Email,
            ["telephone"] = site?.Phone,
            ["sameAs"] = sameAs.Length == 0 ? null : sameAs,
            ["hasMap"] = site?.mapLink,
            ["address"] = string.IsNullOrWhiteSpace(site?.Address)
                ? null
                : new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = site.Address,
                    ["addressCountry"] = "TR"
                },
            ["contactPoint"] = string.IsNullOrWhiteSpace(site?.Phone) && string.IsNullOrWhiteSpace(site?.Email)
                ? null
                : new Dictionary<string, object?>
                {
                    ["@type"] = "ContactPoint",
                    ["telephone"] = site?.Phone,
                    ["email"] = site?.Email,
                    ["contactType"] = "customer service",
                    ["availableLanguage"] = new[] { "tr", "en" }
                }
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string WebSite(SiteSetting? site, IdeioSeoOptions options)
    {
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["@id"] = options.Absolute("/#website"),
            ["url"] = options.Absolute("/"),
            ["name"] = options.BrandName,
            ["description"] = site?.MetaDescription,
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@id"] = options.Absolute("/#organization")
            },
            ["inLanguage"] = "tr-TR"
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string Service(
        Service service,
        SiteSetting? site,
        IdeioSeoOptions options,
        SeoTextFormatter formatter)
    {
        var url = options.ServiceUrl(service);
        var description = service.MetaDescription ??
                          formatter.OneLine(service.HomeDescription ?? service.Description);

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Service",
            ["@id"] = url + "#service",
            ["name"] = service.Title,
            ["description"] = description,
            ["url"] = url,
            ["image"] = string.IsNullOrWhiteSpace(service.Image) ? null : options.ImageUrl(service.Image),
            ["provider"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = options.Absolute("/#organization"),
                ["name"] = options.BrandName,
                ["url"] = options.Absolute("/")
            },
            ["areaServed"] = new Dictionary<string, object?>
            {
                ["@type"] = "Country",
                ["name"] = "Türkiye"
            },
            ["inLanguage"] = service.Language.ToString()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string BlogPosting(
        Blog blog,
        SiteSetting? site,
        IdeioSeoOptions options,
        SeoTextFormatter formatter)
    {
        var url = options.BlogUrl(blog);
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["@id"] = url + "#article",
            ["headline"] = blog.Title,
            ["description"] = blog.MetaDescription ?? formatter.OneLine(blog.Description),
            ["url"] = url,
            ["mainEntityOfPage"] = url,
            ["datePublished"] = blog.CreatedAt.ToUniversalTime().ToString("O"),
            ["dateModified"] = blog.CreatedAt.ToUniversalTime().ToString("O"),
            ["image"] = string.IsNullOrWhiteSpace(blog.Image) ? null : options.ImageUrl(blog.Image),
            ["author"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = options.BrandName,
                ["url"] = options.Absolute("/")
            },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = options.Absolute("/#organization"),
                ["name"] = options.BrandName,
                ["logo"] = string.IsNullOrWhiteSpace(site?.Logo)
                    ? null
                    : new Dictionary<string, object?>
                    {
                        ["@type"] = "ImageObject",
                        ["url"] = options.ImageUrl(site.Logo)
                    }
            },
            ["inLanguage"] = "tr-TR"
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string Person(
        Team member,
        IdeioSeoOptions options,
        SeoTextFormatter formatter)
    {
        var url = options.TeamUrl(member);
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Person",
            ["@id"] = url + "#person",
            ["name"] = member.Name,
            ["jobTitle"] = member.Position,
            ["description"] = formatter.OneLine(member.Description),
            ["url"] = url,
            ["image"] = string.IsNullOrWhiteSpace(member.Image) ? null : options.ImageUrl(member.Image),
            ["worksFor"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = options.Absolute("/#organization"),
                ["name"] = options.BrandName
            },
            ["inLanguage"] = member.Language.ToString()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static string Breadcrumb(params (string Name, string Url)[] items)
    {
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items.Select((item, index) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = item.Name,
                ["item"] = item.Url
            }).ToArray()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }
}

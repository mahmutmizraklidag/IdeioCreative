using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdeioCreative.Entities;
using IdeioCreative.Models;

namespace IdeioCreative.Seo;

public static class SeoJsonLdBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Organization(SiteSetting? site, IdeioSeoOptions options, bool includeLocalBusiness = false)
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
            .Select(x => x?.Trim())
            .Where(IsWebUrl)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var local = options.LocalBusiness;
        var name = string.IsNullOrWhiteSpace(local.Name) ? options.BrandName : local.Name.Trim();
        var address = string.IsNullOrWhiteSpace(site?.Address)
            ? null
            : new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = site.Address.Trim(),
                ["addressCountry"] = local.AddressCountry
            };
        if (includeLocalBusiness && address is not null)
        {
            address["addressLocality"] = NullIfBlank(local.AddressLocality);
            address["addressRegion"] = NullIfBlank(local.AddressRegion);
            address["postalCode"] = NullIfBlank(local.PostalCode);
        }

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = includeLocalBusiness
                ? new[] { "Organization", "LocalBusiness", "ProfessionalService" }
                : (object)"Organization",
            ["@id"] = options.Absolute("/#organization"),
            ["name"] = name,
            ["alternateName"] = name == options.BrandName ? null : options.BrandName,
            ["url"] = options.Absolute("/"),
            ["logo"] = string.IsNullOrWhiteSpace(site?.Logo) ? null : options.ImageUrl(site.Logo),
            ["description"] = NullIfBlank(site?.MetaDescription),
            ["email"] = NullIfBlank(site?.Email),
            ["telephone"] = NullIfBlank(site?.Phone),
            ["sameAs"] = sameAs.Length == 0 ? null : sameAs,
            ["hasMap"] = includeLocalBusiness && IsWebUrl(local.MapUrl)
                ? local.MapUrl!.Trim()
                : IsWebUrl(site?.mapLink) ? site!.mapLink!.Trim() : null,
            ["address"] = address is null ? null : WithoutNullValues(address),
            ["contactPoint"] = string.IsNullOrWhiteSpace(site?.Phone) && string.IsNullOrWhiteSpace(site?.Email)
                ? null
                : new Dictionary<string, object?>
                {
                    ["@type"] = "ContactPoint",
                    ["telephone"] = NullIfBlank(site?.Phone),
                    ["email"] = NullIfBlank(site?.Email),
                    ["contactType"] = "customer service",
                    ["availableLanguage"] = new[] { "tr", "en" }
                }
        };

        if (includeLocalBusiness)
        {
            var image = string.IsNullOrWhiteSpace(site?.BannerImage) ? site?.Logo : site.BannerImage;
            if (!string.IsNullOrWhiteSpace(image))
                data["image"] = options.ImageUrl(image);

            if (local.Latitude is double latitude && local.Longitude is double longitude &&
                double.IsFinite(latitude) && double.IsFinite(longitude) &&
                latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
            {
                data["geo"] = new Dictionary<string, object?>
                {
                    ["@type"] = "GeoCoordinates",
                    ["latitude"] = latitude,
                    ["longitude"] = longitude
                };
            }

            var hours = local.OpeningHours.Where(group => group.IsValid)
                .Select(group => new Dictionary<string, object?>
                {
                    ["@type"] = "OpeningHoursSpecification",
                    ["dayOfWeek"] = group.Days
                        .Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true).ToString()).Distinct().ToArray(),
                    ["opens"] = group.Opens,
                    ["closes"] = group.Closes
                }).ToArray();
            if (hours.Length > 0)
                data["openingHoursSpecification"] = hours;
        }

        if (data["contactPoint"] is Dictionary<string, object?> contactPoint)
            data["contactPoint"] = WithoutNullValues(contactPoint);

        return JsonSerializer.Serialize(WithoutNullValues(data), JsonOptions);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsWebUrl(string? value) => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http";

    private static Dictionary<string, object?> WithoutNullValues(Dictionary<string, object?> data) =>
        data.Where(item => item.Value is not null).ToDictionary(item => item.Key, item => item.Value);

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

    public static string FaqPage(FaqSectionViewModel section)
    {
        var url = FaqContent.CanonicalPageUrl(section.PageUrl);
        if (section.Items.Count == 0 || string.IsNullOrEmpty(url))
            return string.Empty;

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["@id"] = url + "#faq",
            ["url"] = url,
            ["name"] = "Sık Sorulan Sorular",
            ["mainEntity"] = section.Items.Select(item => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = item.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = item.Answer
                }
            }).ToArray()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }
}

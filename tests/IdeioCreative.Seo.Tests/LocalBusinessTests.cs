using System.Globalization;
using System.Text.Json;
using IdeioCreative.Entities;
using IdeioCreative.Seo;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class LocalBusinessTests
{
    [Fact]
    public void BusinessEntitySharesIdentityAndDatabaseContactDetailsWithOrganization()
    {
        var site = new SiteSetting
        {
            Address = "Stored street No: 12, Bayrakli / Izmir", Phone = "+90 (232) 123 45 67",
            Email = "agency@example.test", Logo = "logo.png", BannerImage = "banner.png",
            mapLink = "https://maps.google.com/?q=stored-address",
            Facebook = " https://www.instagram.com/example/ ",
            Instagram = "https://www.instagram.com/example/",
            LinkedIn = "https://www.linkedin.com/company/example/",
            Twitter = "javascript:alert(1)", YouTube = "/relative-profile"
        };
        var options = new IdeioSeoOptions
        {
            LocalBusiness = new LocalBusinessSeoOptions
            {
                Name = "Agency business name", AddressLocality = "Bayrakli", AddressRegion = "Izmir",
                PostalCode = "35530", Latitude = 38.4505697, Longitude = 27.1840711,
                MapUrl = "https://www.google.com/maps?cid=123456",
                OpeningHours =
                {
                    new BusinessOpeningHours
                    {
                        Days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                        Opens = "09:00", Closes = "18:00", Label = "Weekdays"
                    },
                    new BusinessOpeningHours
                    {
                        Days = new[] { "Saturday", "Sunday" }, Opens = "00:00", Closes = "00:00", Label = "Weekend"
                    }
                }
            }
        };
        using var ordinaryJson = JsonDocument.Parse(SeoJsonLdBuilder.Organization(site, options));
        using var businessJson = JsonDocument.Parse(SeoJsonLdBuilder.Organization(site, options, includeLocalBusiness: true));
        var ordinary = ordinaryJson.RootElement;
        var business = businessJson.RootElement;
        Assert.Equal("Organization", ordinary.GetProperty("@type").GetString());
        Assert.Equal(new[] { "Organization", "LocalBusiness", "ProfessionalService" },
            business.GetProperty("@type").EnumerateArray().Select(type => type.GetString()));
        Assert.Equal("https://ideiocreative.com/#organization", business.GetProperty("@id").GetString());
        foreach (var field in new[] { "@id", "name", "alternateName", "telephone", "email", "logo", "sameAs" })
            Assert.Equal(ordinary.GetProperty(field).GetRawText(), business.GetProperty(field).GetRawText());
        Assert.Equal(site.Address, business.GetProperty("address").GetProperty("streetAddress").GetString());
        Assert.Equal(site.Address, ordinary.GetProperty("address").GetProperty("streetAddress").GetString());
        Assert.Equal("35530", business.GetProperty("address").GetProperty("postalCode").GetString());
        Assert.Equal(options.LocalBusiness.Name, business.GetProperty("name").GetString());
        Assert.Equal(options.BrandName, business.GetProperty("alternateName").GetString());
        Assert.Equal("https://ideiocreative.com/img/banner.png", business.GetProperty("image").GetString());
        Assert.Equal(options.LocalBusiness.MapUrl, business.GetProperty("hasMap").GetString());
        Assert.Equal(site.mapLink, ordinary.GetProperty("hasMap").GetString());
        Assert.Equal(new[] { "https://www.instagram.com/example/", "https://www.linkedin.com/company/example/" },
            business.GetProperty("sameAs").EnumerateArray().Select(profile => profile.GetString()));
        Assert.Equal(site.Phone, business.GetProperty("contactPoint").GetProperty("telephone").GetString());
        Assert.Equal(site.Email, business.GetProperty("contactPoint").GetProperty("email").GetString());
        Assert.False(ordinary.TryGetProperty("geo", out _));
        Assert.False(ordinary.TryGetProperty("openingHoursSpecification", out _));
        var hours = business.GetProperty("openingHoursSpecification").EnumerateArray().ToList();
        Assert.Equal(2, hours.Count);
        Assert.Equal("OpeningHoursSpecification", hours[0].GetProperty("@type").GetString());
        Assert.Equal("09:00", hours[0].GetProperty("opens").GetString());
        Assert.Equal("18:00", hours[0].GetProperty("closes").GetString());
        Assert.Equal("00:00", hours[1].GetProperty("opens").GetString());
        Assert.Equal("00:00", hours[1].GetProperty("closes").GetString());
        Assert.Equal("Weekdays: 09.00 - 18.00", options.LocalBusiness.OpeningHours[0].DisplayText);
        Assert.Equal("Weekend: Kapal\u0131", options.LocalBusiness.OpeningHours[1].DisplayText);
        AssertNoNullValues(business);
    }

    [Fact]
    public void MissingOptionalDataIsOmittedWithoutInventedAddressHoursOrCoordinates()
    {
        var site = new SiteSetting
        {
            Phone = "   ", Email = "", Address = "\t", MetaDescription = " ", Facebook = "#",
            Instagram = "javascript:alert(1)", mapLink = "invalid-map"
        };
        foreach (var settings in new SiteSetting?[] { null, site })
        {
            using var json = JsonDocument.Parse(SeoJsonLdBuilder.Organization(settings, new IdeioSeoOptions(), includeLocalBusiness: true));
            Assert.Equal("https://ideiocreative.com/#organization", json.RootElement.GetProperty("@id").GetString());
            foreach (var field in new[]
            {
                "address", "telephone", "email", "description", "logo", "image", "sameAs", "hasMap",
                "geo", "openingHoursSpecification", "contactPoint", "alternateName"
            })
                Assert.False(json.RootElement.TryGetProperty(field, out _), $"Missing {field} must be omitted.");
            AssertNoNullValues(json.RootElement);
        }
    }

    [Fact]
    public void InvalidCoordinatesAndHoursAreExcludedAndValidMapFallbackRemains()
    {
        var options = new IdeioSeoOptions();
        options.LocalBusiness.MapUrl = "javascript:alert(1)";
        options.LocalBusiness.OpeningHours.AddRange(new[]
        {
            new BusinessOpeningHours { Days = new[] { "Funday" }, Opens = "09:00", Closes = "18:00" },
            new BusinessOpeningHours { Days = new[] { "Monday" }, Opens = "24:30", Closes = "18:00" },
            new BusinessOpeningHours { Days = Array.Empty<string>(), Opens = "09:00", Closes = "18:00" }
        });
        var site = new SiteSetting { mapLink = "https://maps.google.com/?q=valid-fallback", Email = "agency@example.test" };
        foreach (var coordinates in new (double? Latitude, double? Longitude)[]
        {
            (null, 27), (38, null), (-91, 27), (91, 27), (38, -181), (38, 181),
            (double.NaN, 27), (38, double.PositiveInfinity), (double.NegativeInfinity, 27)
        })
        {
            options.LocalBusiness.Latitude = coordinates.Latitude;
            options.LocalBusiness.Longitude = coordinates.Longitude;
            using var json = JsonDocument.Parse(SeoJsonLdBuilder.Organization(site, options, includeLocalBusiness: true));
            Assert.False(json.RootElement.TryGetProperty("geo", out _));
            Assert.False(json.RootElement.TryGetProperty("openingHoursSpecification", out _));
            Assert.Equal(site.mapLink, json.RootElement.GetProperty("hasMap").GetString());
            Assert.False(json.RootElement.GetProperty("contactPoint").TryGetProperty("telephone", out _));
            AssertNoNullValues(json.RootElement);
        }
        Assert.All(options.LocalBusiness.OpeningHours, hours =>
        {
            Assert.False(hours.IsValid);
            Assert.Equal("", hours.DisplayText);
        });
    }

    [Fact]
    public void JsonCoordinatesAreCultureIndependentAndAdminTextCannotCloseScriptElements()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            const string unsafeText = "Agency \"quoted\" </script><script>alert('fixture')</script>";
            var options = new IdeioSeoOptions();
            options.LocalBusiness.Name = unsafeText;
            options.LocalBusiness.Latitude = 38.4505697;
            options.LocalBusiness.Longitude = 27.1840711;
            var content = SeoJsonLdBuilder.Organization(new SiteSetting { Address = unsafeText, MetaDescription = unsafeText },
                options, includeLocalBusiness: true);
            Assert.DoesNotContain("</script>", content, StringComparison.OrdinalIgnoreCase);
            using var json = JsonDocument.Parse(content);
            Assert.Equal(unsafeText, json.RootElement.GetProperty("name").GetString());
            Assert.Equal(unsafeText, json.RootElement.GetProperty("address").GetProperty("streetAddress").GetString());
            Assert.Equal(unsafeText, json.RootElement.GetProperty("description").GetString());
            var geo = json.RootElement.GetProperty("geo");
            Assert.Equal(JsonValueKind.Number, geo.GetProperty("latitude").ValueKind);
            Assert.Equal(JsonValueKind.Number, geo.GetProperty("longitude").ValueKind);
            Assert.Equal(38.4505697, geo.GetProperty("latitude").GetDouble());
            Assert.Equal(27.1840711, geo.GetProperty("longitude").GetDouble());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static void AssertNoNullValues(JsonElement value)
    {
        Assert.NotEqual(JsonValueKind.Null, value.ValueKind);
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
                AssertNoNullValues(property.Value);
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var element in value.EnumerateArray())
                AssertNoNullValues(element);
    }
}

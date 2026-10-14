using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IdeioCreative.Seo;

public sealed class LocalBusinessSeoOptions
{
    public string? Name { get; set; }
    public string? AddressLocality { get; set; }
    public string? AddressRegion { get; set; }
    public string? PostalCode { get; set; }
    public string AddressCountry { get; set; } = "TR";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? MapUrl { get; set; }
    public List<BusinessOpeningHours> OpeningHours { get; set; } = new();
}

public sealed class BusinessOpeningHours
{
    private static readonly string[] TimeFormats = { "HH:mm", "HH:mm:ss" };
    private static readonly Dictionary<string, string> DayLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monday"] = "Pzt",
        ["Tuesday"] = "Sal",
        ["Wednesday"] = "Çar",
        ["Thursday"] = "Per",
        ["Friday"] = "Cum",
        ["Saturday"] = "Cmt",
        ["Sunday"] = "Paz"
    };

    public string[] Days { get; set; } = Array.Empty<string>();
    public string Opens { get; set; } = "";
    public string Closes { get; set; } = "";
    public string? Label { get; set; }

    public bool IsValid =>
        Days is { Length: > 0 } &&
        Days.All(day => day is not null && DayLabels.ContainsKey(day)) &&
        TryParseTime(Opens, out _) &&
        TryParseTime(Closes, out _);

    public string DisplayText
    {
        get
        {
            if (!IsValid)
                return "";

            var group = string.IsNullOrWhiteSpace(Label)
                ? string.Join(", ", Days.Select(day => DayLabels[day]))
                : Label.Trim();

            TryParseTime(Opens, out var opens);
            TryParseTime(Closes, out var closes);

            if (opens == TimeOnly.MinValue && closes == TimeOnly.MinValue)
                return $"{group}: Kapalı";

            return $"{group}: {Opens.Replace(':', '.')} - {Closes.Replace(':', '.')}";
        }
    }

    private static bool TryParseTime(string? value, out TimeOnly result) =>
        TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}

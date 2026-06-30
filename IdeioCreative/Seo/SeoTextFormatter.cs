using System;
using System.Net;
using System.Text.RegularExpressions;

namespace IdeioCreative.Seo;

public sealed class SeoTextFormatter
{
    private static readonly Regex ScriptRegex = new(
        @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        @"<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BreakRegex = new(
        @"<(br|\/p|\/div|\/li|\/h[1-6])\s*\/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(
        @"[ \t\f\v]+",
        RegexOptions.Compiled);

    private static readonly Regex MultiLineRegex = new(
        @"\n\s*\n\s*\n+",
        RegexOptions.Compiled);

    public string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = ScriptRegex.Replace(html, " ");
        text = StyleRegex.Replace(text, " ");
        text = BreakRegex.Replace(text, "\n");
        text = TagRegex.Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r", string.Empty);
        text = MultiSpaceRegex.Replace(text, " ");
        text = MultiLineRegex.Replace(text, "\n\n");

        return text.Trim();
    }

    public string OneLine(string? html, int maxLength = 300)
    {
        var value = ToPlainText(html)
            .Replace("\n", " ")
            .Trim();

        value = MultiSpaceRegex.Replace(value, " ");

        if (value.Length <= maxLength)
            return value;

        return value[..maxLength].TrimEnd() + "…";
    }
}

using System.Net;
using System.Text;

namespace IdeioCreative.Tools;

/// <summary>Prepares stored HTML without reserializing its surrounding content.</summary>
public static class ImageHtml
{
    public static string WithLoadingAttributes(string? html, string? imageDescription = null) =>
        Rewrite(html, imageDescription, normalizeHeadings: false);

    // Content fragments sit below the page's main H1. Preserve their visual
    // heading size while preventing stored content from adding another H1.
    public static string PrepareContent(string? html, string? imageDescription = null) =>
        Rewrite(html, imageDescription, normalizeHeadings: true);

    private static string Rewrite(string? html, string? imageDescription, bool normalizeHeadings)
    {
        if (string.IsNullOrEmpty(html))
            return html ?? string.Empty;

        StringBuilder? result = null;
        var copiedThrough = 0;
        var position = 0;
        while ((position = html.IndexOf('<', position)) >= 0)
        {
            if (html.AsSpan(position).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = html.IndexOf("-->", position + 4, StringComparison.Ordinal);
                if (end < 0)
                    break;
                position = end + 3;
                continue;
            }

            if (html.AsSpan(position).StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                var end = html.IndexOf("]]>", position + 9, StringComparison.Ordinal);
                if (end < 0)
                    break;
                position = end + 3;
                continue;
            }

            var nameStart = position + 1;
            var closing = nameStart < html.Length && html[nameStart] == '/';
            if (closing)
                nameStart++;
            if (nameStart >= html.Length)
                break;
            if (!char.IsAsciiLetter(html[nameStart]))
            {
                if (html[nameStart] is '!' or '?')
                {
                    var declarationEnd = FindTagEnd(html, nameStart + 1);
                    if (declarationEnd < 0)
                        break;
                    position = declarationEnd + 1;
                }
                else
                    position++;
                continue;
            }

            var nameEnd = nameStart + 1;
            while (nameEnd < html.Length && IsNameCharacter(html[nameEnd]))
                nameEnd++;
            var tagEnd = FindTagEnd(html, nameEnd);
            if (tagEnd < 0)
                break;
            var name = html.AsSpan(nameStart, nameEnd - nameStart);
            var validNameBoundary = nameEnd == tagEnd || IsHtmlWhitespace(html[nameEnd]) || html[nameEnd] == '/';

            string? replacement = null;
            if (!closing && validNameBoundary && name.Equals("img", StringComparison.OrdinalIgnoreCase))
                replacement = RewriteTag(html, position, nameEnd, tagEnd, imageDescription, isHeading: false);
            else if (normalizeHeadings && validNameBoundary && name.Equals("h1", StringComparison.OrdinalIgnoreCase))
            {
                replacement = closing
                    ? html[position..nameStart] + "h2" + html[nameEnd..(tagEnd + 1)]
                    : RewriteTag(html, position, nameEnd, tagEnd, imageDescription, isHeading: true);
            }
            if (replacement is not null)
            {
                result ??= new StringBuilder(html.Length + 80);
                result.Append(html, copiedThrough, position - copiedThrough);
                result.Append(replacement);
                copiedThrough = tagEnd + 1;
            }

            position = tagEnd + 1;
            if (!closing && validNameBoundary && IsRawTextElement(name))
            {
                // These elements treat apparent image tags as text, including quoted script strings.
                position = FindRawTextEnd(html, position, name);
                if (position < 0)
                    break;
            }
        }

        if (result is null)
            return html;
        return result.Append(html, copiedThrough, html.Length - copiedThrough).ToString();
    }

    private static string? RewriteTag(string html, int tagStart, int nameEnd, int tagEnd,
        string? imageDescription, bool isHeading)
    {
        var attributes = new List<(int SpaceStart, int Start, int End, string Name, string? Value)>();
        var position = nameEnd;
        var insertAt = nameEnd;
        while (position < tagEnd)
        {
            var spaceStart = position;
            while (position < tagEnd && IsHtmlWhitespace(html[position]))
                position++;
            if (position == tagEnd)
                break;
            if (html[position] == '/')
            {
                if (position + 1 != tagEnd)
                    return null;
                break;
            }
            if (spaceStart == position)
                return null; // Keep malformed tags intact rather than repairing their syntax.

            var attributeStart = position;
            while (position < tagEnd && !IsHtmlWhitespace(html[position]) && html[position] is not '=' and not '/')
            {
                if (html[position] is '\0' or '"' or '\'' or '<' or '>' or '`')
                    return null;
                position++;
            }
            if (position == attributeStart)
                return null;
            var attributeName = html[attributeStart..position];
            var attributeEnd = position;
            while (position < tagEnd && IsHtmlWhitespace(html[position]))
                position++;

            string? value = null;
            if (position < tagEnd && html[position] == '=')
            {
                position++;
                while (position < tagEnd && IsHtmlWhitespace(html[position]))
                    position++;
                if (position == tagEnd)
                    return null;
                if (html[position] is '"' or '\'')
                {
                    var quote = html[position++];
                    var valueStart = position;
                    while (position < tagEnd && html[position] != quote)
                        position++;
                    if (position == tagEnd)
                        return null;
                    value = html[valueStart..position];
                    position++;
                }
                else
                {
                    var valueStart = position;
                    while (position < tagEnd && !IsHtmlWhitespace(html[position]))
                    {
                        if (html[position] is '\0' or '"' or '\'' or '<' or '>' or '=' or '`')
                            return null;
                        position++;
                    }
                    value = html[valueStart..position];
                }
                attributeEnd = position;
            }
            else
                position = attributeEnd;

            attributes.Add((spaceStart, attributeStart, attributeEnd, attributeName, value));
            insertAt = attributeEnd;
        }

        var edits = new List<(int Start, int End, string Text)>();
        if (isHeading)
        {
            edits.Add((tagStart + 1, nameEnd, "h2"));
            var classAttribute = attributes.FirstOrDefault(attribute => attribute.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
            var classes = WebUtility.HtmlDecode(classAttribute.Value) ?? string.Empty;
            if (!classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains("h1"))
            {
                var value = $"class=\"{WebUtility.HtmlEncode(("h1 " + classes).Trim())}\"";
                edits.Add(classAttribute.Name is null
                    ? (insertAt, insertAt, " " + value)
                    : (classAttribute.Start, classAttribute.End, value));
            }
            return ApplyEdits(html, tagStart, tagEnd, edits);
        }

        var priority = attributes.FirstOrDefault(attribute => attribute.Name.Equals("fetchpriority", StringComparison.OrdinalIgnoreCase)).Value;
        var loadingValue = string.Equals(WebUtility.HtmlDecode(priority), "high", StringComparison.OrdinalIgnoreCase) ? "eager" : "lazy";
        var source = attributes.FirstOrDefault(attribute => attribute.Name.Equals("src", StringComparison.OrdinalIgnoreCase)).Value;
        var alternative = attributes.FirstOrDefault(attribute => attribute.Name.Equals("alt", StringComparison.OrdinalIgnoreCase)).Value;
        var alternativeValue = string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(alternative))
            ? ImageAlternativeText.Resolve(imageDescription, WebUtility.HtmlDecode(source))
            : WebUtility.HtmlDecode(alternative)!;
        var hasLoading = false;
        var hasDecoding = false;
        var hasAlternative = false;
        foreach (var attribute in attributes)
        {
            if (attribute.Name.Equals("loading", StringComparison.OrdinalIgnoreCase))
            {
                edits.Add(hasLoading
                    ? (attribute.SpaceStart, attribute.End, string.Empty)
                    : (attribute.Start, attribute.End, $"loading=\"{loadingValue}\""));
                hasLoading = true;
            }
            else if (attribute.Name.Equals("decoding", StringComparison.OrdinalIgnoreCase))
            {
                edits.Add(hasDecoding
                    ? (attribute.SpaceStart, attribute.End, string.Empty)
                    : (attribute.Start, attribute.End, "decoding=\"async\""));
                hasDecoding = true;
            }
            else if (attribute.Name.Equals("alt", StringComparison.OrdinalIgnoreCase))
            {
                if (hasAlternative)
                    edits.Add((attribute.SpaceStart, attribute.End, string.Empty));
                else if (string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(attribute.Value)))
                    edits.Add((attribute.Start, attribute.End, $"alt=\"{WebUtility.HtmlEncode(alternativeValue)}\""));
                hasAlternative = true;
            }
        }
        if (!hasLoading || !hasDecoding || !hasAlternative)
            edits.Add((insertAt, insertAt, (!hasLoading ? $" loading=\"{loadingValue}\"" : "")
                + (!hasDecoding ? " decoding=\"async\"" : "")
                + (!hasAlternative ? $" alt=\"{WebUtility.HtmlEncode(alternativeValue)}\"" : "")));

        return ApplyEdits(html, tagStart, tagEnd, edits);
    }

    private static string? ApplyEdits(string html, int tagStart, int tagEnd, List<(int Start, int End, string Text)> edits)
    {
        edits.Sort((left, right) => left.Start.CompareTo(right.Start));
        var rewritten = new StringBuilder(tagEnd - tagStart + 50);
        var copiedThrough = tagStart;
        foreach (var edit in edits)
        {
            rewritten.Append(html, copiedThrough, edit.Start - copiedThrough);
            rewritten.Append(edit.Text);
            copiedThrough = edit.End;
        }
        rewritten.Append(html, copiedThrough, tagEnd + 1 - copiedThrough);
        var replacement = rewritten.ToString();
        return html.AsSpan(tagStart, tagEnd + 1 - tagStart).SequenceEqual(replacement) ? null : replacement;
    }

    private static int FindTagEnd(string html, int position)
    {
        var quote = '\0';
        for (; position < html.Length; position++)
        {
            var character = html[position];
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
            }
            else if (character is '"' or '\'')
                quote = character;
            else if (character == '>')
                return position;
            else if (character == '<')
                return -1;
        }
        return -1;
    }

    private static int FindRawTextEnd(string html, int position, ReadOnlySpan<char> name)
    {
        if (name.Equals("plaintext", StringComparison.OrdinalIgnoreCase))
            return -1;
        while ((position = html.IndexOf("</", position, StringComparison.Ordinal)) >= 0)
        {
            var afterName = position + 2 + name.Length;
            if (afterName < html.Length && html.AsSpan(position + 2, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase)
                && (IsHtmlWhitespace(html[afterName]) || html[afterName] is '>' or '/'))
            {
                var end = FindTagEnd(html, afterName);
                return end < 0 ? -1 : end + 1;
            }
            position += 2;
        }
        return -1;
    }

    private static bool IsRawTextElement(ReadOnlySpan<char> name) =>
        name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase)
        || name.Equals("textarea", StringComparison.OrdinalIgnoreCase) || name.Equals("title", StringComparison.OrdinalIgnoreCase)
        || name.Equals("xmp", StringComparison.OrdinalIgnoreCase) || name.Equals("iframe", StringComparison.OrdinalIgnoreCase)
        || name.Equals("noembed", StringComparison.OrdinalIgnoreCase) || name.Equals("noframes", StringComparison.OrdinalIgnoreCase)
        || name.Equals("plaintext", StringComparison.OrdinalIgnoreCase);

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or ':';
    private static bool IsHtmlWhitespace(char character) => character is ' ' or '\t' or '\n' or '\r' or '\f';
}

using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace IdeioCreative.Tools;

// Covers images in views, layouts and components, including future additions.
[HtmlTargetElement("img", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ImageLoadingTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var isHighPriority = output.Attributes.TryGetAttribute("fetchpriority", out var priority) &&
            string.Equals(priority.Value?.ToString(), "high", StringComparison.OrdinalIgnoreCase);

        if (isHighPriority)
            output.Attributes.SetAttribute("loading", "eager");
        else if (!output.Attributes.ContainsName("loading"))
            output.Attributes.SetAttribute("loading", "lazy");

        output.Attributes.SetAttribute("decoding", "async");

        if (!output.Attributes.TryGetAttribute("alt", out var alternative) ||
            string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(alternative.Value?.ToString())))
        {
            output.Attributes.TryGetAttribute("src", out var source);
            output.Attributes.TryGetAttribute("title", out var title);
            output.Attributes.SetAttribute("alt", ImageAlternativeText.Resolve(
                title?.Value?.ToString(), source?.Value?.ToString()));
        }
    }
}

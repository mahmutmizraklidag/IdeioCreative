using Microsoft.AspNetCore.Razor.TagHelpers;

namespace IdeioCreative.Tools;

[HtmlTargetElement("img", TagStructure = TagStructure.WithoutEndTag)]
[HtmlTargetElement("script", Attributes = "src")]
[HtmlTargetElement("link", Attributes = "href", TagStructure = TagStructure.WithoutEndTag)]
[HtmlTargetElement("source", TagStructure = TagStructure.WithoutEndTag)]
[HtmlTargetElement("video")]
public sealed class AssetFingerprintTagHelper(ContentHashedAssets assets) : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        foreach (var name in new[] { "src", "href", "poster" })
        {
            if (!output.Attributes.TryGetAttribute(name, out var attribute)) continue;
            var value = attribute.Value?.ToString();
            if (value is null) continue;
            var versioned = assets.Url(value);
            if (versioned != value) output.Attributes.SetAttribute(name, versioned);
        }
        if (output.Attributes.TryGetAttribute("srcset", out var sources))
            output.Attributes.SetAttribute("srcset", assets.SourceSet(sources.Value?.ToString() ?? ""));
    }
}

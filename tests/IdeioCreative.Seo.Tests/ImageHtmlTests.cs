using IdeioCreative.Tools;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class ImageHtmlTests
{
    [Fact]
    public void RichContentKeepsImageUrlsFormattingAndSurroundingMarkup()
    {
        const string html = "<section class='gallery'><p>İzmir &amp; reklam</p><picture><source srcset='wide.webp 2x'>\n"
            + "<IMG src='photo.webp?caption=a&gt;b' srcset=\"one.webp 1x, two.webp 2x\" alt='a > b' style=\"width: 50%; height: auto\" /></picture></section>";
        const string expected = "<section class='gallery'><p>İzmir &amp; reklam</p><picture><source srcset='wide.webp 2x'>\n"
            + "<IMG src='photo.webp?caption=a&gt;b' srcset=\"one.webp 1x, two.webp 2x\" alt='a > b' style=\"width: 50%; height: auto\" loading=\"lazy\" decoding=\"async\" /></picture></section>";

        Assert.Equal(expected, ImageHtml.WithLoadingAttributes(html));
        Assert.Equal(expected, ImageHtml.WithLoadingAttributes(expected));
    }

    [Fact]
    public void ExistingHintsAreNormalizedWithoutDuplicatesAndHighPriorityRemainsEager()
    {
        const string html = "<img src=ordinary.webp LoAdInG = 'eager' loading=auto DECODING=sync decoding><img src='hero.webp' fetchpriority=HIGH loading=lazy>"
            + "<img src=second.webp fetchpriority='h&#105;gh' decoding=auto>";
        const string expected = "<img src=ordinary.webp loading=\"lazy\" decoding=\"async\" alt=\"ordinary\"><img src='hero.webp' fetchpriority=HIGH loading=\"eager\" decoding=\"async\" alt=\"hero\">"
            + "<img src=second.webp fetchpriority='h&#105;gh' decoding=\"async\" loading=\"eager\" alt=\"second\">";

        var actual = ImageHtml.WithLoadingAttributes(html);
        Assert.Equal(expected, actual);
        Assert.Equal(actual, ImageHtml.WithLoadingAttributes(actual));
    }

    [Fact]
    public void CommentsAndRawTextRemainUntouchedWhileFollowingImagesAreProcessed()
    {
        const string ignored = "<!-- <img src='comment.webp'> --><script>const template = '<img src=script.webp>'; const close = '</scripture>';</script>"
            + "<STYLE>.example::after { content: '<img src=style.webp>'; }</STYLE><textarea><img src=textarea.webp></textarea>"
            + "<title><img src=title.webp></title><![CDATA[<img src=cdata.webp>]]><xmp><img src=xmp.webp></xmp>";
        const string image = "<img src='visible.webp'>";

        Assert.Equal(ignored + "<img src='visible.webp' loading=\"lazy\" decoding=\"async\" alt=\"visible\">", ImageHtml.WithLoadingAttributes(ignored + image));
        Assert.Same(ignored, ImageHtml.WithLoadingAttributes(ignored));
    }

    [Fact]
    public void MissingAlternativesUseEscapedContextAndExistingDescriptionsArePreserved()
    {
        const string html = "<img src='one.webp' alt=' &#xA0; '><img src='two.webp' alt=\"Detailed &amp; useful\"><img src='three.webp'>";
        var actual = ImageHtml.WithLoadingAttributes(html, "Service & \"quote\"");
        Assert.Contains("alt=\"Service &amp; &quot;quote&quot;\"", actual);
        Assert.Contains("alt=\"Detailed &amp; useful\"", actual);
        Assert.Equal(actual, ImageHtml.WithLoadingAttributes(actual, "Service & \"quote\""));

        var fallback = ImageHtml.WithLoadingAttributes("<img src='/img/web-tasarimi.png?token=private'>");
        Assert.Contains("alt=\"web tasarimi\"", fallback);
        var embedded = ImageHtml.WithLoadingAttributes("<img src='data:image/png;base64,AAAA'>");
        Assert.DoesNotContain("alt=\"AAAA", embedded);
        Assert.Contains("alt=\"", embedded);
    }

    [Fact]
    public void ContentHeadingsBecomeSubheadingsWithoutChangingTextOrInertMarkup()
    {
        const string ignored = "<!-- <h1>Comment</h1> --><script>const sample = '<h1>Script</h1>';</script><textarea><h1>Editor</h1></textarea>";
        const string headings = "<H1 id='first' class='section-title'>First &amp; more</H1><h1>Second</h1><h2>Existing subheading</h2>";
        const string expected = "<h2 id='first' class=\"h1 section-title\">First &amp; more</h2><h2 class=\"h1\">Second</h2><h2>Existing subheading</h2>";
        var actual = ImageHtml.PrepareContent(ignored + headings);
        Assert.Equal(ignored + expected, actual);
        Assert.Equal(actual, ImageHtml.PrepareContent(actual));
        Assert.Same(headings, ImageHtml.WithLoadingAttributes(headings));
    }

    [Fact]
    public void MalformedOrIncompleteFragmentsArePreservedInsteadOfRepaired()
    {
        foreach (var html in new[]
        {
            "<p>No images &lt;img&gt;</p>", "<img src='unterminated.webp>", "<img src=broken=attribute>", "<img loading=>",
            "<img src='missing-separator.webp'alt='text'>", "<!-- <img src=hidden.webp>", "<script>const html = '<img src=x>';",
            "<div title='unterminated <img src=x>", "<img! src='not-an-image-tag'>"
        })
            Assert.Same(html, ImageHtml.WithLoadingAttributes(html));

        Assert.Equal(string.Empty, ImageHtml.WithLoadingAttributes(null));
        Assert.Equal(string.Empty, ImageHtml.WithLoadingAttributes(string.Empty));
    }
}

# Open Graph and Twitter cards

Both public layouts render `_SocialMeta.cshtml` once in `<head>`. It supplies
`og:title`, `og:description`, `og:image`, `og:url`, `og:type`, `og:locale=tr_TR`,
`og:site_name`, image type/dimensions/alt text and
`twitter:card=summary_large_image` with matching Twitter title, description,
image and alt text. Blog detail pages use `article`; other pages use `website`.

Titles and descriptions follow page metadata, with a brand description fallback.
Stored HTML is converted to plain text, and Razor escapes attribute values.
URLs use the configured public `BaseUrl`, canonical page paths and no tracking
query string or fragment. They do not depend on the incoming Host header.

The builder selects a local page image from service, sector, blog or About data
only when its PNG, JPEG or WebP header declares exactly **1200 x 630** pixels.
Missing files, incompatible dimensions/formats and remote URLs use the default
brand image at `assets/img/social/ideio-creative.png`, which is a real 1200 x 630
PNG generated from the existing logo. It is not replaced by a small logo file.
An individual view can override the image with
`ViewData["SocialImage"] = "/assets/img/social/page-image.png"` under the same
dimension checks. Image URLs include their content hash through site delivery.

Regenerate the default image after updating the brand logo:

```powershell
./tools/build-social-image.ps1
```

Publish the application and default PNG together. Existing previews may stay in
social networks' own caches until the URL is re-scraped. Tests verify the tags on
public pages, unique properties, canonical tracking-free URLs, blog article
types, accessible 1200 x 630 images, image format/dimension selection and fallbacks.

References: [Open Graph protocol](https://ogp.me/),
[X cards](https://developer.x.com/en/docs/x-for-websites/cards/overview/summary-card-with-large-image).

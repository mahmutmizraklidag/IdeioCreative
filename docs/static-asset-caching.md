# Static asset caching

`AddSiteDelivery` and `UseSiteDelivery` configure content-hashed static asset URLs,
browser cache headers and Brotli/gzip response compression. Static requests are
handled before the database-backed page-data middleware.

| Resource | Policy |
| --- | --- |
| Assets with a verified SHA-256 hash in their filename | `public, max-age=31536000, immutable` |
| Original, unversioned static URLs (including old `?v=` URLs) | `public, max-age=300, must-revalidate` |
| Successful public HTML without a stronger existing policy | `public, max-age=60, must-revalidate` |
| Form responses with antiforgery tokens, admin/authenticated HTML and responses setting cookies | Existing `no-store` protection is preserved/enforced |

For example, `/assets/css/site.bundle.min.css` becomes
`/assets/css/<64-character-sha256>.site.bundle.min.css`. The filename hash is based
on the actual file bytes. The static-file provider validates it before serving
the original file. A false or obsolete hash returns 404 instead of serving
different content under an immutable URL. These are virtual filename aliases;
files are not duplicated or renamed on disk, and stored CMS filenames stay valid.

The Razor tag helper versions local image, script, stylesheet, icon, preload,
video/poster and source-set URLs. Stored CMS images and picture sources receive
the same treatment through `AssetUrls.PrepareContent`. Remote and missing assets
are left unchanged. Existing `asp-append-version` attributes can stay in place;
their query strings are not used to decide immutability.

The asset build rewrites local CSS image/font URLs to the same hash-filename
format before generating CSS bundles. Consequently changing a CSS dependency
changes its filename and the containing bundle's hash. Run `npm run build:assets`
after changing an image/font referenced from CSS, then publish the bundles and
their dependencies together. Deployment should replace each original file
atomically; all instances should use the same asset version during rollout.

The static-file middleware retains ETag, Last-Modified, conditional requests and
byte ranges. HTML is never given the static one-year policy. The shared public
layout contains an antiforgery-protected quote form, so those pages retain the
framework's `no-cache, no-store` policy rather than sharing form tokens in caches.
Short HTML caching applies only where no stronger policy is required.

Brotli and gzip are enabled for HTTP and HTTPS with the standard text MIME types
and SVG. The client negotiates the encoding through `Accept-Encoding`; the
middleware emits `Content-Encoding` and `Vary: Accept-Encoding`. Already-compressed
image/font/media formats are not compressed again. Third-party headers are
controlled by their own servers.

Publish the application changes, then inspect a real asset response with the
browser's Network panel or `curl.exe -I <asset-url>`. Check both a versioned CSS/JS
filename URL and an unversioned image/font. Send `Accept-Encoding: br` and
`Accept-Encoding: gzip` separately to verify both compression paths. If IIS, a reverse proxy or a CDN overrides the
application headers, its configuration also needs to preserve these policies.
Run Lighthouse again after publishing. Browser caching improves repeat visits;
image resizing/compression is still needed to reduce the initial download.

Validation covers real HTTP responses for hashed/unversioned assets, changed
content, invalid hashes, font/media files, 304 responses, byte ranges, HTML and
form/admin cache policies, CMS image/source-set URLs, both compression algorithms
and rendered public-page asset loading.

References: [ASP.NET Core static-file headers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-8.0#set-http-response-headers),
[Chrome cache lifetime guidance](https://developer.chrome.com/docs/performance/insights/cache),
[HTTP cache busting](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Cache-Control#caching_static_assets_with_cache_busting),
[ASP.NET Core response compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-8.0).

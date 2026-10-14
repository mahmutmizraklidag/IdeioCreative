# Dynamic sitemap and modification dates

`/sitemap.xml` queries the current content on every request. It includes the
configured static routes, Turkish services and published blog posts. New content
is added automatically, deleted content disappears, and scheduled posts stay out
until their publication date. URLs use `https://ideiocreative.com` and contain no
query string or fragment.

`SeoPageModifications` records the UTC time of real public content changes inside
the same `SaveChanges` transaction as the content. This also records list-page
changes when an item is deleted. Unchanged saves, contact submissions and SMTP
settings do not change these dates. A failed save does not publish a new date.

Apply the migration before deploying the application:

```powershell
dotnet ef database update --project IdeioCreative/IdeioCreative.csproj
```

Alternatively, run the idempotent SQL script in [sitemap-lastmod.sql](sitemap-lastmod.sql).
This script contains only the new migration and assumes the previous migrations
have already been applied. Neither command was run against the site's database
as part of this code change.

The initial template modification date is **2026-10-14**, the actual date of the
canonical URL and shared SEO markup changes in this work. It is configured as
`IdeioSeoOptions.PageTemplatesLastModified`. Update this date only after a
significant change to the shared page templates; do not replace it with the
request time, build time or deployment time. This gives the existing pages a
verified source-change date without inventing missing historical CMS edit dates.

Each URL uses the latest applicable template, public content or publication
date. Later CMS changes override the initial date automatically. The number of
URLs is not fixed at 16.

Validation:

```powershell
dotnet test tests/IdeioCreative.Seo.Tests/IdeioCreative.Seo.Tests.csproj --configuration SitemapQa
```

Tests cover the XML endpoint, 16 initial URLs, repeat requests, edits and deletion,
unpublished content, empty edits, private settings, failed saves and query strings.

Google's specification for meaningful, accurate `lastmod` values:
https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap

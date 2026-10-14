using System.Net;
using System.Text.RegularExpressions;
using IdeioCreative.Data;
using IdeioCreative.Seo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IdeioCreative.Seo.Tests;

public sealed class RobotsTests
{
    private static readonly string[] AllowedAgents =
    {
        "Googlebot", "OAI-SearchBot", "ChatGPT-User", "ClaudeBot", "PerplexityBot",
        "Perplexity-User", "Google-Extended", "OtherCrawler"
    };

    private static readonly string[] PublicPaths =
    {
        "/", "/hakkimizda", "/iletisim", "/ekibimiz", "/hizmetlerimiz", "/blog",
        "/hizmetlerimiz/web-tasarimi", "/blog/web-rehberi?utm=example&gclid=example",
        "/assets/js/site.bundle.min.js", "/assets/css/site.bundle.min.css",
        "/administrator", "/accounting"
    };

    private static readonly string[] PrivatePaths =
    {
        "/admin", "/admin/", "/admin/Users", "/admin?utm=example&gclid=example",
        "/Admin", "/Admin/", "/Admin/Login", "/Admin?utm=example&gclid=example",
        "/account", "/account/", "/account/profile", "/account?utm=example&gclid=example",
        "/Account", "/Account/", "/Account/Profile", "/Account?utm=example&gclid=example"
    };

    [Fact]
    public async Task RobotsAllowsPublicPagesForAiAndSearchBotsWhileKeepingPrivateAreasBlocked()
    {
        await using var host = await RobotsHost.Start();
        var groups = await ReadRobots(host.Http);

        foreach (var agent in AllowedAgents.Append("GPTBot"))
        {
            AssertPublicAndPrivatePaths(groups, agent);
        }
    }

    [Fact]
    public async Task DisablingTrainingOnlyBlocksGptBotAndPreservesSearchCrawlerPermissions()
    {
        await using var host = await RobotsHost.Start(options => options.AllowGptBotForTraining = false);
        var groups = await ReadRobots(host.Http);

        foreach (var path in PublicPaths.Concat(PrivatePaths))
        {
            Assert.False(IsAllowed(groups, "GPTBot", path), $"GPTBot must be blocked at {path}.");
        }

        foreach (var agent in AllowedAgents)
        {
            AssertPublicAndPrivatePaths(groups, agent);
        }
    }

    private static void AssertPublicAndPrivatePaths(IReadOnlyList<RobotsGroup> groups, string agent)
    {
        foreach (var path in PublicPaths)
        {
            Assert.True(IsAllowed(groups, agent, path), $"{agent} must be allowed at {path}.");
        }

        foreach (var path in PrivatePaths)
        {
            Assert.False(IsAllowed(groups, agent, path), $"{agent} must be blocked at {path}.");
        }
    }

    private static async Task<List<RobotsGroup>> ReadRobots(HttpClient http)
    {
        using var response = await http.GetAsync("/robots.txt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet?.ToLowerInvariant());
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sitemap: https://ideiocreative.com/sitemap.xml", content);

        var groups = new List<RobotsGroup>();
        RobotsGroup? current = null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var field = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (field.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (current is null || current.Rules.Count > 0)
                {
                    current = new RobotsGroup();
                    groups.Add(current);
                }
                current.Agents.Add(value);
            }
            else if (current is not null && value.Length > 0 &&
                     (field.Equals("Allow", StringComparison.OrdinalIgnoreCase) ||
                      field.Equals("Disallow", StringComparison.OrdinalIgnoreCase)))
            {
                current.Rules.Add(new RobotsRule(value,
                    field.Equals("Allow", StringComparison.OrdinalIgnoreCase)));
            }
        }

        Assert.NotEmpty(groups);
        return groups;
    }

    // Named groups override the wildcard group; their matching rules are combined.
    // The longest URL rule wins, with Allow winning an equal-length tie.
    private static bool IsAllowed(IReadOnlyList<RobotsGroup> groups, string agent, string path)
    {
        var candidates = groups.Select(group => new
        {
            Group = group,
            Specificity = group.Agents.Where(token => token != "*" &&
                    agent.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => token.Length).DefaultIfEmpty(-1).Max()
        }).ToList();
        var specificity = candidates.Max(candidate => candidate.Specificity);
        var selected = specificity >= 0
            ? candidates.Where(candidate => candidate.Specificity == specificity).Select(candidate => candidate.Group)
            : groups.Where(group => group.Agents.Contains("*"));
        var matching = selected.SelectMany(group => group.Rules)
            .Where(rule => Matches(rule.Pattern, path))
            .OrderByDescending(rule => rule.Pattern.Length)
            .ThenByDescending(rule => rule.Allow)
            .FirstOrDefault();
        return matching?.Allow ?? true;
    }

    private static bool Matches(string pattern, string path)
    {
        var endsAtUrlEnd = pattern.EndsWith('$');
        var expression = "^" + Regex.Escape(endsAtUrlEnd ? pattern[..^1] : pattern)
            .Replace("\\*", ".*") + (endsAtUrlEnd ? "$" : "");
        return Regex.IsMatch(path, expression, RegexOptions.CultureInvariant);
    }

    private sealed class RobotsGroup
    {
        public List<string> Agents { get; } = new();
        public List<RobotsRule> Rules { get; } = new();
    }

    private sealed record RobotsRule(string Pattern, bool Allow);

    private sealed class RobotsHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Http { get; }

        private RobotsHost(WebApplication app)
        {
            _app = app;
            Http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }

        public static async Task<RobotsHost> Start(Action<IdeioSeoOptions>? configure = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase(databaseName));
            builder.Services.AddIdeioAiSeo(options => configure?.Invoke(options));
            var app = builder.Build();
            app.MapIdeioAiSeo<DatabaseContext>();
            await app.StartAsync();
            return new RobotsHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await _app.DisposeAsync();
        }
    }
}

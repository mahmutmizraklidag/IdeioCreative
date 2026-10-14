using IdeioCreative.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IdeioCreative.Seo;

public static class SeoPageModificationTracker
{
    public const string SharedPagePath = "*";

    private static readonly HashSet<string> PrivateSettings = new(StringComparer.Ordinal)
    {
        nameof(SiteSetting.SmtpServer), nameof(SiteSetting.SmtpPort),
        nameof(SiteSetting.SmtpEmail), nameof(SiteSetting.EmailPassword)
    };

    public static IReadOnlyCollection<string> ChangedPagePaths(
        ChangeTracker tracker, IdeioSeoOptions options, DateTime localNow)
    {
        tracker.DetectChanges();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in tracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var changedProperties = entry.Properties
                .Where(property => !Equals(property.OriginalValue, property.CurrentValue))
                .Select(property => property.Metadata.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (entry.State == EntityState.Modified && changedProperties.Count == 0)
                continue;

            switch (entry.Entity)
            {
                case SiteSetting site when IsPreferred(entry, site.Language, options):
                    if (entry.State != EntityState.Modified || changedProperties.Except(PrivateSettings).Any())
                        paths.Add(SharedPagePath);
                    break;

                case Service service when IsPreferred(entry, service.Language, options):
                    if (service.PageType == ServicePageType.Service)
                    {
                        paths.Add("/");
                        paths.Add(options.ServicesPath);
                        paths.UnionWith(options.AdditionalStaticPaths);
                    }
                    else
                        paths.Add(options.SectorsPath);
                    paths.Add(new Uri(options.ServiceUrl(service)).AbsolutePath);

                    if (entry.State == EntityState.Modified && changedProperties.Contains(nameof(Service.PageType)))
                    {
                        var previousType = entry.OriginalValues.GetValue<ServicePageType>(nameof(Service.PageType));
                        var previousParent = previousType == ServicePageType.Sector ? options.SectorsPath : options.ServicesPath;
                        paths.Add(previousParent);
                        paths.Add(new Uri(options.Absolute(previousParent.TrimEnd('/') + "/"
                            + Uri.EscapeDataString(entry.OriginalValues.GetValue<string>(nameof(Service.Slug))))).AbsolutePath);
                        if (previousType == ServicePageType.Service)
                        {
                            paths.Add("/");
                            paths.UnionWith(options.AdditionalStaticPaths);
                        }
                    }

                    // Service names and links also appear in the shared footer.
                    if (entry.State != EntityState.Modified || changedProperties.Overlaps(new[]
                        { nameof(Service.Title), nameof(Service.Slug), nameof(Service.Language), nameof(Service.PageType) }))
                        paths.Add(SharedPagePath);
                    break;

                case ServiceFaq faq when VisibleFaqChanged(entry, faq):
                    var parent = faq.Service ?? tracker.Entries<Service>()
                        .FirstOrDefault(item => item.Entity.Id == faq.ServiceId)?.Entity
                        ?? tracker.Context.Set<Service>().AsNoTracking()
                            .FirstOrDefault(item => item.Id == faq.ServiceId);
                    if (parent is not null && options.IsPreferredLanguage(parent.Language))
                        paths.Add(new Uri(options.ServiceUrl(parent)).AbsolutePath);
                    break;

                case About about when IsPreferred(entry, about.Language, options):
                    paths.Add("/");
                    paths.Add(options.AboutPath);
                    paths.UnionWith(options.AdditionalStaticPaths);
                    break;

                case Team team:
                    paths.Add(options.TeamPath);
                    if (team.IsHomePage || (entry.State != EntityState.Added &&
                        entry.OriginalValues.GetValue<bool>(nameof(Team.IsHomePage))))
                        paths.Add("/");
                    if (options.IncludeTeamDetailPages)
                        paths.Add(new Uri(options.TeamUrl(team)).AbsolutePath);
                    break;

                case Reference:
                    paths.Add("/");
                    paths.Add(options.AboutPath);
                    paths.UnionWith(options.AdditionalStaticPaths);
                    break;

                case Blog blog when blog.CreatedAt <= localNow ||
                    (entry.State != EntityState.Added &&
                     entry.OriginalValues.GetValue<DateTime>(nameof(Blog.CreatedAt)) <= localNow):
                    paths.Add("/");
                    paths.Add(options.BlogPath);
                    paths.Add(new Uri(options.BlogUrl(blog)).AbsolutePath);
                    break;
            }
        }

        return paths.Select(path => path == SharedPagePath
                ? path
                : new Uri(options.Absolute(path)).AbsolutePath.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPreferred(EntityEntry entry, Language currentLanguage, IdeioSeoOptions options) =>
        options.IsPreferredLanguage(currentLanguage) ||
        (entry.State != EntityState.Added &&
         options.IsPreferredLanguage(entry.OriginalValues.GetValue<Language>("Language")));

    private static bool VisibleFaqChanged(EntityEntry entry, ServiceFaq faq)
    {
        var currentVisible = faq.IsPublished && !string.IsNullOrWhiteSpace(faq.Question)
            && !string.IsNullOrWhiteSpace(faq.Answer);
        if (entry.State == EntityState.Added)
            return currentVisible;

        var wasVisible = entry.OriginalValues.GetValue<bool>(nameof(ServiceFaq.IsPublished))
            && !string.IsNullOrWhiteSpace(entry.OriginalValues.GetValue<string>(nameof(ServiceFaq.Question)))
            && !string.IsNullOrWhiteSpace(entry.OriginalValues.GetValue<string>(nameof(ServiceFaq.Answer)));
        if (entry.State == EntityState.Deleted)
            return wasVisible;
        if (currentVisible != wasVisible)
            return true;
        if (!currentVisible)
            return false;

        return faq.Question.Trim() != entry.OriginalValues.GetValue<string>(nameof(ServiceFaq.Question)).Trim()
            || faq.Answer.Trim() != entry.OriginalValues.GetValue<string>(nameof(ServiceFaq.Answer)).Trim()
            || faq.SortOrder != entry.OriginalValues.GetValue<int>(nameof(ServiceFaq.SortOrder));
    }
}

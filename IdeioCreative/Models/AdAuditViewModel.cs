using IdeioCreative.Entities;

namespace IdeioCreative.Models;

public sealed class AdAuditViewModel
{
    public SiteSetting SiteSetting { get; init; } = new();
    public About About { get; init; } = new();
    public IReadOnlyList<Reference> References { get; init; } = Array.Empty<Reference>();
    public IReadOnlyList<Service> Services { get; init; } = Array.Empty<Service>();
}

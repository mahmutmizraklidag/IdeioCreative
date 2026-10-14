using IdeioCreative.Entities;

namespace IdeioCreative.Models;

public sealed class NotFoundPageViewModel
{
    public IReadOnlyList<Service> Services { get; init; } = Array.Empty<Service>();
    public string? Phone { get; init; }
    public string PhoneNumber => new((Phone ?? string.Empty)
        .Where(character => char.IsAsciiDigit(character) || character == '+').ToArray());
}

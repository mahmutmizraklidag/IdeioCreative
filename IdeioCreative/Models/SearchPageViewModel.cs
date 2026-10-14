namespace IdeioCreative.Models;

public sealed class SearchPageViewModel
{
    public const int MaxQueryLength = 120;
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<SearchResultViewModel> Results { get; init; } = Array.Empty<SearchResultViewModel>();
}

public sealed record SearchResultViewModel(
    string Title, string? Description, string Slug, string Controller, string Category);

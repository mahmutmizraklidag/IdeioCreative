namespace IdeioCreative.Models;

public sealed class FaqSectionViewModel
{
    public string PageUrl { get; init; } = string.Empty;
    public IReadOnlyList<FaqItemViewModel> Items { get; init; } = Array.Empty<FaqItemViewModel>();
}

public sealed class FaqItemViewModel
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}

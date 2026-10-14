namespace IdeioCreative.Entities;

public sealed class SeoPageModification
{
    public long Id { get; set; }
    public string PagePath { get; set; } = string.Empty;
    public DateTime LastModifiedUtc { get; set; }
}

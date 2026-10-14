namespace IdeioCreative.Entities;

public sealed class ServiceFaq
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

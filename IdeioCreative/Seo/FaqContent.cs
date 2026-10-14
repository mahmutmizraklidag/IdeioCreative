using IdeioCreative.Entities;
using IdeioCreative.Models;

namespace IdeioCreative.Seo;

public static class FaqContent
{
    public static FaqSectionViewModel ForPage(IEnumerable<ServiceFaq> faqs, string pageUrl)
    {
        ArgumentNullException.ThrowIfNull(faqs);

        return new FaqSectionViewModel
        {
            PageUrl = CanonicalPageUrl(pageUrl),
            Items = faqs
                .Where(faq => faq.IsPublished && !string.IsNullOrWhiteSpace(faq.Question) && !string.IsNullOrWhiteSpace(faq.Answer))
                .OrderBy(faq => faq.SortOrder)
                .ThenBy(faq => faq.Id)
                .Select(faq => new FaqItemViewModel
                {
                    Id = faq.Id,
                    Question = faq.Question.Trim(),
                    Answer = faq.Answer.Trim()
                })
                .ToArray()
        };
    }

    internal static string CanonicalPageUrl(string? pageUrl)
    {
        if (!Uri.TryCreate(pageUrl?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
            return string.Empty;

        return uri.GetLeftPart(UriPartial.Path);
    }
}

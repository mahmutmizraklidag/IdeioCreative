using System;
using System.Collections.Generic;
using IdeioCreative.Entities;

namespace IdeioCreative.Seo;

public sealed class IdeioSeoOptions
{
    public string BaseUrl { get; set; } = "https://www.ideiocreative.com";
    public string BrandName { get; set; } = "İdeio Creative";
    public string ImageBasePath { get; set; } = "/img/";

    public string AboutPath { get; set; } = "/hakkimizda";
    public string ServicesPath { get; set; } = "/hizmetlerimiz";
    public string TeamPath { get; set; } = "/ekibimiz";
    public string BlogPath { get; set; } = "/blog";
    public string ContactPath { get; set; } = "/iletisim";

    public string PreferredLanguageName { get; set; } = "Turkish";
    public int MaxBlogCountInLlms { get; set; } = 20;
    public bool AllowGptBotForTraining { get; set; }
    public bool IncludeTeamDetailPages { get; set; }

    public List<string> AdditionalStaticPaths { get; set; } = new();

    /// <summary>
    /// Dil enum değerini URL ön ekine dönüştürür. Belirtilmezse English/EN için /en,
    /// diğer diller için boş değer kullanılır.
    /// </summary>
    public Func<Language, string>? LanguagePrefixResolver { get; set; }

    /// <summary>
    /// Projedeki route farklıysa hizmet URL yolunu burada özelleştirebilirsiniz.
    /// Dönen değer relatif veya mutlak URL olabilir.
    /// </summary>
    public Func<Service, string>? ServicePathResolver { get; set; }

    public Func<Team, string>? TeamPathResolver { get; set; }
    public Func<Blog, string>? BlogPathResolver { get; set; }

    public string Absolute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BaseUrl.TrimEnd('/');

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        return $"{BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    public string ImageUrl(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return string.Empty;

        if (Uri.TryCreate(image, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var basePath = ImageBasePath.Trim('/');
        var file = image.TrimStart('~', '/');

        if (!string.IsNullOrWhiteSpace(basePath) &&
            !file.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            file = $"{basePath}/{file}";
        }

        return Absolute(file);
    }

    public string GetLanguagePrefix(Language language)
    {
        if (LanguagePrefixResolver is not null)
            return NormalizePrefix(LanguagePrefixResolver(language));

        var value = language.ToString();
        return value.Equals("English", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("EN", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("İngilizce", StringComparison.OrdinalIgnoreCase)
            ? "/en"
            : string.Empty;
    }

    public bool IsPreferredLanguage(Language language)
    {
        var value = language.ToString();

        if (value.Equals(PreferredLanguageName, StringComparison.OrdinalIgnoreCase))
            return true;

        return PreferredLanguageName.Equals("Turkish", StringComparison.OrdinalIgnoreCase) &&
               (value.Equals("TR", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Türkçe", StringComparison.OrdinalIgnoreCase));
    }

    public string ServiceUrl(Service service)
    {
        var custom = ServicePathResolver?.Invoke(service);
        if (!string.IsNullOrWhiteSpace(custom))
            return Absolute(custom);

        var prefix = GetLanguagePrefix(service.Language);
        return Absolute($"{prefix}{ServicesPath.TrimEnd('/')}/{Uri.EscapeDataString(service.Slug)}");
    }

    public string TeamUrl(Team member)
    {
        var prefix = GetLanguagePrefix(member.Language);

        if (!IncludeTeamDetailPages)
            return Absolute($"{prefix}{TeamPath}");

        var custom = TeamPathResolver?.Invoke(member);
        if (!string.IsNullOrWhiteSpace(custom))
            return Absolute(custom);

        return Absolute($"{prefix}{TeamPath.TrimEnd('/')}/{Uri.EscapeDataString(member.Slug)}");
    }

    public string BlogUrl(Blog blog)
    {
        var custom = BlogPathResolver?.Invoke(blog);
        if (!string.IsNullOrWhiteSpace(custom))
            return Absolute(custom);

        return Absolute($"{BlogPath.TrimEnd('/')}/{Uri.EscapeDataString(blog.Slug)}");
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix == "/")
            return string.Empty;

        return "/" + prefix.Trim('/');
    }
}

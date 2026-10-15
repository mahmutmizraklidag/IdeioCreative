using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace IdeioCreative.Tools;

// Hash aliases resolve to the original file, so CMS filenames and uploads stay usable.
public sealed class ContentHashedAssets(IWebHostEnvironment environment, IMemoryCache cache) : IFileProvider
{
    private readonly IFileProvider _files = environment.WebRootFileProvider;

    public string Url(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("//") || url.StartsWith('#') ||
            Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme != "file")
            return url;

        var suffixIndex = url.IndexOfAny(['?', '#']);
        var path = suffixIndex < 0 ? url : url[..suffixIndex];
        var suffix = suffixIndex < 0 ? "" : url[suffixIndex..];
        if (path.StartsWith("~/")) path = path[1..];
        if (!path.StartsWith('/')) path = "/" + path;
        if (TryOriginalPath(path, out _, out _)) return url;

        var hash = Hash(Uri.UnescapeDataString(path));
        if (hash is null) return url;
        var slash = path.LastIndexOf('/');
        return path[..(slash + 1)] + hash + "." + path[(slash + 1)..] + suffix;
    }

    public string SourceSet(string value)
    {
        if (value.Contains("data:", StringComparison.OrdinalIgnoreCase)) return value;
        return string.Join(",", value.Split(',').Select(candidate =>
        {
            var trimmed = candidate.TrimStart();
            var space = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
            var source = space < 0 ? trimmed : trimmed[..space];
            return candidate[..(candidate.Length - trimmed.Length)] + Url(source) + (space < 0 ? "" : trimmed[space..]);
        }));
    }

    public string PrepareContent(string? html, string? description = null) =>
        ImageHtml.PrepareContent(html, description, Url, SourceSet);

    public IFileInfo GetFileInfo(string subpath)
    {
        if (!TryOriginalPath(subpath, out var original, out var expected))
            return _files.GetFileInfo(subpath);
        return string.Equals(Hash(original), expected, StringComparison.Ordinal)
            ? _files.GetFileInfo(original)
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => _files.GetDirectoryContents(subpath);
    public IChangeToken Watch(string filter) => _files.Watch(filter);

    private string? Hash(string path)
    {
        var file = _files.GetFileInfo(path);
        if (!file.Exists || file.IsDirectory) return null;
        return cache.GetOrCreate((typeof(ContentHashedAssets), path, file.Length, file.LastModified), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            entry.AddExpirationToken(_files.Watch(path));
            using var stream = file.CreateReadStream();
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        });
    }

    public static bool TryOriginalPath(string path, out string original, out string hash)
    {
        original = path;
        hash = "";
        var start = path.LastIndexOf('/') + 1;
        if (path.Length <= start + 65 || path[start + 64] != '.') return false;
        var candidate = path.AsSpan(start, 64);
        foreach (var character in candidate)
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        hash = candidate.ToString();
        original = path[..start] + path[(start + 65)..];
        return true;
    }
}

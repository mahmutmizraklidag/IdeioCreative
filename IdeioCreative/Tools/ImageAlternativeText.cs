namespace IdeioCreative.Tools;

internal static class ImageAlternativeText
{
    public static string Resolve(string? description, string? source)
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description.Trim();

        if (!string.IsNullOrWhiteSpace(source))
        {
            var isAbsolute = Uri.TryCreate(source, UriKind.Absolute, out var uri);
            if (isAbsolute && uri!.Scheme is not "http" and not "https" and not "file")
                return "İdeio Creative görseli";
            var path = isAbsolute
                ? uri!.AbsolutePath
                : source.Split('?', '#')[0];
            var filename = path[(path.LastIndexOf('/') + 1)..];
            var extension = filename.LastIndexOf('.');
            if (extension > 0)
                filename = filename[..extension];
            var name = Uri.UnescapeDataString(filename).Replace('-', ' ').Replace('_', ' ').Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return "İdeio Creative görseli";
    }
}

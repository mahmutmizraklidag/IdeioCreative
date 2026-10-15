using System.Buffers.Binary;
using IdeioCreative.Entities;
using IdeioCreative.Models;
using IdeioCreative.Tools;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;

namespace IdeioCreative.Seo;

public sealed record SocialMetadata(string Title, string Description, string Url, string Type,
    string ImageUrl, string ImageType, string ImageAlt, string SiteName);

public sealed class SocialMetadataBuilder(IOptions<IdeioSeoOptions> options, SeoTextFormatter text, ContentHashedAssets assets)
{
    public const string DefaultImagePath = "/assets/img/social/ideio-creative.png";

    public SocialMetadata Create(ViewDataDictionary view, SiteSetting site, string requestPath)
    {
        var seo = options.Value;
        var title = text.OneLine(view["Title"]?.ToString(), 160);
        if (string.IsNullOrWhiteSpace(title)) title = seo.BrandName;
        if (!title.Contains(seo.BrandName, StringComparison.OrdinalIgnoreCase)) title += " | " + seo.BrandName;

        var description = text.OneLine(view["MetaDescription"]?.ToString(), 200);
        if (string.IsNullOrWhiteSpace(description)) description = text.OneLine(site.MetaDescription, 200);
        if (string.IsNullOrWhiteSpace(description)) description = "İdeio Creative hizmetleri, projeleri ve iletişim bilgileri.";

        var path = view["CanonicalPath"] as string;
        if (string.IsNullOrWhiteSpace(path)) path = string.IsNullOrWhiteSpace(requestPath) ? "/" : requestPath;
        var canonical = new UriBuilder(seo.Absolute(path)) { Query = "", Fragment = "" }.Uri.AbsoluteUri;

        var image = view["SocialImage"] as string ?? (view.Model switch
        {
            Service service => service.Image,
            Blog blog => blog.Image,
            AboutViewModel about => about.About?.Image,
            _ => null
        });
        var imagePath = LocalImagePath(image, seo.ImageBasePath);
        var imageType = ImageTypeAtRequiredSize(imagePath);
        if (imageType is null)
        {
            imagePath = DefaultImagePath;
            imageType = "image/png";
        }

        return new SocialMetadata(title, description, canonical, view.Model is Blog ? "article" : "website",
            new Uri(seo.Absolute(assets.Url(imagePath!))).AbsoluteUri, imageType,
            imagePath == DefaultImagePath ? seo.BrandName + " marka görseli" : title + " paylaşım görseli", seo.BrandName);
    }

    private static string? LocalImagePath(string? image, string imageBasePath)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;
        image = image.Trim();
        if (image.StartsWith("//") || image.Contains("://") || image.Contains('?') || image.Contains('#')) return null;
        if (Path.GetExtension(image).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp")) return null;
        if (image.StartsWith("~/")) return image[1..];
        if (image.StartsWith('/')) return image;
        return "/" + imageBasePath.Trim('/') + "/" + Uri.EscapeDataString(image);
    }

    private string? ImageTypeAtRequiredSize(string? path)
    {
        if (path is null) return null;
        var file = assets.GetFileInfo(Uri.UnescapeDataString(path));
        if (!file.Exists || file.IsDirectory) return null;
        using var stream = file.CreateReadStream();
        Span<byte> header = stackalloc byte[30];
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) != header.Length) return null;
        if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 1200 &&
                BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 630 ? "image/png" : null;

        if (header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            var width = 0;
            var height = 0;
            if (header[12..16].SequenceEqual("VP8X"u8))
            {
                width = 1 + header[24] + (header[25] << 8) + (header[26] << 16);
                height = 1 + header[27] + (header[28] << 8) + (header[29] << 16);
            }
            else if (header[12..16].SequenceEqual("VP8 "u8) && header[23..26].SequenceEqual(new byte[] { 157, 1, 42 }))
            {
                width = BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]) & 0x3fff;
                height = BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]) & 0x3fff;
            }
            else if (header[12..16].SequenceEqual("VP8L"u8) && header[20] == 47)
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(header[21..25]);
                width = 1 + (int)(size & 0x3fff);
                height = 1 + (int)((size >> 14) & 0x3fff);
            }
            return width == 1200 && height == 630 ? "image/webp" : null;
        }

        if (header[0] != 255 || header[1] != 216 || !stream.CanSeek) return null;
        stream.Position = 2;
        int Word() { var high = stream.ReadByte(); var low = stream.ReadByte(); return high < 0 || low < 0 ? -1 : high * 256 + low; }
        while (stream.Position < Math.Min(stream.Length, 1_048_576))
        {
            if (stream.ReadByte() != 255) continue;
            int marker;
            do { marker = stream.ReadByte(); } while (marker == 255);
            if (marker < 0 || marker is 217 or 218) break;
            if (marker == 1 || marker is >= 208 and <= 216) continue;
            var length = Word();
            if (length < 2) break;
            if (marker is >= 192 and <= 207 && marker is not (196 or 200 or 204))
            {
                stream.ReadByte();
                var height = Word();
                var width = Word();
                return width == 1200 && height == 630 ? "image/jpeg" : null;
            }
            stream.Seek(length - 2, SeekOrigin.Current);
        }
        return null;
    }
}

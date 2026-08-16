using System;
using System.IO;

namespace XNote.Utils;


public static class VideoLink
{
    public const string VideoScheme = "xnote-video";
    public const string LinkScheme = "xnote-link";
    public const string VideoPrefix = VideoScheme + "://";
    public const string LinkPrefix = LinkScheme + "://";

    
    public static string EncodeVideoCard(string path) => VideoPrefix + Uri.EscapeDataString(path);

    
    public static string EncodeLinkCard(string url) => LinkPrefix + Uri.EscapeDataString(url);

    public static bool IsVideoCard(string? href) =>
        !string.IsNullOrEmpty(href) &&
        (href.StartsWith(VideoPrefix, StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith(LinkPrefix, StringComparison.OrdinalIgnoreCase));

    public static bool TryGetVideoPath(string? href, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrEmpty(href) ||
            !href.StartsWith(VideoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var decoded = Uri.UnescapeDataString(href[VideoPrefix.Length..]);
        if (string.IsNullOrEmpty(decoded)) return false;
        path = decoded;
        return true;
    }

    public static bool TryGetLinkUrl(string? href, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrEmpty(href) ||
            !href.StartsWith(LinkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var decoded = Uri.UnescapeDataString(href[LinkPrefix.Length..]);
        if (string.IsNullOrEmpty(decoded)) return false;
        url = decoded;
        return true;
    }

    public static bool IsYoutubeUrl(string? url) => TryGetYouTubeId(url) != null;

    
    public static string? TryGetYouTubeId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be")
        {
            var id = uri.AbsolutePath.Trim('/');
            return IsValidId(id) ? id : null;
        }

        var isYt = host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal);
        if (!isYt) return null;

        var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length > 0)
        {
            if (segs[0] == "watch")
            {
                var v = GetQueryParam(uri.Query, "v");
                return v != null && IsValidId(v) ? v : null;
            }

            if (segs[0] is "embed" or "shorts" or "live" or "v" && segs.Length >= 2 && IsValidId(segs[1]))
            {
                return segs[1];
            }
        }

        var qv = GetQueryParam(uri.Query, "v");
        return qv != null && IsValidId(qv) ? qv : null;
    }

    
    public static bool IsDirectMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        var ext = Path.GetExtension(uri.AbsolutePath);
        foreach (var e in VideoStore.SupportedVideoExtensions)
        {
            if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsValidId(string? id) => id is { Length: 11 } && AllIdChars(id);

    private static bool AllIdChars(string id)
    {
        foreach (var c in id)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return true;
    }

    private static string? GetQueryParam(string? query, string name)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var eq = part.IndexOf('=');
            var key = eq < 0 ? part : part[..eq];
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
            var value = eq < 0 ? string.Empty : part[(eq + 1)..];
            return System.Net.WebUtility.UrlDecode(value);
        }
        return null;
    }
}
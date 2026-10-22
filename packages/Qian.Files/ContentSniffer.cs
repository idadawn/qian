using System.Text;

namespace Qian.Files;

// 服务端 MIME 判定：不信任客户端声明。二进制按魔数识别；能解码为文本时先找活跃 Web 标记（HTML / SVG / XML / XHTML），
// 声明为活跃类型或脚本的文本保持该声明（只会更严格），其余文本沿用声明的文本类型或退为 text/plain；不认识的二进制为 application/octet-stream。
internal static class ContentSniffer
{
    internal const int HeadLength = 4096;
    internal const string OctetStream = "application/octet-stream";
    private static readonly string[] HtmlTags =
        ["<!doctype html", "<html", "<head", "<body", "<script", "<iframe", "<frameset", "<frame", "<object", "<embed", "<style", "<title",
         "<meta", "<link", "<base", "<form", "<img", "<a ", "<a>", "<div", "<table", "<font", "<br", "<p ", "<p>", "<h1", "<b ", "<b>", "<!--"];
    private static readonly HashSet<string> ActiveTypes = new(StringComparer.Ordinal)
        { "text/html", "image/svg+xml", "application/xhtml+xml", "application/xml", "text/xml" };
    private static readonly HashSet<string> ScriptTypes = new(StringComparer.Ordinal)
        { "text/javascript", "application/javascript", "application/x-javascript", "text/ecmascript", "application/ecmascript" };
    private static readonly HashSet<string> TextApplicationTypes = new(StringComparer.Ordinal)
        { "application/json", "application/x-ndjson", "application/yaml", "application/x-yaml", "application/sql" };
    private static readonly HashSet<string> ZipBased = new(StringComparer.Ordinal)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.spreadsheet", "application/vnd.oasis.opendocument.presentation", "application/epub+zip", "application/java-archive"
    };
    private static readonly HashSet<string> Ole2Based = new(StringComparer.Ordinal)
        { "application/msword", "application/vnd.ms-excel", "application/vnd.ms-powerpoint", "application/vnd.ms-outlook" };
    private static readonly HashSet<string> IsoMediaBased = new(StringComparer.Ordinal)
        { "video/mp4", "video/quicktime", "audio/mp4", "audio/x-m4a", "image/heic", "image/heif", "image/avif" };
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.Ordinal)
    {
        ["image/png"] = ".png", ["image/jpeg"] = ".jpg", ["image/gif"] = ".gif", ["image/webp"] = ".webp", ["image/svg+xml"] = ".svg",
        ["application/pdf"] = ".pdf", ["application/zip"] = ".zip", ["application/gzip"] = ".gz", ["application/x-7z-compressed"] = ".7z",
        ["text/plain"] = ".txt", ["text/csv"] = ".csv", ["text/markdown"] = ".md", ["text/html"] = ".html", ["application/xhtml+xml"] = ".xhtml",
        ["application/xml"] = ".xml", ["text/xml"] = ".xml", ["application/json"] = ".json", ["text/javascript"] = ".js",
        ["video/mp4"] = ".mp4", ["audio/mpeg"] = ".mp3",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx"
    };

    // 规范化客户端声明：小写、去参数；不合法返回 null。
    internal static string? Normalize(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared) || declared.Length > 255) return null;
        var value = declared.Split(';', 2)[0].Trim().ToLowerInvariant();
        var parts = value.Split('/');
        return parts.Length == 2 && Token(parts[0]) && Token(parts[1]) ? value : null;
        static bool Token(string part) => part.Length is > 0 and <= 127 && char.IsAsciiLetterOrDigit(part[0]) &&
            part.All(c => char.IsAsciiLetterOrDigit(c) || "!#$&^_.+-".Contains(c, StringComparison.Ordinal));
    }

    internal static string Extension(string contentType) => Extensions.TryGetValue(contentType, out var extension) ? extension : "";

    internal static bool Allows(UploadPolicy policy, string contentType) => policy.AllowedTypes.Any(allowed =>
        allowed == contentType || (allowed.EndsWith("/*", StringComparison.Ordinal) && contentType.StartsWith(allowed[..^1], StringComparison.Ordinal)));

    internal static string Detect(ReadOnlySpan<byte> head, string declared)
    {
        if (head.Length == 0) return ActiveTypes.Contains(declared) || ScriptTypes.Contains(declared) || IsText(declared) ? declared : OctetStream;
        if (head.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8)) return "image/gif";
        if (head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (head.StartsWith("%PDF-"u8)) return "application/pdf";
        if (head.StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x03, 0x04]) || head.StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x05, 0x06])) return ZipBased.Contains(declared) ? declared : "application/zip";
        if (head.StartsWith((ReadOnlySpan<byte>)[0x1F, 0x8B])) return "application/gzip";
        if (head.StartsWith((ReadOnlySpan<byte>)[0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])) return "application/x-7z-compressed";
        if (head.StartsWith((ReadOnlySpan<byte>)[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])) return Ole2Based.Contains(declared) ? declared : OctetStream;
        if (head.Length >= 8 && head[4..8].SequenceEqual("ftyp"u8)) return IsoMediaBased.Contains(declared) ? declared : "video/mp4";
        if (head.StartsWith("ID3"u8)) return "audio/mpeg";
        if (head.StartsWith("MZ"u8) || head.StartsWith((ReadOnlySpan<byte>)[0x7F, 0x45, 0x4C, 0x46]) || head.StartsWith((ReadOnlySpan<byte>)[0x00, 0x61, 0x73, 0x6D])) return OctetStream;
        var text = Decode(head);
        if (text is null) return OctetStream;
        if (Markup(text) is { } active) return active;
        if (ActiveTypes.Contains(declared) || ScriptTypes.Contains(declared)) return declared;
        return IsText(declared) ? declared : "text/plain";
    }

    private static bool IsText(string declared) =>
        (declared.StartsWith("text/", StringComparison.Ordinal) && !ActiveTypes.Contains(declared)) || TextApplicationTypes.Contains(declared);

    // 识别活跃 Web 标记：前缀按 WHATWG 嗅探的标签表，另外在整段头部里找 script / svg / html，宁可判严。
    private static string? Markup(string text)
    {
        var lower = text.TrimStart((char)0xFEFF, ' ', '\t', '\r', '\n', '\f').ToLowerInvariant();
        if (lower.StartsWith("<?xml", StringComparison.Ordinal))
        {
            if (lower.Contains("<svg", StringComparison.Ordinal)) return "image/svg+xml";
            if (lower.Contains("<html", StringComparison.Ordinal) || lower.Contains("<script", StringComparison.Ordinal)) return "application/xhtml+xml";
            return "application/xml";
        }
        if (lower.StartsWith("<svg", StringComparison.Ordinal)) return "image/svg+xml";
        if (HtmlTags.Any(tag => lower.StartsWith(tag, StringComparison.Ordinal))) return "text/html";
        if (lower.Contains("<script", StringComparison.Ordinal) || lower.Contains("<html", StringComparison.Ordinal) ||
            lower.Contains("<iframe", StringComparison.Ordinal) || lower.Contains("javascript:", StringComparison.Ordinal)) return "text/html";
        if (lower.Contains("<svg", StringComparison.Ordinal)) return "image/svg+xml";
        return null;
    }

    // 只接受 UTF-8（可带 BOM、末尾允许截断的多字节序列）与带 BOM 的 UTF-16；含 NUL 的 UTF-8 视为二进制。
    private static string? Decode(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) return Encoding.Unicode.GetString(head[2..(head.Length - (head.Length % 2))]);
        if (head.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) return Encoding.BigEndianUnicode.GetString(head[2..(head.Length - (head.Length % 2))]);
        if (head.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) head = head[3..];
        if (head.Contains((byte)0)) return null;
        var strict = new UTF8Encoding(false, true);
        for (var trim = 0; trim <= 3 && trim <= head.Length; trim++)
        {
            try { return strict.GetString(head[..(head.Length - trim)]); }
            catch (DecoderFallbackException) { }
        }
        return null;
    }
}

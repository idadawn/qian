using System.Text.RegularExpressions;

namespace Qian.Config;

/// <summary>公开的规范引用；Kubernetes 定位只在受信任存储绑定中使用。</summary>
public sealed partial class SecretReference
{
    private SecretReference(string scope, string path) { Scope = scope; Path = path; }
    /// <summary>platform、tenant 或 dev。</summary>
    public string Scope { get; }
    /// <summary>规范化的引用路径，不是文件系统路径。</summary>
    public string Path { get; }
    /// <summary>解析引用；拒绝内嵌凭据、查询、fragment、空段和穿越。</summary>
    public static SecretReference Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.StartsWith("secretRef://", StringComparison.Ordinal) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Host is not ("platform" or "tenant" or "dev") || uri.UserInfo.Length != 0 || !uri.IsDefaultPort ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new FormatException("密钥引用格式无效。");
        // 从原始文本取路径，避免 Uri 先消去 dot-segment 后掩盖穿越输入。
        var separator = value.IndexOf('/', "secretRef://".Length);
        if (separator < 0) throw new FormatException("密钥引用缺少路径。");
        var segments = value[(separator + 1)..].Split('/').Select(Uri.UnescapeDataString).ToArray();
        if (segments.Any(segment => segment is "" or "." or ".." || !Segment().IsMatch(segment)))
            throw new FormatException("密钥引用路径无效。");
        return new SecretReference(uri.Host, string.Join('/', segments));
    }
    /// <summary>格式检查，不输出无效输入。</summary>
    public static bool IsValid(string? value)
    {
        if (value is null) return false;
        try { Parse(value); return true; }
        catch (FormatException) { return false; }
    }
    /// <inheritdoc />
    public override string ToString() => "secretRef://" + Scope + "/" + Path;
    [GeneratedRegex(@"\A[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Segment();
}

/// <summary>与 policy.permissions.secrets 条目对应的权限，不因声明本身而自动获批。</summary>
public sealed record SecretGrant(string Name, string Scope);

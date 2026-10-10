using Qian.Surfaces.Abstractions;
using System.Text.RegularExpressions;

namespace Qian.Surfaces;

public sealed partial class SurfaceValidator
{
    /// <inheritdoc />
    public MarketplaceValidationResult ValidateMarketplace(string file, string content, string? traceId = null)
    {
        var trace = Trace(traceId);
        var parsed = SurfaceReader.Parse(file, content, false, trace);
        var diagnostics = parsed.Diagnostics.ToList();
        if (parsed.Syntax is null || diagnostics.Any(item => item.Severity == "error")) return new(null, diagnostics);
        var data = parsed.Syntax.Data;
        diagnostics.AddRange(_schemas.Validate("marketplace", file, data, parsed.Syntax.Lines, trace));
        if (diagnostics.Any(item => item.Severity == "error")) return new(null, diagnostics);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var categories = data.TryGetProperty("categories", out var categoryList) ? categoryList.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal) : null;
        var index = 0;
        foreach (var entry in data.GetProperty("plugins").EnumerateArray())
        {
            var pointer = "/plugins/" + index++;
            if (!names.Add(entry.GetProperty("name").GetString()!)) Error(pointer + "/name", SurfaceErrors.Name, "市场内安装名必须唯一。");
            if (categories is not null && !categories.Contains(entry.GetProperty("category").GetString()!)) Error(pointer + "/category", SurfaceErrors.Reference, "分类未在市场 categories 中声明。");
            var source = entry.GetProperty("source");
            if (source.TryGetProperty("path", out var relative) && !SafeRelative(relative.GetString()!)) Error(pointer + "/source/path", SurfaceErrors.Path, "来源路径必须留在仓库内。");
            foreach (var field in new[] { "url", "repo" })
            {
                if (!source.TryGetProperty(field, out var location)) continue;
                if (!Uri.TryCreate(location.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) || uri.Query.TrimStart('?').Split('&').Select(pair => Uri.UnescapeDataString(pair.Split('=')[0])).Any(key => Regex.IsMatch(key, "token|secret|password|credential|signature|api[-_]?key|authorization", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))))
                    Error(pointer + "/source/" + field, SurfaceErrors.Policy, "远程来源必须使用无凭据片段的 HTTPS 地址。");
            }
        }
        if (data.GetProperty("owner").TryGetProperty("publicKeys", out var keys))
        {
            index = 0;
            foreach (var key in keys.EnumerateArray())
            {
                var value = key.GetString()!; var pointer = "/owner/publicKeys/" + index++;
                if (value.Contains("PRIVATE KEY", StringComparison.Ordinal)) Error(pointer, SurfaceErrors.Secret, "市场只能声明公钥，不能携带私钥。");
                else if (!value.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) && !SafeRelative(value)) Error(pointer, SurfaceErrors.Path, "公钥路径必须留在市场仓库内。");
            }
        }
        return new(diagnostics.Any(item => item.Severity == "error") ? null : data, diagnostics.OrderBy(item => item.Error.Pointer, StringComparer.Ordinal).ToArray());
        void Error(string pointer, Qian.Runtime.Errors.ErrorCode code, string message)
            => diagnostics.Add(Diagnostics.Create(file, pointer, Diagnostics.Line(parsed.Syntax.Lines, pointer), code, message, trace));
    }

    private static bool SafeRelative(string value) => value.Length > 0 && !value.StartsWith('/') && !value.Contains('\\') &&
        !value.Contains(':') && !value.Any(char.IsControl) && !value.Split('/').Contains("..", StringComparer.Ordinal);
}

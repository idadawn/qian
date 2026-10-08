using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

internal static class Diagnostics
{
    private static readonly ErrorCatalog Catalog = CreateCatalog();
    private static ErrorCatalog CreateCatalog()
    {
        var catalog = ErrorCatalog.CreateDefault();
        foreach (var code in SurfaceErrors.All) catalog.Register(code);
        return catalog;
    }
    internal static string Token(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    internal static SurfaceDiagnostic Create(string file, string pointer, int line, ErrorCode code, string reason, string traceId, string severity = "error")
    {
        var prefix = string.Join('/', file.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
        var error = new QianError(PlatformErrors.InputInvalid)
        {
            Errors = [new QianFieldError(code, prefix + "#" + pointer, new Dictionary<string, string> { ["reason"] = reason })]
        };
        return new SurfaceDiagnostic(file, Math.Max(1, line), severity, Catalog.CreateDetail(error, traceId));
    }
    internal static int Line(IReadOnlyDictionary<string, int> lines, string pointer)
    {
        while (!lines.ContainsKey(pointer) && pointer.Length > 0) pointer = pointer[..pointer.LastIndexOf('/')];
        return lines.GetValueOrDefault(pointer, 1);
    }
}

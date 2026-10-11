using Qian.Runtime.Errors;

namespace Qian.Config;

/// <summary>秘密解析的统一错误，不把输入值或物理路径放进错误文案。</summary>
public static class SecretErrors
{
    /// <summary>引用格式不合法。</summary>
    public static readonly ErrorCode InvalidReference = Code("SECRET_REFERENCE_INVALID", 400, "密钥引用无效", "Invalid secret reference");
    /// <summary>引用未获声明和审批授权，或租户不匹配。</summary>
    public static readonly ErrorCode AccessDenied = Code("SECRET_ACCESS_DENIED", 403, "密钥访问未获授权", "Secret access denied");
    /// <summary>允许访问的引用没有可用文件。</summary>
    public static readonly ErrorCode NotFound = Code("SECRET_NOT_FOUND", 404, "密钥引用无法解析", "Secret reference not found");
    /// <summary>秘密读取或审计设施暂不可用。</summary>
    public static readonly ErrorCode Unavailable = Code("SECRET_UNAVAILABLE", 503, "密钥服务暂不可用", "Secret service unavailable");
    /// <summary>配置中出现非引用敏感数据。</summary>
    public static readonly ErrorCode Exposed = Code("CONFIG_SECRET_EXPOSED", 400, "配置含非引用敏感数据", "Configuration contains sensitive data");
    /// <summary>敏感字段规则无法安全解释。</summary>
    public static readonly ErrorCode SchemaInvalid = Code("CONFIG_SECRET_SCHEMA_INVALID", 400, "敏感配置规则无效", "Invalid sensitive configuration schema");
    /// <summary>向宿主目录登记本组件错误。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var handles = new List<IDisposable>();
        try
        {
            foreach (var code in new[] { InvalidReference, AccessDenied, NotFound, Unavailable, Exposed, SchemaInvalid }) handles.Add(catalog.Register(code));
            return new Registration(handles);
        }
        catch { foreach (var handle in handles.AsEnumerable().Reverse()) handle.Dispose(); throw; }
    }
    private static ErrorCode Code(string code, int status, string chinese, string english) => ErrorCode.Protocol(code, status, "qian.config",
        new Dictionary<string, string> { ["zh-CN"] = chinese, ["en"] = english },
        new Dictionary<string, string> { ["zh-CN"] = chinese + "。", ["en"] = english + "." });
    private sealed class Registration(List<IDisposable> handles) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var handle in handles.AsEnumerable().Reverse()) handle.Dispose();
        }
    }
}

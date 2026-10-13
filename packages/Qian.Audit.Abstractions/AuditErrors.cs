using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>审计组件的稳定错误码。</summary>
public static class AuditErrors
{
    /// <summary>输入审计 Schema 无法安全解析；不回显 Schema 或输入原值。</summary>
    public static readonly ErrorCode SchemaInvalid = ErrorCode.Protocol("AUDIT_SCHEMA_INVALID", 400, "qian.audit",
        new Dictionary<string, string> { ["zh-CN"] = "审计 Schema 无效", ["en"] = "Invalid audit schema" },
        new Dictionary<string, string> { ["zh-CN"] = "审计输入 Schema 无法安全解析，请检查引用、敏感标记和支持的约束。", ["en"] = "The audit input schema cannot be resolved safely. Check references, sensitive annotations, and supported constraints." });
    /// <summary>登记本组件错误码；返回可交给 Effect 的撤销句柄。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog); return catalog.Register(SchemaInvalid);
    }
}

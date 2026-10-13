using Qian.Runtime.Errors;

namespace Qian.Authz.Abstractions;

/// <summary>授权领域的错误词汇；协议拒绝复用冻结内置码，其余定义归属 qian.authz。</summary>
public static class AuthzErrors
{
    /// <summary>复用既有 AUTHZ_DENIED，避免为同一个全局错误码登记不同定义。</summary>
    public static readonly ErrorCode Denied = PlatformErrors.AuthzDenied;
    /// <summary>授权与互斥约束冲突。</summary>
    public static readonly ErrorCode ConstraintMutex = Business("CONSTRAINT_MUTEX", 409,
        "授权互斥冲突", "授权与互斥约束 {constraint} 冲突。",
        "Mutually exclusive grant", "The grant conflicts with mutual-exclusion constraint {constraint}.");
    /// <summary>用户数量达到规则上限，拒绝继续授予。</summary>
    public static readonly ErrorCode CardinalityUserLimit = Business("CARDINALITY_USER_LIMIT", 422,
        "用户基数超限", "用户数量已达到规则上限 {limit}。",
        "User cardinality exceeded", "The user count has reached the policy limit {limit}.");
    /// <summary>批量业务部分完成；逐项结果由业务能力的输出契约描述。</summary>
    public static readonly ErrorCode PartialSuccess = Business("PARTIAL_SUCCESS", 409,
        "部分操作完成", "部分操作已完成；请检查逐项结果后再处理剩余项。",
        "Partially completed", "Some operations completed; inspect the item results before processing the remaining items.");
    /// <summary>权限声明字段无效，放在 INPUT_INVALID 的 errors 中。</summary>
    public static readonly ErrorCode MetadataInvalid = ErrorCode.Field("AUTHZ_METADATA_INVALID", "qian.authz",
        new Dictionary<string, string> { ["zh-CN"] = "权限元数据无效", ["en"] = "Invalid authorization metadata" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
    /// <summary>可登记和导出的完整错误词汇。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = Array.AsReadOnly(new[] { Denied, ConstraintMutex, CardinalityUserLimit, PartialSuccess, MetadataInvalid });

    /// <summary>成组登记本组件错误码；失败撤销已登记项，返回可交给 ctx.Effect 的幂等释放句柄。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var code in All) registrations.Add(catalog.Register(code));
            return new Registration(registrations);
        }
        catch
        {
            foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose();
            throw;
        }
    }

    private static ErrorCode Business(string code, int status, string titleZh, string messageZh, string titleEn, string messageEn) =>
        ErrorCode.Business(code, status, "qian.authz",
            new Dictionary<string, string> { ["zh-CN"] = titleZh, ["en"] = titleEn },
            new Dictionary<string, string> { ["zh-CN"] = messageZh, ["en"] = messageEn });

    private sealed class Registration(List<IDisposable> entries) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var entry in entries.AsEnumerable().Reverse()) entry.Dispose();
        }
    }
}

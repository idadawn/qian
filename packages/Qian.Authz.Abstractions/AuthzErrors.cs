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
    /// <summary>租户专属权限点或角色绑定不能落到租户之下的子作用域（应用、Agent 会话）；S1 的全部权限点都是租户专属。</summary>
    public static readonly ErrorCode ScopeMismatch = Business("AUTHZ_SCOPE_MISMATCH", 422,
        "绑定作用域不匹配", "租户专属权限点只能绑定到租户本身，不能绑定到子作用域 {scope}。",
        "Binding scope mismatch", "Tenant-exclusive permissions can only be bound to the tenant itself, not to the sub-scope {scope}.");
    /// <summary>租户最后一个持有成员管理权限的主体不可被移除或降级，否则租户自锁。</summary>
    public static readonly ErrorCode LastAdministratorLockout = Business("AUTHZ_LAST_ADMIN_LOCKOUT", 409,
        "不能移除最后一个管理员", "{role} 是租户最后一个持有成员管理权限的绑定，移除或降级会使租户无人可管理。",
        "Last administrator lockout", "{role} is the tenant's last binding that holds member-management permissions; removing or downgrading it would lock the tenant out.");
    /// <summary>权限声明字段无效，放在 INPUT_INVALID 的 errors 中。</summary>
    public static readonly ErrorCode MetadataInvalid = ErrorCode.Field("AUTHZ_METADATA_INVALID", "qian.authz",
        new Dictionary<string, string> { ["zh-CN"] = "权限元数据无效", ["en"] = "Invalid authorization metadata" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
    /// <summary>可登记和导出的完整错误词汇。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = Array.AsReadOnly(new[] { Denied, ConstraintMutex, CardinalityUserLimit, PartialSuccess, ScopeMismatch, LastAdministratorLockout, MetadataInvalid });

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

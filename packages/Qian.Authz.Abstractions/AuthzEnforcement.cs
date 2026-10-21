using Qian.Runtime;

namespace Qian.Authz.Abstractions;

/// <summary>
/// 强制执行点（PEP）共用的执行端口：HTTP 端点、gRPC 拦截器与命令管道都把「主体 + 权限点 + 入口」交给它，
/// 由它调用唯一判定点（<see cref="IAuthzService"/> 的 Check / Explain，平台主体经 <see cref="ISuperadminBypass"/>），
/// 校验令牌携带的 identity_revision，把每次判定写入审计（拒绝必记、放行可配采样），再发 authz/decided 供观察者消费。
/// 它不是第二套判定引擎：Allow 只可能来自 Explain 或显式旁路。
/// </summary>
public static class AuthzEnforcement
{
    /// <summary>执行端口的服务键，由 qian.authz 提供。</summary>
    public static readonly ServiceKey Key = new("AuthzEnforcement");
    /// <summary>决策审计记录的能力 ID；每条 decision_logs 都以它写入审计链。</summary>
    public const string DecisionCapabilityId = "qian.authz/decision";
    /// <summary>HTTP 端点入口。</summary>
    public const string HttpEntryPoint = "http";
    /// <summary>gRPC 拦截器入口。</summary>
    public const string GrpcEntryPoint = "grpc";
    /// <summary>Mediator 命令管道入口。</summary>
    public const string CommandEntryPoint = "command";
    /// <summary>审计组件未装配时的拒绝原因：入口与不存在同为 404（S1-AUD-001 口径），适配器据此映射状态码。</summary>
    public const string AuditUnavailable = "audit_unavailable";
    /// <summary>解析当前 scope 的执行端口；没有 Provider 时沿用运行时的缺失服务错误。</summary>
    public static IAuthzEnforcement Enforcement(this IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Inject<IAuthzEnforcement>(Key);
    }
}

/// <summary>
/// 端点 / gRPC 方法元数据：该入口没有单一权限点，授权在处理器内按主体可见性完成（能力目录按可见能力过滤、me/permissions 之类的平台表面）。
/// PEP 对带此标记的入口不再缺省拒绝，契约不写 x-qian-permission；标记必须给出理由，复核时逐个核对。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PermissionEnforcedInHandlerAttribute : Attribute
{
    /// <summary>说明处理器内如何完成授权。</summary>
    public PermissionEnforcedInHandlerAttribute(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason); Reason = reason;
    }
    /// <summary>处理器内授权方式的说明。</summary>
    public string Reason { get; }
}

/// <summary>一次强制执行：判定、记录并返回可解释结果；只抛宿主装配错误，拒绝以结果表达。</summary>
public interface IAuthzEnforcement
{
    /// <summary>
    /// 执行一次判定。主体带租户而当前租户上下文为空时抛出（宿主未建立可信租户边界），平台主体（TenantId 为 null）走显式旁路；
    /// 缺少权限点声明、令牌 identity_revision 陈旧、委托主体、审计不可用都返回拒绝，不会放行。
    /// </summary>
    ValueTask<AuthorizationDecision> EnforceAsync(EnforcementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 已认证主体的判定视图：从已校验令牌复制，不从请求参数推导。IdentityRevision 为令牌携带的身份修订（S2S 令牌可缺省）；
/// Delegated 表示带 act 委托链，S1 一律拒绝。
/// </summary>
public sealed record EnforcementSubject(string Id, string? TenantId, string Kind, long? IdentityRevision = null, bool Delegated = false)
{
    /// <inheritdoc />
    public override string ToString() => "EnforcementSubject(redacted)";
}

/// <summary>
/// 一次强制执行的输入。Permission 为端点 / 命令声明的权限点全名，未声明时为 null（缺省拒绝并记录）；
/// Channel 为审计通道（ui / api / mcp / a2a / job / event / hook）；EntryPoint 为 http / grpc / command；Target 为路由、方法或能力 ID。
/// ；Transaction 非空（命令管道）时放行的决策审计随该事务提交，拒绝仍独立写入。</summary>
public sealed record EnforcementRequest(EnforcementSubject Subject, string? Permission, string? Resource, string Channel, string EntryPoint, string Target, System.Data.Common.DbTransaction? Transaction = null);

/// <summary>一次已记录的判定：解释、是否经平台超管旁路、审计记录的 ID 与时间。</summary>
public sealed class AuthorizationDecision
{
    /// <summary>复制输入与解释。</summary>
    public AuthorizationDecision(Guid id, EnforcementRequest request, AuthorizationExplanation explanation, bool bypass, bool logged, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(explanation);
        Id = id; Request = request; Explanation = explanation; Bypass = bypass; Logged = logged; DecidedAt = decidedAt;
    }
    /// <summary>判定实例 ID，与审计记录 ID 相同。</summary>
    public Guid Id { get; }
    /// <summary>判定输入。</summary>
    public EnforcementRequest Request { get; }
    /// <summary>与唯一判定点一致的完整解释。</summary>
    public AuthorizationExplanation Explanation { get; }
    /// <summary>是否经平台超管显式旁路放行。</summary>
    public bool Bypass { get; }
    /// <summary>本次判定是否写入了决策审计（放行按采样，拒绝必记）。</summary>
    public bool Logged { get; }
    /// <summary>判定时间。</summary>
    public DateTimeOffset DecidedAt { get; }
    /// <summary>是否放行。</summary>
    public bool Allow => Explanation.Decision.Allow;
    /// <summary>稳定原因码。</summary>
    public string Reason => Explanation.Decision.Reason;
}

/// <summary>Authz seam 的进程内扩展事件（组件模型规格 §6.4）。</summary>
public static class AuthzRuntimeEvents
{
    /// <summary>环绕一次判定：高级权限模式或 step-up 可包裹链尾（真实 Explain）；链尾之外不产生放行。</summary>
    [QianEvent(Mode = DispatchMode.Waterfall)]
    public static readonly QianEvent<EnforcementRequest, AuthorizationExplanation> Checking =
        QianEvent.Define<EnforcementRequest, AuthorizationExplanation>("authz/checking", DispatchMode.Waterfall);
    /// <summary>每次已记录的判定，供审计与可解释性消费者观察；监听器异常被隔离，不影响判定结果。</summary>
    [QianEvent(Mode = DispatchMode.Emit)]
    public static readonly QianEvent<AuthorizationDecision, object?> Decided =
        QianEvent.Define<AuthorizationDecision, object?>("authz/decided", DispatchMode.Emit);
}

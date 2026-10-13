using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk;

/// <summary>基础组合包的三个强制执行点。</summary>
public enum CommandPolicyKind
{
    /// <summary>验证租户与 scope。</summary>
    Tenancy,
    /// <summary>真实权限判定。</summary>
    Authorization,
    /// <summary>审计策略及同事务写入。</summary>
    Audit
}

/// <summary>基础 Provider 的监听登记入口；执行器同时核验服务、实际进入与卸载取消。</summary>
public static class CommandPolicies
{
    /// <summary>租户强制监听器的存活键。</summary>
    public static readonly ServiceKey TenancyKey = new("CommandTenancy");
    /// <summary>授权强制监听器的存活键。</summary>
    public static readonly ServiceKey AuthorizationKey = new("CommandAuthorization");
    /// <summary>审计强制监听器的存活键。</summary>
    public static readonly ServiceKey AuditKey = new("CommandAudit");

    /// <summary>由 qian.tenancy、qian.authz 或 qian.audit 在 Apply 中登记并随 Effect 撤销。</summary>
    public static void Register(IPluginContext context, CommandPolicyKind kind, Func<CommandInvocation, Func<ValueTask<object?>>, ValueTask<object?>> handler)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(handler);
        var (owner, key, priority) = Definition(kind);
        if (context.Entry.Plugin != owner) throw Denied();
        var lease = new PolicyLease(); context.Effect(lease.Dispose);
        context.On(CommandEvents.Dispatching, DispatchMode.Waterfall,
            (Func<CommandInvocation, Func<ValueTask<object?>>, ValueTask<object?>>)(async (invocation, next) =>
            {
                if (!ReferenceEquals(invocation.Source.TryInject<PolicyLease>(key), lease)) return await next().ConfigureAwait(false);
                lease.EnsureActive(); invocation.CancellationToken.ThrowIfCancellationRequested(); invocation.Enter(kind);
                var result = await handler(invocation, next).ConfigureAwait(false);
                lease.EnsureActive(); invocation.CancellationToken.ThrowIfCancellationRequested(); return result;
            }), priority);
        context.Provide(key, lease);
    }

    internal static PolicySnapshot Require(IPluginContext source) => new(source,
        source.TryInject<PolicyLease>(TenancyKey) ?? throw Denied(), source.TryInject<PolicyLease>(AuthorizationKey) ?? throw Denied(), source.TryInject<PolicyLease>(AuditKey) ?? throw Denied());
    internal static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
    private static (string Owner, ServiceKey Key, int Priority) Definition(CommandPolicyKind kind) => kind switch
    {
        CommandPolicyKind.Tenancy => ("qian.tenancy", TenancyKey, 300),
        CommandPolicyKind.Authorization => ("qian.authz", AuthorizationKey, 200),
        CommandPolicyKind.Audit => ("qian.audit", AuditKey, 100),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal sealed class PolicyLease : IDisposable
    {
        private readonly CancellationTokenSource _stop = new(); private readonly CancellationToken _token; private int _disposed;
        internal PolicyLease() => _token = _stop.Token;
        internal CancellationToken Stopped => _token;
        internal void EnsureActive() { if (Volatile.Read(ref _disposed) != 0) throw Denied(); }
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) { try { _stop.Cancel(); } finally { _stop.Dispose(); } } }
    }
    internal sealed class PolicySnapshot(IPluginContext source, PolicyLease tenancy, PolicyLease authorization, PolicyLease audit)
    {
        internal CancellationTokenSource Link(CancellationToken ct) => CancellationTokenSource.CreateLinkedTokenSource(ct, tenancy.Stopped, authorization.Stopped, audit.Stopped);
        internal void Check()
        {
            tenancy.EnsureActive(); authorization.EnsureActive(); audit.EnsureActive();
            if (!ReferenceEquals(source.TryInject<PolicyLease>(TenancyKey), tenancy) || !ReferenceEquals(source.TryInject<PolicyLease>(AuthorizationKey), authorization) || !ReferenceEquals(source.TryInject<PolicyLease>(AuditKey), audit)) throw Denied();
        }
    }
}

using Kun.Authorization;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.Abstractions;

// DEC-023：进程内不可序列化的开户授权凭据，不是另一份租户上下文，也不是登录主体或角色。
// 仅 Tenancy、SDK、Authz 三个受信程序集可见；普通插件和请求输入没有签发入口。
internal static class TenantInitializationAuthorization
{
    private static readonly AsyncLocal<Grant?> CurrentGrant = new();
    private static readonly AsyncLocal<Invocation?> CurrentInvocation = new();

    internal static IDisposable Begin(IPluginContext source, string actor, IReadOnlySet<string> capabilities, string actorKind = "user")
    {
        if (source.Entry.Plugin != "qian.tenancy" || source.Scope.Kind != "tenant" || !Guid.TryParseExact(source.Scope.Id, "D", out var id) ||
            id == Guid.Empty || actorKind is not ("user" or "service") || string.IsNullOrWhiteSpace(actor) || CurrentGrant.Value is not null) throw Denied();
        var grant = new Grant(source, actor, actorKind, capabilities.ToHashSet(StringComparer.Ordinal));
        CurrentGrant.Value = grant;
        return new Lease(() => { grant.Close(); CurrentGrant.Value = null; });
    }

    internal static IDisposable? Enter(IPluginContext source, string capability, Type requestType, IReadOnlyList<string> permissions)
    {
        if (CurrentGrant.Value is not { } grant) return null;
        grant.EnsureActive();
        if (!ReferenceEquals(source, grant.Source) || !grant.Capabilities.Contains(capability) || CurrentInvocation.Value is not null ||
            permissions.Count == 0 || permissions.Any(permission => !PermissionCode.IsValid(permission))) throw Denied();
        lock (grant.Used) if (!grant.Used.Add(capability)) throw Denied();
        var invocation = new Invocation(grant, capability, requestType, permissions.ToHashSet(StringComparer.Ordinal));
        CurrentInvocation.Value = invocation;
        return new Lease(() => { invocation.Close(); CurrentInvocation.Value = null; });
    }

    internal static bool Allows(string capability, ScopeKey scope)
    {
        if (CurrentInvocation.Value is not { } invocation) return false;
        invocation.EnsureActive();
        return invocation.Capability == capability && invocation.Grant.Source.Scope == scope;
    }
    internal static bool Allows<TRequest>() where TRequest : IPermissionProtectedRequest => CurrentInvocation.Value is { } invocation &&
        Allows(invocation.Capability, invocation.Grant.Source.Scope) && invocation.RequestType == typeof(TRequest) && invocation.Permissions.SetEquals(TRequest.RequiredPermissions);
    internal static bool Active => CurrentInvocation.Value is { } invocation && Allows(invocation.Capability, invocation.Grant.Source.Scope);
    internal static bool AllowsPermission(PermissionRequest request) => Active && request.Subject.Id == Actor && request.Subject.TenantId == Tenant &&
        CurrentInvocation.Value!.Permissions.Contains(request.Permission);
    internal static void Claim<TRequest>(string capability, ScopeKey scope) where TRequest : IPermissionProtectedRequest
    {
        if (!Allows(capability, scope) || !Allows<TRequest>()) throw Denied();
        CurrentInvocation.Value!.Claim();
    }
    internal static string Actor => CurrentInvocation.Value is { } invocation && Allows(invocation.Capability, invocation.Grant.Source.Scope)
        ? invocation.Grant.Actor : throw Denied();
    internal static string ActorKind => Active ? CurrentInvocation.Value!.Grant.ActorKind : throw Denied();
    internal static string Tenant => CurrentInvocation.Value is { } invocation && Allows(invocation.Capability, invocation.Grant.Source.Scope)
        ? invocation.Grant.Source.Scope.Id! : throw Denied();
    private static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
    private abstract class Lifetime
    {
        private int _closed;
        internal void Close() => Interlocked.Exchange(ref _closed, 1);
        internal virtual void EnsureActive() { if (Volatile.Read(ref _closed) != 0) throw Denied(); }
    }
    private sealed class Grant(IPluginContext source, string actor, string actorKind, HashSet<string> capabilities) : Lifetime
    {
        internal IPluginContext Source { get; } = source;
        internal string Actor { get; } = actor;
        internal string ActorKind { get; } = actorKind;
        internal HashSet<string> Capabilities { get; } = capabilities;
        internal HashSet<string> Used { get; } = new(StringComparer.Ordinal);
        internal override void EnsureActive() { base.EnsureActive(); _ = Source.Services; }
    }
    private sealed class Invocation(Grant grant, string capability, Type requestType, HashSet<string> permissions) : Lifetime
    {
        private int _claimed;
        internal void Claim() { if (Interlocked.Exchange(ref _claimed, 1) != 0) throw Denied(); }
        internal Grant Grant { get; } = grant;
        internal string Capability { get; } = capability;
        internal Type RequestType { get; } = requestType;
        internal HashSet<string> Permissions { get; } = permissions;
        internal override void EnsureActive() { base.EnsureActive(); Grant.EnsureActive(); }
    }
    private sealed class Lease(Action close) : IDisposable
    {
        private int _closed;
        public void Dispose() { if (Interlocked.Exchange(ref _closed, 1) == 0) close(); }
    }
}

using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Secrets;

/// <summary>不可变策略与按需文件读取的组合；不缓存已解析值。</summary>
public sealed class SecretResolverFactory : ISecretResolverFactory
{
    private readonly MountedSecretStore _store;
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SecretAccessPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ISecretAccessObserver _observer;
    private readonly bool _allowDev;

    /// <summary>构造解析器工厂，复制绑定和策略，避免调用方事后扩大授权。</summary>
    public SecretResolverFactory(SecretResolverOptions options, ISecretAccessObserver observer)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(observer);
        _store = new MountedSecretStore(options.MountRoot); _observer = observer; _allowDev = options.AllowDevelopmentReferences;
        foreach (var item in options.Bindings)
        {
            var reference = Parse(item.Reference);
            string path = reference.Path;
            if (reference.Scope == "tenant")
            {
                if (string.IsNullOrWhiteSpace(item.TenantId) || !path.StartsWith(item.TenantId + "/", StringComparison.Ordinal)) throw Error(SecretErrors.InvalidReference);
                path = path[(item.TenantId.Length + 1)..];
            }
            else if (item.TenantId is not null) throw Error(SecretErrors.InvalidReference);
            if (!_bindings.TryAdd(reference.ToString(), new Binding(reference, path, item.Location, item.TenantId))) throw Error(SecretErrors.InvalidReference);
        }
        foreach (var policy in options.Policies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Id);
            foreach (var grant in policy.Declared.Concat(policy.Approved)) ValidatePermission(grant);
            if (!_policies.TryAdd(policy.Id, new SecretAccessPolicy(policy.Id, policy.Declared.ToArray(), policy.Approved.ToArray())))
                throw Error(SecretErrors.InvalidReference);
        }
    }

    /// <inheritdoc />
    public ISecretResolver ForPlugin(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Bound(this, context.Entry.Plugin, () => context.Services.GetService<ITenantContext>()?.TenantId, () =>
        {
            // 每次使用都核对上下文仍然活动且 Config 可见，缓存解析器不能跨卸载继续使用。
            if (!ReferenceEquals(context.Config().Secrets, this)) throw Error(SecretErrors.AccessDenied);
            var tenant = context.Services.GetService<ITenantContext>()?.TenantId;
            if (context.Scope.Kind == "tenant" && context.Scope.Id != tenant) throw Error(SecretErrors.AccessDenied);
        });
    }
    /// <inheritdoc />
    public ISecretResolver ForHost(string policyId, ITenantContext? tenantContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        return new Bound(this, policyId, () => tenantContext?.TenantId, static () => { });
    }

    private async ValueTask<SecretValue> ResolveAsync(string input, string caller, string? tenantId, Action check,
        CancellationToken cancellationToken)
    {
        var auditReference = "[invalid-reference]";
        var auditCaller = _policies.ContainsKey(caller) ? caller : "[unknown-caller]";
        SecretValue? result = null;
        try
        {
            check();
            var reference = Parse(input);
            if (!_policies.TryGetValue(caller, out var policy)) throw Error(SecretErrors.AccessDenied);
            _bindings.TryGetValue(reference.ToString(), out var binding);
            if (binding is null && reference.Scope == "tenant" && tenantId is not null)
                _bindings.TryGetValue("secretRef://tenant/" + tenantId + "/" + reference.Path, out binding);
            // 未登记和无权访问的引用不对调用方暴露不同的存在性信息。
            if (binding is null) throw Error(SecretErrors.AccessDenied);
            auditReference = binding.Reference.ToString();
            if (!Allows(policy.Declared, binding) || !Allows(policy.Approved, binding) ||
                binding.Reference.Scope == "tenant" && (tenantId is null || binding.TenantId != tenantId) ||
                binding.Reference.Scope == "dev" && !_allowDev) throw Error(SecretErrors.AccessDenied);
            result = await _store.ReadAsync(binding.Location, cancellationToken).ConfigureAwait(false);
            check();
            await _observer.RecordAsync(new(auditCaller, auditReference, tenantId, true, "resolved"), cancellationToken).ConfigureAwait(false);
            check();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result?.Dispose(); throw;
        }
        catch (Exception error)
        {
            result?.Dispose();
            var failure = error switch
            {
                QianErrorException qian => qian,
                ObjectDisposedException or ServiceNotReadyException => Error(SecretErrors.AccessDenied),
                _ => Error(SecretErrors.Unavailable)
            };
            try { await _observer.RecordAsync(new(auditCaller, auditReference, tenantId, false, failure.Error.Code.Code), CancellationToken.None).ConfigureAwait(false); }
            catch { throw Error(SecretErrors.Unavailable); }
            throw failure;
        }
    }

    private static bool Allows(IReadOnlyList<SecretGrant> grants, Binding binding) => grants.Any(grant =>
        grant.Scope == binding.Reference.Scope && (grant.Name == binding.Path ||
            grant.Name.EndsWith("/*", StringComparison.Ordinal) && binding.Path.StartsWith(grant.Name[..^1], StringComparison.Ordinal)));
    private static void ValidatePermission(SecretGrant permission)
    {
        if (string.IsNullOrWhiteSpace(permission.Name)) throw Error(SecretErrors.InvalidReference);
        if (permission.Scope is not ("platform" or "tenant" or "dev")) throw Error(SecretErrors.InvalidReference);
        var name = permission.Name.EndsWith("/*", StringComparison.Ordinal) ? permission.Name[..^2] : permission.Name;
        _ = Parse("secretRef://" + permission.Scope + "/" + name);
    }
    private static SecretReference Parse(string value)
    {
        try { return SecretReference.Parse(value); }
        catch (Exception error) when (error is FormatException or ArgumentException) { throw Error(SecretErrors.InvalidReference); }
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    private sealed record Binding(SecretReference Reference, string Path, KubernetesSecretLocation Location, string? TenantId);
    private sealed class Bound(SecretResolverFactory owner, string caller, Func<string?> tenant, Action check) : ISecretResolver
    {
        public ValueTask<SecretValue> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedTenant = tenant();
            return owner.ResolveAsync(reference, caller, expectedTenant, () =>
            {
                check();
                if (tenant() != expectedTenant) throw Error(SecretErrors.AccessDenied);
            }, cancellationToken);
        }
    }
}

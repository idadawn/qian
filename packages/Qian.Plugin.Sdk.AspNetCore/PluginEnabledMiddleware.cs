using System.Security.Claims;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk.AspNetCore;

/// <summary>路由、认证及可信租户建立后，按 Registry 的实际 scope 视图拦截插件端点。</summary>
public sealed class PluginEnabledMiddleware(RequestDelegate next, PluginEndpointOptions options)
{
    private static readonly object EvidenceKey = new();
    /// <summary>未知、未挂载和被过滤的绑定均拒绝为相同 NOT_FOUND；不替代能力权限判定。</summary>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(tenant);
        context.Items.Remove(EvidenceKey);
        var endpoint = context.GetEndpoint(); var binding = endpoint?.Metadata.GetMetadata<PluginEndpointBinding>();
        if (binding is null) { await next(context).ConfigureAwait(false); return; }
        if (context.User.Identity is not ClaimsIdentity { IsAuthenticated: true } identity || !tenant.IsMultiTenant || string.IsNullOrWhiteSpace(tenant.TenantId))
            throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var claims = identity.FindAll("tenant_id").ToArray();
        if (claims.Length != 1 || claims[0].Value != tenant.TenantId) throw NotFound();
        var scope = new ScopeKey("tenant", tenant.TenantId);
        IPluginContext registryContext;
        try { registryContext = options.RegistryContext(context.RequestServices); }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException) { throw NotFound(); }
        IReadOnlyList<RegistryScopeEntry> entries;
        try
        {
            var registry = registryContext.Registry();
            entries = await registry.GetScopeEntriesAsync(scope, binding.Key, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { throw NotFound(); }
        var selected = entries.SingleOrDefault(entry => entry.EntryId == binding.EntryId && entry.PluginId == binding.PluginId &&
            entry.Key == binding.Key && entry.Name == binding.Name && (binding.Version is null || entry.Version == binding.Version));
        if (selected is null) throw NotFound();
        context.Items[EvidenceKey] = new Evidence(endpoint!, binding, scope);
        try { await next(context).ConfigureAwait(false); }
        finally { context.Items.Remove(EvidenceKey); }
    }
    internal static bool HasEvidence(HttpContext context, PluginEndpointBinding binding, ITenantContext tenant) =>
        context.Items.TryGetValue(EvidenceKey, out var value) && value is Evidence evidence && ReferenceEquals(evidence.Endpoint, context.GetEndpoint()) &&
        ReferenceEquals(evidence.Binding, binding) && evidence.Scope.Kind == "tenant" && evidence.Scope.Id == tenant.TenantId;
    internal static QianErrorException NotFound() => new(new QianError(PlatformErrors.NotFound));
    private sealed record Evidence(Endpoint Endpoint, PluginEndpointBinding Binding, ScopeKey Scope);
}

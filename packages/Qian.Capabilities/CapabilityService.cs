using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Authorization;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Tenancy;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities;

internal sealed class CapabilityService(IRegistryService registry, IPermissionChecker permissions, ISurfaceReader schemas, IToolContractHasher hasher, IHttpContractSource? httpContracts = null) : ICapabilityService, IDisposable
{
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;

    public async ValueTask<CapabilityCatalog> ListAsync(IPluginContext caller, ICurrentUser user, string? kind = null, string? query = null, CancellationToken cancellationToken = default)
    {
        if (kind is not (null or "command" or "query" or "event" or "entity") || query?.Length > 200) throw Error(PlatformErrors.InputInvalid);
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        return new CapabilityCatalog(visible.Where(item => (kind is null || item.Entry.GetProperty("kind").GetString() == kind) &&
            (string.IsNullOrWhiteSpace(query) || item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Entry.GetProperty("description").GetString()!.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.Entry).ToArray(), 1000);
    }

    public async ValueTask<string> ComposePromptAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, true, linked.Token).ConfigureAwait(false);
        var output = new StringBuilder();
        foreach (var item in visible)
        {
            output.Append("工具：").AppendLine(item.Id);
            output.AppendLine(item.Entry.GetProperty("description").GetString());
            output.Append("输入契约：").AppendLine(item.Entry.GetProperty("inputSchema").GetRawText());
        }
        return output.ToString();
    }

    public async ValueTask<JsonElement> GetOpenApiAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        var result = CapabilityOpenApi.Build(visible.Select(item => item.Entry));
        if (httpContracts is null) return result;
        var tenant = await caller.Tenancy().GetByIdAsync(Guid.Parse(caller.Scope.Id), linked.Token).ConfigureAwait(false);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) return result;
        var native = await httpContracts.GetDocumentAsync(caller, user, linked.Token).ConfigureAwait(false);
        var merged = JsonNode.Parse(result.GetRawText())!.AsObject();
        Merge(merged["paths"]!.AsObject(), native.GetProperty("paths"));
        foreach (var category in native.GetProperty("components").EnumerateObject())
        {
            var components = merged["components"]!.AsObject();
            if (components[category.Name] is not JsonObject target) { target = new JsonObject(); components[category.Name] = target; }
            Merge(target, category.Value);
        }
        using var document = JsonDocument.Parse(merged.ToJsonString());
        var parsed = Microsoft.OpenApi.OpenApiDocument.Parse(document.RootElement.GetRawText(), "json");
        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0)
        {
            var error = new InvalidOperationException("聚合 HTTP 文档无效。");
            error.Data["contractDiagnostics"] = string.Join("; ", parsed.Diagnostic?.Errors.Select(item => item.ToString()) ?? []);
            throw error;
        }
        return document.RootElement.Clone();
    }

    public async ValueTask<JsonElement> InvokeAsync(IPluginContext caller, ICurrentUser user, string id, JsonElement input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        var item = visible.SingleOrDefault(item => item.Id == id) ?? throw Error(PlatformErrors.NotFound);
        // 不缓存目录结果为授权；执行前还会由绑定和实际 Mediator 策略核验可见性与权限。
        if (!Current(caller, item)) throw Error(PlatformErrors.NotFound);
        return await item.Binding.InvokeAsync(caller, input, linked.Token).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<VisibleCapability>> Visible(IPluginContext caller, ICurrentUser user, bool model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject) throw Error(PlatformErrors.Unauthenticated);
        if (caller.Scope.Kind != "tenant" || caller.Scope.Id != principal.TenantId || principal.Actor is not null ||
            principal.Kind is not (PrincipalKind.User or PrincipalKind.Service or PrincipalKind.Agent) ||
            !ReferenceEquals(caller.TryInject<IRegistryService>(RegistryDefinition.Key), registry)) throw Error(PlatformErrors.NotFound);
        if (!Guid.TryParseExact(principal.TenantId, "D", out var tenantId)) throw Error(PlatformErrors.NotFound);
        var tenant = await caller.Tenancy().GetByIdAsync(tenantId, ct).ConfigureAwait(false);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) return [];
        var subject = new AuthorizationSubject(principal.Subject, principal.TenantId, principal.Kind switch
        { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => "agent" });
        var scopes = principal.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var view = await registry.GetScopeEntriesAsync(caller.Scope, CapabilityBindingRegistration.Key, ct).ConfigureAwait(false);
        var registrations = caller.List(CapabilityBindingRegistration.Key); var result = new List<VisibleCapability>();
        foreach (var registration in registrations.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (registration.Value is not IExecutableCapability binding || binding.Contract.Id != registration.Name ||
                !view.Any(entry => entry.EntryId == registration.OwnerEntryId && entry.Scope == registration.Scope && entry.Name == registration.Name && entry.PluginId == binding.Contract.ComponentId)) continue;
            var entry = CapabilityContractCompiler.Describe(binding.Contract, schemas, hasher); var metadata = binding.Contract.Metadata;
            if ((model || principal.Kind == PrincipalKind.Agent) && !metadata.GetProperty("agent").GetProperty("expose").GetBoolean()) continue;
            if (metadata.GetProperty("requiredScopes").EnumerateArray().Any(scope => !scopes.Contains(scope.GetString()!))) continue;
            if (!metadata.TryGetProperty("requiredPermission", out var permission) || string.IsNullOrWhiteSpace(permission.GetString())) continue;
            if (!(await permissions.CheckAsync(new PermissionRequest(subject, permission.GetString()!), ct).ConfigureAwait(false)).Allow) continue;
            var item = new VisibleCapability(binding.Contract.Id, entry, binding);
            if (Current(caller, item)) result.Add(item);
        }
        ct.ThrowIfCancellationRequested();
        if (!user.IsAuthenticated || user.UserId != subject.Id || user.Principal?.TenantId != subject.TenantId || user.Principal?.Kind != principal.Kind || user.Principal?.Scope != principal.Scope)
            throw Error(PlatformErrors.NotFound);
        return result.AsReadOnly();
    }

    private static void Merge(JsonObject target, JsonElement source)
    {
        foreach (var item in source.EnumerateObject())
        {
            var node = JsonNode.Parse(item.Value.GetRawText());
            if (target[item.Name] is { } old && !JsonNode.DeepEquals(old, node)) throw new InvalidOperationException("组件 HTTP 契约发生命名冲突。");
            target[item.Name] = node;
        }
    }
    private static bool Current(IPluginContext caller, VisibleCapability item)
    {
        try { return ReferenceEquals(caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, item.Id), item.Binding); }
        catch (Exception error) when (error is QianErrorException or ObjectDisposedException) { return false; }
    }
    private CancellationTokenSource Link(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        { try { _lifetime.Source.Cancel(); } finally { _lifetime.Source.Dispose(); } }
    }
    private static (CancellationTokenSource, CancellationToken) CreateLifetime()
    { var source = new CancellationTokenSource(); return (source, source.Token); }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    private sealed record VisibleCapability(string Id, JsonElement Entry, IExecutableCapability Binding);
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config;
using Qian.Audit;
using System.Security.Cryptography;
using System.Text;
using Qian.Data;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Tenancy;

internal sealed class TenantSettingsService(IPluginContext context, PostgresTenantDirectory directory) : ITenantSettings, ITenantSettingsOperations, ITenantQuota, IDisposable
{
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();
    private readonly ConcurrentDictionary<string, (long Stamp, TenantSettingsDocument Document)> _cache = new(StringComparer.Ordinal);
    private int _disposed;
    private long _generation;
    public void Dispose() { Interlocked.Exchange(ref _disposed, 1); _cache.Clear(); }
    internal void Invalidate(HookNotification notification)
    {
        if (notification.Source.Entry.Plugin != "qian.registry") return;
        Interlocked.Increment(ref _generation);
        var prefix = notification.Source.Scope.Kind == "tenant" ? notification.Source.Scope.Id + ":" : "";
        foreach (var key in _cache.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))) _cache.TryRemove(key, out _);
    }
    private void Active() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    private ScopeKey Scope()
    {
        Active();
        if (!Guid.TryParseExact(_tenant.TenantId, "D", out var id) || id == Guid.Empty) throw Error(PlatformErrors.Unauthenticated);
        return new ScopeKey("tenant", id.ToString());
    }
    private async Task Platform(Guid id, CancellationToken ct)
    {
        Active(); if (_tenant.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
        if (await directory.GetByIdAsync(id, ct).ConfigureAwait(false) is null) throw Error(PlatformErrors.NotFound);
    }
    public async ValueTask<T?> Get<T>(string pluginId, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 256) throw Error(PlatformErrors.InputInvalid);
        var document = await Read(pluginId, cancellationToken).ConfigureAwait(false);
        var current = document.Config;
        foreach (var segment in key.Replace(':', '.').Split('.'))
        {
            if (segment.Length == 0) throw Error(PlatformErrors.InputInvalid);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                var fallback = context.TryInject<IConfigService>(ConfigDefinition.Key)?.GetValue(pluginId + ":" + key.Replace('.', ':'));
                if (fallback is null) return default;
                return typeof(T) == typeof(string) ? (T)(object)fallback : JsonSerializer.Deserialize<T>(fallback);
            }
        }
        return current.Deserialize<T>(JsonSerializerOptions.Web);
    }
    private async ValueTask<TenantSettingsDocument> Read(string pluginId, CancellationToken ct, bool fresh = false)
    {
        var scope = Scope(); var cacheKey = scope.Id + ":settings:" + pluginId; var generation = Volatile.Read(ref _generation);
        if (!fresh && _cache.TryGetValue(cacheKey, out var cached) && Stopwatch.GetElapsedTime(cached.Stamp) < TimeSpan.FromSeconds(1)) return cached.Document;
        var registry = context.Registry();
        var entries = (await registry.GetConfigurationViewAsync(scope, ct).ConfigureAwait(false)).Where(entry => entry.PluginId == pluginId).ToArray();
        if (entries.Length == 0) throw Error(PlatformErrors.NotFound);
        if (entries.Length != 1) throw Error(PlatformErrors.Conflict);
        var entry = entries[0]; var component = registry.GetInstalledComponents().Single(item => item.PluginId == pluginId && item.Version == entry.Version);
        if (!component.Manifest.GetProperty("scopes").EnumerateArray().Any(value => value.GetString() == "tenant")) throw Error(PlatformErrors.InputInvalid);
        var result = new TenantSettingsDocument(pluginId, entry.EntryId, entry.Revision ?? 0, entry.Config?.Clone() ?? JsonSerializer.SerializeToElement(new { }));
        if (_cache.Count >= 4096) _cache.Clear();
        Active();
        if (generation == Volatile.Read(ref _generation)) _cache[cacheKey] = (Stopwatch.GetTimestamp(), result);
        return result;
    }
    public async ValueTask<TenantSettingsDocument> ReadAsync(Guid id, string pluginId, CancellationToken cancellationToken = default)
    {
        await Platform(id, cancellationToken).ConfigureAwait(false);
        using var tenant = _tenant.BeginScope(id.ToString());
        return await Read(pluginId, cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask<TenantSettingsDocument> WriteAsync(Guid id, string pluginId, UpdateTenantSettings request, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await Platform(id, cancellationToken).ConfigureAwait(false);
        if (request.Config.ValueKind != JsonValueKind.Object) throw Error(PlatformErrors.InputInvalid);
        using var tenant = _tenant.BeginScope(id.ToString());
        var current = await Read(pluginId, cancellationToken).ConfigureAwait(false);
        var entry = JsonSerializer.SerializeToElement(new { id = current.EntryId, config = request.Config });
        var saved = await context.Registry().MountAsync(Scope(), entry, request.ExpectedRevision, actor, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _generation);
        _cache.TryRemove(id + ":settings:" + pluginId, out _);
        var result = await Read(pluginId, cancellationToken, fresh: true).ConfigureAwait(false);
        await context.Scoped(Scope()).Audit().AppendAsync(new AuditRecord(Guid.CreateVersion7(), id.ToString(), "user", actor, null,
            "qian.tenancy/settings.write", "v1", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.Config.GetRawText()))),
            "api", pluginId, "success", System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString(), DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { pluginId, entryId = result.EntryId, revision = saved.Revision })), cancellationToken).ConfigureAwait(false);
        return result;
    }
    private async Task<TenantQuotas> Limits(Guid id, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var value = await session.Context.Tenants.Where(row => row.Id == id).Select(row => row.Quotas).SingleOrDefaultAsync(ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        return JsonSerializer.Deserialize<TenantQuotas>(value, JsonSerializerOptions.Web)!;
    }
    public async ValueTask<TenantQuotaView> QuotaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Platform(id, cancellationToken).ConfigureAwait(false);
        using var tenant = _tenant.BeginScope(id.ToString());
        var limits = await Limits(id, cancellationToken).ConfigureAwait(false);
        var counter = context.Scoped(Scope()).TryInject<ITenantUserCounter>(TenantSettings.UserCounterKey);
        return new TenantQuotaView(limits, counter is null ? null : await counter.CountUsersAsync(cancellationToken).ConfigureAwait(false), counter is not null);
    }
    public async ValueTask EnsureCanAddUserAsync(int currentUsers, TenantMemberKind kind, CancellationToken cancellationToken = default)
    {
        var scope = Scope(); var limits = await Limits(Guid.Parse(scope.Id!), cancellationToken).ConfigureAwait(false);
        TenantQuotaRules.Check(limits, currentUsers, kind);
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
internal static class TenantQuotaRules
{
    internal static string Serialize(TenantQuotas? quotas)
    {
        if (quotas?.Users < 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        return JsonSerializer.Serialize(quotas ?? new TenantQuotas(), JsonSerializerOptions.Web);
    }
    internal static void Check(TenantQuotas quotas, int users, TenantMemberKind kind)
    {
        if (users < 0 || !Enum.IsDefined(kind)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (kind != TenantMemberKind.PlatformSuperadmin && quotas.Users is { } limit && users >= limit)
            throw new QianErrorException(new QianError(TenancyErrors.UserLimit, new Dictionary<string, string> { ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
    }
}

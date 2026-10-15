using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Plugin.Sdk;
using Qian.Audit;
using System.Security.Cryptography;
using System.Text;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class TenantExpiryCapability(IPluginContext owner, ITenantLifecycle lifecycle) : IExecutableCapability, IDisposable
{
    private int _disposed;
    public CapabilityContract Contract { get; } = new("qian.tenancy", """{"name":"tenant.scan-expired","description":"扫描到期租户并幂等进入宽限，供调度器 callback 调用。","sideEffect":"write","policyHint":"allow","idempotent":true,"async":"none","requiredScopes":[],"requiredSecrets":[],"dataClassification":"internal","costTier":"low","contractVersion":"v1","audit":true,"agent":{"expose":true,"requiresConfirmation":false},"binding":{"kind":"command","id":"qian.tenancy/tenant.scan-expired"},"requiredPermission":"qian.tenancy:tenant:scan"}""",
        TenantScanJson.Default.ExpiryScanInput, TenantScanJson.Default.ExpiryScanResult);
    public async ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0 || caller.Scope != ScopeKey.Global || caller.Entry.Plugin is not ("qian.tenancy" or "qian.scheduler") ||
            !ReferenceEquals(owner.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, Contract.Id), this)) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        ExpiryScanInput request;
        try { request = input.Deserialize(TenantScanJson.Default.ExpiryScanInput) ?? throw new JsonException(); }
        catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)); }
        if (request.IdempotencyKey == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        await owner.Audit().AppendPlatformAsync(owner, new AuditRecord(Guid.CreateVersion7(), null, "service", caller.Entry.Plugin, null,
            Contract.Id, "v1", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.GetRawText()))), "job", null,
            "started", System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { limit = request.Limit })), cancellationToken).ConfigureAwait(false);
        var result = await lifecycle.ScanExpiredAsync(request.Limit, caller.Entry.Plugin, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new ExpiryScanResult(result), TenantScanJson.Default.ExpiryScanResult);
    }
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ExpiryScanInput(Guid IdempotencyKey, int Limit = 100);
internal sealed record ExpiryScanResult(int Expired);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExpiryScanInput))][JsonSerializable(typeof(ExpiryScanResult))]
internal sealed partial class TenantScanJson : JsonSerializerContext;

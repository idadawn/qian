using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;

namespace Qian.Audit.Native;

internal sealed class NativeAuditCommandPolicy(IAuditService audit)
{
    private readonly ConcurrentDictionary<(CapabilityContract Contract, bool Hook), AuditInputProjection> _policies = new();
    internal async ValueTask<object?> Handle(CommandInvocation invocation, Func<ValueTask<object?>> next)
    {
        if (_policies.Count >= 256) _policies.Clear();
        var policy = _policies.GetOrAdd((invocation.Contract, invocation.Channel == "hook"), static key =>
        {
            var metadata = key.Contract.Metadata;
            using var schema = JsonDocument.Parse(key.Contract.InputSchema.ToJsonString());
            return new AuditInputProjection(schema.RootElement, metadata.GetProperty("sideEffect").GetString() != "read", metadata.GetProperty("audit").GetBoolean() || key.Hook);
        });
        var inputHash = policy.InputHash(invocation.Input); var result = await next().ConfigureAwait(false);
        if (inputHash is null) return result;
        var principal = invocation.Principal;
        if (principal.Actor is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        var actor = principal.Kind switch
        {
            Qian.Identity.Abstractions.PrincipalKind.User => "user", Qian.Identity.Abstractions.PrincipalKind.Service => "service",
            Qian.Identity.Abstractions.PrincipalKind.Agent => "agent", _ => throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied))
        };
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("component", invocation.Contract.ComponentId); writer.WriteString("entry", invocation.Source.Entry.Id); writer.WriteEndObject();
        }
        using var payload = JsonDocument.Parse(buffer.WrittenMemory);
        var record = new AuditRecord(invocation.Id, principal.TenantId, actor, principal.Subject, null, invocation.Contract.Id,
            invocation.Contract.Metadata.GetProperty("contractVersion").GetString()!, inputHash, invocation.Channel, null, "success", invocation.TraceId, invocation.StartedAt, payload.RootElement);
        await audit.AppendEnlistedAsync(record, invocation.Transaction, invocation.CancellationToken).ConfigureAwait(false); return result;
    }
}

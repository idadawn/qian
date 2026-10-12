using System.Text.Json;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;

namespace Qian.Audit;

// 生产 command/dispatching 监听器。next 包含实际 Mediator handler 及同会话 SaveChanges，外层事务尚未提交。
internal sealed class AuditCommandPolicy(IAuditService audit)
{
    internal async ValueTask<object?> Handle(CommandInvocation invocation, Func<ValueTask<object?>> next)
    {
        var metadata = invocation.Contract.Metadata;
        using var schema = JsonDocument.Parse(invocation.Contract.InputSchema.ToJsonString());
        var policy = new AuditInputPolicy(schema.RootElement, metadata.GetProperty("sideEffect").GetString() != "read", metadata.GetProperty("audit").GetBoolean());
        var inputHash = policy.InputHash(invocation.Input);
        var result = await next().ConfigureAwait(false);
        if (inputHash is null) return result;
        var principal = invocation.Principal;
        // S1 不接受 act 委托令牌，不能把未实现的委托解析静默记成普通调用。
        if (principal.Actor is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        var actor = principal.Kind switch
        {
            Qian.Identity.Abstractions.PrincipalKind.User => "user",
            Qian.Identity.Abstractions.PrincipalKind.Service => "service",
            Qian.Identity.Abstractions.PrincipalKind.Agent => "agent",
            _ => throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied))
        };
        var record = new AuditRecord(invocation.Id, principal.TenantId, actor, principal.Subject, null,
            invocation.Contract.Id, metadata.GetProperty("contractVersion").GetString()!, inputHash, invocation.Channel, null,
            "success", invocation.TraceId, invocation.StartedAt,
            JsonSerializer.SerializeToElement(new { component = invocation.Contract.ComponentId, entry = invocation.Source.Entry.Id }));
        await audit.AppendEnlistedAsync(record, invocation.Transaction, invocation.CancellationToken).ConfigureAwait(false);
        return result;
    }
}

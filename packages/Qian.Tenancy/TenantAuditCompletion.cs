using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Runtime;

namespace Qian.Tenancy;

// 仅用于已经执行过的操作的补充记录；前置授权审计仍必须成功，不能借此放宽准入。
internal static partial class TenantAuditCompletion
{
    internal static async Task AppendAsync(IPluginContext context, AuditRecord record, Func<AuditRecord, CancellationToken, Task<AuditChainEntry>> append)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await append(record, deadline.Token).ConfigureAwait(false); }
        catch (Exception error)
        {
            // 不打印异常消息或对象：数据库异常可能包含配置、凭据或审计载荷。
            CompletionFailed(context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Tenancy.AuditCompletion"),
                record.CapabilityId, record.TenantId, record.Resource, record.Result, record.TraceId, record.Id, error.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "租户操作补充审计失败；Capability={Capability} Tenant={Tenant} Resource={Resource} Result={Result} TraceId={TraceId} AuditId={AuditId} ErrorType={ErrorType}")]
    private static partial void CompletionFailed(ILogger logger, string capability, string? tenant, string? resource, string result, string traceId, Guid auditId, string errorType);
}

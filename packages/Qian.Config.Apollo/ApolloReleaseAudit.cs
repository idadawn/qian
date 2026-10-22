using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Runtime;

namespace Qian.Config.Apollo;

// 把 Apollo 发布记录写进平台审计链（PRD 7.8 / 7.11）。审计端口可缺席（standalone 无 qian.audit），就绪后按序补写暂存记录；
// 审计组件卸载后等新的服务就绪再继续。写入失败只记日志，不阻断配置生效：配置发布不在业务事务里，平台链未启用时同样只记日志。
internal static partial class ApolloReleaseAudit
{
    internal const string ContractVersion = "1.0.0";

    internal static void Attach(IPluginContext ctx, ApolloReleaseTracker releases)
    {
        var stop = new CancellationTokenSource();
        var pump = Task.Run(() => PumpAsync(ctx, releases, stop.Token));
        ctx.Effect(async () =>
        {
            await stop.CancelAsync().ConfigureAwait(false);
            try { await pump.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            stop.Dispose();
        });
    }

    private static async Task PumpAsync(IPluginContext ctx, ApolloReleaseTracker releases, CancellationToken ct)
    {
        ApolloReleaseRecord? carry = null;
        var warned = false;
        try
        {
            if (ctx.TryInject<IAuditService>(AuditDefinition.Key) is null) LogAuditAbsent(ctx.Logger);
            while (!ct.IsCancellationRequested)
            {
                var audit = await ctx.WhenReadyAsync<IAuditService>(AuditDefinition.Key, ct).ConfigureAwait(false);
                while (true)
                {
                    var release = carry ?? await releases.Records.ReadAsync(ct).ConfigureAwait(false);
                    carry = release;
                    try
                    {
                        await audit.AppendPlatformAsync(ctx, Record(releases.AppId, release), ct).ConfigureAwait(false);
                        carry = null;
                    }
                    // 审计组件已卸载：保留这条，等新的审计服务就绪后补写；短暂退避，避免旧注册尚未撤下时空转。
                    catch (Exception error) when (error is ObjectDisposedException or ServiceNotReadyException)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        if (!warned) LogAppendFailed(ctx.Logger, release.Namespace, error);
                        else LogAppendFailedAgain(ctx.Logger, release.Namespace);
                        warned = true; carry = null;
                    }
                }
            }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException) { }
    }

    internal static AuditRecord Record(string appId, ApolloReleaseRecord release)
    {
        var trace = Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        return new AuditRecord(Guid.CreateVersion7(), null, "service", appId, null,
            release.Rejected ? ConfigEvents.ReleaseRejectedCapabilityId : ConfigEvents.ReleaseAppliedCapabilityId, ContractVersion,
            release.InputHash, "event", release.Resource, release.Rejected ? "rejected" : "applied", trace, release.OccurredAt, Payload(release));
    }

    // 补充数据只有 namespace、变更键数与拒绝原因码。
    private static JsonElement Payload(ApolloReleaseRecord release)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("namespace", release.Namespace);
            writer.WriteNumber("changedKeys", release.ChangedKeys);
            if (release.Rejected)
            {
                writer.WriteStartArray("reasons");
                foreach (var reason in release.Reasons) writer.WriteStringValue(reason);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    [LoggerMessage(EventId = 2111, Level = LogLevel.Debug, Message = "未装配审计组件，Apollo 发布记录暂存在内存（有界），审计就绪后补写。")]
    private static partial void LogAuditAbsent(ILogger logger);
    [LoggerMessage(EventId = 2112, Level = LogLevel.Warning, Message = "Apollo namespace {Namespace} 的发布记录未能写入平台审计链（平台链需显式启用 chain.platformRole），已丢弃。")]
    private static partial void LogAppendFailed(ILogger logger, string @namespace, Exception error);
    [LoggerMessage(EventId = 2113, Level = LogLevel.Debug, Message = "Apollo namespace {Namespace} 的发布记录未能写入平台审计链，已丢弃。")]
    private static partial void LogAppendFailedAgain(ILogger logger, string @namespace);
}

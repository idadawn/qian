using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz;

// 授权变更 → 身份修订递增（S1-DEC-027，PRD 7.5 / R7）。授予的增、改、撤提交后，在同一调用内对每个受影响的 user 主体：
// 1. 经身份端口递增它在该租户的 identity_revision：身份侧随即在 userinfo / 会话校验拒绝旧令牌，refresh 换到带新修订的令牌；
// 2. 把本组件的修订下限抬到端口返回的新修订：旧令牌在任一宿主的 PEP 也随即被拒（401，客户端据此换令牌）。
// 只在身份组件与本组件同进程时生效（standalone / 参考宿主），身份组件不在本进程即跳过；拆分部署由身份侧订阅 authz.grant.changed.v1（S1-IDP-012）。
// 授权变更已提交、判定已按租户授权修订生效，所以这里不回滚也不向调用方抛错：瞬时故障在预算（3 秒）内退避重试，仍失败记警告与计数；
// 确定性结果直接跳过、不重试：主体不是 GUID，该租户没有它的成员身份（NOT_FOUND）。
internal sealed class IdentityRevisionPropagation(IPluginContext owner, IDataService data)
{
    private static readonly Action<ILogger, string, Exception?> s_skipped = LoggerMessage.Define<string>(
        LogLevel.Debug, new EventId(3, "IdentityRevisionSkipped"), "授权变更后的身份修订递增已跳过（{Reason}）。");
    private static readonly Action<ILogger, string, double, string, Exception?> s_failed = LoggerMessage.Define<string, double, string>(
        LogLevel.Warning, new EventId(4, "IdentityRevisionFailed"),
        "授权变更已提交，但身份修订递增的 {Step} 一步在 {Budget} 秒内没有完成（{ErrorType}），旧令牌将保留到过期或客户端换令牌。");

    /// <summary>一次写入的总预算：递增与抬高下限都在其中，用尽即放弃（S1-DEC-027 Q2）。</summary>
    internal TimeSpan Budget { get; init; } = TimeSpan.FromSeconds(3);

    internal async Task PropagateAsync(string tenantId, IReadOnlyList<(string SubjectId, string Change)> users)
    {
        if (users.Count == 0) return;
        var accounts = Accounts();
        if (accounts is null) { foreach (var _ in users) Skip("identity_unavailable"); return; }
        // 授权变更已经提交：调用方此后取消也要在预算内收尾，所以只受预算约束，不接调用方的取消令牌。
        using var budget = new CancellationTokenSource(Budget);
        foreach (var (subjectId, change) in users)
        {
            if (!Guid.TryParseExact(subjectId, "D", out var person) || person == Guid.Empty) { Skip("invalid_subject"); continue; }
            long revision = 0;
            if (!await TryAsync("bump", async ct => revision = await accounts.BumpIdentityRevisionAsync(person, "authz.grant." + change, ct).ConfigureAwait(false), budget.Token)
                    .ConfigureAwait(false)) continue;
            if (await TryAsync("floor", ct => IdentityRevisionGuard.RaiseAsync(data, tenantId, "user", subjectId, revision, ct), budget.Token).ConfigureAwait(false))
                AuthzMetrics.IdentityRevisions.Add(1, new KeyValuePair<string, object?>("outcome", "bumped"));
        }
    }

    // 身份组件在本进程才有账号端口；本组件已卸载（上下文关闭）时同样没有可调用的端口。
    private IIdentityAccounts? Accounts()
    {
        try { return owner.TryInject<IIdentityAccounts>(IdentityDefinition.AccountsKey); }
        catch (ObjectDisposedException) { return null; }
    }

    // 成功返回 true。确定性错误（结构化错误码、身份组件已卸载）记为跳过；其余按瞬时故障退避重试，预算用尽记为失败。
    private async Task<bool> TryAsync(string step, Func<CancellationToken, Task> action, CancellationToken budget)
    {
        for (var attempt = 1; ; attempt++)
        {
            Exception failure;
            try { await action(budget).ConfigureAwait(false); return true; }
            catch (QianErrorException error) { Skip(error.Error.Code.Code); return false; }
            catch (Exception error) when (error is ObjectDisposedException or ServiceNotReadyException) { Skip(step + "_unavailable"); return false; }
            catch (Exception error) { failure = error; }
            try { await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(400, 50 << Math.Min(attempt - 1, 3))), budget).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                s_failed(owner.Logger, step, Budget.TotalSeconds, failure.GetType().Name, null);
                AuthzMetrics.IdentityRevisions.Add(1, new KeyValuePair<string, object?>("outcome", "failed"), new KeyValuePair<string, object?>("reason", step));
                return false;
            }
        }
    }

    private void Skip(string reason)
    {
        s_skipped(owner.Logger, reason, null);
        AuthzMetrics.IdentityRevisions.Add(1, new KeyValuePair<string, object?>("outcome", "skipped"), new KeyValuePair<string, object?>("reason", reason));
    }
}

/// <summary>qian.authz 的计量。</summary>
internal static class AuthzMetrics
{
    public const string MeterName = "Qian.Authz";
    private static readonly Meter Meter = new(MeterName);
    // 授权变更后身份修订递增的结果：outcome 为 bumped / skipped / failed，reason 为跳过或失败的稳定原因（不含主体标识）。
    public static readonly Counter<long> IdentityRevisions = Meter.CreateCounter<long>("qian.authz.identity_revision.propagations", description: "授权变更后身份修订递增的结果");
}

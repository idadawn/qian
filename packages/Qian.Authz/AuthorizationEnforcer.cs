using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Runtime;

namespace Qian.Authz;

// 三处 PEP（HTTP、gRPC、命令管道）共用的执行核心（ADR-006 D6.6 / D6.8）：
// 1. 主体与租户边界 fail-closed：委托与非法主体拒绝；主体带租户但上下文为空抛错（宿主没建可信租户边界）；平台主体只走显式旁路；
// 2. 令牌 identity_revision 低于服务端见过的最高值即旧令牌，拒绝；
// 3. 判定只来自唯一判定点：租户主体经 authz/checking 链尾的 Explain，平台主体经 ISuperadminBypass（自带审计）；
// 4. 每次判定写决策审计（拒绝必记、放行按采样）并 emit authz/decided；审计不可用或写不成一律拒绝。
internal sealed class AuthorizationEnforcer(IPluginContext owner, RbacPermissionChecker checker, ISuperadminBypass? bypass, ITenantContext tenant,
    IdentityRevisionGuard revisions, double sampling) : IAuthzEnforcement
{
    private const string ContractVersion = "1.0.0";
    private static readonly Action<ILogger, string, Exception?> s_decisionLogFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2, "DecisionLogFailed"), "授权决策审计写入失败（{Reason}），本次判定按拒绝处理。");

    public async ValueTask<AuthorizationDecision> EnforceAsync(EnforcementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var subject = request.Subject ?? throw new ArgumentException("缺少主体。", nameof(request));
        if (request.Channel is not ("ui" or "api" or "mcp" or "a2a" or "job" or "event" or "hook") ||
            request.EntryPoint is not (AuthzEnforcement.HttpEntryPoint or AuthzEnforcement.GrpcEntryPoint or AuthzEnforcement.CommandEntryPoint) ||
            !Identifier(request.Target, 384) || (request.Resource is not null && !Identifier(request.Resource, 512)))
            throw new ArgumentException("强制执行请求的通道、入口、目标或资源无效。", nameof(request));
        if (!Identifier(subject.Id, 200) || subject.Kind is not ("user" or "service" or "agent")) return await DenyAsync(request, "invalid_subject", cancellationToken).ConfigureAwait(false);
        if (subject.Delegated || subject.Kind == "agent") return await DenyAsync(request, "delegation_not_supported", cancellationToken).ConfigureAwait(false);
        var current = string.IsNullOrEmpty(tenant.TenantId) ? null : tenant.TenantId;
        if (subject.TenantId is not null && current is null)
            throw new InvalidOperationException("已认证主体带租户，但宿主没有为本次调用建立租户上下文；PEP 必须放在可信租户边界之后。");
        if (subject.TenantId is not null && subject.TenantId != current) return await DenyAsync(request, "subject_tenant_mismatch", cancellationToken).ConfigureAwait(false);
        if (subject.TenantId is null && current is not null) return await DenyAsync(request, "tenant_context_mismatch", cancellationToken).ConfigureAwait(false);
        if (request.Permission is null) return await DenyAsync(request, "permission_not_declared", cancellationToken).ConfigureAwait(false);
        if (!PermissionCode.IsValid(request.Permission)) return await DenyAsync(request, "invalid_permission", cancellationToken).ConfigureAwait(false);
        if (subject.TenantId is null) return await BypassAsync(request, cancellationToken).ConfigureAwait(false);
        if (await revisions.CheckAsync(subject, cancellationToken).ConfigureAwait(false) is { } stale) return await DenyAsync(request, stale, cancellationToken).ConfigureAwait(false);
        var permission = new PermissionRequest(new AuthorizationSubject(subject.Id, subject.TenantId, subject.Kind), request.Permission, request.Resource);
        var explanation = await owner.DispatchAsync(AuthzRuntimeEvents.Checking, request,
            () => checker.ExplainDetailedAsync(permission, subject.IdentityRevision, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (tenant.TenantId != current) return await DenyAsync(request, "tenant_context_changed", cancellationToken).ConfigureAwait(false);
        return await RecordAsync(request, explanation, bypass: false, cancellationToken).ConfigureAwait(false);
    }

    // 没有单一权限点的已认证入口只比对 identity_revision（S1-YUE-012）：与 EnforceAsync 第 2 步同一张下限表、同一拒绝原因与决策日志；
    // 主体租户与当前上下文不一致不在这里拒绝（处理器按租户边界自己拒绝），平台主体与不带修订的主体不比对。
    public async ValueTask<AuthorizationDecision?> CheckIdentityRevisionAsync(EnforcementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var subject = request.Subject ?? throw new ArgumentException("缺少主体。", nameof(request));
        if (request.Channel is not ("ui" or "api" or "mcp" or "a2a" or "job" or "event" or "hook") ||
            request.EntryPoint is not (AuthzEnforcement.HttpEntryPoint or AuthzEnforcement.GrpcEntryPoint or AuthzEnforcement.CommandEntryPoint) ||
            !Identifier(request.Target, 384) || (request.Resource is not null && !Identifier(request.Resource, 512)))
            throw new ArgumentException("强制执行请求的通道、入口、目标或资源无效。", nameof(request));
        if (subject.TenantId is null || subject.IdentityRevision is null) return null;
        var current = string.IsNullOrEmpty(tenant.TenantId) ? null : tenant.TenantId;
        if (current is null)
            throw new InvalidOperationException("已认证主体带租户，但宿主没有为本次调用建立租户上下文；PEP 必须放在可信租户边界之后。");
        if (subject.TenantId != current || !Identifier(subject.Id, 200) || subject.Kind is not ("user" or "service" or "agent")) return null;
        return await revisions.CheckAsync(subject, cancellationToken).ConfigureAwait(false) is { } stale
            ? await DenyAsync(request, stale, cancellationToken).ConfigureAwait(false)
            : null;
    }

    // 平台主体：旁路服务自己先写审计再放行（拒绝不写），所以放行不再重复记录；拒绝按决策日志写进平台链（平台链未配置时只能记日志）。
    private async ValueTask<AuthorizationDecision> BypassAsync(EnforcementRequest request, CancellationToken ct)
    {
        if (bypass is null) return await DenyAsync(request, "platform_subject_requires_bypass", ct).ConfigureAwait(false);
        var subject = request.Subject;
        var decision = await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject(subject.Id, null, subject.Kind), request.Permission!, null, request.Resource, request.Channel), ct).ConfigureAwait(false);
        var explanation = decision.Allow
            ? new AuthorizationExplanation(decision, [PlatformRoles.Superadmin], [], [])
            : new AuthorizationExplanation(decision, [], [], [request.Permission!]);
        if (decision.Allow)
        {
            var allowed = new AuthorizationDecision(Guid.CreateVersion7(), request, explanation, bypass: true, logged: true, DateTimeOffset.UtcNow);
            await owner.DispatchAsync(AuthzRuntimeEvents.Decided, allowed, ct).ConfigureAwait(false);
            return allowed;
        }
        return await RecordAsync(request, explanation, bypass: true, ct).ConfigureAwait(false);
    }

    private ValueTask<AuthorizationDecision> DenyAsync(EnforcementRequest request, string reason, CancellationToken ct) =>
        RecordAsync(request, new AuthorizationExplanation(new PermissionDecision(false, reason, []), [], [], request.Permission is { } permission && PermissionCode.IsValid(permission) ? [permission] : []), bypass: false, ct);

    private async ValueTask<AuthorizationDecision> RecordAsync(EnforcementRequest request, AuthorizationExplanation explanation, bool bypass, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        var allow = explanation.Decision.Allow;
        var sampled = !allow || sampling >= 1 || (sampling > 0 && Random.Shared.NextDouble() < sampling);
        if (sampled)
        {
            var failure = await AppendAsync(id, request, explanation, bypass, now, ct).ConfigureAwait(false);
            if (failure is not null)
            {
                s_decisionLogFailed(owner.Logger, failure, null);
                // 放行必须有据可查；拒绝本来就是拒绝，原因保留，只是没能记录。
                if (allow) explanation = new AuthorizationExplanation(new PermissionDecision(false, failure, []), explanation.MatchedRoles, explanation.MatchedGrants, [request.Permission!]);
                sampled = false;
            }
        }
        var decision = new AuthorizationDecision(id, request, explanation, bypass, sampled, now);
        await owner.DispatchAsync(AuthzRuntimeEvents.Decided, decision, ct).ConfigureAwait(false);
        return decision;
    }

    // 返回 null 表示已写入；否则为稳定失败原因。有租户上下文时进该租户的链（含平台主体落在租户上下文里的拒绝，与旁路审计同一链）；
    // 无租户上下文的平台主体拒绝进平台链（需要 Audit 配置平台角色）。
    private async Task<string?> AppendAsync(Guid id, EnforcementRequest request, AuthorizationExplanation explanation, bool bypass, DateTimeOffset now, CancellationToken ct)
    {
        IAuditService? audit;
        try { audit = owner.TryInject<IAuditService>(AuditDefinition.Key); }
        catch (ObjectDisposedException) { audit = null; }
        if (audit is null) return AuthzEnforcement.AuditUnavailable;
        var subject = request.Subject;
        var input = JsonSerializer.SerializeToUtf8Bytes(new[] { request.Permission ?? "", request.Resource ?? "", request.EntryPoint, request.Target, request.Channel }, AuthzJson.Default.StringArray);
        var trace = Activity.Current?.TraceId is { } traceId && traceId != default ? traceId.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        var payload = JsonSerializer.SerializeToElement(new DecisionLogPayload(request.Permission, request.EntryPoint, request.Target, request.Resource,
            explanation.Decision.Allow ? "allow" : "deny", explanation.Decision.Reason, explanation.MatchedRoles, explanation.MatchedGrants, explanation.Missing,
            explanation.Decision.MatchedPolicies, subject.IdentityRevision, bypass), AuthzJson.Default.DecisionLogPayload);
        var chain = string.IsNullOrEmpty(tenant.TenantId) ? null : tenant.TenantId;
        AuditRecord record;
        try
        {
            record = new AuditRecord(id, chain, subject.Kind, subject.Id, null, AuthzEnforcement.DecisionCapabilityId, ContractVersion,
                Convert.ToHexStringLower(SHA256.HashData(input)), request.Channel, request.Resource, explanation.Decision.Allow ? "allowed" : "denied", trace, now, payload);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return "decision_log_invalid"; }
        try
        {
            if (chain is null) await audit.AppendPlatformAsync(owner, record, ct).ConfigureAwait(false);
            // 命令管道的放行决策随命令的可串行化事务提交：命令自己的审计条目在同一事务里接在它后面，链上不会出现快照之外的条目；
            // 拒绝会让该事务回滚，所以拒绝独立写入（拒绝必记）。
            else if (request.Transaction is { } transaction && explanation.Decision.Allow) await audit.AppendEnlistedAsync(record, transaction, ct).ConfigureAwait(false);
            else await audit.AppendAsync(record, ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return "decision_log_failed"; }
    }

    private static bool Identifier(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
}

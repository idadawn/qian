using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Authz;

// ADR-006 D6.3「平台超管旁路在策略中显式声明并审计」：只有平台主体经此端口显式请求、持有效 platform.superadmin、且审计写成功才放行。
// 拒绝不写审计，原因只给稳定码；审计不可用或写入失败一律拒绝。producer 是 Authz 组件自己的 global 上下文，平台链写入以它为生产方。
// 未配置专用平台登录（platform 为 null）时端口仍在，但一律拒绝：清单的 provides 是静态的，缺配置不等于放行。
internal sealed class SuperadminBypassService(IDataService data, ITenantContext tenant, IPermissionRegistry registry,
    PostgresPlatformRoleStore? platform, IPluginContext producer) : ISuperadminBypass
{
    internal const string CapabilityId = "qian.authz/superadmin.bypass";
    private static readonly Action<ILogger, Exception?> s_auditFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1, "SuperadminBypassAuditFailed"), "平台超管旁路的审计写入失败，已拒绝本次访问。");
    private static readonly IReadOnlyList<string> Policies = Array.AsReadOnly(["role:" + PlatformRoles.Superadmin, "bypass:" + PlatformRoles.SuperadminBypass]);

    public async ValueTask<PermissionDecision> AuthorizeAsync(SuperadminAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (platform is null) return Deny("platform_role_not_configured");
        var subject = access.Subject;
        if (subject is null || subject.TenantId is not null || subject.Kind != "user" || !Identifier(subject.Id, 200)) return Deny("invalid_subject");
        if (!PermissionCode.IsValid(access.Permission)) return Deny("invalid_permission");
        if (access.Channel is not ("ui" or "api" or "mcp" or "a2a" or "job" or "event" or "hook")) return Deny("invalid_channel");
        if (access.Resource is not null && !Identifier(access.Resource, 512)) return Deny("invalid_resource");
        // 审计链的租户键是规范 UUID；目标租户与当前租户上下文必须一致，平台资源要求无租户上下文。
        if (access.TenantId is not null && (!Guid.TryParseExact(access.TenantId, "D", out var parsed) || parsed == Guid.Empty || parsed.ToString("D") != access.TenantId))
            return Deny("invalid_tenant");
        var current = string.IsNullOrEmpty(tenant.TenantId) ? null : tenant.TenantId;
        if (access.TenantId != current) return Deny("tenant_context_mismatch");
        if (!await RegisteredAsync(access, cancellationToken).ConfigureAwait(false)) return Deny("permission_not_registered");
        if (!await platform.IsSuperadminAsync(subject.Id, cancellationToken).ConfigureAwait(false)) return Deny("not_superadmin");
        if ((string.IsNullOrEmpty(tenant.TenantId) ? null : tenant.TenantId) != current) return Deny("tenant_context_changed");
        if (producer.TryInject<IAuditService>(AuditDefinition.Key) is not { } audit) return Deny("audit_unavailable");
        try
        {
            var record = Record(access);
            if (access.TenantId is null) await audit.AppendPlatformAsync(producer, record, cancellationToken).ConfigureAwait(false);
            else await audit.AppendAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            s_auditFailed(producer.Logger, error);
            return Deny("audit_failed");
        }
        return new PermissionDecision(true, "superadmin_bypass", Policies);
    }

    // 租户资源：该组件策略须已装进目标租户，且权限点在当前活动登记中；平台资源：权限点须在已持久化目录的某个版本中且该版本仍处于活动登记。
    private async Task<bool> RegisteredAsync(SuperadminAccess access, CancellationToken ct)
    {
        var plugin = access.Permission.Split(':')[0];
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        if (access.TenantId is not null)
        {
            var install = await db.PolicyInstalls.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == plugin, ct).ConfigureAwait(false);
            return install is not null && registry.Find(access.Permission, install.Version) is not null;
        }
        var versions = await db.Permissions.AsNoTracking().Where(row => row.PluginId == plugin && row.Code == access.Permission)
            .Select(row => row.Version).ToArrayAsync(ct).ConfigureAwait(false);
        return versions.Any(version => registry.Find(access.Permission, version) is not null);
    }

    private static AuditRecord Record(SuperadminAccess access)
    {
        var input = JsonSerializer.SerializeToUtf8Bytes(new[] { access.Permission, access.TenantId ?? "", access.Resource ?? "", access.Channel }, AuthzJson.Default.StringArray);
        var trace = Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
        {
            ["bypass"] = PlatformRoles.SuperadminBypass, ["role"] = PlatformRoles.Superadmin, ["permission"] = access.Permission
        }, AuthzJson.Default.DictionaryStringString);
        return new AuditRecord(Guid.CreateVersion7(), access.TenantId, "user", access.Subject.Id, null, CapabilityId, "1.0.0",
            Convert.ToHexStringLower(SHA256.HashData(input)), access.Channel, access.Resource, "allowed", trace, DateTimeOffset.UtcNow, payload);
    }

    private static bool Identifier(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
    private static PermissionDecision Deny(string reason) => new(false, reason, []);
}

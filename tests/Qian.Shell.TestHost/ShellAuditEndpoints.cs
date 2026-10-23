using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Audit;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Shell.TestHost;

// 验收端口只接受固定结构的浏览器拒绝报告；租户与主体始终来自验签结果，报告不是授权事实。
internal static class ShellAuditEndpoints
{
    internal const string TenantA = "11111111-1111-4111-8111-111111111111";
    internal const string TenantB = "22222222-2222-4222-8222-222222222222";
    private static readonly string[] s_fields = ["name", "remoteId", "scope", "reason", "code", "hostVersion", "requiredVersion"];

    internal static void Map(WebApplication app, AuditConsumer consumer, AsyncLocalTenantContext tenant, JsonWebTokenHandler tokens,
        TokenValidationParameters validation, string yueVersion)
    {
        app.MapPost("/api/shell/audit/remote-rejected", async (HttpContext http) =>
        {
            var identity = await Identity(http, "report", tokens, validation);
            if (identity is null) return Error(PlatformErrors.Unauthenticated);
            if (http.Request.ContentLength is null or > 2048 || !http.Request.HasJsonContentType()) return Error(PlatformErrors.InputInvalid);
            JsonDocument document;
            try { document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted); }
            catch (JsonException) { return Error(PlatformErrors.InputInvalid); }
            using (document)
            {
                var json = document.RootElement;
                if (json.ValueKind != JsonValueKind.Object) return Error(PlatformErrors.InputInvalid);
                var properties = json.EnumerateObject().ToArray();
                if (properties.Length != s_fields.Length || properties.Any(p => !s_fields.Contains(p.Name, StringComparer.Ordinal) || p.Value.ValueKind != JsonValueKind.String) ||
                    properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != s_fields.Length) return Error(PlatformErrors.InputInvalid);
                var tenantId = identity.FindFirst("tenant_id")!.Value;
                // 可信验收快照：仅租户 A 的 qian.tenancy 被挂载；bad 场景的引擎范围由服务端固定，不能由报告改变。
                if (tenantId != TenantA || json.GetProperty("scope").GetString() != "tenant:" + tenantId) return Error(PlatformErrors.AuthzDenied);
                if (json.GetProperty("name").GetString() != "shell.remote.rejected" || json.GetProperty("remoteId").GetString() != "qian.tenancy" ||
                    json.GetProperty("reason").GetString() != "engines.yue" || json.GetProperty("code").GetString() != "INPUT_INVALID" ||
                    json.GetProperty("hostVersion").GetString() != yueVersion || json.GetProperty("requiredVersion").GetString() != "^99.0.0")
                    return Error(PlatformErrors.InputInvalid);
                using var scope = tenant.BeginScope(tenantId);
                var metadata = JsonSerializer.SerializeToElement(new { name = "shell.remote.rejected", remoteId = "qian.tenancy", scope = "tenant:" + tenantId,
                    reason = "engines.yue", code = "INPUT_INVALID", hostVersion = yueVersion, requiredVersion = "^99.0.0", source = "browser-report" });
                var record = new AuditRecord(Guid.NewGuid(), tenantId, "user", identity.FindFirst("sub")!.Value, null,
                    "qian.shell.remote.rejected", "1.0.0", Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(metadata))), "ui", "qian.tenancy",
                    "reported-rejected", ActivityTraceId.CreateRandom().ToString(), DateTimeOffset.UtcNow, metadata);
                await consumer.Context!.Audit().AppendAsync(record, http.RequestAborted);
                return Results.NoContent();
            }
        });
        app.MapGet("/__acceptance/audit", async (HttpContext http) =>
        {
            var identity = await Identity(http, "read", tokens, validation);
            if (identity is null) return Error(PlatformErrors.Unauthenticated);
            var tenantId = identity.FindFirst("tenant_id")!.Value;
            using var scope = tenant.BeginScope(tenantId);
            var audit = consumer.Context!.Audit();
            var verification = await audit.VerifyAsync(http.RequestAborted);
            var entries = await audit.ReadAsync(0, 100, cancellationToken: http.RequestAborted);
            return Results.Json(new { valid = verification.Valid, checkedEntries = verification.CheckedEntries,
                records = entries.Select(e => new { sequence = e.Sequence, contentHash = e.ContentHash, actorId = e.Record.ActorId,
                    tenantId = e.Record.TenantId, capabilityId = e.Record.CapabilityId, payload = e.Record.Payload }) });
        });
    }

    private static async Task<ClaimsIdentity?> Identity(HttpContext http, string purpose, JsonWebTokenHandler tokens, TokenValidationParameters validation)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var header = http.Request.Headers.Authorization;
        if (header.Count != 1 || !header[0]!.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var result = await tokens.ValidateTokenAsync(header[0]![7..], validation);
        if (!result.IsValid || result.ClaimsIdentity.FindFirst("purpose")?.Value != purpose ||
            result.ClaimsIdentity.FindFirst("principal_kind")?.Value != "user" || result.ClaimsIdentity.FindFirst("sub")?.Value != "shell-acceptance-user" ||
            result.ClaimsIdentity.FindFirst("tenant_id")?.Value is not (TenantA or TenantB)) return null;
        return result.ClaimsIdentity;
    }
    private static QianErrorResult Error(ErrorCode code) => QianResults.Error(new QianError(code));
}

internal sealed class AuditConsumer : IQianPlugin
{
    internal static readonly string[] Bindings = ["audit_tenant", "audit_platform"];
    internal IPluginContext? Context { get; private set; }
    public IReadOnlyList<ServiceKey> Inject => [AuditDefinition.Key];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
}

using System.Text.Json;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Lifecycle;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry;

internal sealed partial class RegistryService
{
    public async ValueTask<RegistryScopePatchResult> PutScopePatchAsync(string tenantId, ScopeKey scope, string entryId,
        RegistryScopePatchRequest request, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user); ArgumentNullException.ThrowIfNull(request);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject)
            throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        // 目标只来自路径；普通租户不能靠路径切换上下文，平台主体也不能从委托或服务身份升级为超管。
        if (!Guid.TryParseExact(tenantId, "D", out var tenantGuid) || tenantGuid.ToString("D") != tenantId ||
            scope.Kind != "tenant" || scope.Id != tenantId || principal.Actor is not null ||
            (principal.TenantId is null ? principal.Kind != PrincipalKind.User :
                principal.TenantId != tenantId || _tenant.TenantId != tenantId)) throw NotFound();
        using var target = _tenant.BeginScope(tenantId);
        await AuthorizeAsync(scope, user, cancellationToken, RegistryDefinition.ScopeWritePermission).ConfigureAwait(false);
        // S1-ENT-001 尚未交付 Entitlement Definition/Provider。不能拿 Installed、令牌角色或任意布尔值代替权益判定。
        // 接入真实端口前，租户权限判定即使通过也必须 fail-closed；平台运营只走已审计的超管旁路。
        if (principal.TenantId is not null)
        {
            // R32：只有同时持有诊断读取权限者可获知原因，其他主体仍只看到 404。
            await AuthorizeAsync(scope, user, cancellationToken).ConfigureAwait(false);
            throw new QianErrorException(new QianError(RegistryErrors.NotEntitled,
                new Dictionary<string, string> { ["reason"] = "权益服务不可用，拒绝租户 scope 写入。" }));
        }
        if (string.IsNullOrWhiteSpace(entryId) || entryId.Length > 128 || entryId.Any(char.IsControl)) throw NotFound();
        var patch = request.Patch;
        if (request.Revision < 0 || patch.ValueKind != JsonValueKind.Object ||
            !patch.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != entryId)
            throw Invalid("#/patch", "patch.id 必须与路径条目一致，revision 必须为非负整数。");
        if (System.Text.Encoding.UTF8.GetByteCount(patch.GetRawText()) > 1_048_576)
            throw new QianErrorException(new QianError(PlatformErrors.PayloadTooLarge));
        // 先用同一 patch/v2 解析器校验所有字段；禁用入口不能静默吞掉非法配置、租户字段或未知字段。
        var read = _options.Surfaces.Parse("registry/scope-patches.json", patch.GetRawText());
        if (read.Syntax is null || read.Diagnostics.Any(item => item.Severity == "error") ||
            _options.Surfaces.ValidateSchema("patch", "registry/scope-patches.json", JsonSerializer.SerializeToElement(new[] { read.Syntax.Data })).Any(item => item.Severity == "error") ||
            patch.TryGetProperty("scope", out var declared) && declared.GetString() != "tenant:" + tenantId)
            throw Invalid("#/patch", "patch 不符合 patch/v2 契约或 scope 与路径不一致。");
        if (patch.TryGetProperty("disabled", out var requestedDisable) && requestedDisable.ValueKind == JsonValueKind.True &&
            patch.EnumerateObject().Any(property => property.Name is not ("id" or "scope" or "disabled")))
            throw Invalid("#/patch", "卸载只接受 id、scope 与 disabled，不能同时修改配置或组件引用。");
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.registry", EventActor.User(principal.Subject)));
        MountResult result;
        try
        {
            var lifecycle = _context.Lifecycle();
            result = patch.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True
                ? await lifecycle.UnmountAtRevisionAsync(scope, entryId, request.Revision, principal.Subject, cancellationToken).ConfigureAwait(false)
                : await lifecycle.MountAtRevisionAsync(scope, patch, request.Revision, principal.Subject, cancellationToken).ConfigureAwait(false);
        }
        catch (QianErrorException error) when (error.Error.Code.Code == RegistryErrors.RevisionConflict.Code)
        { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        return new RegistryScopePatchResult(result.Scope, result.EntryId, result.Revision, result.State, result.ReasonCode);
    }
}

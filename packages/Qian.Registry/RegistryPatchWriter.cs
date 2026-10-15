using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry;

// 此入口只完成格式/配置/依赖准入，不代替服务层的真实鉴权、权益和制品信任检查。
// publish 为 true 的写入来自公开挂载 / 卸载，随同一事务把 registry.scope.*.v1 放进 Outbox。
internal sealed class RegistryPatchWriter(PostgresPatchRepository patches, RegistryProfileComposer composer)
{
    internal Task<ScopePatchSnapshot> PutAsync(ScopeKey scope, string entryId, JsonElement patch, int expectedRevision, string actor, CancellationToken ct)
        => PutAsync(scope, entryId, patch, expectedRevision, actor, false, ct);

    internal Task<ScopePatchSnapshot> PutAsync(ScopeKey scope, string entryId, JsonElement patch, int expectedRevision, string actor, bool publish, CancellationToken ct)
        => patches.PutValidatedAsync(scope, entryId, patch, expectedRevision, actor, (rows, token) => Validate(scope, rows, token), publish, ct);

    internal Task<int> RemoveAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken ct)
        => RemoveAsync(scope, entryId, expectedRevision, actor, false, ct);

    internal Task<int> RemoveAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, bool publish, CancellationToken ct)
        => patches.RemoveValidatedAsync(scope, entryId, expectedRevision, actor, (rows, token) => Validate(scope, rows, token), publish, ct);

    private ValueTask Validate(ScopeKey scope, IReadOnlyList<ScopePatchSnapshot> rows, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = composer.Compose(scope, rows);
        if (!result.IsValid) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
        {
            Errors = result.Diagnostics.Where(d => d.Severity == "error").Take(100).Select(d => new QianFieldError(
                RegistryErrors.Invalid, d.Error.Pointer ?? "registry/scope-patches.json#",
                new Dictionary<string, string> { ["reason"] = "候选 scope patch 未通过共用装配校验。" })).ToArray()
        });
        return ValueTask.CompletedTask;
    }
}

using System.Text.Json;
using Qian.Profiles.Abstractions;
using Qian.Runtime;

namespace Qian.Registry;

// 管理服务在权限与归属校验后调用；此类只读取当前租户的数据并复用静态装配规则。
internal sealed class RegistryProfileComposer(PostgresPatchRepository patches, IProfileLoader loader, ProfileLoadOptions options)
{
    internal async Task<ProfileLoadResult> LoadAsync(ScopeKey scope, CancellationToken ct)
        => Compose(scope, await patches.ListAsync(scope, ct).ConfigureAwait(false));

    internal ProfileLoadResult Compose(ScopeKey scope, IReadOnlyList<ScopePatchSnapshot> rows)
    {
        if (options.ScopePatches.Count != 0) throw new InvalidOperationException("平台注册表必须从未合入数据库层的文件装配输入开始。");
        if (rows.Any(row => row.Scope != scope)) throw new InvalidOperationException("scope patch 集合包含其它作用域。");
        return loader.Load(new ProfileLoadOptions
        {
            WorkingDirectory = options.WorkingDirectory, Profile = options.Profile, Patches = options.Patches,
            HomeDirectory = options.HomeDirectory, IncludeHomePatch = options.IncludeHomePatch, ComponentDirectories = options.ComponentDirectories,
            ScopePatches = [new ScopePatchLayer
            {
                Scope = scope, Source = "registry/scope-patches.json",
                Patches = JsonSerializer.SerializeToElement(rows.OrderBy(row => row.EntryId, StringComparer.Ordinal).Select(row => row.Patch))
            }]
        });
    }
}

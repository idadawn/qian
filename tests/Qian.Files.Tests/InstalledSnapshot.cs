using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;

namespace Qian.Files.Tests;

// 只提供预装快照的 Registry 替身：qian.files 只经 GetInstalledComponents 读各组件的 metadata 声明，其余管理面不在本项目的测试范围。
internal sealed class InstalledSnapshotPlugin(IReadOnlyList<InstalledComponentMetadata> installed) : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [];

    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.Provide<IRegistryService>(RegistryDefinition.Key, new InstalledSnapshot(installed));
        return ValueTask.CompletedTask;
    }
}

internal sealed class InstalledSnapshot(IReadOnlyList<InstalledComponentMetadata> installed) : IRegistryService
{
    internal int Reads { get; private set; }

    public IReadOnlyList<InstalledComponentMetadata> GetInstalledComponents() { Reads++; return installed; }
    public ValueTask StartAsync(CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask SynchronizeAsync(ScopeKey scope, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<IReadOnlyList<RegistryScopeEntry>> GetScopeEntriesAsync(ScopeKey scope, ServiceKey key, CancellationToken cancellationToken = default) => throw Unsupported();
    // 租户配置（Tenancy 的 ITenantSettings）经它读合成视图：替身里没有任何条目，读取得到 NOT_FOUND，文件组件回退组件配置的配额。
    public ValueTask<IReadOnlyList<RegistryEntryView>> GetConfigurationViewAsync(ScopeKey scope, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<RegistryEntryView>>([]);
    public ValueTask<IReadOnlyList<RegistryEntryView>> GetScopeViewAsync(ScopeKey scope, ICurrentUser user, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<RegistryEntryView> GetScopeEntryAsync(ScopeKey scope, string entryId, ICurrentUser user, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<RegistryScopePatchResult> PutScopePatchAsync(string tenantId, ScopeKey scope, string entryId, RegistryScopePatchRequest request, ICurrentUser user,
        CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<RegistryArtifactPermissions> GetPluginPermissionsAsync(string pluginId, string version, ICurrentUser user, CancellationToken cancellationToken = default) =>
        throw Unsupported();
    public ValueTask<RegistryArtifactPermissions?> GetArtifactPermissionsAsync(string digest, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<RegistryArtifactEntry> SubmitArtifactAsync(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, string digest, string actor,
        CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<ScopePatchSnapshot> MountAsync(ScopeKey scope, JsonElement entry, int expectedRevision, string actor, CancellationToken cancellationToken = default) =>
        throw Unsupported();
    public ValueTask<int> UnmountAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<ScopePatchSnapshot?> GetScopePatchAsync(ScopeKey scope, string entryId, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<IReadOnlyList<ScopePatchSnapshot>> GetScopePatchesAsync(ScopeKey scope, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<int> RestoreScopePatchAsync(ScopeKey scope, string entryId, JsonElement? previous, int expectedRevision, string actor,
        CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask<RegistryArtifactEntry> RecordArtifactStateAsync(string digest, string state, string signedBy, CancellationToken cancellationToken = default) =>
        throw Unsupported();
    public ValueTask<IReadOnlyList<RegistryScopeDiagnostic>> GetScopeDiagnosticsAsync(ScopeKey scope, CancellationToken cancellationToken = default) => throw Unsupported();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // 声明了上传策略的组件快照：metadata 按组件内路径为键，与 Registry 绑定真实制品时相同。
    internal static InstalledComponentMetadata Component(string pluginId, string version, object? uploadPolicies) => new(pluginId, version,
        JsonSerializer.SerializeToElement(new { id = pluginId, version }),
        uploadPolicies is null ? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement> { ["metadata/upload-policies.json"] = JsonSerializer.SerializeToElement(uploadPolicies, Web) });

    // 与组件作者手写的 JSON 同形：camelCase、不写 null。
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static NotSupportedException Unsupported() => new("测试替身只提供预装快照。");
}

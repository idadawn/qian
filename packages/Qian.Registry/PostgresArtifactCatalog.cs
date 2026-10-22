using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

internal sealed class RegistryArtifactSnapshot
{
    internal required string Digest { get; init; }
    internal required string State { get; init; }
    internal required ParsedComponent Component { get; init; }
    internal required IReadOnlyList<RegistryArtifactFile> Files { get; init; }
    internal required IReadOnlyDictionary<string, JsonElement> Metadata { get; init; }
    internal required string SubmittedBy { get; init; }
    internal required DateTimeOffset SubmittedAt { get; init; }
    internal JsonElement? SignatureVerification { get; init; }
}

// 供后续已授权的登记/发布服务调用；没有把 JSON 布尔值当作验签结果的入口。
internal sealed class PostgresArtifactCatalog(IDataService data)
{
    internal async Task<RegistryArtifactSnapshot?> GetAsync(string digest, CancellationToken ct)
    {
        if (!RegistryArtifactContent.ValidDigest(digest)) throw RegistryArtifactContent.Invalid("artifact.json#/digest", "制品摘要无效。");
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.PluginArtifacts.AsNoTracking().SingleOrDefaultAsync(row => row.Digest == digest, ct).ConfigureAwait(false);
        return row is null ? null : Snapshot(row);
    }

    internal async Task<RegistryArtifactSnapshot> SubmitAsync(RegistryArtifactContent artifact, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl))
            throw RegistryArtifactContent.Invalid("artifact.json#/submittedBy", "提交主体无效。");
        var version = artifact.Component.Manifest.GetProperty("version").GetString()!;
        if (artifact.Component.Id.Length > 128 || version.Length > 200 || artifact.Component.Profile.Length > 128)
            throw RegistryArtifactContent.Invalid(".qian-plugin/plugin.json#", "制品标识或版本超过存储上限。");
        var existing = await GetAsync(artifact.Digest, ct).ConfigureAwait(false);
        if (existing is not null) return existing;
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var row = new PluginArtifactRow
        {
            Digest = artifact.Digest, PluginId = artifact.Component.Id, Version = version,
            Manifest = artifact.Component.Manifest.GetRawText(), Documents = JsonSerializer.Serialize(artifact.Component.Documents),
            Files = JsonSerializer.Serialize(artifact.Files), Metadata = JsonSerializer.Serialize(artifact.Metadata),
            Profile = artifact.Component.Profile, SubmittedBy = actor, SubmittedAt = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero)
        };
        session.Context.PluginArtifacts.Add(row);
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_plugin_artifacts" })
        { return await GetAsync(artifact.Digest, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("并发登记的制品未找到。"); }
        return Snapshot(row);
    }

    // Lifecycle 的回写：验签结论只记算法与公钥指纹，不保存签名字节或公钥原文。
    internal async Task<RegistryArtifactSnapshot> RecordAsync(string digest, string state, string signedBy, CancellationToken ct)
    {
        if (!RegistryArtifactContent.ValidDigest(digest)) throw RegistryArtifactContent.Invalid("artifact.json#/digest", "制品摘要无效。");
        if (state is not (RegistryArtifactStates.Installed or RegistryArtifactStates.Progressing or RegistryArtifactStates.Healthy))
            throw RegistryArtifactContent.Invalid("artifact.json#/state", "回写状态只能是 Installed、Progressing 或 Healthy。");
        if (!RegistryArtifactContent.ValidDigest(signedBy)) throw RegistryArtifactContent.Invalid("artifact.json#/signedBy", "验签公钥指纹无效。");
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.PluginArtifacts.SingleOrDefaultAsync(row => row.Digest == digest, ct).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        row.State = state;
        row.SignatureVerification = JsonSerializer.Serialize(new { algorithm = "ed25519", keyId = signedBy });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return Snapshot(row);
    }

    // 反向权限读取只要清单快照：按摘要不论状态（供可信平台插件），按组件版本只认 Lifecycle 已回写安装结论的制品，Submitted 候选不作依据。
    // Lifecycle 保证同一组件版本至多安装一个制品；若出现第二个，Single 抛出而不是任选其一。
    internal async Task<(string Digest, string PluginId, string Version, JsonElement Manifest)?> ManifestAsync(string digest, CancellationToken ct)
    {
        if (!RegistryArtifactContent.ValidDigest(digest)) throw RegistryArtifactContent.Invalid("artifact.json#/digest", "制品摘要无效。");
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.PluginArtifacts.AsNoTracking().Where(row => row.Digest == digest)
            .Select(row => new { row.Digest, row.PluginId, row.Version, row.Manifest }).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : (row.Digest, row.PluginId, row.Version, JsonSerializer.Deserialize<JsonElement>(row.Manifest));
    }

    internal async Task<(string Digest, string PluginId, string Version, JsonElement Manifest)?> InstalledManifestAsync(string pluginId, string version, CancellationToken ct)
    {
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.PluginArtifacts.AsNoTracking().Where(row => row.PluginId == pluginId && row.Version == version && row.State != RegistryArtifactStates.Submitted)
            .Select(row => new { row.Digest, row.PluginId, row.Version, row.Manifest }).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : (row.Digest, row.PluginId, row.Version, JsonSerializer.Deserialize<JsonElement>(row.Manifest));
    }

    private static RegistryArtifactSnapshot Snapshot(PluginArtifactRow row) => new()
    {
        Digest = row.Digest, State = row.State,
        Component = new ParsedComponent(row.PluginId, JsonSerializer.Deserialize<JsonElement>(row.Manifest),
            Array.AsReadOnly(JsonSerializer.Deserialize<SurfaceDocument[]>(row.Documents)!)) { Profile = row.Profile },
        Files = Array.AsReadOnly(JsonSerializer.Deserialize<RegistryArtifactFile[]>(row.Files)!), Metadata = new ReadOnlyDictionary<string, JsonElement>(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.Metadata)!),
        SubmittedBy = row.SubmittedBy, SubmittedAt = row.SubmittedAt,
        SignatureVerification = row.SignatureVerification is null ? null : JsonSerializer.Deserialize<JsonElement>(row.SignatureVerification)
    };
}

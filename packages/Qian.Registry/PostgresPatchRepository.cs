using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

// 仅供注册表服务使用的持久化原语；不公开绕过权限、权益或组件配置校验的写入端口。
internal sealed partial class PostgresPatchRepository(IDataService data, ITenantContext tenant, ISurfaceReader reader)
{
    internal const string ChangeChannel = "qian_registry_patch_changed";
    private const string File = "registry/scope-patches.json";

    internal async Task<IReadOnlyList<ScopePatchSnapshot>> ListAsync(ScopeKey scope, CancellationToken ct)
    {
        var owner = Owner(scope);
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var name = ScopeName(scope);
        var rows = await session.Context.ScopePatches.AsNoTracking().Where(row => row.Scope == name && !row.IsDeleted)
            .OrderBy(row => row.EntryId).ToListAsync(ct).ConfigureAwait(false);
        CheckOwner(owner);
        return rows.Select(row => Snapshot(row, scope)).ToArray();
    }

    internal async Task<ScopePatchSnapshot?> GetAsync(ScopeKey scope, string entryId, CancellationToken ct)
    {
        var owner = Owner(scope); CheckEntry(entryId);
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        var name = ScopeName(scope);
        var row = await session.Context.ScopePatches.AsNoTracking().SingleOrDefaultAsync(row => row.Scope == name && row.EntryId == entryId && !row.IsDeleted, ct).ConfigureAwait(false);
        CheckOwner(owner);
        return row is null ? null : Snapshot(row, scope);
    }

    internal Task<ScopePatchSnapshot> PutAsync(ScopeKey scope, string entryId, JsonElement patch, int expectedRevision, string actor, CancellationToken ct)
        => PutCore(scope, entryId, patch, expectedRevision, actor, null, false, ct);

    // publish：公开挂载 / 卸载写入，随行登记领域事件，集成事件经处理器进入同一事务的 Outbox。
    internal Task<ScopePatchSnapshot> PutValidatedAsync(ScopeKey scope, string entryId, JsonElement patch, int expectedRevision, string actor,
        Func<IReadOnlyList<ScopePatchSnapshot>, CancellationToken, ValueTask> validate, bool publish, CancellationToken ct)
        => PutCore(scope, entryId, patch, expectedRevision, actor, validate, publish, ct);

    private async Task<ScopePatchSnapshot> PutCore(ScopeKey scope, string entryId, JsonElement patch, int expectedRevision, string actor,
        Func<IReadOnlyList<ScopePatchSnapshot>, CancellationToken, ValueTask>? validate, bool publish, CancellationToken ct)
    {
        var owner = Owner(scope); CheckEntry(entryId); CheckActor(actor);
        if (expectedRevision < 0) throw Invalid("#/revision", "修订不能为负数。");
        var replacement = Validate(scope, entryId, patch);
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var name = ScopeName(scope);
        if (validate is not null) await LockScope(session.Context, owner, name, ct).ConfigureAwait(false);
        var row = await session.Context.ScopePatches.SingleOrDefaultAsync(row => row.Scope == name && row.EntryId == entryId, ct).ConfigureAwait(false);
        if ((row is null || row.IsDeleted) ? expectedRevision != 0 : row.Revision != expectedRevision) throw Conflict();
        if (row is not null && !row.IsDeleted && SamePatch(row, replacement))
        {
            if (validate is not null) await ValidateCandidate(session.Context, row, scope, validate, ct).ConfigureAwait(false);
            CheckOwner(owner);
            return Snapshot(row, scope);
        }
        if (row is null)
        {
            row = new ScopePatchRow { TenantId = owner, Scope = name, EntryId = entryId };
            session.Context.ScopePatches.Add(row);
        }
        if (row.Revision == int.MaxValue) throw Conflict();
        row.Plugin = replacement.Plugin; row.PluginVersion = replacement.PluginVersion; row.Config = replacement.Config;
        row.Disabled = replacement.Disabled; row.DisabledSpecified = replacement.DisabledSpecified; row.Extensions = replacement.Extensions;
        row.Note = replacement.Note; row.IsDeleted = false;
        row.Revision++; row.UpdatedBy = actor; row.UpdatedAt = DatabaseTime();
        if (validate is not null) await ValidateCandidate(session.Context, row, scope, validate, ct).ConfigureAwait(false);
        await Commit(session, row, transaction, owner, publish ? Change(row) : null, ct).ConfigureAwait(false);
        return Snapshot(row, scope);
    }

    internal Task<int> RemoveAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken ct)
        => RemoveCore(scope, entryId, expectedRevision, actor, null, false, ct);

    internal Task<int> RemoveValidatedAsync(ScopeKey scope, string entryId, int expectedRevision, string actor,
        Func<IReadOnlyList<ScopePatchSnapshot>, CancellationToken, ValueTask> validate, bool publish, CancellationToken ct)
        => RemoveCore(scope, entryId, expectedRevision, actor, validate, publish, ct);

    private async Task<int> RemoveCore(ScopeKey scope, string entryId, int expectedRevision, string actor,
        Func<IReadOnlyList<ScopePatchSnapshot>, CancellationToken, ValueTask>? validate, bool publish, CancellationToken ct)
    {
        var owner = Owner(scope); CheckEntry(entryId); CheckActor(actor);
        if (expectedRevision <= 0) throw Invalid("#/revision", "撤销必须携带当前正整数修订。");
        await using var session = await data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var name = ScopeName(scope);
        if (validate is not null) await LockScope(session.Context, owner, name, ct).ConfigureAwait(false);
        var row = await session.Context.ScopePatches.SingleOrDefaultAsync(row => row.Scope == name && row.EntryId == entryId, ct).ConfigureAwait(false);
        if (row is null || row.IsDeleted || row.Revision != expectedRevision || row.Revision == int.MaxValue) throw Conflict();
        row.IsDeleted = true; row.Revision++; row.UpdatedBy = actor; row.UpdatedAt = DatabaseTime();
        // 撤销后不保留配置内容；墓碑仅保留并发控制及归属信息。
        row.Plugin = null; row.PluginVersion = null; row.Config = null; row.Extensions = null; row.Note = null; row.Disabled = false; row.DisabledSpecified = false;
        if (validate is not null) await ValidateCandidate(session.Context, row, scope, validate, ct).ConfigureAwait(false);
        await Commit(session, row, transaction, owner, publish ? Change(row) : null, ct).ConfigureAwait(false);
        return row.Revision;
    }

    private static Task<int> LockScope(RegistryDbContext db, string owner, string scope, CancellationToken ct)
    {
        // 防止不同条目分别通过准入后共同破坏依赖树；数据库事务锁适用于多副本。
        var identity = JsonSerializer.Serialize(new[] { owner, scope });
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))", ct);
    }

    private static async Task ValidateCandidate(RegistryDbContext db, ScopePatchRow candidate, ScopeKey scope,
        Func<IReadOnlyList<ScopePatchSnapshot>, CancellationToken, ValueTask> validate, CancellationToken ct)
    {
        var rows = await db.ScopePatches.AsNoTracking().Where(row => row.Scope == candidate.Scope && !row.IsDeleted && row.EntryId != candidate.EntryId)
            .OrderBy(row => row.EntryId).ToListAsync(ct).ConfigureAwait(false);
        if (!candidate.IsDeleted) rows.Add(candidate);
        await validate(rows.Select(row => Snapshot(row, scope)).ToArray(), ct).ConfigureAwait(false);
    }

    // 撤销或禁用即卸载；其余写入（新增条目或替换配置）即挂载。
    private static ScopePatchChanged Change(ScopePatchRow row)
    {
        var change = new ScopePatchChanged(row.Scope, row.EntryId, row.Plugin, row.PluginVersion, row.Revision, !row.IsDeleted && !row.Disabled);
        row.Raise(change);
        return change;
    }

    private async Task Commit(IDataSession<RegistryDbContext> session, ScopePatchRow row,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, string owner, ScopePatchChanged? change, CancellationToken ct)
    {
        CheckOwner(owner);
        try
        {
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            // 保存前领域事件已分发；处理器没有执行说明宿主漏装了事件接线，宁可不提交也不丢事件。
            if (change is { Published: false }) throw new InvalidOperationException("注册表变更事件未进入 Outbox：平台宿主须以 AddQianRegistryEvents 登记处理器。");
            // PostgreSQL 仅在事务提交后投递通知；载荷不包含 config、note 或凭据。
            var payload = JsonSerializer.Serialize(new { tenantId = owner, scope = row.Scope, entryId = row.EntryId, revision = row.Revision, removed = row.IsDeleted });
            await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_notify({ChangeChannel}, {payload})", ct).ConfigureAwait(false);
            CheckOwner(owner);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_scope_patches" }) { throw Conflict(); }
    }

    private ScopePatchRow Validate(ScopeKey scope, string entryId, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object) throw Invalid("#/0", "patch 必须为条目对象。");
        var raw = patch.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > 1_048_576) throw new QianErrorException(new QianError(PlatformErrors.PayloadTooLarge));
        var parsed = reader.Parse(File, raw);
        if (parsed.Syntax is null || parsed.Diagnostics.Any(d => d.Severity == "error")) throw FromDiagnostics(parsed.Diagnostics);
        var diagnostics = reader.ValidateSchema("patch", File, JsonSerializer.SerializeToElement(new[] { parsed.Syntax.Data }));
        if (diagnostics.Any(d => d.Severity == "error")) throw FromDiagnostics(diagnostics);
        if (patch.GetProperty("id").GetString() != entryId) throw Invalid("#/0/id", "条目标识与目标路径不一致。");
        if (patch.TryGetProperty("scope", out var declared) && declared.GetString() != ScopeName(scope)) throw Invalid("#/0/scope", "scope 与目标路径不一致。");
        var plugin = patch.TryGetProperty("plugin", out var reference) ? reference.GetString() : null;
        var separator = plugin?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        var row = new ScopePatchRow
        {
            Plugin = separator >= 0 ? plugin![..separator] : plugin,
            PluginVersion = separator >= 0 ? plugin![(separator + 1)..] : null,
            Config = patch.TryGetProperty("config", out var config) ? config.GetRawText() : null,
            DisabledSpecified = patch.TryGetProperty("disabled", out _),
            Disabled = patch.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean(),
            Note = patch.TryGetProperty("note", out var note) ? note.GetString() : null
        };
        var extensions = patch.EnumerateObject().Where(property => property.Name.StartsWith("x-", StringComparison.Ordinal))
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        if (extensions.Count != 0) row.Extensions = JsonSerializer.Serialize(extensions);
        if (row.Plugin?.Length > 128 || row.PluginVersion?.Length > 200 || row.Note?.Length > 4000) throw Invalid("#/0", "条目字段超过存储上限。");
        return row;
    }

    private string Owner(ScopeKey scope)
    {
        if (!tenant.IsMultiTenant || string.IsNullOrWhiteSpace(tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (tenant.TenantId.Length > 200 || tenant.TenantId.Any(char.IsControl)) throw Invalid("#/scope", "租户上下文标识无效。");
        if (scope.Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrEmpty(scope.Id) || scope.Id.Length > 200 || scope.Id.Any(value => char.IsWhiteSpace(value) || value == ':'))
            throw Invalid("#/scope", "数据库 patch 必须属于合法租户、应用或会话 scope。");
        if (scope.Kind == "tenant" && scope.Id != tenant.TenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return tenant.TenantId;
    }
    private void CheckOwner(string owner)
    {
        if (!tenant.IsMultiTenant || tenant.TenantId != owner) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
    }
    private static string ScopeName(ScopeKey scope) => scope.Kind + ":" + scope.Id;
    private static void CheckEntry(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || entryId.Length > 128 || !EntryId().IsMatch(entryId)) throw Invalid("#/id", "条目标识无效。");
    }
    private static void CheckActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw Invalid("#/updatedBy", "调用主体标识无效。");
    }
    // PostgreSQL timestamptz 保存微秒；返回值与重新读取必须使用同一精度。
    private static DateTimeOffset DatabaseTime()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }
    private static bool SamePatch(ScopePatchRow row, ScopePatchRow other) => row.Plugin == other.Plugin && row.PluginVersion == other.PluginVersion &&
        row.Disabled == other.Disabled && row.DisabledSpecified == other.DisabledSpecified && row.Note == other.Note && SameJson(row.Config, other.Config) && SameJson(row.Extensions, other.Extensions);
    private static bool SameJson(string? left, string? right) => left is null ? right is null : right is not null && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(left), JsonSerializer.Deserialize<JsonElement>(right));
    private static ScopePatchSnapshot Snapshot(ScopePatchRow row, ScopeKey scope)
    {
        var patch = new Dictionary<string, object?> { ["id"] = row.EntryId, ["scope"] = row.Scope };
        if (row.Plugin is not null) patch["plugin"] = row.Plugin + (row.PluginVersion is not null ? "@" + row.PluginVersion : "");
        if (row.Config is not null) patch["config"] = JsonSerializer.Deserialize<JsonElement>(row.Config);
        if (row.DisabledSpecified) patch["disabled"] = row.Disabled;
        if (row.Note is not null) patch["note"] = row.Note;
        if (row.Extensions is not null) foreach (var property in JsonSerializer.Deserialize<JsonElement>(row.Extensions).EnumerateObject()) patch[property.Name] = property.Value.Clone();
        return new ScopePatchSnapshot(row.TenantId, scope, row.EntryId, JsonSerializer.SerializeToElement(patch), row.Revision, row.UpdatedBy, row.UpdatedAt);
    }
    private static QianErrorException Conflict() => new(new QianError(RegistryErrors.RevisionConflict) { Pointer = File + "#/revision" });
    private static QianErrorException Invalid(string pointer, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(RegistryErrors.Invalid, File + pointer, new Dictionary<string, string> { ["reason"] = reason })] });
    private static QianErrorException FromDiagnostics(IReadOnlyList<SurfaceDiagnostic> diagnostics) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = diagnostics.Where(d => d.Severity == "error").Take(100).Select(d => new QianFieldError(RegistryErrors.Invalid,
        d.Error.Pointer ?? File + "#", new Dictionary<string, string> { ["reason"] = "patch 未通过共用解析或 Schema 校验。" })).ToArray() });
    [GeneratedRegex("^[a-z][a-z0-9.-]*$", RegexOptions.CultureInvariant)] private static partial Regex EntryId();
}

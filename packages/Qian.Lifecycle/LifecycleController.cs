using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Data;
using Qian.Events;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 平台级修订状态机：Install / Activate / Rollback / Deprecate / Retire 与 service 修订的部署健康。所有写入在 PostgreSQL 事务内持
// 组件级 advisory lock，多副本同样串行；每次状态变更与一条平台审计链条目在同一可串行化事务里提交（见 Audit 分部）。租户级与挂载操作见同名分部文件。
internal sealed partial class LifecycleController(
    IPluginContext context, ITenantContext tenant, LifecycleRuntimeOptions options, LifecycleSettings settings, TrustRoots trust,
    IDeploymentExecutor? executor, TimeProvider time) : IPluginLifecycleController
{
    public async ValueTask<PluginRevisionView> InstallAsync(PluginArtifact artifact, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact); CheckActor(actor);
        // 先判签名是否存在，再核对内容摘要，最后对重新计算的摘要验签：签名不符与内容被改动都不能进入登记。
        if (artifact.Signature is not { } signature) throw new QianErrorException(new QianError(LifecycleErrors.ArtifactUnsigned));
        var content = LifecycleArtifact.Read(artifact);
        using var platform = tenant.BeginScope(null);
        var signedBy = await trust.VerifyAsync(content.Digest, signature, cancellationToken).ConfigureAwait(false);
        var registry = context.Registry();
        var existing = await Revision(row => row.PluginId == content.PluginId && row.ArtifactDigest == content.Digest, cancellationToken).ConfigureAwait(false);
        // 同一摘要重复安装返回已有修订；service 修订上次没有完成部署时接着部署。
        if (existing is not null && (existing.Form != "service" || existing.Workload is not null)) return existing.View();
        if (content.Form == "module" && !registry.GetInstalledComponents().Any(component => component.PluginId == content.PluginId &&
            component.Version == content.Version && component.ArtifactDigest == content.Digest))
            throw new QianErrorException(new QianError(LifecycleErrors.RevisionNotDeployed));
        if (content.Form is "function" or "package") throw Unavailable("runtime.form 为 function / package 的组件待 S2 的执行器。");
        if (content.Form == "service" && executor is null) throw Unavailable("宿主没有配置部署执行器，不能安装 service 组件。");
        // 登记修订须入平台审计链（ADR-009 D9.8）：宿主没有装配审计时在提交候选制品之前就拒绝。
        var audit = RequirePlatformAudit("plugin-revision.install");
        await registry.SubmitArtifactAsync(artifact.Files, content.Digest, actor, cancellationToken).ConfigureAwait(false);
        var saved = existing ?? await PlatformAudited(["revision", content.PluginId], "plugin-revision.install", audit, async session =>
        {
            var db = session.Context;
            var rows = await db.Revisions.Where(row => row.PluginId == content.PluginId).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (rows.SingleOrDefault(row => row.ArtifactDigest == content.Digest) is { } raced) return (raced, (AuditRecord?)null);
            if (rows.Any(row => row.Version == content.Version)) throw Conflict("同一版本已安装为另一个制品。");
            var row = new PluginRevisionRow
            {
                PluginId = content.PluginId, Revision = rows.Count == 0 ? 1 : rows.Max(item => item.Revision) + 1, Version = content.Version,
                ArtifactDigest = content.Digest, Form = content.Form, State = PluginRevisionStates.Installed, SignedBy = signedBy, Required = content.Required,
                Health = content.Form == "service" ? DeploymentHealthStates.Progressing : null, InstalledBy = actor, InstalledAt = Now()
            };
            db.Revisions.Add(row);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return (row, PlatformRecord(InstallAuditCapability, content.PluginId, row.Revision, row.InstalledAt, LifecyclePayload(content.PluginId, actor,
                ("revision", row.Revision), ("version", content.Version), ("artifactDigest", content.Digest), ("form", content.Form), ("signedBy", signedBy)), content.Version));
        }, cancellationToken).ConfigureAwait(false);
        if (saved.Form != "service")
        {
            await registry.RecordArtifactStateAsync(saved.ArtifactDigest, RegistryArtifactStates.Installed, saved.SignedBy, cancellationToken).ConfigureAwait(false);
            return saved.View();
        }
        var handle = await executor!.ApplyAsync(new DeploymentRequest(content.PluginId, content.Version, saved.Revision, content.Digest, content.Image!,
            content.Port, content.HealthPath), cancellationToken).ConfigureAwait(false);
        return (await Observe(saved.PluginId, saved.Revision, handle, cancellationToken).ConfigureAwait(false)).View();
    }

    public async ValueTask<PluginRevisionView> ActivateAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default)
    {
        CheckPlugin(pluginId); CheckActor(actor);
        using var platform = tenant.BeginScope(null);
        var audit = RequirePlatformAudit("plugin-revision.activate");
        await RefreshIfService(pluginId, revision, cancellationToken).ConfigureAwait(false);
        using var origin = Origin();
        return await PlatformAudited(["revision", pluginId], "plugin-revision.activate", audit, async session =>
        {
            var target = await session.Context.Revisions.SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Revision == revision, cancellationToken).ConfigureAwait(false)
                ?? throw NotFound();
            if (target.State == PluginRevisionStates.Active) return (target.View(), (AuditRecord?)null);
            return await Promote(session, target, ActivateAuditCapability, actor, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PluginRevisionView> RollbackAsync(string pluginId, int toRevision, string actor, CancellationToken cancellationToken = default)
    {
        CheckPlugin(pluginId); CheckActor(actor);
        using var platform = tenant.BeginScope(null);
        var audit = RequirePlatformAudit("plugin-revision.rollback");
        await RefreshIfService(pluginId, toRevision, cancellationToken).ConfigureAwait(false);
        using var origin = Origin();
        return await PlatformAudited(["revision", pluginId], "plugin-revision.rollback", audit, async session =>
        {
            var rows = await session.Context.Revisions.Where(row => row.PluginId == pluginId).ToListAsync(cancellationToken).ConfigureAwait(false);
            var target = rows.SingleOrDefault(row => row.Revision == toRevision) ?? throw NotFound();
            var active = rows.SingleOrDefault(row => row.State == PluginRevisionStates.Active) ?? throw Conflict("组件没有激活的修订，不能回切。");
            if (toRevision >= active.Revision) throw Conflict("回切目标必须早于当前激活的修订。");
            return await Promote(session, target, RollbackAuditCapability, actor, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<PluginRevisionView> DeprecateAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default) =>
        Transition(pluginId, revision, actor, PluginRevisionStates.Deprecated, "plugin-revision.deprecate", DeprecateAuditCapability, cancellationToken);

    public ValueTask<PluginRevisionView> RetireAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default) =>
        Transition(pluginId, revision, actor, PluginRevisionStates.Retired, "plugin-revision.retire", RetireAuditCapability, cancellationToken);

    public async ValueTask<PluginRevisionView> RefreshDeploymentAsync(string pluginId, int revision, CancellationToken cancellationToken = default)
    {
        CheckPlugin(pluginId);
        using var platform = tenant.BeginScope(null);
        var row = await Revision(item => item.PluginId == pluginId && item.Revision == revision, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
        if (row.Form != "service") throw Conflict("只有 service 修订有部署健康。");
        if (executor is null) throw Unavailable("宿主没有配置部署执行器。");
        if (row.Workload is null || row.DeploymentReference is null || row.ServicePort is null || row.HealthPath is null) throw Conflict("修订尚未提交部署，请重新安装同一制品。");
        var handle = new DeploymentHandle(executor.Name, row.PluginId, row.Version, row.Workload, row.DeploymentReference, row.ServicePort.Value, row.HealthPath);
        return (await Observe(pluginId, revision, handle, cancellationToken).ConfigureAwait(false)).View();
    }

    public async ValueTask<IReadOnlyList<PluginRevisionView>> GetRevisionsAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        CheckPlugin(pluginId);
        using var platform = tenant.BeginScope(null);
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(cancellationToken).ConfigureAwait(false);
        var rows = await session.Context.Revisions.AsNoTracking().Where(row => row.PluginId == pluginId).OrderBy(row => row.Revision).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(row => row.View()).ToArray();
    }

    // 激活与回切共用：原 Active 先降回 Installed 并单独保存（部分唯一索引逐条检查），再提升目标并随同一事务发出 activated 事件；
    // 返回的平台链记录由调用方挂进同一事务追加（激活与回切能力不同，载荷都带此前的 Active 修订）。
    private async Task<(PluginRevisionView View, AuditRecord? Record)> Promote(IDataSession<LifecycleDbContext> session, PluginRevisionRow target, string capability, string actor, CancellationToken ct)
    {
        if (target.State == PluginRevisionStates.Retired) throw Conflict("修订已退役，不能激活或回切。");
        if (target.Form == "service" && target.Health != DeploymentHealthStates.Healthy) throw Conflict("service 修订尚未健康（工作负载状态与 /health 未同时满足）。");
        var previous = await session.Context.Revisions.SingleOrDefaultAsync(row => row.PluginId == target.PluginId && row.State == PluginRevisionStates.Active, ct).ConfigureAwait(false);
        if (previous is not null)
        {
            previous.State = PluginRevisionStates.Installed; previous.RowVersion++;
            await Save(session, null, ct).ConfigureAwait(false);
        }
        var now = Now();
        target.State = PluginRevisionStates.Active; target.ActivatedAt = now; target.RowVersion++;
        var change = new RevisionActivated(target.PluginId, target.Revision, target.Version, previous?.Revision);
        target.Raise(change);
        await Save(session, change, ct).ConfigureAwait(false);
        return (target.View(), PlatformRecord(capability, target.PluginId, target.Revision, now, LifecyclePayload(target.PluginId, actor,
            ("revision", target.Revision), ("version", target.Version), ("previousRevision", previous?.Revision)), target.Version));
    }

    private async ValueTask<PluginRevisionView> Transition(string pluginId, int revision, string actor, string state, string operation, string capability, CancellationToken ct)
    {
        CheckPlugin(pluginId); CheckActor(actor);
        using var platform = tenant.BeginScope(null);
        var audit = RequirePlatformAudit(operation);
        return await PlatformAudited(["revision", pluginId], operation, audit, async session =>
        {
            var row = await session.Context.Revisions.SingleOrDefaultAsync(item => item.PluginId == pluginId && item.Revision == revision, ct).ConfigureAwait(false) ?? throw NotFound();
            if (row.State == state) return (row.View(), (AuditRecord?)null);
            if (row.State == PluginRevisionStates.Active) throw Conflict("Active 修订不能弃用或退役，请先激活另一个修订。");
            if (row.State == PluginRevisionStates.Retired) throw Conflict("修订已退役。");
            var from = row.State;
            row.State = state; row.RowVersion++;
            await Save(session, null, ct).ConfigureAwait(false);
            return (row.View(), PlatformRecord(capability, pluginId, revision, Now(), LifecyclePayload(pluginId, actor,
                ("revision", revision), ("version", row.Version), ("fromState", from), ("toState", state)), row.Version));
        }, ct).ConfigureAwait(false);
    }

    // 激活 / 回切 service 修订前重新观测一次，以当前的双条件为准，而不是安装时的旧结论。
    private async Task RefreshIfService(string pluginId, int revision, CancellationToken ct)
    {
        var row = await Revision(item => item.PluginId == pluginId && item.Revision == revision, ct).ConfigureAwait(false);
        if (row is { Form: "service", Workload: not null } && executor is not null) await RefreshDeploymentAsync(pluginId, revision, ct).ConfigureAwait(false);
    }

    // 观测结论写回修订（Health）与 Registry 的制品目录记录（PRD 7.2.1「双条件回写注册表」）。
    private async Task<PluginRevisionRow> Observe(string pluginId, int revision, DeploymentHandle handle, CancellationToken ct)
    {
        var health = await executor!.ObserveAsync(handle, ct).ConfigureAwait(false);
        var row = await Locked(["revision", pluginId], async session =>
        {
            var row = await session.Context.Revisions.SingleAsync(item => item.PluginId == pluginId && item.Revision == revision, ct).ConfigureAwait(false);
            row.Workload = handle.Workload; row.DeploymentReference = handle.Reference; row.ServicePort = handle.Port; row.HealthPath = handle.HealthPath;
            row.Health = health.Healthy ? DeploymentHealthStates.Healthy : DeploymentHealthStates.Progressing;
            row.HealthDetail = health.Detail.Length <= 500 ? health.Detail : health.Detail[..500]; row.HealthCheckedAt = Now(); row.RowVersion++;
            await Save(session, null, ct).ConfigureAwait(false);
            return row;
        }, ct).ConfigureAwait(false);
        await context.Registry().RecordArtifactStateAsync(row.ArtifactDigest, health.Healthy ? RegistryArtifactStates.Healthy : RegistryArtifactStates.Progressing,
            row.SignedBy, ct).ConfigureAwait(false);
        return row;
    }

    private async Task<PluginRevisionRow?> Revision(System.Linq.Expressions.Expression<Func<PluginRevisionRow, bool>> predicate, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        return await session.Context.Revisions.AsNoTracking().SingleOrDefaultAsync(predicate, ct).ConfigureAwait(false);
    }

    // 一次事务 + 事务级 advisory lock；锁键与租户、组件绑定，多副本同样串行。
    private Task<T> Locked<T>(string[] key, Func<IDataSession<LifecycleDbContext>, Task<T>> work, CancellationToken ct) =>
        Locked(key, IsolationLevel.Unspecified, work, ct);

    // 与审计同事务写入的步骤取可串行化隔离（审计端口只接受这种事务）；冲突由调用方换新会话重做整个步骤。
    private async Task<T> Locked<T>(string[] key, IsolationLevel isolation, Func<IDataSession<LifecycleDbContext>, Task<T>> work, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(isolation, ct).ConfigureAwait(false);
        var identity = JsonSerializer.Serialize(key.Prepend("qian.lifecycle"));
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))", ct).ConfigureAwait(false);
        var result = await work(session).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }

    // 领域事件在保存时分发；处理器没有执行说明宿主漏装了事件接线，宁可不提交也不丢事件。
    private static async Task Save(IDataSession<LifecycleDbContext> session, LifecycleChange? change, CancellationToken ct)
    {
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw new QianErrorException(new QianError(LifecycleErrors.InProgress)); }
        if (change is { Published: false }) throw new InvalidOperationException("生命周期事件未进入 Outbox：平台宿主须以 AddQianLifecycleEvents 登记处理器。");
    }

    // 集成事件来源：producer 为本组件；actor 与关联沿用调用方（运营工具、宿主管理面）打开的事件作用域，没有时以本组件服务主体发布。
    private static IDisposable Origin(string producer = "qian.lifecycle")
    {
        var outer = EventEnvelopeScope.Current;
        return EventEnvelopeScope.Begin(new EventOrigin(producer, outer?.Actor ?? EventActor.Service("qian.lifecycle"))
        { CorrelationId = outer?.CorrelationId, CausationId = outer?.CausationId, TraceParent = outer?.TraceParent });
    }

    // 钩子幂等键：(tenant, plugin, hook, toVersion) 的 SHA-256 截成 RFC 9562 第 8 版 UUID，重试时不变。
    internal static Guid IdempotencyKey(string tenantId, string pluginId, string hook, string toVersion)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { tenantId, pluginId, hook, toVersion })));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    // PostgreSQL timestamptz 保存微秒；返回值与重新读取使用同一精度。
    private DateTimeOffset Now()
    {
        var now = time.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }

    private static void CheckPlugin(string pluginId)
    {
        if (string.IsNullOrEmpty(pluginId) || pluginId.Length > 128 || !LifecycleArtifact.PluginIdPattern().IsMatch(pluginId))
            throw LifecycleArtifact.Invalid("#/pluginId", "组件标识无效。");
    }

    private static void CheckActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw LifecycleArtifact.Invalid("#/actor", "操作者标识无效。");
    }

    private static void CheckVersion(string version, string pointer)
    {
        if (string.IsNullOrEmpty(version) || version.Length > 200 || !SemanticVersioning.Version.TryParse(version, out _))
            throw LifecycleArtifact.Invalid(pointer, "版本必须是语义化版本。");
    }

    // 租户标识只接受小写规范 UUID，与 Tenancy 和事件信封的口径一致。
    private static void CheckTenant(string tenantId)
    {
        if (!Guid.TryParseExact(tenantId, "D", out var id) || id == Guid.Empty || id.ToString("D") != tenantId)
            throw LifecycleArtifact.Invalid("#/tenantId", "租户标识必须是小写规范 UUID。");
    }

    private static QianErrorException NotFound() => new(new QianError(PlatformErrors.NotFound));
    private static QianErrorException Conflict(string reason) => new(new QianError(LifecycleErrors.StateConflict, new Dictionary<string, string> { ["reason"] = reason }));
    private static QianErrorException Unavailable(string reason) => GitOpsDeploymentExecutor.Unavailable(reason);
}

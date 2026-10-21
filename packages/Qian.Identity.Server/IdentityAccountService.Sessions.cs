using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Identity.Server;

// 会话中心。吊销分两段：
//   1. 即时生效：写 sessions.revoked_at（与引起它的操作同事务）。此后账号 Cookie、授权码、refresh 与 userinfo 的校验都读库，立刻拒绝。
//   2. 旁路处理（ProcessRevocationsAsync，后台处理器驱动，多副本用 SKIP LOCKED 分担）：吊销协议授权族与令牌、登记后通道登出投递、
//      清 Redis 索引并写墓碑，最后在会话所属租户的作用域内把集成事件与「已处理」标记写进同一事务。
// 第二段全部幂等，任何一步失败都会在下一轮重试；事件因此每个会话恰好一次，且不受「吊销发生在哪个租户作用域」的限制。
internal sealed partial class IdentityAccountService
{
    private readonly ConcurrentDictionary<Guid, (int Attempts, long RetryAt)> _deferred = new();
    private readonly SemaphoreSlim _background = new(1, 1);
    private volatile bool _closed;

    /// <summary>租户状态对账的间隔（来自会话策略）。</summary>
    internal TimeSpan ReconcileInterval => _account.Session.ReconcileInterval;

    // ---- 后台工作的生命周期 ----

    // 后台工作（吊销旁路、租户对账）互斥运行，并在组件卸载时先排空：卸载紧接着会释放 Data 的会话，
    // 还握着会话的后台工作不能悬在半空——并发释放会让正在执行的命令撞上 Npgsql 的「连接忙」，甚至让卸载一直等下去。
    // 关闭之后新来的后台工作直接返回；已有的做完才放行卸载。
    internal async ValueTask CloseAsync()
    {
        _closed = true;
        await _background.WaitAsync().ConfigureAwait(false);
        _background.Release();
    }

    /// <summary>释放后台互斥量；组件卸载时在 <see cref="CloseAsync"/> 排空之后调用。</summary>
    public void Dispose() => _background.Dispose();

    // ---- 吊销的来源 ----

    // 谁在吊销：调用方（管理接口、租户运营）已打开的事件作用域优先，否则以本组件的服务主体。
    private static string RevokedByActor()
    {
        if (EventEnvelopeScope.Current?.Actor is not { } actor) return "service:qian.identity";
        return (actor.Kind switch { EventActorKind.User => "user:", EventActorKind.Agent => "agent:", _ => "service:" }) + actor.Id;
    }

    private static EventActor ActorOf(string? revokedBy)
    {
        var split = revokedBy?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        if (revokedBy is not null && split > 0 && split < revokedBy.Length - 1)
        {
            var id = revokedBy[(split + 1)..];
            switch (revokedBy[..split])
            {
                case "user": return EventActor.User(id);
                case "agent": return EventActor.Agent(id);
                case "service": return EventActor.Service(id);
            }
        }
        return EventActor.Service("qian.identity");
    }

    // 某 Person 当前有效的会话行，按建立时间升序。数据库是事实来源；不限租户的全量读取顺带修复 Redis 索引。
    private async Task<List<SessionRow>> LiveSessionsAsync(Guid personId, Guid? tenantId, CancellationToken ct)
    {
        var now = Now; var tenant = tenantId?.ToString("D");
        List<SessionRow> rows;
        using (TenantScope(null))
        {
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            rows = await session.Context.Sessions.AsNoTracking()
                .Where(s => s.PersonId == personId && s.RevokedAt == null && s.AbsoluteExpiresAt > now && (tenant == null || s.TenantId == tenant))
                .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync(ct).ConfigureAwait(false);
        }
        if (tenant is null) await _index.HealAsync(personId, [.. rows.Select(s => (s.Id, s.AbsoluteExpiresAt))], now, ct).ConfigureAwait(false);
        return rows;
    }

    // 建立新会话时的并发控制，在建会话的同一事务里执行（调用方已锁定 Person 行，所以同一人的并发登录串行，上限不会被击穿）。
    // 先按单端模式踢掉同端的旧会话，再按并发上限踢掉最早建立的会话，给新会话腾出名额；返回被踢的会话数。
    private async Task<int> EvictForNewSessionAsync(IdentityDbContext db, Guid personId, string deviceType, DateTimeOffset now, CancellationToken ct)
    {
        var live = await IdentitySql.QueryAsync(db, $"""
            SELECT id, device_type FROM {IdentitySql.Sessions}
            WHERE person_id = @person AND revoked_at IS NULL AND absolute_expires_at > @now ORDER BY created_at, id
            """, r => (Id: r.GetGuid(0), Device: r.GetString(1)), ct, ("person", personId), ("now", now)).ConfigureAwait(false);
        var policy = _account.Session;
        var sameDevice = policy.SingleSessionPerDeviceType ? live.Where(s => s.Device == deviceType).Select(s => s.Id).ToArray() : [];
        var kept = live.Where(s => !sameDevice.Contains(s.Id)).Select(s => s.Id).ToList();
        var overflow = Math.Max(0, kept.Count + 1 - policy.MaxConcurrentSessions);
        var oldest = kept.Take(overflow).ToArray();
        var evicted = 0;
        if (sameDevice.Length > 0) evicted += await EvictAsync(db, sameDevice, "single_device", now, ct).ConfigureAwait(false);
        if (oldest.Length > 0) evicted += await EvictAsync(db, oldest, "concurrency_limit", now, ct).ConfigureAwait(false);
        return evicted;
    }

    private static Task<int> EvictAsync(IdentityDbContext db, Guid[] sessionIds, string reason, DateTimeOffset now, CancellationToken ct) =>
        IdentitySql.ExecuteAsync(db, $"""
            UPDATE {IdentitySql.Sessions} SET revoked_at = @now, revoke_reason = @reason, revoked_by = @by WHERE id = ANY(@ids) AND revoked_at IS NULL
            """, ct, ("now", now), ("reason", reason), ("by", RevokedByActor()), ("ids", sessionIds));

    /// <summary>租户不可用（冻结、到期、移除）：吊销该租户的全部登录会话。</summary>
    internal async Task<int> RevokeTenantSessionsAsync(Guid tenantId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 32) throw Error(PlatformErrors.InputInvalid);
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var revoked = await IdentitySql.ExecuteAsync(session.Context, $"""
            UPDATE {IdentitySql.Sessions} SET revoked_at = @now, revoke_reason = @reason, revoked_by = @by WHERE tenant_id = @tenant AND revoked_at IS NULL
            """, ct, ("now", Now), ("reason", reason), ("by", RevokedByActor()), ("tenant", tenantId.ToString("D"))).ConfigureAwait(false);
        if (revoked > 0) _signal.Notify();
        return revoked;
    }

    /// <summary>
    /// 核对仍有有效会话的租户在目录里的状态：不是 Active / Trial（含目录里已不存在）的租户，其会话全部吊销。
    /// 这是 <c>tenant.suspended.v1</c> / <c>tenant.expired.v1</c> 订阅之外的兜底——事件丢失、延迟或消费端尚未接线时，
    /// 租户冻结或到期后的会话仍会在一个对账间隔（≤ 30 秒）内失效。
    /// </summary>
    internal async Task<int> ReconcileTenantsAsync(CancellationToken ct)
    {
        if (_closed) return 0;
        await _background.WaitAsync(ct).ConfigureAwait(false);
        try { return _closed ? 0 : await ReconcileCoreAsync(ct).ConfigureAwait(false); }
        finally { _background.Release(); }
    }

    private async Task<int> ReconcileCoreAsync(CancellationToken ct)
    {
        var now = Now;
        List<string> tenants;
        using (TenantScope(null))
        {
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            tenants = await IdentitySql.QueryAsync(session.Context, $"""
                SELECT DISTINCT tenant_id FROM {IdentitySql.Sessions} WHERE revoked_at IS NULL AND tenant_id IS NOT NULL AND absolute_expires_at > @now
                """, r => r.GetString(0), ct, ("now", now)).ConfigureAwait(false);
        }
        var revoked = 0;
        foreach (var raw in tenants)
        {
            if (!Guid.TryParseExact(raw, "D", out var tenantId)) continue;
            var tenant = await Directory.GetByIdAsync(tenantId, ct).ConfigureAwait(false);
            if (tenant is { Status: TenantStatus.Active or TenantStatus.Trial }) continue;
            revoked += await RevokeTenantSessionsAsync(tenantId, tenant is null ? "tenant_missing" : "tenant_" + tenant.Status.ToString().ToLowerInvariant(), ct).ConfigureAwait(false);
        }
        return revoked;
    }

    // ---- 旁路处理 ----

    private sealed record PendingRevocation(Guid Id, Guid PersonId, Guid? MembershipId, Guid? TenantId, string Reason, string? RevokedBy, DateTimeOffset ExpiresAt);

    /// <summary>处理待处理的吊销旁路，最多 <paramref name="max"/> 个会话；返回本次处理完成的数量。多副本安全。</summary>
    internal async Task<int> ProcessRevocationsAsync(int max, CancellationToken ct)
    {
        if (_closed) return 0;
        await _background.WaitAsync(ct).ConfigureAwait(false);
        try { return _closed ? 0 : await ProcessCoreAsync(max, ct).ConfigureAwait(false); }
        finally { _background.Release(); }
    }

    private async Task<int> ProcessCoreAsync(int max, CancellationToken ct)
    {
        var tick = Environment.TickCount64;
        var skipped = _deferred.Count(item => item.Value.RetryAt > tick);
        List<PendingRevocation> candidates;
        using (TenantScope(null))
        {
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            candidates = await IdentitySql.QueryAsync(session.Context, $"""
                SELECT id, person_id, membership_id, tenant_id, revoke_reason, revoked_by, absolute_expires_at FROM {IdentitySql.Sessions}
                WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL ORDER BY revoked_at, id LIMIT @max
                """, r => new PendingRevocation(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2),
                    r.IsDBNull(3) ? null : Guid.ParseExact(r.GetString(3), "D"), r.IsDBNull(4) ? "revoked" : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
                    r.GetFieldValue<DateTimeOffset>(6)), ct, ("max", max + skipped)).ConfigureAwait(false);
        }
        var done = 0;
        foreach (var item in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (_deferred.TryGetValue(item.Id, out var deferred) && deferred.RetryAt > tick) continue;
            if (done >= max) break;
            try
            {
                if (await ProcessOneAsync(item, ct).ConfigureAwait(false)) done++;
                _deferred.TryRemove(item.Id, out _);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 吊销早已生效，旁路稍后重试；失败的会话按指数退避让路，不挡住后面的会话，也不刷屏。
                var attempts = _deferred.TryGetValue(item.Id, out var previous) ? previous.Attempts + 1 : 1;
                _deferred[item.Id] = (attempts, Environment.TickCount64 + Math.Min(60, 1L << Math.Min(attempts, 6)) * 1000);
                RevocationDeferred(_logger, error.GetType().Name, attempts);
            }
        }
        return done;
    }

    private async Task<bool> ProcessOneAsync(PendingRevocation item, CancellationToken ct)
    {
        // 幂等的外部收尾先做：协议授权族、令牌与后通道登出投递；Redis 索引与墓碑。
        await using (var scope = _ctx.Services.CreateAsyncScope())
            await IdentityProtocolSessions.RevokeAsync(scope.ServiceProvider, _options, item.PersonId.ToString("D"), item.Id.ToString("D"), ct).ConfigureAwait(false);
        await _index.ForgetAsync(item.PersonId, [(item.Id, item.ExpiresAt)], Now, ct).ConfigureAwait(false);

        // 事件与完成标记同事务，并在会话所属租户的作用域内入队；平台会话（无租户）在无租户作用域内入队。
        using var scopeTenant = TenantScope(item.TenantId); using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.identity", ActorOf(item.RevokedBy)));
        await using var data = await OpenAsync(ct).ConfigureAwait(false);
        var db = data.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var claimed = await IdentitySql.QueryAsync(db, $"SELECT id FROM {IdentitySql.Sessions} WHERE id = @id AND revocation_processed_at IS NULL FOR UPDATE SKIP LOCKED",
            r => r.GetGuid(0), ct, ("id", item.Id)).ConfigureAwait(false);
        if (claimed.Count == 0) return false;
        var person = await db.Persons.SingleAsync(p => p.Id == item.PersonId, ct).ConfigureAwait(false);
        // 事件里的修订与令牌一致：Person 修订 + 成员修订。
        var revision = person.IdentityRevision;
        if (item.MembershipId is { } membershipId)
            revision += await db.Memberships.AsNoTracking().Where(m => m.Id == membershipId).Select(m => m.IdentityRevision).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        await IdentitySql.ExecuteAsync(db, $"UPDATE {IdentitySql.Sessions} SET revocation_processed_at = @now WHERE id = @id", ct, ("now", Now), ("id", item.Id)).ConfigureAwait(false);
        IdentityIntegrationEvent change = item.Reason == "logout"
            ? new IdentityLogoutEvent(person.Id, revision, item.Id, item.Reason, item.MembershipId)
            : new IdentitySessionRevokedEvent(person.Id, revision, item.Id, item.Reason, item.MembershipId);
        await SaveAsync(data, person, change, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "会话吊销的旁路处理失败，将退避后重试；ErrorType={ErrorType}，已失败 {Attempts} 次")]
    private static partial void RevocationDeferred(ILogger logger, string errorType, int attempts);

    // ---- 修订 ----

    public async ValueTask<long> BumpIdentityRevisionAsync(Guid personId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 64) throw IdentifierRules.Invalid("#/reason");
        var tenantId = RequireTenant();
        using var scope = TenantScope(tenantId);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var db = session.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // 行级安全把这条 UPDATE 限在当前租户：同一 Person 在别的租户的成员身份不受影响。
        var member = await IdentitySql.QueryAsync(db, $"""
            UPDATE {IdentitySql.Memberships} SET identity_revision = identity_revision + 1, updated_at = now()
            WHERE person_id = @person AND status <> 'purged' RETURNING identity_revision
            """, r => r.GetInt64(0), cancellationToken, ("person", personId)).ConfigureAwait(false);
        if (member.Count != 1) throw Error(PlatformErrors.NotFound);
        var revision = await db.Persons.AsNoTracking().Where(p => p.Id == personId).Select(p => p.IdentityRevision).SingleAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return revision + member[0];
    }

    // ---- 管理面 ----

    /// <summary>已校验的调用者：谁、在哪个身份下、有什么管理资格。平台资格只在平台身份的会话下生效，租户身份下不继承。</summary>
    internal sealed record SessionCaller(Guid PersonId, Guid SessionId, Guid? TenantId, Guid? MembershipId, bool Platform, bool TenantAdministrator);

    internal sealed record SessionView(Guid Id, Guid PersonId, Guid? MembershipId, Guid? TenantId, string ClientId, string DeviceType, string? RemoteAddress, string? UserAgent,
        DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, DateTimeOffset ExpiresAt);

    internal sealed record OnlineUserView(Guid PersonId, string DisplayName, Guid? TenantId, int Sessions, IReadOnlyList<string> DeviceTypes, DateTimeOffset LastSeenAt);

    private static SessionView ToView(SessionRow s) =>
        new(s.Id, s.PersonId, s.MembershipId, Guid.TryParseExact(s.TenantId, "D", out var tenant) ? tenant : null, s.ClientId, s.DeviceType, s.RemoteAddress, s.UserAgent,
            s.CreatedAt, s.LastSeenAt, s.AbsoluteExpiresAt);

    /// <summary>把账号 Cookie 里的会话解析成调用者；会话已失效、账号 / 成员 / 租户不可用时返回 null。</summary>
    internal async Task<SessionCaller?> ResolveCallerAsync(AccountSession current, CancellationToken ct)
    {
        var validation = await ValidateAsync(current.SessionId, current.PersonId, current.MembershipId, ct).ConfigureAwait(false);
        if (validation is null) return null;
        var platform = false; var administrator = false;
        if (validation.TenantId is null)
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            platform = await session.Context.Persons.AsNoTracking().AnyAsync(p => p.Id == current.PersonId && p.PlatformAccess, ct).ConfigureAwait(false);
        }
        else if (validation.MembershipId is { } membershipId)
        {
            using var scope = TenantScope(validation.TenantId);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            administrator = await session.Context.Memberships.AsNoTracking().AnyAsync(m => m.Id == membershipId && m.IsTenantAdministrator, ct).ConfigureAwait(false);
        }
        return new SessionCaller(current.PersonId, current.SessionId, validation.TenantId, validation.MembershipId, platform, administrator);
    }

    /// <summary>某 Person 的有效会话视图；<paramref name="tenantId"/> 非空时只含该租户内的会话（租户管理员的视角）。</summary>
    internal async Task<IReadOnlyList<SessionView>> ListViewsAsync(Guid personId, Guid? tenantId, CancellationToken ct) =>
        [.. (await LiveSessionsAsync(personId, tenantId, ct).ConfigureAwait(false)).Select(ToView)];

    internal async Task<SessionView?> FindViewAsync(Guid sessionId, CancellationToken ct)
    {
        var now = Now;
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var row = await session.Context.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && s.RevokedAt == null && s.AbsoluteExpiresAt > now, ct).ConfigureAwait(false);
        return row is null ? null : ToView(row);
    }

    /// <summary>管理侧吊销：指定会话或某 Person 的全部会话（可限定在一个租户内）。actor 写进事件信封。返回吊销的会话数。</summary>
    internal async Task<int> RevokeForAsync(Guid actorPersonId, Guid personId, Guid? sessionId, Guid? tenantId, string reason, CancellationToken ct)
    {
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.identity", EventActor.User(actorPersonId.ToString("D"))));
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var revoked = await RevokeSessionRowsAsync(session.Context, personId, sessionId, null, reason, Now, ct, tenantId).ConfigureAwait(false);
        if (revoked > 0) _signal.Notify();
        return revoked;
    }

    /// <summary>在线用户：窗口内有活动的有效会话，按 Person × 租户聚合。<paramref name="tenantId"/> 为空时是平台视角（含平台身份会话）。</summary>
    internal async Task<IReadOnlyList<OnlineUserView>> OnlineUsersAsync(Guid? tenantId, int limit, int offset, CancellationToken ct)
    {
        var now = Now;
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await IdentitySql.QueryAsync(session.Context, $"""
            SELECT p.id, p.display_name, s.tenant_id, count(*)::int, array_agg(DISTINCT s.device_type ORDER BY s.device_type), max(s.last_seen_at)
            FROM {IdentitySql.Sessions} s JOIN {IdentitySql.Persons} p ON p.id = s.person_id
            WHERE s.revoked_at IS NULL AND s.absolute_expires_at > @now AND s.last_seen_at >= @since AND (@tenant::text IS NULL OR s.tenant_id = @tenant)
            GROUP BY p.id, p.display_name, s.tenant_id
            ORDER BY max(s.last_seen_at) DESC, p.id, s.tenant_id
            LIMIT @limit OFFSET @offset
            """, r => new OnlineUserView(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : Guid.ParseExact(r.GetString(2), "D"), r.GetInt32(3),
                r.GetFieldValue<string[]>(4), r.GetFieldValue<DateTimeOffset>(5)), ct,
            ("now", now), ("since", now - _account.Session.OnlineWindow), ("tenant", tenantId?.ToString("D")), ("limit", limit), ("offset", offset)).ConfigureAwait(false);
    }
}

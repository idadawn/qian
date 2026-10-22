using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Identity.Server;

/// <summary>
/// 身份 Provider 的账号实现：账号管理、统一登录流水线、成员开户与用户计数共用同一套存储与规则。
/// 每个操作在自己的数据会话和事务里完成；Person 级操作在无租户作用域，Membership 级操作在目标租户作用域，
/// 所以租户表始终受 RLS 约束，从不绕过。
/// </summary>
internal sealed partial class IdentityAccountService : IIdentityService, IIdentityAccounts, ISignInPipeline, ITenantMembershipProvisioner, ITenantUserCounter, IAccountSessionValidator, IDisposable
{
    private readonly IPluginContext _ctx;
    private readonly IdentityServerOptions _options;
    private readonly IdentityAccountOptions _account;
    private readonly Argon2idPasswordHasher _hasher;
    private readonly ITenantContext _tenant;
    private readonly ILogger _logger;
    private readonly ISessionIndex _index;
    private readonly SessionSignal _signal;

    internal IdentityAccountService(IPluginContext ctx, IdentityServerOptions options)
    {
        _ctx = ctx; _options = options; _account = options.Accounts; _account.Validate();
        _hasher = new Argon2idPasswordHasher(_account.Hashing);
        _tenant = ctx.Services.GetRequiredService<ITenantContext>();
        Clock = ctx.Services.GetService<TimeProvider>() ?? TimeProvider.System;
        _logger = ctx.Logger;
        _index = ctx.Services.GetRequiredService<ISessionIndex>();
        _signal = ctx.Services.GetRequiredService<SessionSignal>();
        _factors.Seed(new PasswordFactor(this));
    }

    /// <summary>认证因子注册表：内置 password，其他因子由对应组件注册。</summary>
    internal IAuthenticatorFactorRegistry Factors => _factors;

    /// <summary>测试注入点：强制产生 Person 主键冲突以验证重试，不影响生产。</summary>
    internal Func<Guid> PersonIds { get; set; } = Guid.CreateVersion7;

    /// <summary>测试注入点：推进时间以验证锁定到期、票据过期与会话寿命；生产取宿主的 TimeProvider。</summary>
    internal TimeProvider Clock { get; set; }

    private DateTimeOffset Now => Clock.GetUtcNow();

    private ValueTask<IDataSession<IdentityDbContext>> OpenAsync(CancellationToken ct) => _ctx.Data().OpenAsync<IdentityDbContext>(ct);

    private ITenantDirectory Directory => _ctx.Tenancy();

    private IDisposable TenantScope(Guid? tenantId) => _tenant.BeginScope(tenantId?.ToString("D"));

    // 当前已验证的租户上下文；Membership 级操作不接受请求自带的租户。
    private Guid RequireTenant() => Guid.TryParseExact(_tenant.TenantId, "D", out var id) && id != Guid.Empty
        ? id : throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));

    private static QianErrorException Error(ErrorCode code, IReadOnlyDictionary<string, string>? args = null) => new(new QianError(code, args));

    private static bool IsUniqueViolation(DbUpdateException error) => error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string SidHash(Guid sessionId) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(sessionId.ToByteArray()))[..16];

    // ---- IIdentityService ----

    public Principal? CurrentPrincipal => null;

    // 连接器注册在运行时的 Identity.Connectors 注册表（R38），按当前租户上下文的 scope 解析，见 IdentityAccountService.Connectors.cs。
    public IIdentityConnectorRegistry Connectors => new TenantConnectorView(this);

    public async Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await LoadPersonAsync(session.Context, personId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        using var scope = TenantScope(tenantId);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await session.Context.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == membershipId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToMembership(row);
    }

    public async Task<IReadOnlyList<Abstractions.Session>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var rows = await LiveSessionsAsync(personId, null, cancellationToken).ConfigureAwait(false);
        return rows.Select(s => new Abstractions.Session(s.Id, s.PersonId, s.MembershipId, s.ClientId, s.DeviceType, s.AbsoluteExpiresAt, s.IdentityRevision)).ToArray();
    }

    // 吊销即时生效（写 revoked_at）：此后账号 Cookie、授权码与 refresh 的校验都读库，立刻拒绝。
    // 协议授权族、后通道登出、Redis 索引与集成事件是旁路，由会话中心的后台处理器随后完成，见 IdentityAccountService.Sessions.cs。
    public async Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 32) throw Error(PlatformErrors.InputInvalid);
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var revoked = await RevokeSessionRowsAsync(session.Context, request.PersonId, request.SessionId, null, request.Reason, Now, cancellationToken).ConfigureAwait(false);
        if (revoked > 0) _signal.Notify();
    }

    private static Task<int> RevokeSessionRowsAsync(IdentityDbContext db, Guid personId, Guid? sessionId, Guid? membershipId, string reason, DateTimeOffset now, CancellationToken ct, Guid? tenantId = null) =>
        IdentitySql.ExecuteAsync(db, $"""
            UPDATE {IdentitySql.Sessions} SET revoked_at = @now, revoke_reason = @reason, revoked_by = @by
            WHERE person_id = @person AND revoked_at IS NULL AND (@sid::uuid IS NULL OR id = @sid) AND (@membership::uuid IS NULL OR membership_id = @membership)
              AND (@tenant::text IS NULL OR tenant_id = @tenant)
            """, ct, ("now", now), ("reason", reason), ("by", RevokedByActor()), ("person", personId), ("sid", sessionId), ("membership", membershipId), ("tenant", tenantId?.ToString("D")));

    // ---- ITenantUserCounter ----

    /// <summary>当前租户的真实成员数：含租户管理员，不含具备平台访问资格的 Person，已清除的成员不计。</summary>
    public async ValueTask<int> CountUsersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        using var scope = TenantScope(tenantId);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return await CountMembersAsync(session.Context, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> CountMembersAsync(IdentityDbContext db, CancellationToken ct) =>
        (from member in db.Memberships.AsNoTracking()
         join person in db.Persons.AsNoTracking() on member.PersonId equals person.Id
         where member.Status != AccountStatuses.Purged && !person.PlatformAccess
         select member.Id).CountAsync(ct);

    // ---- 投影 ----

    private static async Task<Person?> LoadPersonAsync(IdentityDbContext db, Guid personId, CancellationToken ct)
    {
        var row = await db.Persons.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);
        if (row is null) return null;
        var identifiers = await db.Identifiers.AsNoTracking().Where(i => i.PersonId == personId).OrderBy(i => i.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        return ToPerson(row, identifiers);
    }

    private static Person ToPerson(PersonRow row, IReadOnlyList<PersonIdentifierRow> identifiers)
    {
        var views = identifiers.Select(i => { var kind = IdentifierRules.ParseKind(i.Kind); return new IdentifierView(kind, IdentifierRules.Mask(kind, i.Value), i.VerifiedAt is not null); }).ToArray();
        string? Masked(IdentifierKind kind) => views.FirstOrDefault(v => v.Kind == kind)?.Masked;
        var username = identifiers.FirstOrDefault(i => i.Kind == "username")?.Value;
        return new Person(row.Id, row.DisplayName, username, Masked(IdentifierKind.Email), Masked(IdentifierKind.Phone), row.IdentityRevision)
        { Status = row.Status, PlatformAccess = row.PlatformAccess, Identifiers = views };
    }

    private static Membership ToMembership(MembershipRow row) =>
        new(row.Id, row.PersonId, Guid.ParseExact(row.TenantId, "D"), row.DisplayName, row.Status, row.IsTenantAdministrator)
        { Account = row.Account, IdentityRevision = row.IdentityRevision };
}

/// <summary>账号会话有效性：账号 Cookie 与 refresh 授权前由协议端点调用，宿主自带账号服务时不装配。</summary>
internal interface IAccountSessionValidator
{
    /// <summary>会话、Person、Membership 与租户都仍可登录时返回当前身份修订，否则 null。</summary>
    Task<AccountValidation?> ValidateAsync(Guid sessionId, Guid personId, Guid? membershipId, CancellationToken ct);
}

/// <summary>校验通过时的最新事实，协议端点用它覆盖 Cookie 里的旧修订。</summary>
internal sealed record AccountValidation(long IdentityRevision, Guid? TenantId, Guid? MembershipId);

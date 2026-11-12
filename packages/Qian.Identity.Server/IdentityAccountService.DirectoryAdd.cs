using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Identity.Server;

// 租户管理员直接新增成员（S1-IDP-017，R145 / R158）。一个事务里完成：解析标识 → 已是成员拒绝 → 账号资格 → 配额 → 新建自然人（需要时）→ 成员
// → directory.member.created.v1 → 初始角色。加锁顺序与成员建立相同：先成员计数锁，再目录锁，然后才写行（见 LockDirectoryAsync）。
internal sealed partial class IdentityAccountService
{
    /// <summary>新成员的初始角色（R158）：由本组件经授权组件的授予端口授予，与开通编排第 07 步、开发种子同一端口。</summary>
    internal const string InitialMemberRole = "tenant.user";

    // 身份组件给出的密码策略错误指向改密请求的字段，换成新增成员请求的字段。
    private const string InitialPasswordPointer = "#/initial_password";

    public async ValueTask<DirectoryMemberAdded> AddMemberAsync(DirectoryMemberInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = RequireTenant();
        var name = request.DisplayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || name.Any(char.IsControl)) throw IdentifierRules.Invalid("#/display_name");
        // 只收手机号与用户名（R145）；邮箱形似的输入与非法输入一样按格式不符拒绝，不回显原值。
        if (IdentifierRules.Classify(request.Identifier, _account.DefaultPhoneCountryCode) is not { Kind: IdentifierKind.Phone or IdentifierKind.Username } identifier)
            throw IdentifierRules.Invalid("#/identifier");
        string? account = null;
        if (request.Account is not null)
        {
            account = request.Account.Normalize(System.Text.NormalizationForm.FormKC).Trim().ToLowerInvariant();
            if (!AccountPattern.IsMatch(account)) throw IdentifierRules.Invalid("#/account");
        }
        // 初始密码在拿锁之前校验并哈希（Argon2id 较慢）；标识最终对应已有自然人时这份哈希不用、不保存。
        string? hash = null;
        if (request.InitialPassword is not null)
        {
            using var secret = PasswordSecret.From(request.InitialPassword);
            try { CheckPassword(secret); }
            catch (QianErrorException error) when (error.Error.Code == IdentityErrors.PasswordPolicyViolation)
            { throw new QianErrorException(new QianError(error.Error.Code, error.Error.Args) { Pointer = InitialPasswordPointer }, error); }
            hash = _hasher.Hash(secret.Bytes);
        }
        var kind = IdentifierRules.KindName(identifier.Kind);
        using var scope = TenantScope(tenantId); using var origin = OpenOrigin();
        for (var attempt = 0; ; attempt++)
        {
            await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await IdentitySql.LockAsync(db, "members:" + tenantId.ToString("D"), cancellationToken).ConfigureAwait(false);
            var directoryLock = await LockDirectoryAsync(db, tenantId, cancellationToken).ConfigureAwait(false);
            // 标识表是全局表（不受租户 RLS 约束）：解析结果与登录时一致。
            var personId = await db.Identifiers.AsNoTracking().Where(i => i.Kind == kind && i.Value == identifier.Value)
                .Select(i => (Guid?)i.PersonId).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var existing = personId is { } found ? await db.Persons.AsNoTracking().SingleOrDefaultAsync(p => p.Id == found, cancellationToken).ConfigureAwait(false) : null;
            if (existing is not null)
            {
                // 已是本租户成员（停用、锁定、已从目录删除的也算）：不新建、不恢复，由管理员在成员列表里处理。
                if (await db.Memberships.AsNoTracking().AnyAsync(m => m.PersonId == existing.Id, cancellationToken).ConfigureAwait(false))
                    throw new QianErrorException(new QianError(IdentityErrors.DirectoryMemberExists) { Pointer = "#/identifier" });
                // 平台身份对租户不可见、不计配额（Q12），停用或锁定的账号登录不了：都不能加为成员，与运营指定管理员同一口径，不区分原因。
                if (existing.PlatformAccess || existing.Status != AccountStatuses.Active)
                    throw new QianErrorException(new QianError(IdentityErrors.DirectoryMemberIneligible) { Pointer = "#/identifier" });
            }
            else if (hash is null)
                throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(PlatformErrors.Required, InitialPasswordPointer)] });
            // 配额端口缺席（未装配租户组件的独立宿主）时不限额；装配后满额抛 CARDINALITY_USER_LIMIT，此时什么都还没写。
            if (_ctx.TryInject<ITenantQuota>(TenantSettings.QuotaKey) is { } quota)
                await quota.EnsureCanAddUserAsync(await CountMembersAsync(db, cancellationToken).ConfigureAwait(false), TenantMemberKind.Member, cancellationToken).ConfigureAwait(false);
            var now = Now;
            Guid subject;
            if (existing is null)
            {
                subject = PersonIds();
                if (subject == Guid.Empty) throw new InvalidOperationException("Person 标识不能为空。");
                // 管理员录入的手机号没有经过验证码核验，按未验证保存；用户名没有核验流程。登录索引随建档写入本租户。
                db.Persons.Add(new PersonRow
                {
                    Id = subject, DisplayName = name, PasswordHash = hash, PasswordChangedAt = now, MustChangePassword = true, TenantIds = [tenantId.ToString("D")]
                });
                db.Identifiers.Add(new PersonIdentifierRow
                {
                    Id = Guid.CreateVersion7(), PersonId = subject, Kind = kind, Value = identifier.Value, VerifiedAt = identifier.Kind == IdentifierKind.Username ? now : null
                });
                db.PasswordHistory.Add(new PasswordHistoryRow { Id = Guid.CreateVersion7(), PersonId = subject, Hash = hash!, CreatedAt = now });
            }
            else subject = existing.Id;
            var row = new MembershipRow
            {
                Id = Guid.CreateVersion7(), PersonId = subject, DisplayName = name, Status = AccountStatuses.Active,
                Account = account ?? (existing is null ? await NewMemberAccountAsync(db, identifier, attempt, cancellationToken).ConfigureAwait(false)
                    : await DefaultAccountAsync(db, subject, attempt, cancellationToken).ConfigureAwait(false))
            };
            db.Memberships.Add(row);
            var created = new DirectoryEventBatch();
            created.Add(revision => new DirectoryMemberCreatedEvent(revision, row.Id, row.PersonId, row.Status, row.IdentityRevision));
            var changes = await RaiseDirectoryEventsAsync(db, directoryLock, created, cancellationToken).ConfigureAwait(false);
            try { await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false); EnsurePublished(changes); }
            catch (DbUpdateException error) when (IsUniqueViolation(error))
            {
                var constraint = (error.InnerException as PostgresException)?.ConstraintName ?? "";
                // 别的租户同时用同一个标识新建了自然人：换会话重来，这次会解析到他、改走绑定。
                if (constraint.Contains("person_identifiers", StringComparison.Ordinal) && attempt < 3) continue;
                if (constraint.Contains("account", StringComparison.Ordinal))
                {
                    // 调用方指定的账号被占用是业务拒绝；自动生成的账号撞车换一个重试。
                    if (account is not null || attempt >= 3) throw new QianErrorException(new QianError(IdentityErrors.AccountInUse) { Pointer = "#/account" });
                    continue;
                }
                // 主键撞车（几乎不可能）换号重试，绝不覆盖已有自然人。
                if (constraint.Contains("persons", StringComparison.Ordinal) && attempt < 3) continue;
                throw Error(PlatformErrors.Conflict);
            }
            if (existing is not null)
                await IdentitySql.ExecuteAsync(db, $"UPDATE {IdentitySql.Persons} SET tenant_ids = array_append(tenant_ids, @tenant) WHERE id = @id AND NOT (@tenant = ANY(tenant_ids))", cancellationToken,
                    ("tenant", tenantId.ToString("D")), ("id", subject)).ConfigureAwait(false);
            // 初始角色在提交之前授予：授予失败则成员、自然人与目录事件随事务一起回滚，调用方可原样重试。授权写入有自己的事务，
            // 先于本事务提交；本事务提交本身失败时只留下一条不属于任何成员的角色绑定，不授予任何访问（没有成员就拿不到本租户令牌）。
            // 授权组件随后递增身份修订时读不到这条尚未提交的成员，按 NOT_FOUND 跳过：新成员还没有任何令牌，无需失效。
            var granted = await GrantInitialRoleAsync(tenantId, subject, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new DirectoryMemberAdded(ToMembership(row), existing is null, granted);
        }
    }

    // HTTP 入口的审计（S1-IDP-017 验收 6）：宿主装了 Audit 时写目标租户的审计链，开始记录写不进去就不执行；独立身份宿主没装 Audit 时不写。
    // 标准能力入口不走这里：它经调用方上下文取 Audit，缺席即不可用。
    internal IAuditService? TenantAudit(Guid tenantId) => _ctx.Scoped(new ScopeKey("tenant", tenantId.ToString("D"))).TryInject<IAuditService>(AuditDefinition.Key);

    // 新建自然人的缺省账号：用户名（未被本租户占用时），否则随机的不透明账号；不取手机号，账号创建后不可改。
    private static async Task<string> NewMemberAccountAsync(IdentityDbContext db, (IdentifierKind Kind, string Value) identifier, int attempt, CancellationToken ct)
    {
        if (attempt == 0 && identifier.Kind == IdentifierKind.Username && !await db.Memberships.AnyAsync(m => m.Account == identifier.Value, ct).ConfigureAwait(false))
            return identifier.Value;
        return "m" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6));
    }

    // R158：同一运行时装了授权组件时授予 tenant.user；授权组件缺席（独立身份宿主）时不授予，返回空集合。授予人取当前事件来源的主体，
    // 即发起新增的租户管理员。
    private async Task<IReadOnlyList<string>> GrantInitialRoleAsync(Guid tenantId, Guid personId, CancellationToken ct)
    {
        if (_ctx.Scoped(new ScopeKey("tenant", tenantId.ToString("D"))).TryInject<ITenantRoleGrant>(TenantRoleGrant.Key) is not { } grants) return [];
        var actor = EventEnvelopeScope.Current?.Actor.Id ?? "qian.identity";
        await grants.AssignAsync(InitialMemberRole, "user", personId.ToString("D"), actor, ct).ConfigureAwait(false);
        return [InitialMemberRole];
    }
}

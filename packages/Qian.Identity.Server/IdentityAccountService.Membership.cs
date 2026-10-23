using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Identity.Server;

// Membership：person × tenant。创建在同一事务里持计数锁，先过租户配额判定端口再插入，并发创建不会一起越过上限。
internal sealed partial class IdentityAccountService
{
    private static readonly Regex AccountPattern = new(@"\A[a-z0-9][a-z0-9_.@\-]{0,63}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public async ValueTask<Membership> CreateMembershipAsync(CreateMembership request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = RequireTenant();
        var name = request.DisplayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || name.Any(char.IsControl)) throw IdentifierRules.Invalid("#/display_name");
        string? account = null;
        if (request.Account is not null)
        {
            account = request.Account.Normalize(System.Text.NormalizationForm.FormKC).Trim().ToLowerInvariant();
            if (!AccountPattern.IsMatch(account)) throw IdentifierRules.Invalid("#/account");
        }
        if (request.MembershipId == Guid.Empty) throw IdentifierRules.Invalid("#/membership_id");
        return await CreateMembershipCoreAsync(tenantId, request.PersonId, name, account, request.IsTenantAdministrator, request.MembershipId, cancellationToken).ConfigureAwait(false);
    }

    // 开户编排的成员创建端口（ITenantMembershipProvisioner）：按 (成员 ID, Person) 幂等，重试不会重复占用配额。
    public async ValueTask ProvisionAsync(TenantAdministrator administrator, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw IdentifierRules.Invalid("#/idempotency_key");
        var tenantId = RequireTenant();
        if (administrator.MembershipId == Guid.Empty || !Guid.TryParseExact(administrator.SubjectId, "D", out var personId) || personId == Guid.Empty)
            throw IdentifierRules.Invalid("#/administrator");
        // 管理员必须已是一个存在的 Person（经本组件创建或选择）；缺席时失败，不把它当作成功。
        Person person;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            person = await LoadPersonAsync(session.Context, personId, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        }
        if (person.Status == AccountStatuses.Purged) throw Error(PlatformErrors.NotFound);
        await CreateMembershipCoreAsync(tenantId, personId, person.DisplayName, null, true, administrator.MembershipId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Membership> CreateMembershipCoreAsync(Guid tenantId, Guid personId, string displayName, string? account, bool administrator, Guid? membershipId, CancellationToken ct)
    {
        using var scope = TenantScope(tenantId);
        for (var attempt = 0; ; attempt++)
        {
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            // 同租户的成员创建串行：计数、配额判定与插入在同一把事务锁内。
            await IdentitySql.LockAsync(db, "members:" + tenantId.ToString("D"), ct).ConfigureAwait(false);
            var person = await db.Persons.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);
            if (person is null || person.Status == AccountStatuses.Purged) throw Error(PlatformErrors.NotFound);
            var existing = await db.Memberships.SingleOrDefaultAsync(m => m.PersonId == personId, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                // 同一 Person 重复建立：指针一致则幂等返回，必要时补上管理员标志与可用状态；指针不一致是冲突。
                if (existing.Status == AccountStatuses.Purged || existing.DirectoryDeleted || membershipId is { } wanted && wanted != existing.Id) throw Error(PlatformErrors.Conflict);
                if (administrator && !existing.IsTenantAdministrator || existing.Status != AccountStatuses.Active)
                {
                    existing.IsTenantAdministrator |= administrator; existing.Status = AccountStatuses.Active; existing.IdentityRevision++;
                    await session.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return ToMembership(existing);
            }
            if (membershipId is { } id && await db.Memberships.AnyAsync(m => m.Id == id, ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            var kind = person.PlatformAccess ? TenantMemberKind.PlatformSuperadmin : administrator ? TenantMemberKind.Administrator : TenantMemberKind.Member;
            // 配额端口缺席（未装配租户组件的独立宿主）时不限额；装配后超限抛 CARDINALITY_USER_LIMIT。
            if (_ctx.TryInject<ITenantQuota>(TenantSettings.QuotaKey) is { } quota)
                await quota.EnsureCanAddUserAsync(await CountMembersAsync(db, ct).ConfigureAwait(false), kind, ct).ConfigureAwait(false);
            var row = new MembershipRow
            {
                Id = membershipId ?? Guid.CreateVersion7(), PersonId = personId, DisplayName = displayName, Status = AccountStatuses.Active, IsTenantAdministrator = administrator,
                Account = account ?? await DefaultAccountAsync(db, personId, attempt, ct).ConfigureAwait(false)
            };
            db.Memberships.Add(row);
            try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
            catch (DbUpdateException error) when (IsUniqueViolation(error))
            {
                var constraint = (error.InnerException as PostgresException)?.ConstraintName ?? "";
                if (constraint.Contains("account", StringComparison.Ordinal))
                {
                    // 调用方指定的账号被占用是业务拒绝；自动生成的账号撞车（几乎不可能）换一个重试。
                    if (account is not null || attempt >= 3) throw Error(IdentityErrors.AccountInUse);
                    continue;
                }
                throw Error(PlatformErrors.Conflict);
            }
            // 维护登录索引：与成员行同事务，且只追加不存在的租户。
            await IdentitySql.ExecuteAsync(db, $"UPDATE {IdentitySql.Persons} SET tenant_ids = array_append(tenant_ids, @tenant) WHERE id = @id AND NOT (@tenant = ANY(tenant_ids))", ct,
                ("tenant", tenantId.ToString("D")), ("id", personId)).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return ToMembership(row);
        }
    }

    // 默认账号：Person 的用户名（若有且未被占用），否则随机的不透明账号；账号创建后不可改，所以不取手机号或邮箱。
    private static async Task<string> DefaultAccountAsync(IdentityDbContext db, Guid personId, int attempt, CancellationToken ct)
    {
        if (attempt == 0)
        {
            var username = await db.Identifiers.AsNoTracking().Where(i => i.PersonId == personId && i.Kind == "username").Select(i => i.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (username is not null && !await db.Memberships.AnyAsync(m => m.Account == username, ct).ConfigureAwait(false)) return username;
        }
        return "m" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

// 管理员维护（S1-TEN-010）：账号、成员与凭据都经 qian.identity 的端口读写，tenant_admins 只存成员指针。
// 替换按「新管理员成员 → 新 tenant.owner → 撤销原管理员 → 改指针 → 补审计」的顺序：每步幂等、指针最后才改，
// 中途失败用同一请求重试即可收敛；与开通续跑共用同一把租户锁，开通第 05–07 步全部成功之前不接受替换。
internal sealed class TenantAdministrationService(IPluginContext context, PostgresTenantDirectory directory) : ITenantAdministration
{
    private const string Owner = "tenant.owner";
    private static readonly string[] AdministratorSteps = ["05:membership", "06:administrator", "07:owner"];
    // 身份组件按它自己的请求形状给出错误位置，换成运营请求体的字段，页面才能把错误定位到对应输入框。
    private static readonly Dictionary<string, string> AccountPointers = new(StringComparer.Ordinal)
    { ["#/display_name"] = "#/displayName", ["#/identifiers"] = "#/identifier", ["#/identifiers/0/value"] = "#/identifier", ["#/new_password"] = "#/initialPassword" };
    private static readonly Dictionary<string, string> PasswordPointers = new(StringComparer.Ordinal) { ["#/new_password"] = "#/newPassword" };
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();

    // 身份组件缺席时 Inject 抛 ServiceNotReadyException，运营入口按组件缺席返回 404。
    private IIdentityAccounts Accounts => context.Inject<IIdentityAccounts>(IdentityDefinition.AccountsKey);
    private IIdentityService Identity => context.Inject<IIdentityService>(IdentityDefinition.Key);

    public async ValueTask<TenantAdminAccount?> FindAccountAsync(TenantAdminAccountLookup request, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request);
        var identifier = request.Identifier?.Trim();
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 254 || identifier.Any(char.IsControl)) throw Invalid("#/identifier");
        var person = await Accounts.FindPersonAsync(identifier, cancellationToken).ConfigureAwait(false);
        return person is null || !Eligible(person) ? null : Account(person);
    }

    public async ValueTask<TenantAdminAccount> CreateAccountAsync(CreateTenantAdminAccount request, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request);
        var kind = request.IdentifierKind switch
        {
            TenantAdminIdentifierKind.Username => IdentifierKind.Username, TenantAdminIdentifierKind.Phone => IdentifierKind.Phone,
            TenantAdminIdentifierKind.Email => IdentifierKind.Email, _ => throw Invalid("#/identifierKind")
        };
        if (string.IsNullOrEmpty(request.InitialPassword)) throw Invalid("#/initialPassword");
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(request.InitialPassword));
        try
        {
            // 运营录入的手机号与邮箱没有经过验证码核验，按未验证落库；用户名没有核验流程。
            var person = await Accounts.CreatePersonAsync(new CreatePerson(request.DisplayName, [new IdentifierInput(kind, request.Identifier, kind == IdentifierKind.Username)])
            { InitialPassword = proof, RequirePasswordChange = true }, cancellationToken).ConfigureAwait(false);
            return Account(person);
        }
        catch (QianErrorException error) { throw Remap(error, AccountPointers); }
    }

    public async ValueTask<TenantAdminList> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Check(); await Existing(id, cancellationToken).ConfigureAwait(false);
        using var scope = _tenant.BeginScope(id.ToString());
        var admin = await PointerAsync(cancellationToken).ConfigureAwait(false) is { } membershipId ? await DescribeAsync(membershipId, cancellationToken).ConfigureAwait(false) : null;
        return new TenantAdminList(admin is null ? [] : [admin]);
    }

    public async ValueTask<TenantAdminList> ReplaceAsync(Guid id, ReplaceTenantAdmins request, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ValidateActor(actor);
        if (request.Items is not [{ SubjectId: var subjectId }] || subjectId == Guid.Empty) throw Invalid("#/items");
        await Existing(id, cancellationToken).ConfigureAwait(false);
        return await TenancyLocks.RunAsync(context, "tenant:" + id, async () =>
        {
            using var scope = _tenant.BeginScope(id.ToString());
            await EnsureProvisionedAsync(cancellationToken).ConfigureAwait(false);
            var identity = Identity; var accounts = Accounts;
            // 平台身份不能当租户管理员（Q12：平台超管对租户不可见、不计配额）；停用或锁定的账号登录不了，指定了也没有意义。
            var person = await identity.GetPersonAsync(subjectId, cancellationToken).ConfigureAwait(false);
            if (person is null || !Eligible(person) || person.Status != AccountStatuses.Active) throw Invalid("#/items/0/subjectId");
            var grants = context.Scoped(new ScopeKey("tenant", id.ToString())).Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
            var subject = person.Id.ToString("D");
            // 已是成员时幂等返回并补上管理员标志；新成员经配额判定，满额 CARDINALITY_USER_LIMIT，此时什么都还没改。
            var membership = await accounts.CreateMembershipAsync(new CreateMembership(person.Id, person.DisplayName) { IsTenantAdministrator = true }, cancellationToken).ConfigureAwait(false);
            await grants.AssignAsync(Owner, "user", subject, actor, cancellationToken).ConfigureAwait(false);
            var previous = await PointerAsync(cancellationToken).ConfigureAwait(false);
            // 原管理员只撤销 tenant.owner 与管理员标志，成员身份与其它角色保留，会话不吊销（授权变更递增 identity_revision，PRD 13.3.3）。
            // 指针仍指向原管理员时重试会再做一遍，两步都幂等；新 owner 已经授予，撤销不会撞上防自锁。
            if (previous is { } old && old != membership.Id && await identity.GetMembershipAsync(old, cancellationToken).ConfigureAwait(false) is { } prior)
            {
                await grants.RevokeAsync(Owner, "user", prior.PersonId.ToString("D"), actor, cancellationToken).ConfigureAwait(false);
                try { await accounts.SetTenantAdministratorAsync(old, false, cancellationToken).ConfigureAwait(false); }
                catch (QianErrorException error) when (error.Error.Code == PlatformErrors.NotFound) { /* 原成员已清除或已从目录删除，没有标志可撤销 */ }
            }
            if (previous != membership.Id)
            {
                await SetPointerAsync(membership.Id, actor, cancellationToken).ConfigureAwait(false);
                await AuditAsync(id, actor, "qian.tenancy/admin.assign", subject, JsonSerializer.SerializeToElement(new { membershipId = membership.Id, previousMembershipId = previous })).ConfigureAwait(false);
            }
            return new TenantAdminList([await DescribeAsync(membership.Id, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.Conflict)]);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ResetPasswordAsync(Guid id, Guid membershipId, ResetTenantAdminPassword request, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ValidateActor(actor);
        if (string.IsNullOrEmpty(request.NewPassword)) throw Invalid("#/newPassword");
        await Existing(id, cancellationToken).ConfigureAwait(false);
        using var scope = _tenant.BeginScope(id.ToString());
        // 只对当前管理员：否则借任一租户的路径就能重置任意自然人的密码。
        if (membershipId == Guid.Empty || await PointerAsync(cancellationToken).ConfigureAwait(false) != membershipId) throw Error(PlatformErrors.NotFound);
        var identity = Identity;
        var membership = await identity.GetMembershipAsync(membershipId, cancellationToken).ConfigureAwait(false);
        var person = membership is null ? null : await identity.GetPersonAsync(membership.PersonId, cancellationToken).ConfigureAwait(false);
        if (person is null || !Eligible(person)) throw Error(PlatformErrors.NotFound);
        // 自然人全局唯一：旧密码立即失效、下次登录强制改密、该自然人的全部会话吊销，他在其它租户的登录也随之改变。
        using (var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(request.NewPassword)))
        {
            try { await Accounts.ResetPasswordAsync(person.Id, new PasswordReset(proof, "tenant_admin_reset"), cancellationToken).ConfigureAwait(false); }
            catch (QianErrorException error) { throw Remap(error, PasswordPointers); }
        }
        await AuditAsync(id, actor, "qian.tenancy/admin.reset-password", membershipId.ToString("D"), JsonSerializer.SerializeToElement(new { membershipId })).ConfigureAwait(false);
    }

    private void Check()
    {
        if (_tenant.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
    }
    private async Task Existing(Guid id, CancellationToken ct)
    {
        if (await directory.GetByIdAsync(id, ct).ConfigureAwait(false) is null) throw Error(PlatformErrors.NotFound);
    }
    private async Task EnsureProvisionedAsync(CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var done = await session.Context.ProvisionSteps.AsNoTracking().CountAsync(step => AdministratorSteps.Contains(step.Step) && step.Status == "succeeded", ct).ConfigureAwait(false);
        if (done != AdministratorSteps.Length) throw Error(PlatformErrors.Conflict);
    }
    // 运营列表的管理员列：只读成员显示名；没有指针、身份组件缺席或成员已清除都返回 null，不让整页失败。
    internal async Task<string?> AdministratorNameAsync(Guid id, CancellationToken ct)
    {
        using var scope = _tenant.BeginScope(id.ToString());
        if (await PointerAsync(ct).ConfigureAwait(false) is not { } membershipId) return null;
        if (context.TryInject<IIdentityService>(IdentityDefinition.Key) is not { } identity) return null;
        var membership = await identity.GetMembershipAsync(membershipId, ct).ConfigureAwait(false);
        return membership is null || membership.Status == AccountStatuses.Purged ? null : membership.DisplayName;
    }
    private async Task<Guid?> PointerAsync(CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        return await session.Context.Administrators.AsNoTracking().Select(row => (Guid?)row.MembershipId).SingleOrDefaultAsync(ct).ConfigureAwait(false);
    }
    private async Task SetPointerAsync(Guid membershipId, string actor, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.Administrators.SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if (row is null) session.Context.Administrators.Add(new TenantAdminRow { MembershipId = membershipId, CreatedBy = actor });
        else { row.MembershipId = membershipId; row.UpdatedBy = actor; }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    private async Task<TenantAdmin?> DescribeAsync(Guid membershipId, CancellationToken ct)
    {
        var identity = Identity;
        var membership = await identity.GetMembershipAsync(membershipId, ct).ConfigureAwait(false);
        if (membership is null || membership.Status == AccountStatuses.Purged) return null;
        var person = await identity.GetPersonAsync(membership.PersonId, ct).ConfigureAwait(false);
        return person is null ? null : new TenantAdmin(membership.Id, person.Id, membership.DisplayName, membership.Account, membership.Status, Identifiers(person));
    }
    // 补充审计进目标租户的审计链，只记成员指针，不含密码与原始标识；前置的超管旁路审计在平台链上。
    private Task AuditAsync(Guid id, string actor, string capability, string input, JsonElement payload) =>
        TenantAuditCompletion.AppendAsync(context, new AuditRecord(Guid.CreateVersion7(), id.ToString(), "user", actor, null, capability, "v1",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))), "api", "admins", "success",
            ActivityTraceId.CreateRandom().ToHexString(), DateTimeOffset.UtcNow, payload),
            (record, token) => context.Scoped(new ScopeKey("tenant", id.ToString())).Audit().AppendAsync(record, token));

    private static bool Eligible(Person person) => !person.PlatformAccess && person.Status != AccountStatuses.Purged;
    private static TenantAdminAccount Account(Person person) => new(person.Id, person.DisplayName, person.Status, Identifiers(person));
    private static System.Collections.ObjectModel.ReadOnlyCollection<TenantAdminIdentifier> Identifiers(Person person) => Array.AsReadOnly(person.Identifiers.Select(item => new TenantAdminIdentifier(item.Kind switch
    {
        IdentifierKind.Phone => TenantAdminIdentifierKind.Phone, IdentifierKind.Email => TenantAdminIdentifierKind.Email, _ => TenantAdminIdentifierKind.Username
    }, item.Masked, item.Verified)).ToArray());
    private static QianErrorException Remap(QianErrorException error, Dictionary<string, string> pointers)
    {
        // 映射表之外的位置换成整个请求体，不把身份组件内部的字段名带进运营契约。
        string Map(string pointer) => pointers.TryGetValue(pointer, out var mapped) ? mapped : "#";
        var source = error.Error;
        if (source.Pointer is null && source.Errors.Count == 0) return error;
        return new QianErrorException(new QianError(source.Code, source.Args)
        {
            Pointer = source.Pointer is null ? null : Map(source.Pointer), Allowed = source.Allowed,
            Errors = [.. source.Errors.Select(item => new QianFieldError(item.Code, Map(item.Pointer), item.Args) { Allowed = item.Allowed })]
        }, error);
    }
    private static void ValidateActor(string actor)
    { if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw Error(PlatformErrors.InputInvalid); }
    private static QianErrorException Invalid(string pointer) => new(new QianError(PlatformErrors.InputInvalid) { Pointer = pointer });
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}

using System.Globalization;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// Person / Membership / ExternalIdentity：真实 PostgreSQL（受限应用角色、强制 RLS）与真实服务实现。
public sealed class AccountModelTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Person按多标识创建_凭据只存Argon2id哈希与历史_对外投影只含掩码_sub是稳定的v7()
    {
        await using var host = await AccountsHost.Start(postgres);
        using var proof = Proof(Password);
        var person = await host.Accounts.CreatePersonAsync(new CreatePerson("张三", [new(IdentifierKind.Phone, "138 1234 5678", true), new(IdentifierKind.Email, "Zhang.San@Example.com", true), new(IdentifierKind.Username, "ZhangSan")])
            { InitialPassword = proof }, Ct);
        Assert.Equal(7, person.Id.Version);
        Assert.Equal(("138****5678", "z***@example.com", "zhangsan", AccountStatuses.Active), (person.Phone, person.Email, person.UserName, person.Status));
        Assert.Equal("Phone:True,Email:True,Username:False", string.Join(',', person.Identifiers.Select(i => $"{i.Kind}:{i.Verified}")));
        Assert.DoesNotContain(person.Identifiers, i => i.Masked.Contains("13812345678", StringComparison.Ordinal) || i.Masked.Contains("Zhang.San", StringComparison.Ordinal));
        var id = person.Id.ToString("D");
        var hash = await host.Text("SELECT password_hash FROM plugin_qian_identity.persons WHERE id = @id::uuid", ("id", id));
        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.password_history WHERE person_id = @id::uuid", ("id", id)));
        // 标识以规范形式入库：手机补国家码、邮箱与用户名小写。
        Assert.Equal("email=zhang.san@example.com,phone=+8613812345678,username=zhangsan",
            await host.Text("SELECT string_agg(kind || '=' || value, ',' ORDER BY kind) FROM plugin_qian_identity.person_identifiers WHERE person_id = @id::uuid", ("id", id)));
        Assert.Equal("true|0|[]", await host.Text("SELECT must_change_password::text || '|' || identity_revision::text || '|' || mfa_factors::text FROM plugin_qian_identity.persons WHERE id = @id::uuid", ("id", id)));
        Assert.Equal(person.Id, (await host.Accounts.FindPersonAsync("+86 138-1234-5678", Ct))!.Id);
        Assert.Equal(person.Id, (await host.Accounts.FindPersonAsync("ZHANG.SAN@example.com", Ct))!.Id);
        Assert.Null(await host.Accounts.FindPersonAsync("13800000000", Ct));
    }

    [Fact]
    public async Task 标识已被占用时拒绝且不留下半个账号()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (first, phone) = await CreatePerson(host);
        var before = await host.Text("SELECT (SELECT count(*) FROM plugin_qian_identity.persons) || '/' || (SELECT count(*) FROM plugin_qian_identity.person_identifiers) || '/' || (SELECT count(*) FROM plugin_qian_identity.password_history)");
        using var proof = Proof(Password);
        var rejected = await Rejected("IDENTIFIER_IN_USE", async () => await host.Accounts.CreatePersonAsync(new CreatePerson("李四", [new(IdentifierKind.Email, "free@example.com", true), new(IdentifierKind.Phone, "+86 " + phone, true)])
            { InitialPassword = proof }, Ct));
        Assert.Equal(409, rejected.Code.Status);
        Assert.Equal(before, await host.Text("SELECT (SELECT count(*) FROM plugin_qian_identity.persons) || '/' || (SELECT count(*) FROM plugin_qian_identity.person_identifiers) || '/' || (SELECT count(*) FROM plugin_qian_identity.password_history)"));
        Assert.Null(await host.Accounts.FindPersonAsync("free@example.com", Ct));
        Assert.Equal(first.Id, (await host.Accounts.FindPersonAsync(phone, Ct))!.Id);
        // 同一请求里重复的标识、非法的标识都是输入错误，同样不创建账号。
        await Rejected("INPUT_INVALID", async () => await host.Accounts.CreatePersonAsync(new CreatePerson("王五", [new(IdentifierKind.Email, "a@example.com"), new(IdentifierKind.Email, "A@EXAMPLE.COM")]), Ct));
        await Rejected("INPUT_INVALID", async () => await host.Accounts.CreatePersonAsync(new CreatePerson("王五", [new(IdentifierKind.Phone, "12")]), Ct));
        await Rejected("INPUT_INVALID", async () => await host.Accounts.CreatePersonAsync(new CreatePerson("", [new(IdentifierKind.Email, "b@example.com")]), Ct));
        await Rejected("INPUT_INVALID", async () => await host.Accounts.CreatePersonAsync(new CreatePerson("王五", []), Ct));
    }

    [Fact]
    public async Task 不同Person的sub不碰撞_并发创建同一标识只有一个成功_并发创建不同账号各得独立sub_主键冲突换号重试而不覆盖()
    {
        await using var host = await AccountsHost.Start(postgres);
        var shared = NextPhone();
        var racers = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            using var proof = Proof(Password);
            try { return (Person?)await host.Accounts.CreatePersonAsync(new CreatePerson("竞争者" + i, [new(IdentifierKind.Phone, shared, true)]) { InitialPassword = proof }, Ct); }
            catch (QianErrorException error) { Assert.Equal("IDENTIFIER_IN_USE", error.Error.Code.Code); return null; }
        }));
        var winner = Assert.Single(racers, p => p is not null)!;
        Assert.Equal(11, racers.Count(p => p is null));
        Assert.Equal("1/1", await host.Text("SELECT (SELECT count(*) FROM plugin_qian_identity.persons) || '/' || (SELECT count(*) FROM plugin_qian_identity.person_identifiers)"));
        Assert.Equal(winner.Id, (await host.Accounts.FindPersonAsync(shared, Ct))!.Id);

        var many = await Task.WhenAll(Enumerable.Range(0, 16).Select(async i =>
        {
            using var proof = Proof(Password);
            return await host.Accounts.CreatePersonAsync(new CreatePerson("并发" + i, [new(IdentifierKind.Phone, NextPhone(), true)]) { InitialPassword = proof }, Ct);
        }));
        Assert.Equal(16, many.Select(p => p.Id).Distinct().Count());
        Assert.DoesNotContain(many, p => p.Id == winner.Id);

        // 即使生成的 sub 与既有 Person 相同（极端情形），也只换一个新号重试，既有账号原样保留。
        var existing = many[0];
        var ids = new Queue<Guid>([existing.Id, existing.Id, Guid.CreateVersion7()]);
        host.Service.PersonIds = () => ids.Dequeue();
        var created = (await CreatePerson(host, "新人")).Person;
        Assert.Empty(ids);
        Assert.NotEqual(existing.Id, created.Id);
        Assert.Equal("并发0", (await host.Service.GetPersonAsync(existing.Id, Ct))!.DisplayName);
        // 反复冲突超过重试上限时失败，而不是死循环或覆盖。
        host.Service.PersonIds = () => existing.Id;
        await Assert.ThrowsAnyAsync<Exception>(async () => await CreatePerson(host, "注定失败"));
        Assert.Equal("并发0", (await host.Service.GetPersonAsync(existing.Id, Ct))!.DisplayName);
    }

    [Fact]
    public async Task 解绑最后一个已验证标识被拒绝并返回结构化ErrorDetail_换绑不改sub_软删除释放唯一性()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (person, phone) = await CreatePerson(host, extra: [new(IdentifierKind.Username, "tester01")]);
        var catalog = ErrorCatalog.CreateDefault(); using var registered = IdentityErrors.Register(catalog);

        // 手机是唯一已验证的标识（用户名未验证）：解绑被拒，错误是带稳定错误码、状态码与出错位置的 ErrorDetail。
        var error = await Rejected("LAST_VERIFIED_IDENTIFIER", async () => await host.Accounts.UnbindIdentifierAsync(person.Id, IdentifierKind.Phone, phone, Ct));
        var detail = catalog.CreateDetail(error, "0af7651916cd43dd8448eb211c80319c", CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal(("LAST_VERIFIED_IDENTIFIER", 422, "#/value"), (detail.Code, detail.Status, detail.Pointer));
        Assert.False(string.IsNullOrWhiteSpace(detail.Detail));
        Assert.Equal(2, (await host.Service.GetPersonAsync(person.Id, Ct))!.Identifiers.Count);
        // 未验证的标识可以随时解绑，不影响找回。
        var afterUsername = await host.Accounts.UnbindIdentifierAsync(person.Id, IdentifierKind.Username, "TESTER01", Ct);
        Assert.Equal([IdentifierKind.Phone], afterUsername.Identifiers.Select(i => i.Kind));
        // 只剩一个标识时无论是否验证都不能解绑（账号将无法登录）。
        await Rejected("LAST_VERIFIED_IDENTIFIER", async () => await host.Accounts.UnbindIdentifierAsync(person.Id, IdentifierKind.Phone, phone, Ct));
        // 绑定一个已验证的邮箱后，手机就不再是最后一个已验证标识。
        await host.Accounts.BindIdentifierAsync(person.Id, new IdentifierInput(IdentifierKind.Email, "Tester@Example.com", true), Ct);
        var removed = await host.Accounts.UnbindIdentifierAsync(person.Id, IdentifierKind.Phone, phone, Ct);
        Assert.Equal([IdentifierKind.Email], removed.Identifiers.Select(i => i.Kind));
        await Rejected("LAST_VERIFIED_IDENTIFIER", async () => await host.Accounts.UnbindIdentifierAsync(person.Id, IdentifierKind.Email, "tester@example.com", Ct));
        // 解绑的标识是软删除：行保留（可审计），占用释放，别人可以绑定。
        Assert.Equal("true", await host.Text("SELECT is_deleted::text FROM plugin_qian_identity.person_identifiers WHERE person_id = @id::uuid AND kind = 'phone'", ("id", person.Id.ToString("D"))));
        var (other, _) = await CreatePerson(host, "接手人");
        var rebound = await host.Accounts.BindIdentifierAsync(other.Id, new IdentifierInput(IdentifierKind.Phone, phone, true), Ct);
        Assert.Equal(2, rebound.Identifiers.Count);
        Assert.Equal(other.Id, (await host.Accounts.FindPersonAsync(phone, Ct))!.Id);
        // 已被别人占用的标识不能绑定。
        await Rejected("IDENTIFIER_IN_USE", async () => await host.Accounts.BindIdentifierAsync(person.Id, new IdentifierInput(IdentifierKind.Phone, phone, true), Ct));
        // 绑定是幂等的，且可以把未验证的升级为已验证。
        await host.Accounts.BindIdentifierAsync(person.Id, new IdentifierInput(IdentifierKind.Email, "tester@example.com", true), Ct);
        Assert.Equal(2, (await host.Service.GetPersonAsync(other.Id, Ct))!.Identifiers.Count);
    }

    [Fact]
    public async Task 换绑只改标识不改sub_旧号立即可被他人使用_换成未验证标识会让账号失去找回方式时被拒绝()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (person, oldPhone) = await CreatePerson(host);
        var newPhone = NextPhone();
        var before = (await host.Service.GetPersonAsync(person.Id, Ct))!;
        var replaced = await host.Accounts.ReplaceIdentifierAsync(person.Id, IdentifierKind.Phone, oldPhone, new IdentifierInput(IdentifierKind.Phone, newPhone, true), Ct);
        Assert.Equal(person.Id, replaced.Id);
        Assert.True(replaced.IdentityRevision > before.IdentityRevision);
        Assert.Equal(person.Id, (await host.Accounts.FindPersonAsync(newPhone, Ct))!.Id);
        Assert.Null(await host.Accounts.FindPersonAsync(oldPhone, Ct));
        var (taker, _) = await CreatePerson(host, "旧号新主人", oldPhone);
        Assert.Equal(taker.Id, (await host.Accounts.FindPersonAsync(oldPhone, Ct))!.Id);
        // 目标号码已被他人占用，整个换绑回滚：原号码仍属于本人。
        await Rejected("IDENTIFIER_IN_USE", async () => await host.Accounts.ReplaceIdentifierAsync(person.Id, IdentifierKind.Phone, newPhone, new IdentifierInput(IdentifierKind.Phone, oldPhone, true), Ct));
        Assert.Equal(person.Id, (await host.Accounts.FindPersonAsync(newPhone, Ct))!.Id);
        // 唯一的已验证标识不能换成未验证的。
        await Rejected("LAST_VERIFIED_IDENTIFIER", async () => await host.Accounts.ReplaceIdentifierAsync(person.Id, IdentifierKind.Phone, newPhone, new IdentifierInput(IdentifierKind.Phone, NextPhone(), false), Ct));
        Assert.Equal(person.Id, (await host.Accounts.FindPersonAsync(newPhone, Ct))!.Id);
        await Rejected("NOT_FOUND", async () => await host.Accounts.ReplaceIdentifierAsync(person.Id, IdentifierKind.Phone, "+8613000000000", new IdentifierInput(IdentifierKind.Phone, NextPhone(), true), Ct));
    }

    [Fact]
    public async Task 改密校验当前密码与策略_最近五个密码含当前不可复用_改密使会话失效并只发布不含密码的事件()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var signed = await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext("127.0.0.1", "ut"), Ct);
        Assert.Equal(SignInStatus.Authenticated, signed.Status);
        var revision = (await host.Service.GetPersonAsync(person.Id, Ct))!.IdentityRevision;

        async Task Change(string current, string next) { using var a = Proof(current); using var b = Proof(next); await host.Accounts.ChangePasswordAsync(person.Id, new PasswordChange(a, b), Ct); }
        // 当前密码不对 → 422 且指向 current_password；新密码违反策略 → 422 带违反的规则。
        Assert.Equal("#/current_password", (await Rejected("CURRENT_PASSWORD_INVALID", () => Change("wrong-current-1", "Another-Pass-1"))).Pointer);
        var policy = await Rejected("PASSWORD_POLICY_VIOLATION", () => Change(Password, "short"));
        Assert.Equal(("#/new_password", "minLength=8"), (policy.Pointer, policy.Args["rule"]));
        await Rejected("PASSWORD_POLICY_VIOLATION", () => Change(Password, " leading-space-1"));
        await Rejected("PASSWORD_POLICY_VIOLATION", () => Change(Password, new string('x', 129)));
        Assert.True(await HasLiveSession(host, person.Id)); // 失败的改密不动会话

        // 与当前密码相同也算复用。
        Assert.Equal("5", (await Rejected("PASSWORD_REUSED", () => Change(Password, Password))).Args["count"]);
        var chain = new[] { Password, "Passw0rd-Alpha-2", "Passw0rd-Alpha-3", "Passw0rd-Alpha-4", "Passw0rd-Alpha-5" };
        for (var i = 1; i < chain.Length; i++) await Change(chain[i - 1], chain[i]);
        Assert.Equal("5", await host.Text("SELECT count(*) FROM plugin_qian_identity.password_history WHERE person_id = @id::uuid", ("id", person.Id.ToString("D"))));
        // 历史里仍有最初的密码：不可复用；换掉最旧的之后才可以重新使用。
        await Rejected("PASSWORD_REUSED", () => Change(chain[4], Password));
        await Change(chain[4], "Passw0rd-Alpha-6");
        Assert.Equal("5", await host.Text("SELECT count(*) FROM plugin_qian_identity.password_history WHERE person_id = @id::uuid", ("id", person.Id.ToString("D"))));
        await Change("Passw0rd-Alpha-6", Password);

        // 改密使既有会话失效、身份修订递增；旧密码不再可用，新密码可以。
        Assert.False(await HasLiveSession(host, person.Id));
        Assert.True((await host.Service.GetPersonAsync(person.Id, Ct))!.IdentityRevision > revision);
        Assert.Equal(SignInStatus.Failed, (await host.SignIn.SignInAsync(Input(phone, "Passw0rd-Alpha-6"), new LoginContext(), Ct)).Status);
        Assert.Equal(SignInStatus.Authenticated, (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Status);
        // 只落哈希：库里没有任何一处出现密码明文。
        foreach (var secret in chain.Append("Passw0rd-Alpha-6").Append("wrong-current-1"))
            Assert.Equal("0", await host.Text("SELECT (SELECT count(*) FROM plugin_qian_identity.password_history WHERE hash LIKE '%' || @s || '%') + (SELECT count(*) FROM plugin_qian_identity.persons WHERE password_hash LIKE '%' || @s || '%') + (SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason LIKE '%' || @s || '%' OR user_agent LIKE '%' || @s || '%' OR identifier_masked LIKE '%' || @s || '%')", ("s", secret)));
        // 改密事件只通知修订，不含密码或哈希。
        var events = await host.Text("SELECT string_agg(event_type_id || ':' || payload, ' ') FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.password_changed.v1'");
        Assert.Contains("identity.password_changed.v1", events, StringComparison.Ordinal);
        Assert.DoesNotContain("argon2", events, StringComparison.Ordinal);
        Assert.DoesNotContain("Passw0rd", events, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 运营重置密码使旧凭据失效并默认强制下次改密()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        Assert.Equal(SignInStatus.Authenticated, (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Status);
        using (var reset = Proof("Reset-By-Ops-1")) await host.Accounts.ResetPasswordAsync(person.Id, new PasswordReset(reset, "ops_reset"), Ct);
        Assert.False(await HasLiveSession(host, person.Id));
        Assert.Equal(SignInStatus.Failed, (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Status);
        var next = await host.SignIn.SignInAsync(Input(phone, "Reset-By-Ops-1"), new LoginContext(), Ct);
        Assert.Equal(SignInStatus.PasswordChangeRequired, next.Status);
        Assert.Equal("true", await host.Text("SELECT must_change_password::text FROM plugin_qian_identity.persons WHERE id = @id::uuid", ("id", person.Id.ToString("D"))));
        await Rejected("INPUT_INVALID", async () => { using var empty = Proof("Reset-By-Ops-2"); await host.Accounts.ResetPasswordAsync(person.Id, new PasswordReset(empty, ""), Ct); });
        await Rejected("PASSWORD_POLICY_VIOLATION", async () => { using var weak = Proof("weak"); await host.Accounts.ResetPasswordAsync(person.Id, new PasswordReset(weak, "ops_reset"), Ct); });
        await Rejected("NOT_FOUND", async () => { using var any = Proof("Reset-By-Ops-3"); await host.Accounts.ResetPasswordAsync(Guid.CreateVersion7(), new PasswordReset(any, "ops_reset"), Ct); });
    }

    [Fact]
    public async Task 用户配额为5时第6个成员创建返回CARDINALITY_USER_LIMIT_租户管理员计入_平台超管不计入_并发创建不越过上限()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme", limit: 5);
        var admin = (await CreatePerson(host, "管理员")).Person;
        await Join(host, tenant, admin.Id, "管理员", admin: true);
        for (var i = 0; i < 4; i++) await Join(host, tenant, (await CreatePerson(host, "成员" + i)).Person.Id, "成员" + i);
        using (host.Tenant.BeginScope(tenant.Id.ToString("D"))) Assert.Equal(5, await host.Service.CountUsersAsync(Ct));

        var sixth = (await CreatePerson(host, "第六个")).Person;
        var limit = await Rejected("CARDINALITY_USER_LIMIT", async () => await Join(host, tenant, sixth.Id, "第六个"));
        Assert.Equal(("5", 422), (limit.Args["limit"], limit.Code.Status));
        Assert.Equal("5", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", tenant.Id.ToString("D"))));
        // 租户管理员计入（同样被拒）；平台超管不是租户成员，不计入，也不受上限阻挡。
        await Rejected("CARDINALITY_USER_LIMIT", async () => await Join(host, tenant, sixth.Id, "第六个", admin: true));
        var platform = (await CreatePerson(host, "平台超管", platform: true)).Person;
        await Join(host, tenant, platform.Id, "平台超管");
        using (host.Tenant.BeginScope(tenant.Id.ToString("D"))) Assert.Equal(5, await host.Service.CountUsersAsync(Ct));
        // 提供给租户组件的计数端口读到同一个数。
        var counter = host.Loader.GetContext("identity", ScopeKey.Global).Inject<ITenantUserCounter>(TenantSettings.UserCounterKey);
        using (host.Tenant.BeginScope(tenant.Id.ToString("D"))) Assert.Equal(5, await counter.CountUsersAsync(Ct));

        // 并发抢最后一个名额：恰好一个成功，其余是 CARDINALITY_USER_LIMIT，最终人数就是上限。
        var contested = host.Tenants.Add("busy", limit: 5);
        for (var i = 0; i < 4; i++) await Join(host, contested, (await CreatePerson(host, "占位" + i)).Person.Id);
        var candidates = new List<Person>(); for (var i = 0; i < 6; i++) candidates.Add((await CreatePerson(host, "候选" + i)).Person);
        var results = await Task.WhenAll(candidates.Select(async candidate =>
        {
            try { await Join(host, contested, candidate.Id); return "ok"; }
            catch (QianErrorException error) { return error.Error.Code.Code; }
        }));
        Assert.Equal(1, results.Count(r => r == "ok"));
        Assert.Equal(5, results.Count(r => r == "CARDINALITY_USER_LIMIT"));
        Assert.Equal("5", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", contested.Id.ToString("D"))));
    }

    [Fact]
    public async Task 成员创建幂等_账号在租户内唯一且不可改_Membership严格按租户隔离()
    {
        await using var host = await AccountsHost.Start(postgres);
        var a = host.Tenants.Add("alpha"); var b = host.Tenants.Add("beta");
        var (person, _) = await CreatePerson(host, extra: [new(IdentifierKind.Username, "zhangsan")]);
        var first = await Join(host, a, person.Id, "张三");
        Assert.Equal(("zhangsan", AccountStatuses.Active, false, a.Id, person.Id), (first.Account, first.Status, first.IsTenantAdministrator, first.TenantId, first.PersonId));
        Assert.Equal(first.Id, (await Join(host, a, person.Id, "张三")).Id); // 重复建立幂等
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", a.Id.ToString("D"))));
        await Rejected("CONFLICT", async () => await Join(host, a, person.Id, "张三", id: Guid.CreateVersion7())); // 指针不一致
        var (other, _) = await CreatePerson(host, "李四");
        await Rejected("ACCOUNT_IN_USE", async () => await Join(host, a, other.Id, "李四", account: "ZhangSan"));
        var second = await Join(host, a, other.Id, "李四", account: "lisi");
        Assert.Equal("lisi", second.Account);
        await Rejected("INPUT_INVALID", async () => await Join(host, a, (await CreatePerson(host)).Person.Id, "无效", account: "bad account!"));
        // 同一个人在另一个租户可以有自己的成员身份；账号只在租户内唯一。
        var inB = await Join(host, b, other.Id, "李四", account: "lisi");
        Assert.NotEqual(second.Id, inB.Id);
        Assert.Equal(b.Id, inB.TenantId);

        // 租户 A 的作用域里看不到 B 的成员；没有租户作用域时（应用角色、强制 RLS）一行也看不到。
        using (host.Tenant.BeginScope(a.Id.ToString("D")))
        {
            Assert.Null(await host.Service.GetMembershipAsync(inB.Id, Ct));
            Assert.Equal(second.Id, (await host.Service.GetMembershipAsync(second.Id, Ct))!.Id);
        }
        using (host.Tenant.BeginScope(b.Id.ToString("D"))) Assert.Null(await host.Service.GetMembershipAsync(second.Id, Ct));
        Assert.Equal("0", await AppRoleText(postgres, "SELECT count(*) FROM plugin_qian_identity.memberships"));
        Assert.Equal("0", await AppRoleText(postgres, "SELECT count(*) FROM plugin_qian_identity.external_identities"));
        Assert.Equal("3", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships"));
        // 成员 ID 由调用方指定时（开户编排的幂等指针）原样使用。
        var pointer = Guid.CreateVersion7();
        var (third, _) = await CreatePerson(host, "王五");
        Assert.Equal(pointer, (await Join(host, a, third.Id, "王五", id: pointer)).Id);
        // 没有租户上下文时，租户级操作一律拒绝（租户只取自已验证的上下文，不取自请求）。
        await Rejected("AUTHZ_DENIED", async () => await host.Accounts.CreateMembershipAsync(new CreateMembership(third.Id, "x"), Ct));
    }

    [Fact]
    public async Task 开户编排端口按成员ID与Person幂等_管理员必须是已存在的Person_状态变更递增修订并使会话失效()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var provisioner = host.Loader.GetContext("identity", ScopeKey.Global).Inject<ITenantMembershipProvisioner>(TenantProvisioning.MembershipKey);
        var (owner, phone) = await CreatePerson(host, "老板");
        var pointer = new TenantAdministrator(Guid.CreateVersion7(), owner.Id.ToString("D"));
        using (host.Tenant.BeginScope(tenant.Id.ToString("D")))
        {
            await provisioner.ProvisionAsync(pointer, tenant.Id + ":membership", Ct);
            await provisioner.ProvisionAsync(pointer, tenant.Id + ":membership", Ct); // 重试不重复创建
            var member = (await host.Service.GetMembershipAsync(pointer.MembershipId, Ct))!;
            Assert.Equal((owner.Id, true, AccountStatuses.Active, owner.DisplayName), (member.PersonId, member.IsTenantAdministrator, member.Status, member.DisplayName));
            await Rejected("CONFLICT", async () => await provisioner.ProvisionAsync(new TenantAdministrator(Guid.CreateVersion7(), owner.Id.ToString("D")), "k", Ct));
            await Rejected("NOT_FOUND", async () => await provisioner.ProvisionAsync(new TenantAdministrator(Guid.CreateVersion7(), Guid.CreateVersion7().ToString("D")), "k", Ct));
            await Rejected("INPUT_INVALID", async () => await provisioner.ProvisionAsync(new TenantAdministrator(Guid.CreateVersion7(), "owner-c"), "k", Ct));
            await Rejected("INPUT_INVALID", async () => await provisioner.ProvisionAsync(new TenantAdministrator(Guid.Empty, owner.Id.ToString("D")), "k", Ct));
            await Rejected("INPUT_INVALID", async () => await provisioner.ProvisionAsync(pointer, "", Ct));
        }
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", tenant.Id.ToString("D"))));
        // 没有租户上下文时端口拒绝，不在平台作用域下替任何租户开户。
        await Rejected("AUTHZ_DENIED", async () => await provisioner.ProvisionAsync(pointer, "k", Ct));

        var signed = await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct);
        Assert.Equal(SignInStatus.Authenticated, signed.Status);
        var revision = signed.Session!.IdentityRevision;
        using (host.Tenant.BeginScope(tenant.Id.ToString("D")))
        {
            var disabled = await host.Accounts.SetMembershipStatusAsync(pointer.MembershipId, AccountStatuses.Disabled, "left_company", Ct);
            Assert.Equal((AccountStatuses.Disabled, 1L), (disabled.Status, disabled.IdentityRevision));
            await Rejected("INPUT_INVALID", async () => await host.Accounts.SetMembershipStatusAsync(pointer.MembershipId, AccountStatuses.Purged, "x", Ct));
            await Rejected("NOT_FOUND", async () => await host.Accounts.SetMembershipStatusAsync(Guid.CreateVersion7(), AccountStatuses.Disabled, "x", Ct));
        }
        Assert.False(await HasLiveSession(host, owner.Id));
        Assert.Equal(SignInStatus.Failed, (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Status); // 没有可用身份
        using (host.Tenant.BeginScope(tenant.Id.ToString("D"))) await host.Accounts.SetMembershipStatusAsync(pointer.MembershipId, AccountStatuses.Active, "back", Ct);
        var again = await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct);
        Assert.Equal(SignInStatus.Authenticated, again.Status);
        Assert.True(again.Session!.IdentityRevision > revision); // 成员修订只增不减，旧令牌随之失效
    }

    [Fact]
    public async Task 外部身份同租户connector加subject唯一_软删除_不能解绑唯一的登录方式()
    {
        await using var host = await AccountsHost.Start(postgres);
        var a = host.Tenants.Add("alpha"); var b = host.Tenants.Add("beta");
        var (one, _) = await CreatePerson(host, "甲");
        var (two, _) = await CreatePerson(host, "乙");
        var m1 = await Join(host, a, one.Id); var m2 = await Join(host, a, two.Id); var m3 = await Join(host, b, two.Id);
        using (host.Tenant.BeginScope(a.Id.ToString("D")))
        {
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m1.Id, "wecom", "ww-1", "union-1"), Ct);
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m1.Id, "wecom", "ww-1", "union-1"), Ct); // 幂等
            await Rejected("IDENTIFIER_IN_USE", async () => await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m2.Id, "wecom", "ww-1"), Ct));
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m2.Id, "oidc", "ww-1"), Ct); // 不同 connector 互不冲突
            await Rejected("NOT_FOUND", async () => await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m3.Id, "wecom", "ww-9"), Ct)); // 别的租户的成员看不见
        }
        using (host.Tenant.BeginScope(b.Id.ToString("D"))) await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m3.Id, "wecom", "ww-1"), Ct); // 另一租户可再用同一 subject
        Assert.Equal("3", await host.Text("SELECT count(*) FROM plugin_qian_identity.external_identities WHERE is_deleted = false"));
        using (host.Tenant.BeginScope(a.Id.ToString("D")))
        {
            await host.Accounts.UnlinkExternalIdentityAsync(m1.Id, "wecom", "ww-1", Ct);
            await Rejected("NOT_FOUND", async () => await host.Accounts.UnlinkExternalIdentityAsync(m1.Id, "wecom", "ww-1", Ct));
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m2.Id, "wecom", "ww-1"), Ct); // 软删除释放占用
        }
        Assert.Equal("true", await host.Text("SELECT is_deleted::text FROM plugin_qian_identity.external_identities WHERE membership_id = @m::uuid", ("m", m1.Id.ToString("D"))));
        // 没有密码、没有已验证联系方式、也没有别的外部身份的账号，不能解绑它唯一的登录方式。
        var upstreamOnly = await host.Accounts.CreatePersonAsync(new CreatePerson("纯上游", [new(IdentifierKind.Username, "upstream1")]), Ct);
        var m4 = await Join(host, a, upstreamOnly.Id);
        using (host.Tenant.BeginScope(a.Id.ToString("D")))
        {
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m4.Id, "oidc", "sub-4"), Ct);
            await Rejected("LAST_LOGIN_METHOD", async () => await host.Accounts.UnlinkExternalIdentityAsync(m4.Id, "oidc", "sub-4", Ct));
            await host.Accounts.LinkExternalIdentityAsync(new ExternalIdentityLink(m4.Id, "wecom", "sub-5"), Ct);
            await host.Accounts.UnlinkExternalIdentityAsync(m4.Id, "oidc", "sub-4", Ct); // 还有别的外部身份
            await Rejected("LAST_LOGIN_METHOD", async () => await host.Accounts.UnlinkExternalIdentityAsync(m4.Id, "wecom", "sub-5", Ct));
        }
    }

    [Fact]
    public async Task 状态变更递增身份修订_停用的账号密码正确也不签发会话_重复变更幂等()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var before = (await host.Service.GetPersonAsync(person.Id, Ct))!.IdentityRevision;
        var disabled = await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "admin", Ct);
        Assert.Equal((AccountStatuses.Disabled, before + 1), (disabled.Status, disabled.IdentityRevision));
        Assert.Equal(before + 1, (await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "admin", Ct)).IdentityRevision);
        var denied = await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct);
        Assert.Equal("ACCOUNT_DISABLED", denied.Error!.Code.Code);
        Assert.Equal("INVALID_CREDENTIALS", (await host.SignIn.SignInAsync(Input(phone, "wrong-password-1"), new LoginContext(), Ct)).Error!.Code.Code); // 凭据不对时不暴露账号状态
        await Rejected("INPUT_INVALID", async () => await host.Accounts.SetPersonStatusAsync(person.Id, "purged", "x", Ct));
        await Rejected("INPUT_INVALID", async () => await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Active, "", Ct));
        Assert.Equal(AccountStatuses.Active, (await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Active, "restored", Ct)).Status);
        Assert.Equal(SignInStatus.Authenticated, (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Status);
        // 管理员锁定（locked）与限时锁定不同：计数不起作用，只能管理员解锁，并发出锁定事件。
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Locked, "risk", Ct);
        Assert.Equal("ACCOUNT_LOCKED", (await host.SignIn.SignInAsync(Input(phone, Password), new LoginContext(), Ct)).Error!.Code.Code);
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.locked.v1'"));
    }

    private static async Task<bool> HasLiveSession(AccountsHost.Running host, Guid person) =>
        await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE person_id = @id::uuid AND revoked_at IS NULL", ("id", person.ToString("D"))) != "0";

    private static async Task<string> AppRoleText(PostgresFixture postgres, string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(postgres.AppConnectionString); await connection.OpenAsync(Ct);
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), CultureInfo.InvariantCulture) ?? "";
    }
}

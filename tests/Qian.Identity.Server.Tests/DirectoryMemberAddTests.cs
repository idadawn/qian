using System.Net;
using System.Text.Json;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 租户管理员新增成员（S1-IDP-017）：HTTP 入口 POST /api/directory/members 在真实宿主（受限应用角色 + 强制 RLS、同事务 Outbox）上的验收。
// 本宿主不装授权与审计组件，所以 granted_roles 为空、不写审计；初始角色 tenant.user（R158）与审计在 CRM 参考宿主的
// IdentityAuthzTests「新增成员」用例里经真实授权与审计组件验。
public sealed class DirectoryMemberAddTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string ContractPath = "contracts/identity/v1/directory-members.openapi.json";
    private const string Initial = "Initial-Pass-2026";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Code(JsonElement body) => body.GetProperty("code").GetString()!;
    private static string E164(string phone) => "+86" + phone;
    private static (string Code, string? Pointer) Field(JsonElement body)
    {
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        return (error.GetProperty("code").GetString()!, error.GetProperty("pointer").GetString());
    }

    private static Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Add(AccountsHost.Browser admin, object body, bool csrf = true) =>
        admin.Json(HttpMethod.Post, "api/directory/members", body, csrf);

    // 一个租户管理员成员，并以他登录该租户。
    private static async Task<(AccountsHost.Browser Browser, Person Person)> Administrator(AccountsHost.Running host, TenantDescriptor tenant, string name = "管理员")
    {
        var (person, phone) = await CreatePerson(host, name);
        await Join(host, tenant, person.Id, name, admin: true);
        var browser = host.NewBrowser();
        var login = await browser.Login(phone, Password, tenant: tenant.Id);
        Assert.Equal("authenticated", login.Body.GetProperty("status").GetString());
        return (browser, person);
    }

    // 库内事实（以超级用户绕过 RLS，只作断言）：自然人数、持某标识的自然人数、某租户的成员数与目录修订。
    private static async Task<(string Persons, string Identifiers, string Members, string Revision, int Events)> Facts(AccountsHost.Running host, TenantDescriptor tenant, string identifier) => (
        await host.Text("SELECT count(*) FROM plugin_qian_identity.persons"),
        await host.Text("SELECT count(*) FROM plugin_qian_identity.person_identifiers WHERE value = @v", ("v", identifier)),
        await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", tenant.Id.ToString("D"))),
        await host.Text("SELECT coalesce(max(revision), 0)::text FROM plugin_qian_identity.directory_revisions WHERE tenant_id = @t", ("t", tenant.Id.ToString("D"))),
        (await DirectoryEventTests.DirectoryEvents(host, tenant.Id)).Count);

    [Fact]
    public async Task 管理员新增成员_新号码新建自然人_首登强制改密后能登录本租户_响应日志与事件不含明文密码_同事务发出member_created()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("member-add");
        var (admin, adminPerson) = await Administrator(host, tenant);
        var phone = NextPhone();
        var added = await Add(admin, new { identifier = phone, display_name = "新成员", initial_password = Initial, account = "Newbie" });
        Assert.Equal(HttpStatusCode.OK, added.Status);
        Assert.Contains("no-store", added.Raw.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Initial, added.Body.GetRawText(), StringComparison.Ordinal);
        var membership = added.Body.GetProperty("membership");
        var memberId = membership.GetProperty("id").GetGuid(); var personId = membership.GetProperty("person_id").GetGuid();
        Assert.Equal((tenant.Id, "新成员", "active", false, "newbie"), (membership.GetProperty("tenant_id").GetGuid(), membership.GetProperty("display_name").GetString(),
            membership.GetProperty("status").GetString(), membership.GetProperty("is_tenant_administrator").GetBoolean(), membership.GetProperty("account").GetString()));
        Assert.True(added.Body.GetProperty("person_created").GetBoolean());
        Assert.Empty(added.Body.GetProperty("granted_roles").EnumerateArray());
        // 真实响应符合发布的成员管理契约。
        Assert.Empty(DirectoryAgentContract.Load(ContractPath).ValidateComponent("AddMemberResult", added.Body));
        // 库内：管理员录入的手机号按未验证保存，首次登录须改密，凭据只有 Argon2id 哈希；登录索引含本租户。
        Assert.Equal("", await host.Text("SELECT coalesce(verified_at::text, '') FROM plugin_qian_identity.person_identifiers WHERE person_id = @p", ("p", personId)));
        Assert.Equal("True", await host.Text("SELECT must_change_password FROM plugin_qian_identity.persons WHERE id = @p", ("p", personId)));
        Assert.StartsWith("$argon2id$", await host.Text("SELECT password_hash FROM plugin_qian_identity.persons WHERE id = @p", ("p", personId)), StringComparison.Ordinal);
        Assert.Equal(tenant.Id.ToString("D"), await host.Text("SELECT array_to_string(tenant_ids, ',') FROM plugin_qian_identity.persons WHERE id = @p", ("p", personId)));
        // 新成员登录：先被引导修改初始密码，此前没有会话；改密后建立本租户会话，授权端点签发授权码。
        var member = host.NewBrowser();
        var login = await member.Login(phone, Initial);
        Assert.Equal("password_change_required", login.Body.GetProperty("status").GetString());
        Assert.False(member.HasAccountCookie); Assert.Null(await member.Code());
        var changed = await member.Json(HttpMethod.Post, "api/identity/login/password", new { ticket = login.Body.GetProperty("ticket").GetString(), new_password = "Brand-New-Pass-7" });
        Assert.Equal("authenticated", changed.Body.GetProperty("status").GetString());
        var me = await member.Json(HttpMethod.Get, "api/identity/me");
        Assert.Equal((tenant.Id, memberId, personId), (me.Body.GetProperty("tenant_id").GetGuid(), me.Body.GetProperty("membership_id").GetGuid(), me.Body.GetProperty("person_id").GetGuid()));
        Assert.NotNull(await member.Code());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Login(phone, Initial)).Status);
        // 事件：恰一条 directory.member.created.v1，载荷符合 Schema，操作人是发起新增的管理员；Outbox 与日志都不含明文密码。
        var created = Assert.Single(await DirectoryEventTests.DirectoryEvents(host, tenant.Id), envelope => envelope.Payload.GetProperty("membership_id").GetGuid() == memberId);
        Assert.Equal(DirectoryEventIds.MemberCreated, created.Type); DirectoryEventTests.AssertSchema(created);
        Assert.Equal((personId, "active"), (created.Payload.GetProperty("person_id").GetGuid(), created.Payload.GetProperty("status").GetString()));
        Assert.Equal((EventActorKind.User, adminPerson.Id.ToString("D")), (created.Actor.Kind, created.Actor.Id));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.outbox_messages WHERE payload LIKE @p OR payload LIKE @q", ("p", "%" + Initial + "%"), ("q", "%Brand-New-Pass-7%")));
        Assert.DoesNotContain(host.Logs.Messages, message => message.Contains(Initial, StringComparison.Ordinal) || message.Contains("Brand-New-Pass-7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 号码或用户名已对应自然人时绑定为成员不新建也不改凭据_同一自然人重复新增与已停用已删除成员返回结构化冲突()
    {
        await using var host = await AccountsHost.Start(postgres); var a = host.Tenants.Add("member-bind"); var b = host.Tenants.Add("member-bind-other");
        var (admin, _) = await Administrator(host, a);
        var (existing, phone) = await CreatePerson(host, "老用户"); await Join(host, b, existing.Id, "老用户（乙方）");
        var persons = await host.Text("SELECT count(*) FROM plugin_qian_identity.persons");
        // 号码已对应自然人：绑定为甲方成员，显示名取请求的值；请求里的初始密码不使用，他的凭据不变。
        var bound = await Add(admin, new { identifier = E164(phone), display_name = "老用户（甲方）", initial_password = Initial });
        Assert.Equal(HttpStatusCode.OK, bound.Status);
        Assert.False(bound.Body.GetProperty("person_created").GetBoolean());
        Assert.Equal((existing.Id, "老用户（甲方）"), (bound.Body.GetProperty("membership").GetProperty("person_id").GetGuid(), bound.Body.GetProperty("membership").GetProperty("display_name").GetString()));
        Assert.Equal(persons, await host.Text("SELECT count(*) FROM plugin_qian_identity.persons"));
        Assert.Equal("False", await host.Text("SELECT must_change_password FROM plugin_qian_identity.persons WHERE id = @p", ("p", existing.Id)));
        Assert.Equal("authenticated", (await host.NewBrowser().Login(phone, Password, tenant: a.Id)).Body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Login(phone, Initial, tenant: a.Id)).Status);
        Assert.Equal(string.Join(',', new[] { b.Id.ToString("D"), a.Id.ToString("D") }), await host.Text("SELECT array_to_string(tenant_ids, ',') FROM plugin_qian_identity.persons WHERE id = @p", ("p", existing.Id)));
        // 同一自然人重复新增：409 DIRECTORY_MEMBER_EXISTS，位置 #/identifier，不写任何东西。
        var before = await Facts(host, a, E164(phone));
        var again = await Add(admin, new { identifier = phone, display_name = "再加一次" });
        Assert.Equal((HttpStatusCode.Conflict, "DIRECTORY_MEMBER_EXISTS", "#/identifier"), (again.Status, Code(again.Body), again.Body.GetProperty("pointer").GetString()));
        Assert.Equal(before, await Facts(host, a, E164(phone)));
        // 用户名：已对应自然人时同样绑定（大小写不敏感），账号缺省取他的用户名；新用户名新建自然人，用户名标识不需核验。
        var (named, _) = await CreatePerson(host, "赵六", extra: [new IdentifierInput(IdentifierKind.Username, "zhaoliu", true)]);
        var byName = await Add(admin, new { identifier = "ZhaoLiu", display_name = "赵六" });
        Assert.Equal((HttpStatusCode.OK, false, named.Id, "zhaoliu"), (byName.Status, byName.Body.GetProperty("person_created").GetBoolean(),
            byName.Body.GetProperty("membership").GetProperty("person_id").GetGuid(), byName.Body.GetProperty("membership").GetProperty("account").GetString()));
        var fresh = await Add(admin, new { identifier = "qianqi", display_name = "钱七", initial_password = Initial });
        Assert.Equal((HttpStatusCode.OK, true, "qianqi"), (fresh.Status, fresh.Body.GetProperty("person_created").GetBoolean(), fresh.Body.GetProperty("membership").GetProperty("account").GetString()));
        Assert.NotEqual("", await host.Text("SELECT coalesce(verified_at::text, '') FROM plugin_qian_identity.person_identifiers WHERE value = 'qianqi'"));
        // 已停用的成员不恢复：409，状态仍是 disabled；已从目录删除的成员同样 409，不被意外恢复。
        var boundId = bound.Body.GetProperty("membership").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.Json(HttpMethod.Post, $"api/directory/members/{boundId:D}/status", new { status = "disabled", reason = "test" })).Status);
        Assert.Equal("DIRECTORY_MEMBER_EXISTS", Code((await Add(admin, new { identifier = phone, display_name = "老用户" })).Body));
        Assert.Equal("disabled", await host.Text("SELECT status FROM plugin_qian_identity.memberships WHERE id = @id", ("id", boundId)));
        var freshId = fresh.Body.GetProperty("membership").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.Json(HttpMethod.Delete, $"api/directory/members/{freshId:D}")).Status);
        Assert.Equal("DIRECTORY_MEMBER_EXISTS", Code((await Add(admin, new { identifier = "qianqi", display_name = "钱七" })).Body));
        Assert.Equal("disabled|true", await host.Text("SELECT status || '|' || directory_deleted FROM plugin_qian_identity.memberships WHERE id = @id", ("id", freshId)));
        // 字段错误都带位置、不写任何东西：新号码缺初始密码、邮箱或非法标识、弱初始密码、指定账号已被占用。
        var newPhone = NextPhone(); var untouched = await Facts(host, a, E164(newPhone));
        var missing = await Add(admin, new { identifier = newPhone, display_name = "缺密码" });
        Assert.Equal((HttpStatusCode.BadRequest, "INPUT_INVALID", ("REQUIRED", (string?)"#/initial_password")), (missing.Status, Code(missing.Body), Field(missing.Body)));
        foreach (var identifier in new[] { "someone@example.test", "1", "张三" })
        {
            var invalid = await Add(admin, new { identifier, display_name = "非法标识", initial_password = Initial });
            Assert.Equal((HttpStatusCode.BadRequest, "#/identifier"), (invalid.Status, Field(invalid.Body).Pointer));
        }
        var weak = await Add(admin, new { identifier = newPhone, display_name = "弱密码", initial_password = "short" });
        Assert.Equal(((HttpStatusCode)422, "PASSWORD_POLICY_VIOLATION", "#/initial_password"), (weak.Status, Code(weak.Body), weak.Body.GetProperty("pointer").GetString()));
        var taken = await Add(admin, new { identifier = newPhone, display_name = "账号重复", initial_password = Initial, account = "zhaoliu" });
        Assert.Equal((HttpStatusCode.Conflict, "ACCOUNT_IN_USE", "#/account"), (taken.Status, Code(taken.Body), taken.Body.GetProperty("pointer").GetString()));
        Assert.Equal(untouched, await Facts(host, a, E164(newPhone)));
    }

    [Fact]
    public async Task 达到用户配额返回CARDINALITY_USER_LIMIT且无写入_平台身份停用锁定账号不能加为成员()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("member-quota", limit: 2);
        var (admin, _) = await Administrator(host, tenant);
        var (present, _) = await CreatePerson(host, "已有成员"); await Join(host, tenant, present.Id, "已有成员");
        // 满额（管理员与已有成员共 2 人）：新建与绑定都 422，自然人、标识、成员、目录修订与事件都不变。
        var phone = NextPhone(); var before = await Facts(host, tenant, E164(phone));
        var full = await Add(admin, new { identifier = phone, display_name = "超额", initial_password = Initial });
        Assert.Equal(((HttpStatusCode)422, "CARDINALITY_USER_LIMIT"), (full.Status, Code(full.Body)));
        Assert.Equal(before, await Facts(host, tenant, E164(phone)));
        var (outsider, outsiderPhone) = await CreatePerson(host, "外人"); before = await Facts(host, tenant, E164(outsiderPhone));
        Assert.Equal("CARDINALITY_USER_LIMIT", Code((await Add(admin, new { identifier = outsiderPhone, display_name = "外人" })).Body));
        Assert.Equal(before, await Facts(host, tenant, E164(outsiderPhone)));
        // 并发：只剩一个名额时三个请求同时到达，恰好一个成功，其余 422；成员计数锁串行判定，不会一起越过上限。
        var roomy = host.Tenants.Add("member-race", limit: 2); var (raceAdmin, _) = await Administrator(host, roomy);
        var racers = await Task.WhenAll(Enumerable.Range(0, 3).Select(index => Add(raceAdmin, new { identifier = NextPhone(), display_name = "并发" + index, initial_password = Initial })));
        Assert.Equal(1, racers.Count(result => result.Status == HttpStatusCode.OK));
        Assert.All(racers.Where(result => result.Status != HttpStatusCode.OK), result => Assert.Equal("CARDINALITY_USER_LIMIT", Code(result.Body)));
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = @t", ("t", roomy.Id.ToString("D"))));
        // 平台身份、停用或锁定的账号（另一不限额的租户）：422 DIRECTORY_MEMBER_INELIGIBLE，位置 #/identifier，不区分原因，不写任何东西。
        var open = host.Tenants.Add("member-eligibility"); var (openAdmin, _) = await Administrator(host, open);
        var (_, platformPhone) = await CreatePerson(host, "平台运营", platform: true);
        var (disabled, disabledPhone) = await CreatePerson(host, "已停用"); await host.Accounts.SetPersonStatusAsync(disabled.Id, AccountStatuses.Disabled, "test", Ct);
        var (locked, lockedPhone) = await CreatePerson(host, "已锁定"); await host.Accounts.SetPersonStatusAsync(locked.Id, AccountStatuses.Locked, "test", Ct);
        foreach (var candidate in new[] { platformPhone, disabledPhone, lockedPhone })
        {
            before = await Facts(host, open, E164(candidate));
            var refused = await Add(openAdmin, new { identifier = candidate, display_name = "不能加入", initial_password = Initial });
            Assert.Equal(((HttpStatusCode)422, "DIRECTORY_MEMBER_INELIGIBLE", "#/identifier"), (refused.Status, Code(refused.Body), refused.Body.GetProperty("pointer").GetString()));
            Assert.Equal(before, await Facts(host, open, E164(candidate)));
        }
        // 账号恢复启用后可以加入。
        await host.Accounts.SetPersonStatusAsync(disabled.Id, AccountStatuses.Active, "test", Ct);
        Assert.Equal(HttpStatusCode.OK, (await Add(openAdmin, new { identifier = disabledPhone, display_name = "已恢复" })).Status);
    }

    [Fact]
    public async Task 非管理员与平台会话调用被拒且无写入_缺防伪与带租户字段被拒_另一租户看不到本租户新成员_并发新建同一号码只有一个自然人()
    {
        await using var host = await AccountsHost.Start(postgres); var a = host.Tenants.Add("member-guard-a"); var b = host.Tenants.Add("member-guard-b");
        var (admin, _) = await Administrator(host, a, "甲管理员"); var (otherAdmin, _) = await Administrator(host, b, "乙管理员");
        var (regular, regularPhone) = await CreatePerson(host, "普通成员"); await Join(host, a, regular.Id, "普通成员");
        var member = host.NewBrowser(); Assert.Equal("authenticated", (await member.Login(regularPhone, Password, tenant: a.Id)).Body.GetProperty("status").GetString());
        var (_, platformPhone) = await CreatePerson(host, "平台运营", platform: true);
        var platform = host.NewBrowser(); Assert.Equal("authenticated", (await platform.Login(platformPhone, Password)).Body.GetProperty("status").GetString());
        Assert.False((await platform.Json(HttpMethod.Get, "api/identity/me")).Body.TryGetProperty("tenant_id", out _)); // 平台身份：没有租户
        var phone = NextPhone(); var body = new { identifier = phone, display_name = "被拒", initial_password = Initial };
        var before = await Facts(host, a, E164(phone)); var beforeB = await Facts(host, b, E164(phone));
        // 非管理员成员与平台会话：403（平台会话没有租户成员身份，不合成成员）；未登录 401。
        Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), Result(await Add(member, body)));
        Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), Result(await Add(platform, body)));
        Assert.Equal((HttpStatusCode.Unauthorized, "UNAUTHENTICATED"), Result(await Add(host.NewBrowser(), body)));
        // 管理员也要防伪；请求体里的租户字段一律拒绝（未知字段），不改目标租户；请求体必须是 JSON。
        Assert.Equal((HttpStatusCode.BadRequest, "INPUT_INVALID"), Result(await Add(admin, body, csrf: false)));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_REQUEST"), Result(await Add(admin, new { identifier = phone, display_name = "带租户", initial_password = Initial, tenant_id = b.Id })));
        var (header, token) = await admin.Csrf();
        using (var form = new HttpRequestMessage(HttpMethod.Post, "api/directory/members") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["identifier"] = phone }) })
        {
            form.Headers.Add(header, token);
            using var response = await admin.Http.SendAsync(form, Ct);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        Assert.Equal(before, await Facts(host, a, E164(phone))); Assert.Equal(beforeB, await Facts(host, b, E164(phone)));
        // 甲方新增的成员：乙方管理员的列表、按 id 批量与成员归属都看不到；甲方管理员看得到。
        var added = await Add(admin, new { identifier = phone, display_name = "甲方新人", initial_password = Initial });
        Assert.Equal(HttpStatusCode.OK, added.Status);
        var id = added.Body.GetProperty("membership").GetProperty("id").GetGuid();
        static IEnumerable<Guid> Ids(JsonElement list) => list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("membership").GetProperty("id").GetGuid());
        Assert.DoesNotContain(id, Ids((await otherAdmin.Json(HttpMethod.Get, "api/directory/members?limit=200")).Body));
        Assert.Empty(Ids((await otherAdmin.Json(HttpMethod.Get, $"api/directory/members?ids={id:D}")).Body));
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.Json(HttpMethod.Get, $"api/directory/members/{id:D}/relations")).Status);
        Assert.Contains(id, Ids((await admin.Json(HttpMethod.Get, "api/directory/members?limit=200")).Body));
        // 两个租户同时用同一个新号码新增：只有一个自然人，两边的成员都指向他（晚到的一方解析到他、改走绑定）。
        var shared = NextPhone();
        var both = await Task.WhenAll(Add(admin, new { identifier = shared, display_name = "同号甲", initial_password = Initial }),
            Add(otherAdmin, new { identifier = shared, display_name = "同号乙", initial_password = Initial }));
        Assert.All(both, result => Assert.Equal(HttpStatusCode.OK, result.Status));
        Assert.Single(both.Select(result => result.Body.GetProperty("membership").GetProperty("person_id").GetGuid()).Distinct());
        Assert.Single(both, result => result.Body.GetProperty("person_created").GetBoolean());
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.person_identifiers WHERE value = @v", ("v", E164(shared))));
    }

    private static (HttpStatusCode, string) Result((HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw) response) => (response.Status, Code(response.Body));
}

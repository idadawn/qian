using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Capabilities;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using Qian.Testing.Containers;

namespace Qian.Sample.Crm.Tests;

// 运营后台维护租户管理员（S1-TEN-010）：账号查找与新建、替换管理员、重置管理员密码。
// 身份组件、租户运营宿主、超管旁路与两条审计链都是真的；成员、凭据与会话只经 qian.identity 的端口改变。
public sealed partial class TenantAdminTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    private const string Accounts = "/api/tenancy/admin-accounts";
    private const string TemporaryPassword = "Temp-Pass-2026";

    [Fact]
    public async Task 管理员维护只对平台超管开放_账号经身份组件查找或新建后开户_替换管理员转移owner_满额与开通未完成时拒绝_端点进契约()
    {
        await using var ops = await StartOps();
        var (business, client) = (ops.Business, ops.Client);
        var root = business.Token(null, "", "root", "user");
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        var phone = NewPhone();
        (HttpMethod Method, string Path, object? Body)[] endpoints =
        [
            (HttpMethod.Post, Accounts + ":lookup", new TenantAdminAccountLookup(phone)),
            (HttpMethod.Post, Accounts, new CreateTenantAdminAccount("管理员甲", TenantAdminIdentifierKind.Phone, phone, TemporaryPassword)),
            (HttpMethod.Get, Admins(A), null),
            (HttpMethod.Put, Admins(A), new ReplaceTenantAdmins([new(Guid.NewGuid())])),
            (HttpMethod.Post, Admins(A) + "/" + Guid.NewGuid() + ":reset-password", new ResetTenantAdminPassword(TemporaryPassword)),
        ];
        // 无凭据 401；租户主体、非超管的平台用户与 Agent 一律 403（运营入口只收人类平台用户）。
        foreach (var token in new[] { null, business.Token(A, "", "root", "user"), business.Token(null, "", "intruder", "user"), business.Token(null, "", "root", "agent") })
            foreach (var (method, path, body) in endpoints)
                await Rejected(await Send(client, method, path, token, body), token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, token is null ? "UNAUTHENTICATED" : "AUTHZ_DENIED");
        // 目标租户只取自路径：请求体任何一层带 tenantId / tenant_id / id 都在绑定时拒绝。
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
        {
            await Rejected(await Send(client, HttpMethod.Put, Admins(A), root, new Dictionary<string, object?> { ["items"] = new[] { new { subjectId = Guid.NewGuid() } }, [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
            await Rejected(await Send(client, HttpMethod.Put, Admins(A), root, new { items = new[] { new Dictionary<string, object?> { ["subjectId"] = Guid.NewGuid(), [forbidden] = A } } }), HttpStatusCode.BadRequest, "BAD_REQUEST");
            await Rejected(await Send(client, HttpMethod.Post, Accounts, root, new Dictionary<string, object?>
                { ["displayName"] = "甲", ["identifierKind"] = "phone", ["identifier"] = phone, ["initialPassword"] = TemporaryPassword, [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
            await Rejected(await Send(client, HttpMethod.Post, Admins(A) + "/" + Guid.NewGuid() + ":reset-password", root, new Dictionary<string, object?> { ["newPassword"] = TemporaryPassword, [forbidden] = A }),
                HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        Assert.Equal("#/items", await Rejected(await Send(client, HttpMethod.Put, Admins(A), root, new ReplaceTenantAdmins([])), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/items", await Rejected(await Send(client, HttpMethod.Put, Admins(A), root, new ReplaceTenantAdmins([new(Guid.NewGuid()), new(Guid.NewGuid())])), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/items/0/subjectId", await Rejected(await Send(client, HttpMethod.Put, Admins(A), root, new ReplaceTenantAdmins([new(Guid.Empty)])), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/initialPassword", await Rejected(await Send(client, HttpMethod.Post, Accounts, root, new CreateTenantAdminAccount("甲", TenantAdminIdentifierKind.Phone, phone, "short")),
            HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/identifier", await Rejected(await Send(client, HttpMethod.Post, Accounts + ":lookup", root, new TenantAdminAccountLookup("")), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Empty(await Bypasses(source)); // 401 / 403 / 400 都在超管旁路之前拒绝，平台链上没有放行记录。

        // 新建账号：一个登录标识加初始密码；响应只有掩码，没有原值与密码。身份组件的错误位置换成运营请求体的字段。
        string raw;
        using (var response = await Send(client, HttpMethod.Post, Accounts, root, new CreateTenantAdminAccount("管理员甲", TenantAdminIdentifierKind.Phone, phone, TemporaryPassword)))
        { raw = await response.Content.ReadAsStringAsync(Ct); Assert.True(response.StatusCode == HttpStatusCode.Created, raw); Assert.Null(response.Headers.Location); }
        Assert.DoesNotContain(phone, raw, StringComparison.Ordinal); Assert.DoesNotContain(TemporaryPassword, raw, StringComparison.Ordinal);
        var accountA = JsonSerializer.Deserialize<TenantAdminAccount>(raw, JsonSerializerOptions.Web)!;
        Assert.Equal(("管理员甲", AccountStatuses.Active), (accountA.DisplayName, accountA.Status));
        var masked = Assert.Single(accountA.Identifiers);
        Assert.Equal((TenantAdminIdentifierKind.Phone, false), (masked.Kind, masked.Verified));
        Assert.Null(await Rejected(await Send(client, HttpMethod.Post, Accounts, root, new CreateTenantAdminAccount("重复", TenantAdminIdentifierKind.Phone, phone, TemporaryPassword)),
            HttpStatusCode.Conflict, "IDENTIFIER_IN_USE"));
        Assert.Equal("#/identifier", await Rejected(await Send(client, HttpMethod.Post, Accounts, root, new CreateTenantAdminAccount("坏号", TenantAdminIdentifierKind.Phone, "12", TemporaryPassword)),
            HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/initialPassword", await Rejected(await Send(client, HttpMethod.Post, Accounts, root, new CreateTenantAdminAccount("空白", TenantAdminIdentifierKind.Phone, NewPhone(), " " + TemporaryPassword)),
            HttpStatusCode.UnprocessableEntity, "PASSWORD_POLICY_VIOLATION"));
        // 查找：按登录标识（放在请求体）找到可任管理员的账号；不存在与具备平台访问资格的都是 404。
        Assert.Equal((accountA.SubjectId, "管理员甲"), await Lookup(phone));
        await Rejected(await Send(client, HttpMethod.Post, Accounts + ":lookup", root, new TenantAdminAccountLookup(NewPhone())), HttpStatusCode.NotFound, "NOT_FOUND");
        var (superadmin, superPhone) = await NewPerson(business, "平台超管", platform: true);
        await Rejected(await Send(client, HttpMethod.Post, Accounts + ":lookup", root, new TenantAdminAccountLookup(superPhone)), HttpStatusCode.NotFound, "NOT_FOUND");

        // 新建的账号作开户管理员：成员、指针与 tenant.owner 由开通编排第 05–07 步完成。
        var memberA = Guid.CreateVersion7();
        var tenant = await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, "/api/tenancy/tenants", root,
            new CreateTenant("admins", "管理员租户") { Administrator = new(memberA, accountA.SubjectId.ToString("D")) }), HttpStatusCode.Created);
        var id = tenant.Id.ToString();
        var initial = Assert.Single((await Read<TenantAdminList>(await Send(client, HttpMethod.Get, Admins(id), root), HttpStatusCode.OK)).Items);
        Assert.Equal((memberA, accountA.SubjectId, "管理员甲", AccountStatuses.Active), (initial.MembershipId, initial.SubjectId, initial.DisplayName, initial.Status));
        Assert.Equal(TenantAdminIdentifierKind.Phone, Assert.Single(initial.Identifiers).Kind);
        Assert.Equal(accountA.SubjectId.ToString("D"), await Owners(id));
        // 运营列表（S1-TEN-018）：管理员列取当前成员的显示名，用量经身份侧计数端口统计，开户管理员计 1。
        var listed = Assert.Single((await Read<JsonElement>(await Send(client, HttpMethod.Get, "/api/tenancy/tenants?keyword=admins", root), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal(("管理员甲", 1), (listed.GetProperty("administratorName").GetString(), listed.GetProperty("usersUsed").GetInt32()));

        // 替换为已有账号：新管理员建立成员并得到 tenant.owner；原管理员撤销 owner 与管理员标志，成员身份保留。
        var (personB, phoneB) = await NewPerson(business, "管理员乙");
        Assert.Equal((personB.Id, "管理员乙"), await Lookup(phoneB));
        var replaced = Assert.Single((await Read<TenantAdminList>(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(personB.Id)])), HttpStatusCode.OK)).Items);
        var memberB = replaced.MembershipId;
        Assert.Equal((personB.Id, "管理员乙"), (replaced.SubjectId, replaced.DisplayName)); Assert.NotEqual(memberA, memberB);
        Assert.Equal(memberB.ToString(), await Text($"SELECT membership_id::text FROM plugin_qian_tenancy.tenant_admins WHERE tenant_id = '{id}'"));
        Assert.Equal(personB.Id.ToString("D"), await Owners(id));
        Assert.Equal(("false|active", "true|active"), (await Member(memberA), await Member(memberB)));
        // 同一请求重试收敛：成员与指针不变，不重复记审计。
        Assert.Equal(memberB, Assert.Single((await Read<TenantAdminList>(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(personB.Id)])), HttpStatusCode.OK)).Items).MembershipId);
        Assert.Equal("2", await Text($"SELECT count(*)::text FROM plugin_qian_identity.memberships WHERE tenant_id = '{id}'"));

        // 不能指定的人：不存在、具备平台访问资格、已停用的账号；与开户不同的租户路径不存在时 404。
        foreach (var subject in new[] { Guid.NewGuid(), superadmin.Id, await Disabled() })
            Assert.Equal("#/items/0/subjectId", await Rejected(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(subject)])), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        await Rejected(await Send(client, HttpMethod.Get, Admins(Guid.NewGuid().ToString()), root), HttpStatusCode.NotFound, "NOT_FOUND");
        await Rejected(await Send(client, HttpMethod.Put, Admins(Guid.NewGuid().ToString()), root, new ReplaceTenantAdmins([new(personB.Id)])), HttpStatusCode.NotFound, "NOT_FOUND");
        // 开通第 05–07 步没有全部成功时先续跑开通，替换返回 409。
        await Admin($"UPDATE plugin_qian_tenancy.tenant_provision_steps SET status = 'failed' WHERE tenant_id = '{id}' AND step = '07:owner';");
        await Rejected(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(accountA.SubjectId)])), HttpStatusCode.Conflict, "CONFLICT");
        await Admin($"UPDATE plugin_qian_tenancy.tenant_provision_steps SET status = 'succeeded' WHERE tenant_id = '{id}' AND step = '07:owner';");
        Assert.Equal(personB.Id.ToString("D"), await Owners(id));

        // 满额：新成员经身份组件的配额判定被拒，什么都没改；已是成员的原管理员不占新名额，可以换回。
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET quotas = '{{\"users\":2}}' WHERE id = '{id}'::uuid;");
        var (personC, _) = await NewPerson(business, "管理员丙");
        await Rejected(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(personC.Id)])), HttpStatusCode.UnprocessableEntity, "CARDINALITY_USER_LIMIT");
        Assert.Equal((memberB.ToString(), personB.Id.ToString("D"), "0"), (await Text($"SELECT membership_id::text FROM plugin_qian_tenancy.tenant_admins WHERE tenant_id = '{id}'"),
            await Owners(id), await Text($"SELECT count(*)::text FROM plugin_qian_identity.memberships WHERE tenant_id = '{id}' AND person_id = '{personC.Id}'")));
        Assert.Equal(memberA, Assert.Single((await Read<TenantAdminList>(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(accountA.SubjectId)])), HttpStatusCode.OK)).Items).MembershipId);
        Assert.Equal(accountA.SubjectId.ToString("D"), await Owners(id));
        Assert.Equal(("true|active", "false|active"), (await Member(memberA), await Member(memberB)));

        // 租户链：每次指针变化补一条 admin.assign，只记成员指针，没有登录标识。平台链：每次放行都有超管旁路记录。
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(id))
        {
            var assigned = (await source.Audit().ReadAsync(0, 100, cancellationToken: Ct)).Where(entry => entry.Record.CapabilityId == "qian.tenancy/admin.assign").Select(entry => entry.Record).ToArray();
            Assert.Equal([(memberB, memberA), (memberA, memberB)], assigned.Select(record =>
                (record.Payload.GetProperty("membershipId").GetGuid(), record.Payload.GetProperty("previousMembershipId").GetGuid())));
            Assert.All(assigned, record =>
            {
                Assert.Equal(("user", "root", "api", "success"), (record.ActorType, record.ActorId, record.Channel, record.Result));
                Assert.DoesNotContain(phoneB, record.Payload.GetRawText(), StringComparison.Ordinal);
            });
            Assert.True((await source.Audit().VerifyAsync(Ct)).Valid);
        }
        var bypasses = await Bypasses(source);
        Assert.All(bypasses, record => Assert.Equal("superadmin", record.Payload.GetProperty("bypass").GetString()));
        Assert.Contains(bypasses, record => record.Payload.GetProperty("permission").GetString() == "qian.tenancy:admin:create" && record.Resource == "tenancy/admin-accounts");
        Assert.Contains(bypasses, record => record.Payload.GetProperty("permission").GetString() == "qian.tenancy:admin:assign" && record.Resource == "tenancy/tenants/" + id);
        Assert.True((await source.Audit().VerifyPlatformAsync(source, Ct)).Valid);

        // 契约：四条新路径进组件 OpenAPI 文档（与发布契约的比对与 RT-006 准入见 TenantOperationsTests）。
        var document = JsonNode.Parse((await ops.Management.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.tenancy", Ct)).GetRawText())!;
        var paths = document["paths"]!.AsObject();
        Assert.True(paths[Accounts + ":lookup"]!["post"] is not null && paths[Accounts]!["post"] is not null);
        Assert.True(paths["/api/tenancy/tenants/{id}/admins"]!["get"] is not null && paths["/api/tenancy/tenants/{id}/admins"]!["put"] is not null);
        var replace = TenantContractSchemaTests.Resolve(document, paths["/api/tenancy/tenants/{id}/admins"]!["put"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal((1, 1), (replace["properties"]!["items"]!["minItems"]!.GetValue<int>(), replace["properties"]!["items"]!["maxItems"]!.GetValue<int>()));
        var reset = TenantContractSchemaTests.Resolve(document, paths["/api/tenancy/tenants/{id}/admins/{membershipId}:reset-password"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(8, reset["properties"]!["newPassword"]!["minLength"]!.GetValue<int>());

        async Task<(Guid, string)> Lookup(string identifier)
        {
            var account = await Read<TenantAdminAccount>(await Send(client, HttpMethod.Post, Accounts + ":lookup", root, new TenantAdminAccountLookup(identifier)), HttpStatusCode.OK);
            return (account.SubjectId, account.DisplayName);
        }
        Task<string?> Owners(string tenantId) => Text("SELECT string_agg(a.subject_id, ',' ORDER BY a.subject_id) FROM plugin_qian_authz.role_assignments a " +
            $"JOIN plugin_qian_authz.roles r ON a.role_id = r.id WHERE a.tenant_id = '{tenantId}' AND r.code = 'tenant.owner'");
        Task<string?> Member(Guid membership) => Text($"SELECT is_tenant_administrator::text || '|' || status FROM plugin_qian_identity.memberships WHERE id = '{membership}'");
        async Task<Guid> Disabled()
        {
            var (person, _) = await NewPerson(business, "已停用");
            await business.Identity.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "test", Ct);
            return person.Id;
        }
    }

    [Fact]
    public async Task 重置管理员密码经身份组件_旧密码与全部会话立即失效_下次登录强制改密_只对当前管理员_审计不含密码_标humanOnly与确认()
    {
        await using var ops = await StartOps();
        var (business, client) = (ops.Business, ops.Client);
        var root = business.Token(null, "", "root", "user");
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        var identity = business.Identity.Loader!.GetContext("identity", ScopeKey.Global).Inject<IIdentityService>(IdentityDefinition.Key);
        var (former, formerPhone) = await NewPerson(business, "原管理员");
        var formerMember = Guid.CreateVersion7();
        var tenant = await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, "/api/tenancy/tenants", root,
            new CreateTenant("resets", "重置租户") { Administrator = new(formerMember, former.Id.ToString("D")) }), HttpStatusCode.Created);
        var id = tenant.Id.ToString();
        await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, $"/api/tenancy/tenants/{id}:activate", root, new ActivateTenant()), HttpStatusCode.OK);
        var (admin, phone) = await NewPerson(business, "现任管理员");
        var member = Assert.Single((await Read<TenantAdminList>(await Send(client, HttpMethod.Put, Admins(id), root, new ReplaceTenantAdmins([new(admin.Id)])), HttpStatusCode.OK)).Items).MembershipId;
        var before = await SignInAs(phone, IdentityPassword);
        Assert.Equal((SignInStatus.Authenticated, tenant.Id, member), (before.Status, before.Session!.TenantId, before.Session.MembershipId));
        Assert.NotEmpty(await identity.ListSessionsAsync(admin.Id, Ct));

        // 只对当前管理员：原管理员、不存在的成员、别的租户路径都是 404；策略不允许的临时密码按运营请求体的字段报错。
        foreach (var target in new[] { formerMember, Guid.NewGuid() })
            await Rejected(await Send(client, HttpMethod.Post, Reset(id, target), root, new ResetTenantAdminPassword(TemporaryPassword)), HttpStatusCode.NotFound, "NOT_FOUND");
        await Rejected(await Send(client, HttpMethod.Post, Reset(A, member), root, new ResetTenantAdminPassword(TemporaryPassword)), HttpStatusCode.NotFound, "NOT_FOUND");
        Assert.Equal("#/newPassword", await Rejected(await Send(client, HttpMethod.Post, Reset(id, member), root, new ResetTenantAdminPassword("short")), HttpStatusCode.BadRequest, "INPUT_INVALID"));
        Assert.Equal("#/newPassword", await Rejected(await Send(client, HttpMethod.Post, Reset(id, member), root, new ResetTenantAdminPassword(TemporaryPassword + " ")),
            HttpStatusCode.UnprocessableEntity, "PASSWORD_POLICY_VIOLATION"));
        Assert.Equal((SignInStatus.Authenticated, 1), ((await SignInAs(formerPhone, IdentityPassword)).Status, (await identity.ListSessionsAsync(former.Id, Ct)).Count));

        using (var reset = await Send(client, HttpMethod.Post, Reset(id, member), root, new ResetTenantAdminPassword(TemporaryPassword)))
            Assert.True(reset.StatusCode == HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync(Ct));
        // 旧密码立即失效、该自然人的会话全部吊销；临时密码登录必须先改密（与开户初始密码同一语义）。
        Assert.Empty(await identity.ListSessionsAsync(admin.Id, Ct));
        Assert.Equal("INVALID_CREDENTIALS", (await SignInAs(phone, IdentityPassword)).Error!.Code.Code);
        var temporary = await SignInAs(phone, TemporaryPassword);
        Assert.Equal(SignInStatus.PasswordChangeRequired, temporary.Status); Assert.Null(temporary.Session); Assert.NotNull(temporary.Ticket);
        // 其他人不受影响：原管理员的会话还在，仍是成员但已不是管理员。
        Assert.Single(await identity.ListSessionsAsync(former.Id, Ct));
        Assert.Equal("false|active", await Text($"SELECT is_tenant_administrator::text || '|' || status FROM plugin_qian_identity.memberships WHERE id = '{formerMember}'"));

        // 租户链记 admin.reset-password（只有成员指针），平台链记 reset-credential 的超管旁路；两条链都没有密码。
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(id))
        {
            var record = Assert.Single(await source.Audit().ReadAsync(0, 100, cancellationToken: Ct), entry => entry.Record.CapabilityId == "qian.tenancy/admin.reset-password").Record;
            Assert.Equal(("user", "root", "api", "success", member), (record.ActorType, record.ActorId, record.Channel, record.Result, record.Payload.GetProperty("membershipId").GetGuid()));
            Assert.DoesNotContain(TemporaryPassword, record.Payload.GetRawText(), StringComparison.Ordinal);
        }
        // 旁路记的是放行决定：放行后才得出的 404（3 次）与 422（1 次）同样留有记录，校验失败的 400 没有。
        var resets = (await Bypasses(source)).Where(record => record.Payload.GetProperty("permission").GetString() == "qian.tenancy:admin:reset-credential").ToArray();
        Assert.Equal(5, resets.Length);
        Assert.Equal(2, resets.Count(record => record.Resource == $"tenancy/tenants/{id}/admins/{member}"));
        Assert.All(await PlatformChain(source), entry => Assert.DoesNotContain(TemporaryPassword, entry.Record.Payload.GetRawText(), StringComparison.Ordinal));

        // 机器可读的操作描述：替换与重置都不对模型暴露（humanOnly）且要求确认，页面据此弹出二次确认。
        var machine = await Read<JsonElement>(await Send(client, HttpMethod.Get, "/api/tenancy/meta/state-machine", root), HttpStatusCode.OK);
        foreach (var name in new[] { "assign-admin", "reset-admin-password" })
        {
            var action = machine.GetProperty("actions").EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);
            Assert.Equal((false, true), (action.GetProperty("agent").GetProperty("expose").GetBoolean(), action.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean()));
        }

        async Task<SignInResult> SignInAs(string identifier, string password)
        {
            using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(password));
            return await business.Identity.SignIn.SignInAsync(new LoginInput("web", null, null, null, proof) { Identifier = identifier }, new LoginContext(), Ct);
        }
    }

    private static string Admins(string tenant) => $"/api/tenancy/tenants/{tenant}/admins";
    private static string Reset(string tenant, Guid membership) => $"{Admins(tenant)}/{membership}:reset-password";

    private async Task<Ops> StartOps()
    {
        var platform = new LifecyclePlatform { Management = true };
        RunningApp? business = null;
        try
        {
            business = await Start(lifecycle: platform, identity: true);
            var management = await ManagementHost(business, platform);
            return new Ops(platform, business, management, new HttpClient { BaseAddress = new Uri(management.Urls.Single()) });
        }
        catch
        {
            if (business is not null) await business.DisposeAsync();
            platform.Dispose(); throw;
        }
    }

    private sealed record Ops(LifecyclePlatform Platform, RunningApp Business, WebApplication Management, HttpClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await Management.StopAsync(Ct); await Management.DisposeAsync(); await Business.DisposeAsync(); Platform.Dispose();
        }
    }

    // 平台链上管理员维护的超管旁路记录；引导时安装、激活 system 组件的生命周期条目（S1-RT-037）与租户主档的放行不算。
    private static async Task<AuditRecord[]> Bypasses(IPluginContext source) =>
        [.. (await PlatformChain(source)).Select(entry => entry.Record)
            .Where(record => record.Payload.ValueKind == JsonValueKind.Object && record.Payload.TryGetProperty("permission", out var permission) &&
                permission.GetString()!.StartsWith("qian.tenancy:admin:", StringComparison.Ordinal))];

    // 平台链上的全部记录：分页读到末尾（每页 200），链上记录再多也不会漏读。
    private static async Task<List<AuditChainEntry>> PlatformChain(IPluginContext source)
    {
        var entries = new List<AuditChainEntry>(); long after = 0;
        while (true)
        {
            var page = await source.Audit().ReadPlatformAsync(source, after, 200, cancellationToken: Ct);
            entries.AddRange(page);
            if (page.Count < 200) return entries;
            after = page[^1].Sequence;
        }
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        return await client.SendAsync(request, Ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == status, body);
            return JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web)!;
        }
    }

    // 断言错误码，返回错误位置（顶层 pointer 或第一个字段错误的 pointer）。
    private static async Task<string?> Rejected(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == status, body);
            var error = ErrorDetailJson.Deserialize(body)!;
            Assert.Equal(code, error.Code);
            return error.Pointer ?? (error.Errors is [var first, ..] ? first.Pointer : null);
        }
    }
}

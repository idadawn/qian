using System.Net;
using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 连接器 TCK 与绑定流程（S1-IDP-007）：真实 PostgreSQL + 身份服务 HTTP 面，假连接器模拟上游。
// TCK 三项：清单校验、state 篡改拒绝、回调幂等；其余覆盖确认绑定、冲突、过期、重新认证窗口、取消、JIT 与租户 scope 过滤。
public sealed class ConnectorFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Setup(AccountsHost.Running Host, FakeConnector Connector, TenantDescriptor Alpha, TenantDescriptor Beta,
        (Person Person, string Phone, Membership Member) First, (Person Person, string Phone, Membership Member) Second) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    // alpha 租户里两个账号；连接器挂在全局 scope。
    private async Task<Setup> Start(IdentityAccountOptions? options = null)
    {
        var host = await AccountsHost.Start(postgres, options);
        var connector = new FakeConnector();
        await host.MountAsync(ConnectorsPlugin.Define("connectors", ScopeKey.Global, connector));
        var alpha = host.Tenants.Add("alpha"); var beta = host.Tenants.Add("beta");
        var (first, firstPhone) = await CreatePerson(host, "甲");
        var (second, secondPhone) = await CreatePerson(host, "乙");
        return new Setup(host, connector, alpha, beta, (first, firstPhone, await Join(host, alpha, first.Id, "甲成员", account: "jia")),
            (second, secondPhone, await Join(host, alpha, second.Id, "乙成员", account: "yi")));
    }

    // ---- TCK ----

    [Fact]
    public async Task TCK_清单校验_合法假连接器通过_清单不合法的连接器装不上也列不出()
    {
        await using var setup = await Start();
        var host = setup.Host;
        Assert.Empty(ConnectorManifest.Check(setup.Connector.Descriptor));
        var broken = new BrokenConnector();
        Assert.NotEmpty(ConnectorManifest.Check(broken.Descriptor));
        await host.MountAsync(ConnectorsPlugin.Define("broken", ScopeKey.Global, broken));
        Assert.Equal(PluginState.Failed, host.Loader.GetState("broken", ScopeKey.Global));
        var failure = Assert.IsType<QianErrorException>(host.Loader.GetFailure("broken", ScopeKey.Global));
        Assert.Equal(PlatformErrors.InputInvalid, failure.Error.Code);
        var browser = host.NewBrowser();
        var (status, list, _) = await browser.Json(HttpMethod.Get, "api/identity/connectors?tenant=alpha");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([FakeConnector.DefaultId], list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        var item = list.GetProperty("items")[0];
        Assert.Equal(("测试身份源", "oidc"), (item.GetProperty("display_name").GetString(), item.GetProperty("protocol").GetString()));
        Assert.Equal(["pc", "mobile"], item.GetProperty("terminals").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task TCK_state篡改一律拒绝_流程不受影响_换浏览器_换连接器_重复参数同样拒绝()
    {
        await using var setup = await Start();
        var (host, connector) = (setup.Host, setup.Connector);
        var browser = host.NewBrowser();
        var started = await browser.Begin("alpha");
        var issued = connector.LastBegin!;
        // 身份服务生成的 state / nonce 足够长，PKCE verifier 合规，回调地址取自受信 issuer。
        Assert.True(issued.State.Length >= 43 && issued.Nonce.Length >= 43 && issued.CodeVerifier!.Length == 43);
        Assert.Equal(new Uri(host.Issuer + "connect/callback/fake-idp"), issued.CallbackUri);
        var approved = connector.Approve(started.RedirectUri, "ext-1");

        async Task Rejected(AccountsHost.Browser who, Dictionary<string, string> parameters, string path = FakeConnector.DefaultId)
        {
            using var response = await who.Callback(parameters, path);
            Assert.Equal((HttpStatusCode)422, response.StatusCode);
            Assert.Equal(IdentityErrors.ConnectorStateInvalid.Code, ConnectorFlow.Code(JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement));
        }
        var tampered = new Dictionary<string, string>(approved) { ["state"] = approved["state"][..^1] + (approved["state"][^1] == 'A' ? 'B' : 'A') };
        await Rejected(browser, tampered);
        await Rejected(browser, new Dictionary<string, string>(approved) { ["state"] = "" });
        await Rejected(browser, new Dictionary<string, string> { ["code"] = approved["code"] });
        // 同一 state 拿到另一个浏览器（没有发起它的浏览器密钥）里回调。
        await Rejected(host.NewBrowser(), approved);
        // state 属于 fake-idp，却回到别的连接器的回调路径。
        await Rejected(browser, approved, "other-idp");
        // 同名参数重复出现视为篡改。
        using (var duplicated = await browser.Http.GetAsync($"connect/callback/fake-idp?code={approved["code"]}&state={Uri.EscapeDataString(approved["state"])}&state={Uri.EscapeDataString(approved["state"])}", Ct))
            Assert.Equal((HttpStatusCode)422, duplicated.StatusCode);
        // 以上全部被拒，连接器一次也没被调用；原流程完好，合法回调照常进入确认绑定。
        Assert.Equal(0, connector.Completed);
        var flow = await browser.Landed(approved);
        Assert.Equal(started.Flow, flow);
        Assert.Equal("bind", (await browser.View(flow)).GetProperty("next").GetString());
        // 每次被拒的回调各记一条失败审计。
        Assert.Equal("6", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE stage = 'connector' AND outcome = 'failure' AND reason IN ('state_invalid', 'state_mismatch')"));

        // 上游断言里的 nonce 与发起时不符：连接器拒绝，流程以 AUTH_CONNECTOR_STATE_INVALID 结束。
        var other = await browser.Begin("alpha");
        var replayed = await browser.Landed(connector.Approve(other.RedirectUri, "ext-1", nonce: issued.Nonce));
        var view = await browser.View(replayed);
        Assert.Equal(("failed", "error"), (view.GetProperty("status").GetString(), view.GetProperty("next").GetString()));
        Assert.Equal(IdentityErrors.ConnectorStateInvalid.Code, view.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TCK_回调幂等_重放与并发重复回调只处理一次_不签第二个会话()
    {
        await using var setup = await Start();
        var (host, connector) = (setup.Host, setup.Connector);
        var browser = host.NewBrowser();
        // 先绑定，使后续回调直接签发会话。
        var flow = await browser.Arrive(connector, "alpha", "ext-1");
        Assert.Equal(HttpStatusCode.OK, (await browser.Verify(flow, setup.First.Phone, Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.Bind(flow, "link")).Status);
        var sessions = await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE person_id = @p", ("p", setup.First.Person.Id));

        var fresh = host.NewBrowser();
        var started = await fresh.Begin("alpha");
        var approved = connector.Approve(started.RedirectUri, "ext-1");
        var calls = connector.Completed;
        Assert.Equal(started.Flow, await fresh.Landed(approved));
        Assert.True(fresh.HasAccountCookie);
        // 重放：回到同一落点，不再调用连接器，也不再签会话。
        Assert.Equal(started.Flow, await fresh.Landed(approved));
        Assert.Equal(started.Flow, await fresh.Landed(approved));
        Assert.Equal(calls + 1, connector.Completed);
        Assert.Equal((long.Parse(sessions, System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE person_id = @p", ("p", setup.First.Person.Id)));
        Assert.Equal("done", (await fresh.View(started.Flow)).GetProperty("next").GetString());

        // 并发的同一回调：只有一个抢到流程，连接器只被调用一次，其余都回到落点。
        connector.Delay = TimeSpan.FromMilliseconds(300);
        var racing = host.NewBrowser();
        var raced = await racing.Begin("alpha");
        var parameters = connector.Approve(raced.RedirectUri, "ext-2");
        calls = connector.Completed;
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => racing.Callback(parameters)));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        foreach (var response in results) response.Dispose();
        Assert.Equal(calls + 1, connector.Completed);
        Assert.Equal("bind", (await racing.View(raced.Flow)).GetProperty("next").GetString());
    }

    // ---- 绑定流程 ----

    [Fact]
    public async Task 首次登录进入确认绑定_登录已有账号并确认后建立外部身份_再次登录直接进入()
    {
        await using var setup = await Start();
        var (host, connector, alpha) = (setup.Host, setup.Connector, setup.Alpha);
        var browser = host.NewBrowser();
        var flow = await browser.Arrive(connector, "alpha", "ext-1", returnUrl: "/apps/crm?tab=1");
        Assert.False(browser.HasAccountCookie);
        var view = await browser.View(flow);
        Assert.Equal(("bind", "bind"), (view.GetProperty("status").GetString(), view.GetProperty("next").GetString()));
        Assert.Equal(("alpha", "ALPHA"), (view.GetProperty("tenant").GetProperty("code").GetString(), view.GetProperty("tenant").GetProperty("name").GetString()));
        Assert.Equal(FakeConnector.DefaultId, view.GetProperty("connector").GetProperty("id").GetString());
        var binding = view.GetProperty("binding");
        Assert.Equal("外部张三", binding.GetProperty("external").GetProperty("display_name").GetString());
        // 上游资料只给掩码，返回地址在完成前不给。
        var masked = binding.GetProperty("external").GetProperty("email").GetString()!;
        Assert.NotEqual("zhang.ext@corp.example", masked);
        Assert.DoesNotContain("zhang.ext@", view.ToString(), StringComparison.Ordinal);
        Assert.False(binding.GetProperty("verified").GetBoolean());
        Assert.False(binding.GetProperty("allow_jit").GetBoolean());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("return_url").ValueKind);
        Assert.Equal("", await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-1"));

        // 没有新鲜认证就不能绑定；密码错误按登录同一规则拒绝。
        var early = await browser.Bind(flow, "link");
        Assert.Equal((HttpStatusCode.Unauthorized, PlatformErrors.Unauthenticated.Code), (early.Status, ConnectorFlow.Code(early.Body)));
        var wrong = await browser.Verify(flow, setup.First.Phone, "Wrong-Password-1");
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, ConnectorFlow.Code(wrong.Body));
        var verified = await browser.Verify(flow, setup.First.Phone, Password);
        Assert.Equal(HttpStatusCode.OK, verified.Status);
        Assert.True(verified.Body.GetProperty("binding").GetProperty("verified").GetBoolean());
        Assert.Equal("甲成员", verified.Body.GetProperty("binding").GetProperty("account").GetProperty("display_name").GetString());
        Assert.False(browser.HasAccountCookie);

        var bound = await browser.Bind(flow, "link");
        Assert.Equal(HttpStatusCode.OK, bound.Status);
        Assert.Equal(("authenticated", alpha.Id, setup.First.Member.Id, "/apps/crm?tab=1"), (bound.Body.GetProperty("status").GetString(), bound.Body.GetProperty("tenant_id").GetGuid(),
            bound.Body.GetProperty("membership_id").GetGuid(), bound.Body.GetProperty("return_url").GetString()));
        Assert.True(browser.HasAccountCookie);
        Assert.Equal(setup.First.Member.Id.ToString("D"), await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-1"));
        var me = await browser.Json(HttpMethod.Get, "api/identity/me");
        Assert.Equal(setup.First.Member.Id, me.Body.GetProperty("membership_id").GetGuid());
        // 会话的 amr 记录这次是联合认证，且确认绑定前做过密码认证。
        Assert.Equal("{pwd,fed}", await host.Text("SELECT amr::text FROM plugin_qian_identity.sessions WHERE membership_id = @m ORDER BY created_at DESC LIMIT 1", ("m", setup.First.Member.Id)));
        Assert.Equal("done", (await browser.View(flow)).GetProperty("next").GetString());
        // 重复确认不会签出第二个会话。
        Assert.Equal((HttpStatusCode)422, (await browser.Bind(flow, "link")).Status);

        // 再次经连接器登录：回调直接签发会话，不再要求确认。（拨一秒钟，好按创建时间区分两个会话。）
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var again = host.NewBrowser();
        var second = await again.Arrive(connector, "alpha", "ext-1");
        Assert.True(again.HasAccountCookie);
        var done = await again.View(second);
        Assert.Equal(("completed", "done"), (done.GetProperty("status").GetString(), done.GetProperty("next").GetString()));
        Assert.Equal(setup.First.Member.Id, (await again.Json(HttpMethod.Get, "api/identity/me")).Body.GetProperty("membership_id").GetGuid());
        Assert.Equal("{fed}", await host.Text("SELECT amr::text FROM plugin_qian_identity.sessions WHERE membership_id = @m ORDER BY created_at DESC LIMIT 1", ("m", setup.First.Member.Id)));
    }

    [Fact]
    public async Task 同租户重复绑定返回冲突且不泄露对方_其他租户可各自绑定()
    {
        await using var setup = await Start();
        var (host, connector, alpha, beta) = (setup.Host, setup.Connector, setup.Alpha, setup.Beta);
        // 两个浏览器都在 ext-1 未绑定时到达确认绑定。
        var one = host.NewBrowser(); var two = host.NewBrowser();
        var first = await one.Arrive(connector, "alpha", "ext-1");
        var second = await two.Arrive(connector, "alpha", "ext-1");
        Assert.Equal(HttpStatusCode.OK, (await one.Verify(first, setup.First.Phone, Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await one.Bind(first, "link")).Status);
        Assert.Equal(HttpStatusCode.OK, (await two.Verify(second, setup.Second.Phone, Password)).Status);
        var conflict = await two.Bind(second, "link");
        Assert.Equal(HttpStatusCode.Conflict, conflict.Status);
        Assert.Equal(IdentityErrors.BindingConflict.Code, ConnectorFlow.Code(conflict.Body));
        var text = conflict.Body.ToString();
        foreach (var leak in (string[])[setup.First.Member.Id.ToString("D"), setup.First.Person.Id.ToString("D"), "甲", "jia", setup.First.Phone])
            Assert.DoesNotContain(leak, text, StringComparison.Ordinal);
        Assert.False(two.HasAccountCookie);
        Assert.Equal(setup.First.Member.Id.ToString("D"), await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-1"));
        var failed = await two.View(second);
        Assert.Equal(IdentityErrors.BindingConflict.Code, failed.GetProperty("error").GetProperty("code").GetString());

        // 绑定键是 tenant × connector × subject：同一外部身份可以在 beta 绑定 beta 的成员。
        var inBeta = await Join(host, beta, setup.Second.Person.Id, "乙在 beta");
        var three = host.NewBrowser();
        var third = await three.Arrive(connector, "beta", "ext-1");
        Assert.Equal(HttpStatusCode.OK, (await three.Verify(third, setup.Second.Phone, Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await three.Bind(third, "link")).Status);
        Assert.Equal(inBeta.Id.ToString("D"), await host.Bound(beta.Id, FakeConnector.DefaultId, "ext-1"));
        Assert.Equal(setup.First.Member.Id.ToString("D"), await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-1"));
    }

    [Fact]
    public async Task 绑定票据十分钟过期_重新认证有五分钟窗口_取消后流程作废()
    {
        await using var setup = await Start();
        var (host, connector) = (setup.Host, setup.Connector);
        var browser = host.NewBrowser();
        var flow = await browser.Arrive(connector, "alpha", "ext-1");
        Assert.Equal(HttpStatusCode.OK, (await browser.Verify(flow, setup.First.Phone, Password)).Status);
        host.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        var expired = await browser.Bind(flow, "link");
        Assert.Equal(((HttpStatusCode)422, IdentityErrors.BindingExpired.Code), (expired.Status, ConnectorFlow.Code(expired.Body)));
        var view = await browser.View(flow);
        Assert.Equal(("failed", IdentityErrors.BindingExpired.Code), (view.GetProperty("status").GetString(), view.GetProperty("error").GetProperty("code").GetString()));
        // 过期之后再登录或确认，仍然明确报过期。
        Assert.Equal(IdentityErrors.BindingExpired.Code, ConnectorFlow.Code((await browser.Verify(flow, setup.First.Phone, Password)).Body));
        Assert.Equal(IdentityErrors.BindingExpired.Code, ConnectorFlow.Code((await browser.Bind(flow, "link")).Body));
        Assert.Equal("", await host.Bound(setup.Alpha.Id, FakeConnector.DefaultId, "ext-1"));

        // 认证后过了 5 分钟（票据仍有效）：要求重新认证，重新认证后可以确认。
        var again = await browser.Arrive(connector, "alpha", "ext-1");
        Assert.Equal(HttpStatusCode.OK, (await browser.Verify(again, setup.First.Phone, Password)).Status);
        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.False((await browser.View(again)).GetProperty("binding").GetProperty("verified").GetBoolean());
        var stale = await browser.Bind(again, "link");
        Assert.Equal((HttpStatusCode.Unauthorized, PlatformErrors.Unauthenticated.Code), (stale.Status, ConnectorFlow.Code(stale.Body)));
        Assert.Equal(HttpStatusCode.OK, (await browser.Verify(again, setup.First.Phone, Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.Bind(again, "link")).Status);

        // 取消：流程作废，不能再确认。
        var other = host.NewBrowser();
        var cancelled = await other.Arrive(connector, "alpha", "ext-9");
        Assert.Equal(HttpStatusCode.NoContent, (await other.Bind(cancelled, "cancel")).Status);
        Assert.Equal(("cancelled", "restart"), ((await other.View(cancelled)).GetProperty("status").GetString(), (await other.View(cancelled)).GetProperty("next").GetString()));
        Assert.Equal(IdentityErrors.ConnectorStateInvalid.Code, ConnectorFlow.Code((await other.Bind(cancelled, "link")).Body));
        // 流程只认发起它的浏览器。
        Assert.Equal(HttpStatusCode.NotFound, (await host.NewBrowser().Json(HttpMethod.Get, $"api/identity/connector-flows/{again:D}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await host.NewBrowser().Bind(cancelled, "link")).Status);
    }

    [Fact]
    public async Task JIT建号缺省关闭_租户策略打开后建号并绑定_受配额约束()
    {
        await using var setup = await Start();
        var (host, connector, alpha) = (setup.Host, setup.Connector, setup.Alpha);
        var browser = host.NewBrowser();
        var flow = await browser.Arrive(connector, "alpha", "ext-new");
        var denied = await browser.Bind(flow, "create");
        Assert.Equal(((HttpStatusCode)422, PlatformErrors.BusinessRuleViolated.Code), (denied.Status, ConnectorFlow.Code(denied.Body)));
        Assert.Equal("", await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-new"));

        host.Tenants.Set(alpha.Id, "qian.identity.server", "connectors:jitProvisioning", true);
        Assert.True((await browser.View(flow)).GetProperty("binding").GetProperty("allow_jit").GetBoolean());
        var created = await browser.Bind(flow, "create");
        Assert.Equal(HttpStatusCode.OK, created.Status);
        var member = created.Body.GetProperty("membership_id").GetGuid();
        Assert.Equal(member.ToString("D"), await host.Bound(alpha.Id, FakeConnector.DefaultId, "ext-new"));
        // 新账号以上游邮箱为标识（未验证），名字取上游资料；不带密码，只能经连接器登录。
        var person = await host.Text("SELECT person_id::text FROM plugin_qian_identity.external_identities WHERE subject = 'ext-new'");
        Assert.Equal("外部张三", await host.Text("SELECT display_name FROM plugin_qian_identity.persons WHERE id = @p::uuid", ("p", person)));
        Assert.Equal("email|zhang.ext@corp.example|f", await host.Text(
            "SELECT kind || '|' || value || '|' || (verified_at IS NOT NULL)::text::char(1) FROM plugin_qian_identity.person_identifiers WHERE person_id = @p::uuid", ("p", person)));
        Assert.Equal("{fed}", await host.Text("SELECT amr::text FROM plugin_qian_identity.sessions WHERE membership_id = @m", ("m", member)));

        // 配额：beta 已满员时 JIT 被拒，流程失败、不留账号。
        var beta = host.Tenants.Add("gamma", limit: 0);
        host.Tenants.Set(beta.Id, "qian.identity.server", "connectors:jitProvisioning", true);
        var full = await browser.Arrive(connector, "gamma", "ext-quota");
        var rejected = await browser.Bind(full, "create");
        Assert.Equal(TenancyErrors.UserLimit.Code, ConnectorFlow.Code(rejected.Body));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.persons WHERE display_name = '外部张三' AND id <> @p::uuid", ("p", person)));
    }

    [Fact]
    public async Task 连接器按租户scope过滤_未挂载与不存在同样404_受限的全局项同样不可见()
    {
        await using var setup = await Start();
        var (host, alpha, beta) = (setup.Host, setup.Alpha, setup.Beta);
        // corp-idp 只挂在 alpha；alpha 另有一个同名 fake-idp 遮蔽全局项。
        var corp = new FakeConnector("corp-idp", "Alpha 企业身份");
        var shadow = new FakeConnector(FakeConnector.DefaultId, "Alpha 自己的身份源");
        await host.MountAsync(ConnectorsPlugin.Define("alpha-connectors", ConnectorFlow.TenantScope(alpha.Id), corp, shadow));
        var browser = host.NewBrowser();
        async Task<string[]> Listed(string query) =>
            [.. (await browser.Json(HttpMethod.Get, "api/identity/connectors" + query)).Body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString() + "|" + i.GetProperty("display_name").GetString())];
        Assert.Equal(["corp-idp|Alpha 企业身份", "fake-idp|Alpha 自己的身份源"], await Listed("?tenant=alpha"));
        Assert.Equal(["fake-idp|测试身份源"], await Listed("?tenant=beta"));
        Assert.Equal(["fake-idp|测试身份源"], await Listed(""));
        Assert.Equal(["fake-idp|测试身份源"], await Listed("?tenant=nope"));

        // beta 看不到 corp-idp：详情、发起都与根本不存在的连接器同一响应。
        async Task<(HttpStatusCode, string)> Probe(HttpMethod method, string path, object? body = null)
        {
            var (status, problem, _) = await browser.Json(method, path, body);
            return (status, ConnectorFlow.Code(problem));
        }
        var notFound = (HttpStatusCode.NotFound, PlatformErrors.NotFound.Code);
        Assert.Equal(notFound, await Probe(HttpMethod.Get, "api/identity/connectors/corp-idp?tenant=beta"));
        Assert.Equal(notFound, await Probe(HttpMethod.Get, "api/identity/connectors/no-such-idp?tenant=beta"));
        Assert.Equal(notFound, await Probe(HttpMethod.Post, "api/identity/connectors/corp-idp/begin", new { client_id = "web", tenant = "beta" }));
        Assert.Equal(notFound, await Probe(HttpMethod.Post, "api/identity/connectors/no-such-idp/begin", new { client_id = "web", tenant = "beta" }));
        Assert.Equal(notFound, await Probe(HttpMethod.Post, "api/identity/connectors/corp-idp/begin", new { client_id = "web", tenant = "no-such-tenant" }));
        Assert.Equal(0, corp.Begun);
        // alpha 用 fake-idp 时走的是遮蔽它的租户项。
        var started = await browser.Begin("alpha");
        Assert.Equal(1, shadow.Begun);
        Assert.Equal(0, setup.Connector.Begun);
        var shadowed = await browser.Landed(shadow.Approve(started.RedirectUri, "ext-1"));
        Assert.Equal("Alpha 自己的身份源", (await browser.View(shadowed)).GetProperty("connector").GetProperty("display_name").GetString());

        // 运行时 restriction 把全局 fake-idp 从 beta 过滤掉：与未挂载同一表现。
        var global = host.Loader.GetContext("identity", ScopeKey.Global);
        using (global.Restrict(ConnectorFlow.TenantScope(beta.Id), registration => registration.Name != FakeConnector.DefaultId))
        {
            Assert.Empty(await Listed("?tenant=beta"));
            Assert.Equal(notFound, await Probe(HttpMethod.Get, "api/identity/connectors/fake-idp?tenant=beta"));
            Assert.Equal(notFound, await Probe(HttpMethod.Post, "api/identity/connectors/fake-idp/begin", new { client_id = "web", tenant = "beta" }));
        }
        Assert.Equal(["fake-idp|测试身份源"], await Listed("?tenant=beta"));

        // 发起之后连接器被卸载：回调同样 404，不调用连接器。
        var pending = await browser.Begin("alpha", "corp-idp");
        var approved = corp.Approve(pending.RedirectUri, "ext-2");
        await host.Loader.UnmountAsync("alpha-connectors", ConnectorFlow.TenantScope(alpha.Id), Ct);
        using var gone = await browser.Callback(approved, "corp-idp");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(0, corp.Completed);
    }

    [Fact]
    public async Task 上游拒绝时记AUTH_CONNECTOR_DENIED_错误描述当不可信数据截断()
    {
        await using var setup = await Start();
        var (host, connector) = (setup.Host, setup.Connector);
        var browser = host.NewBrowser();
        var started = await browser.Begin("alpha");
        var approved = connector.Approve(started.RedirectUri, "ext-1");
        var description = "用户取消\r\n<script>alert(1)</script>" + new string('长', 400);
        var flow = await browser.Landed(new Dictionary<string, string> { ["state"] = approved["state"], ["error"] = "access_denied", ["error_description"] = description });
        var view = await browser.View(flow);
        Assert.Equal(("failed", "error"), (view.GetProperty("status").GetString(), view.GetProperty("next").GetString()));
        Assert.Equal(IdentityErrors.ConnectorDenied.Code, view.GetProperty("error").GetProperty("code").GetString());
        var shown = view.GetProperty("upstream_error_description").GetString()!;
        Assert.Equal(200, shown.Length);
        Assert.DoesNotContain('\n', shown);
        Assert.StartsWith("用户取消<script>", shown, StringComparison.Ordinal);
        // 错误文案来自错误目录，不取上游描述。
        Assert.DoesNotContain("用户取消", view.GetProperty("error").ToString(), StringComparison.Ordinal);
        Assert.Equal(0, connector.Completed);
    }

    [Fact]
    public async Task 卡在处理中的流程过了时限按上游超时呈现_不会一直等待()
    {
        await using var setup = await Start();
        var (host, connector) = (setup.Host, setup.Connector);
        var browser = host.NewBrowser();
        var started = await browser.Begin("alpha");
        // 模拟回调处理到一半进程退出：流程停在 processing。
        await host.Exec("UPDATE plugin_qian_identity.connector_flows SET status = 'processing' WHERE id = @id", ("id", started.Flow));
        Assert.Equal("wait", (await browser.View(started.Flow)).GetProperty("next").GetString());
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal("wait", (await browser.View(started.Flow)).GetProperty("next").GetString());
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var view = await browser.View(started.Flow);
        Assert.Equal(("failed", "error"), (view.GetProperty("status").GetString(), view.GetProperty("next").GetString()));
        Assert.Equal(PlatformErrors.GatewayTimeout.Code, view.GetProperty("error").GetProperty("code").GetString());
        // 迟到的同一回调只回到落点，不再交给连接器。
        Assert.Equal(started.Flow, await browser.Landed(connector.Approve(started.RedirectUri, "ext-1")));
        Assert.Equal(0, connector.Completed);
    }

    [Fact]
    public async Task 统一流水线只认服务端签发的连接器票据()
    {
        await using var setup = await Start();
        var pipeline = setup.Host.SignIn;
        using var forged = Proof("forged-connector-ticket-0000000000000000000000");
        var result = await pipeline.SignInAsync(new LoginInput("web", setup.Alpha.Id, FakeConnector.DefaultId, null, forged), new LoginContext(), Ct);
        Assert.Equal((SignInStatus.Failed, IdentityErrors.LoginTicketInvalid), (result.Status, result.Error!.Code));
        using var other = Proof("forged-connector-ticket-0000000000000000000000");
        var unknown = await pipeline.SignInAsync(new LoginInput("web", setup.Alpha.Id, "no-such-idp", null, other), new LoginContext(), Ct);
        Assert.Equal("#/connector_id", Assert.Single(unknown.Error!.Errors!).Pointer);
    }

    [Fact]
    public async Task 详情接口只给清单不回传配置与密钥_明文密钥的配置装不上()
    {
        await using var setup = await Start();
        var host = setup.Host;
        const string Secret = "super-secret-value-123", Issuer = "https://sso.corp.example/realms/main";
        var secrets = new StaticSecrets("secretRef://platform/identity/connector-oidc/client-secret", Secret);
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        await host.MountAsync(
            ConnectorsPlugin.Oidc("connector.oidc", new { issuer = Issuer, clientId = "qian-portal", clientSecret = secrets.Reference, displayName = "集团统一身份" }, secrets, handler),
            ConnectorsPlugin.Oidc("connector.plain", new { name = "plain", issuer = Issuer, clientId = "qian-portal", clientSecret = Secret }, secrets, handler, "plain"));
        Assert.Equal(PluginState.Ready, host.Loader.GetState("connector.oidc", ScopeKey.Global));
        // 明文密钥：组件装配即失败，连接器不出现。
        Assert.Equal(PluginState.Failed, host.Loader.GetState("connector.plain", ScopeKey.Global));
        var failure = Assert.IsType<QianErrorException>(host.Loader.GetFailure("connector.plain", ScopeKey.Global));
        Assert.Contains(failure.Error.Errors!, e => e.Pointer == "#/clientSecret");
        Assert.DoesNotContain(Secret, failure.ToString(), StringComparison.Ordinal);

        var browser = host.NewBrowser();
        var (status, list, _) = await browser.Json(HttpMethod.Get, "api/identity/connectors?tenant=alpha");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["fake-idp", "oidc"], list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        var (found, detail, _) = await browser.Json(HttpMethod.Get, "api/identity/connectors/oidc?tenant=alpha");
        Assert.Equal(HttpStatusCode.OK, found);
        Assert.Equal(("oidc", "集团统一身份", "oidc"), (detail.GetProperty("id").GetString(), detail.GetProperty("display_name").GetString(), detail.GetProperty("protocol").GetString()));
        Assert.Equal("/connect/callback/{connectorId}", detail.GetProperty("callback_template").GetString());
        Assert.Equal(host.Issuer + "connect/callback/oidc", detail.GetProperty("callback_uri").GetString());
        Assert.True(detail.GetProperty("config_schema").GetProperty("properties").GetProperty("clientSecret").GetProperty("x-qian-sensitive").GetBoolean());
        // 详情只是清单：任何配置值都不回传，更不会解析密钥。
        var text = detail.GetRawText();
        foreach (var value in (string[])[secrets.Reference, Secret, Issuer, "qian-portal"])
            Assert.DoesNotContain(value, text, StringComparison.Ordinal);
        Assert.Equal(0, secrets.Resolved);
    }

    // 配置 Schema 里的密钥字段没有标敏感：清单校验不通过。
    private sealed class BrokenConnector : IIdentityConnector
    {
        public ConnectorDescriptor Descriptor { get; } = new("broken-idp", "坏清单", "oidc", [ConnectorTerminals.Pc],
            JsonDocument.Parse("""{"type":"object","additionalProperties":false,"properties":{"clientSecret":{"type":"string"}}}""").RootElement.Clone());
        public Task<ChallengeResult> BeginSignInAsync(ConnectorContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExternalAssertion> CompleteSignInAsync(ConnectorCallback callback, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

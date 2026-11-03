using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Tenancy.Signup;
using Qian.Testing.Containers;

namespace Qian.Sample.Crm.Tests;

// 租户自助开通（S1-TEN-019）：申请人接口挂在身份服务同一宿主的公开分支上（identity profile 的形状），运营接口挂在平台运营宿主上；
// 身份组件、租户开通编排与状态机、Authz 超管旁路与平台审计链都是真的。发件端口是测试里的记录器（开发宿主用开发期发件箱）。
public sealed partial class TenantSignupTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    private const string Password = "Signup-Horse-2026";
    private const string Schema = TenancySignupDbContext.Schema;

    [Fact]
    public async Task 发码与核验_没有发件端口时503_写请求非JSON或跨源被拒_已注册与否响应相同_验证码只存哈希且单次有效_错5次作废_会话Cookie属性_按手机号与来源限流带RetryAfter()
    {
        await using var ops = await StartOps(messenger: false);
        using var browser = new Browser(ops.Business.Client.BaseAddress!);
        var outbox = ops.Outbox;

        // 没有发件端口：发验证码 503，自助开通不可用；不写挑战。
        await Rejected(await browser.Send(HttpMethod.Post, Verifications, new { phone = NewPhone() }), HttpStatusCode.ServiceUnavailable, "SIGNUP_UNAVAILABLE");
        Assert.Equal("0", await Text($"SELECT count(*) FROM {Schema}.verifications"));
        await Mount(ops.Business, MessengerDefinition(outbox));

        // 写请求只收 application/json，Origin（缺省时 Sec-Fetch-Site）必须同源；未知字段（含租户字段）在绑定时拒绝。被拒的请求都不写挑战。
        var phone = NewPhone();
        await Rejected(await browser.Send(HttpMethod.Post, Verifications, content: new StringContent("{\"phone\":\"" + phone + "\"}", Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        await Rejected(await browser.Send(HttpMethod.Post, Verifications, new { phone }, origin: "http://evil.example"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        await Rejected(await browser.Send(HttpMethod.Post, Verifications, new { phone }, origin: ""), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        await Rejected(await browser.Send(HttpMethod.Post, Verifications, new { phone }, origin: "", fetchSite: "cross-site"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
            await Rejected(await browser.Send(HttpMethod.Post, Verifications, new Dictionary<string, object?> { ["phone"] = phone, [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        Assert.Equal("#/phone", (await Rejected(await browser.Send(HttpMethod.Post, Verifications, new { phone = "12" }), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("0", await Text($"SELECT count(*) FROM {Schema}.verifications"));
        // 同源页面不带 Origin 时看 Sec-Fetch-Site。
        using (var sameSite = await browser.Send(HttpMethod.Post, Verifications, new { phone }, origin: "", fetchSite: "same-origin"))
            Assert.Equal(HttpStatusCode.Accepted, sameSite.StatusCode);

        // 已注册与未注册的号码：状态、形状与时间间隔完全相同（核验之前不暴露号码是否已注册），两边都真的发了码。
        var (person, registered) = await NewPerson(ops.Business, "已有账号");
        var fresh = NewPhone();
        var known = await Json(await browser.Send(HttpMethod.Post, Verifications, new { phone = registered }), HttpStatusCode.Accepted);
        var unknown = await Json(await browser.Send(HttpMethod.Post, Verifications, new { phone = fresh }), HttpStatusCode.Accepted);
        Assert.Equal(known.EnumerateObject().Select(item => item.Name), unknown.EnumerateObject().Select(item => item.Name));
        Assert.Equal(["expiresAt", "id", "resendAfter"], known.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.Equal(Window(known), Window(unknown));
        var knownChallenge = known.GetProperty("id").GetGuid(); var unknownChallenge = unknown.GetProperty("id").GetGuid();
        var knownCode = outbox.Code(knownChallenge); var unknownCode = outbox.Code(unknownChallenge);
        Assert.Equal(("+86" + registered, "+86" + fresh), (outbox.Phone(knownChallenge), outbox.Phone(unknownChallenge)));

        // 验证码只存加盐哈希：没有原值列，哈希不是验证码的直接摘要，整行文本里也找不到验证码。
        Assert.Equal("attempts,code_hash,code_salt,consumed_at,created_at,expires_at,id,phone,source",
            await Text($"SELECT string_agg(column_name, ',' ORDER BY column_name) FROM information_schema.columns WHERE table_schema = '{Schema}' AND table_name = 'verifications'"));
        Assert.Equal("False", await Text($"SELECT code_hash = sha256(convert_to('{knownCode}', 'UTF8')) FROM {Schema}.verifications WHERE id = '{knownChallenge}'"));
        Assert.Equal("0", await Text($"SELECT count(*) FROM {Schema}.verifications v WHERE position('{knownCode}' in v::text) > 0"));

        // 错误 5 次作废：剩余次数逐次递减（文案里），第 5 次后连正确的验证码也只能重新获取。
        var wrong = knownCode == "000000" ? "000001" : "000000";
        for (var remaining = 4; remaining >= 0; remaining--)
        {
            var error = await Rejected(await browser.Send(HttpMethod.Post, Session, new { verificationId = knownChallenge, code = wrong }), HttpStatusCode.BadRequest, "SIGNUP_CODE_INVALID");
            Assert.Contains(remaining.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        }
        await Rejected(await browser.Send(HttpMethod.Post, Session, new { verificationId = knownChallenge, code = knownCode }), HttpStatusCode.BadRequest, "SIGNUP_CODE_EXPIRED");
        await Rejected(await browser.Send(HttpMethod.Post, Session, new { verificationId = Guid.NewGuid(), code = knownCode }), HttpStatusCode.BadRequest, "SIGNUP_CODE_EXPIRED");
        Assert.Null(browser.Token);

        // 核验通过建立会话：Cookie HttpOnly、SameSite=Strict、Path=/api/signup，回环上不带 Secure；令牌只存 SHA-256。验证码单次有效。
        string token;
        using (var opened = await browser.Send(HttpMethod.Post, Session, new { verificationId = unknownChallenge, code = unknownCode }))
        {
            var body = await opened.Content.ReadAsStringAsync(Ct);
            Assert.True(opened.StatusCode == HttpStatusCode.OK, body);
            using var applicant = JsonDocument.Parse(body);
            Assert.Equal((Mask(fresh), false), (applicant.RootElement.GetProperty("phone").GetString(), applicant.RootElement.GetProperty("existingAccount").GetBoolean()));
            Assert.False(applicant.RootElement.TryGetProperty("displayName", out var noName) && noName.ValueKind != JsonValueKind.Null);
            var cookie = SessionCookie(opened);
            Assert.Contains("path=/api/signup", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
            token = browser.Token!;
            Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        }
        Assert.Equal("0", await Text($"SELECT count(*) FROM {Schema}.sessions s WHERE position('{token}' in s::text) > 0"));
        await Rejected(await browser.Send(HttpMethod.Post, Session, new { verificationId = unknownChallenge, code = unknownCode }), HttpStatusCode.BadRequest, "SIGNUP_CODE_EXPIRED");
        Assert.Equal((Mask(fresh), false), Applicant(await Json(await browser.Send(HttpMethod.Get, Session), HttpStatusCode.OK)));
        // 退出：会话吊销、Cookie 清掉；之后读当前申请人 401。
        using (var closed = await browser.Send(HttpMethod.Delete, Session)) Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.Null(browser.Token);
        browser.Restore(token);
        await Rejected(await browser.Send(HttpMethod.Get, Session), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await Rejected(await browser.Send(HttpMethod.Get, Mine), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");

        // 已有号码：核验通过之后才告诉申请人「将以既有账号申请」。
        await Age($"phone = '+86{registered}'", "2 minutes");
        var existing = await Verify(ops, browser, registered);
        Assert.Equal((Mask(registered), true, "已有账号"), (existing.GetProperty("phone").GetString(), existing.GetProperty("existingAccount").GetBoolean(), existing.GetProperty("displayName").GetString()));
        Assert.Equal(person.Id, Guid.Parse(await Text($"SELECT person_id::text FROM {Schema}.sessions ORDER BY created_at DESC LIMIT 1") ?? ""));

        // 回环之外（请求的主机不是回环名）：会话 Cookie 必须 Secure。
        using (var remote = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = ops.Business.Client.BaseAddress })
        {
            var remotePhone = NewPhone();
            var challenge = (await Json(await Remote(remote, Verifications, new { phone = remotePhone }), HttpStatusCode.Accepted)).GetProperty("id").GetGuid();
            using var opened = await Remote(remote, Session, new { verificationId = challenge, code = outbox.Code(challenge) });
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            Assert.Contains("secure", SessionCookie(opened), StringComparison.OrdinalIgnoreCase);
        }

        // 按手机号限流：60 秒内重发 429（Retry-After 不超过重发间隔）；一小时内第 6 次 429（Retry-After 到最早一次满一小时）。
        var limited = NewPhone();
        Assert.Equal(HttpStatusCode.Accepted, await Status(browser.Send(HttpMethod.Post, Verifications, new { phone = limited })));
        var resend = await RateLimited(await browser.Send(HttpMethod.Post, Verifications, new { phone = limited }));
        Assert.InRange(resend, 1, 60);
        for (var i = 0; i < 4; i++)
        {
            await Age($"phone = '+86{limited}'", "2 minutes");
            Assert.Equal(HttpStatusCode.Accepted, await Status(browser.Send(HttpMethod.Post, Verifications, new { phone = limited })));
        }
        await Age($"phone = '+86{limited}'", "2 minutes");
        Assert.InRange(await RateLimited(await browser.Send(HttpMethod.Post, Verifications, new { phone = limited })), 61, 3600);
        Assert.Equal("5", await Text($"SELECT count(*) FROM {Schema}.verifications WHERE phone = '+86{limited}'"));

        // 按来源地址限流：同一来源一小时内第 21 次 429。先把之前的挑战挪出窗口。
        await Age("true", "2 hours");
        for (var i = 0; i < 20; i++) Assert.Equal(HttpStatusCode.Accepted, await Status(browser.Send(HttpMethod.Post, Verifications, new { phone = NewPhone() })));
        Assert.InRange(await RateLimited(await browser.Send(HttpMethod.Post, Verifications, new { phone = NewPhone() })), 61, 3600);
        Assert.Equal("20", await Text($"SELECT count(*) FROM {Schema}.verifications WHERE created_at > now() - interval '1 hour'"));

        static string Window(JsonElement ticket) => (ticket.GetProperty("expiresAt").GetDateTimeOffset() - ticket.GetProperty("resendAfter").GetDateTimeOffset()).ToString();
        static Task<HttpResponseMessage> Remote(HttpClient client, string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, body.GetType()) };
            request.Headers.Host = "signup.example.test"; request.Headers.Add("Origin", "http://signup.example.test");
            return SendAndDispose(client, request);
        }
    }

    [Fact]
    public async Task 提交与撤回_新号码建自然人且手机号已验证无成员身份_已有号码以该自然人申请不改密码_停用锁定与平台身份被拒_一人一条进行中_租户号冲突被拒_撤回只对待审核_跨申请人404()
    {
        await using var ops = await StartOps();
        var address = ops.Business.Client.BaseAddress!;
        var accounts = ops.Business.Identity.Accounts;

        // 新号码：会话里还没有自然人，本人的申请为空；提交须带姓名与密码，字段错误落在对应位置。
        using var first = new Browser(address);
        var phone = NewPhone();
        await Verify(ops, first, phone);
        Assert.Empty((await Json(await first.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal("#/displayName", (await Rejected(await Submit(first, Body("alpha01", name: null)), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/password", (await Rejected(await Submit(first, Body("alpha01", password: null)), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/password", (await Rejected(await Submit(first, Body("alpha01", password: "short")), HttpStatusCode.UnprocessableEntity, "PASSWORD_POLICY_VIOLATION")).Field());
        Assert.Equal("#/code", (await Rejected(await Submit(first, Body("admin")), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/code", (await Rejected(await Submit(first, Body("ab")), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/acceptTerms", (await Rejected(await Submit(first, Body("alpha01", acceptTerms: false)), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/size", (await Rejected(await Submit(first, Body("alpha01", size: "2-3")), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/email", (await Rejected(await Submit(first, Body("alpha01", email: "not-mail")), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
        {
            var body = Body("alpha01"); body[forbidden] = A;
            await Rejected(await Submit(first, body), HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        // 非 JSON、跨源的提交同样被拒；到这里一个账号都没有建。
        await Rejected(await first.Send(HttpMethod.Post, Mine, content: new StringContent(JsonSerializer.Serialize(Body("alpha01")), Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        await Rejected(await first.Send(HttpMethod.Post, Mine, Body("alpha01"), origin: "http://evil.example"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        Assert.Null(await accounts.FindPersonAsync(phone, Ct));
        var before = first.Token!;
        var submitted = await Json(await Submit(first, Body("alpha01", company: "  甲方科技  ")), HttpStatusCode.Created);
        var firstId = submitted.GetProperty("id").GetGuid();
        Assert.Equal(("submitted", "甲方科技", "alpha01", "申请人", Mask(phone)), (submitted.GetProperty("status").GetString(), submitted.GetProperty("companyName").GetString(),
            submitted.GetProperty("code").GetString(), submitted.GetProperty("contactName").GetString(), submitted.GetProperty("phone").GetString()));
        Assert.Matches("^AP-[0-9]{8}-0001$", submitted.GetProperty("number").GetString());
        Assert.DoesNotContain(Password, submitted.GetRawText(), StringComparison.Ordinal);
        // 提交后会话轮换并绑定到新建的自然人：旧令牌作废。
        Assert.NotEqual(before, first.Token);
        using (var stale = new Browser(address)) { stale.Restore(before); await Rejected(await stale.Send(HttpMethod.Get, Mine), HttpStatusCode.Unauthorized, "UNAUTHENTICATED"); }
        // 自然人：手机号已验证、没有平台访问资格、没有任何成员身份；密码按身份策略保存（能过第一因子，只是没有可进入的企业）。
        var created = await accounts.FindPersonAsync(phone, Ct) ?? throw new InvalidOperationException("没有建自然人。");
        Assert.Equal(("申请人", AccountStatuses.Active, false), (created.DisplayName, created.Status, created.PlatformAccess));
        var identifier = Assert.Single(created.Identifiers);
        Assert.Equal((IdentifierKind.Phone, true), (identifier.Kind, identifier.Verified));
        Assert.Empty((await accounts.ListPersonTenantsAsync(created.Id, Ct))!);
        Assert.Equal("NO_AVAILABLE_TENANT", (await SignIn(ops.Business, phone, Password)).Error!.Code.Code);
        Assert.Equal("INVALID_CREDENTIALS", (await SignIn(ops.Business, phone, Password + "x")).Error!.Code.Code);
        Assert.Equal(created.Id, Guid.Parse(await Text($"SELECT person_id::text FROM {Schema}.applications WHERE id = '{firstId}'") ?? ""));
        Assert.Equal("0", await Text($"SELECT count(*) FROM {Schema}.applications a WHERE position('{Password}' in a::text) > 0"));
        // 同一账号同时只能有一条进行中的申请。
        await Rejected(await Submit(first, ExistingBody("alpha02")), HttpStatusCode.Conflict, "SIGNUP_APPLICATION_OPEN");

        // 已有号码：以该自然人申请，不建新账号、不改密码；姓名与密码都不得出现。
        var (person, registered) = await NewPerson(ops.Business, "老用户");
        using var second = new Browser(address);
        Assert.True((await Verify(ops, second, registered)).GetProperty("existingAccount").GetBoolean());
        Assert.Equal("#/password", (await Rejected(await Submit(second, Body("beta01", name: null)), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/displayName", (await Rejected(await Submit(second, Body("beta01")), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        // 租户号与进行中的申请冲突、与已有租户冲突都被拒；可用性查询给出原因。
        Assert.Equal("#/code", (await Rejected(await Submit(second, ExistingBody("alpha01")), HttpStatusCode.Conflict, "SIGNUP_CODE_TAKEN")).Field());
        await Admin($"INSERT INTO plugin_qian_tenancy.tenants(id, code, name, isolation, status, created_at) VALUES ('{Guid.CreateVersion7()}'::uuid, 'existco', '已有租户', 'row', 'active', now());");
        Assert.Equal("#/code", (await Rejected(await Submit(second, ExistingBody("existco")), HttpStatusCode.Conflict, "SIGNUP_CODE_TAKEN")).Field());
        Assert.Equal((false, "taken"), await Availability(second, "alpha01"));
        Assert.Equal((false, "taken"), await Availability(second, "existco"));
        Assert.Equal((false, "reserved"), await Availability(second, "admin"));
        Assert.Equal((false, "invalid"), await Availability(second, "ab"));
        Assert.Equal((false, "invalid"), await Availability(second, "Upper1"));
        Assert.Equal((true, null), await Availability(second, "beta01"));
        var secondApplication = await Json(await Submit(second, ExistingBody("beta01")), HttpStatusCode.Created);
        Assert.Equal(("老用户", Mask(registered)), (secondApplication.GetProperty("contactName").GetString(), secondApplication.GetProperty("phone").GetString()));
        Assert.Equal(person.Id, Guid.Parse(await Text($"SELECT person_id::text FROM {Schema}.applications WHERE id = '{secondApplication.GetProperty("id").GetGuid()}'") ?? ""));
        Assert.Equal("true", await Text($"SELECT existing_account::text FROM {Schema}.applications WHERE id = '{secondApplication.GetProperty("id").GetGuid()}'"));
        // 原密码照旧可用（只是还没有企业），说明没有改密码。
        Assert.Equal("NO_AVAILABLE_TENANT", (await SignIn(ops.Business, registered, IdentityPassword)).Error!.Code.Code);

        // 停用、锁定、平台身份：核验通过也不能申请。
        foreach (var status in new[] { AccountStatuses.Disabled, AccountStatuses.Locked })
        {
            var (blocked, blockedPhone) = await NewPerson(ops.Business, "不可申请");
            await accounts.SetPersonStatusAsync(blocked.Id, status, "test", Ct);
            await RejectedAccount(blockedPhone);
        }
        var (_, platformPhone) = await NewPerson(ops.Business, "平台超管", platform: true);
        await RejectedAccount(platformPhone);

        // 申请人只能看到本人的申请；别人的申请与不存在同为 404。撤回只对待审核生效。
        Assert.Equal([firstId], (await Json(await first.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        await Rejected(await second.Send(HttpMethod.Post, Withdraw(firstId), new { }), HttpStatusCode.NotFound, "NOT_FOUND");
        await Rejected(await second.Send(HttpMethod.Post, Withdraw(Guid.NewGuid()), new { }), HttpStatusCode.NotFound, "NOT_FOUND");
        await Rejected(await first.Send(HttpMethod.Post, Withdraw(firstId), content: new StringContent("", Encoding.UTF8, "text/plain")), HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        await Rejected(await first.Send(HttpMethod.Post, Withdraw(firstId), new { }, origin: "http://evil.example"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        await Rejected(await first.Send(HttpMethod.Post, Withdraw(firstId), new Dictionary<string, object?> { ["tenantId"] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        var withdrawn = await Json(await first.Send(HttpMethod.Post, Withdraw(firstId), new { }), HttpStatusCode.OK);
        Assert.Equal("withdrawn", withdrawn.GetProperty("status").GetString());
        Assert.True(withdrawn.TryGetProperty("completedAt", out var completed) && completed.ValueKind == JsonValueKind.String);
        await Rejected(await first.Send(HttpMethod.Post, Withdraw(firstId), new { }), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        // 撤回释放租户号与「进行中」名额：同一账号可以另交新申请，别人也能用这个租户号。
        Assert.Equal((true, null), await Availability(first, "alpha01"));
        var again = await Json(await Submit(first, ExistingBody("alpha01")), HttpStatusCode.Created);
        Assert.Matches("^AP-[0-9]{8}-0003$", again.GetProperty("number").GetString());
        Assert.Equal([again.GetProperty("id").GetGuid(), firstId], (await Json(await first.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()));

        async Task RejectedAccount(string number)
        {
            using var browser = new Browser(address);
            var challenge = await SendCode(browser, number);
            await Rejected(await browser.Send(HttpMethod.Post, Session, new { verificationId = challenge, code = ops.Outbox.Code(challenge) }), HttpStatusCode.Forbidden, "SIGNUP_ACCOUNT_UNAVAILABLE");
            Assert.Null(browser.Token);
        }
    }

    [Fact]
    public async Task 申请人会话随号码归属作废_号码从绑定的自然人上换掉或改绑给别人后旧会话读申请人与本人申请都401_重新核验的会话属于新主人()
    {
        await using var ops = await StartOps();
        var address = ops.Business.Client.BaseAddress!;
        var accounts = ops.Business.Identity.Accounts;

        // 甲以既有账号核验并提交：会话绑定甲（提交后轮换，仍绑定甲），本人的申请里有这一条。
        var (jia, phone) = await NewPerson(ops.Business, "原主人");
        using var stale = new Browser(address);
        Assert.True((await Verify(ops, stale, phone)).GetProperty("existingAccount").GetBoolean());
        var applied = (await Json(await Submit(stale, ExistingBody("ownerco")), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Assert.Equal([applied], (await Json(await stale.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        var token = stale.Token!;

        // 甲把手机号换成别的号码：这个号码不再属于会话绑定的自然人，旧会话随即作废——不再代表甲，也不退回成未注册号码的会话。
        await accounts.ReplaceIdentifierAsync(jia.Id, IdentifierKind.Phone, phone, new IdentifierInput(IdentifierKind.Phone, NewPhone(), true), Ct);
        Assert.Null(await accounts.FindPersonAsync(phone, Ct));
        await Expired();

        // 号码再绑给乙：旧会话仍然作废，拿不到乙的身份与申请；乙重新核验得到的会话才属于乙。
        var (yi, _) = await NewPerson(ops.Business, "新主人");
        await accounts.BindIdentifierAsync(yi.Id, new IdentifierInput(IdentifierKind.Phone, phone, true), Ct);
        await Expired();
        await Age($"phone = '+86{phone}'", "2 minutes");
        using var fresh = new Browser(address);
        var current = await Verify(ops, fresh, phone);
        Assert.Equal((Mask(phone), true, "新主人"), (current.GetProperty("phone").GetString(), current.GetProperty("existingAccount").GetBoolean(), current.GetProperty("displayName").GetString()));
        Assert.Empty((await Json(await fresh.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());

        // 401 会顺带清掉浏览器里的 Cookie：每次都把旧令牌放回去，确认作废的是服务端判定，而不是请求没带 Cookie。
        async Task Expired()
        {
            foreach (var path in new[] { Session, Mine })
            {
                stale.Restore(token);
                await Rejected(await stale.Send(HttpMethod.Get, path), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
            }
        }
    }

    // ---- 宿主与辅助 ----

    private const string Verifications = "/api/signup/verifications";
    private const string Session = "/api/signup/session";
    private const string Mine = "/api/signup/me/applications";
    private static string Withdraw(Guid id) => $"/api/signup/me/applications/{id}:withdraw";

    private async Task<Ops> StartOps(object? config = null, bool messenger = true)
    {
        var platform = new LifecyclePlatform { Management = true };
        RunningApp? business = null; WebApplication? management = null;
        try
        {
            business = await Start(lifecycle: platform, identity: true, configure: builder =>
            {
                // 与身份服务同一个数据库连接（受限应用登录）；开通申请的表是全局表，没有 RLS。
                builder.Services.AddQianData<TenancySignupDbContext>((services, options) => options.UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Tenancy"))
                    .UseKunSchema(Schema));
                builder.Services.AddSingleton<IStartupFilter>(new SignupBranch());
            }, schema: SchemaSql);
            // 运营宿主重挂 Authz（带平台角色与本组件的权限目录，目录随 Authz 装载持久化）与 Audit；本组件在其后挂载，旁路端口按请求解析。
            var permissions = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.tenancy.signup/metadata/permissions.json")));
            var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(TenancySignupDefinition.ComponentId, "1.0.0", permissions, JsonSerializer.SerializeToElement(Array.Empty<object>()));
            var host = business;
            management = await ManagementHost(business, platform, catalogs: [catalog],
                configurePipeline: app => app.MapQianTenancySignupOperations(_ => Loader(host).GetContext("signup", ScopeKey.Global)));
            var outbox = new Outbox();
            await Mount(business, messenger ? [MessengerDefinition(outbox), SignupDefinition(config)] : [SignupDefinition(config)]);
            return new Ops(platform, business, management, new HttpClient { BaseAddress = new Uri(management.Urls.Single()) }, outbox);
        }
        catch
        {
            if (management is not null) { await management.StopAsync(Ct); await management.DisposeAsync(); }
            if (business is not null) await business.DisposeAsync();
            platform.Dispose(); throw;
        }
    }

    // 生产同一套迁移的建表脚本；每次宿主从空 schema 开始，授权给与身份服务相同的应用登录。
    private static string SchemaSql(IServiceProvider services) =>
        $"DROP SCHEMA IF EXISTS {Schema} CASCADE;" + services.GetRequiredService<TenancySignupDbContext>().GetService<IMigrator>().GenerateScript() +
        $"GRANT USAGE ON SCHEMA {Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {Schema} TO qian_app;";

    private static PluginLoader Loader(RunningApp business) => business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;

    private static async Task Mount(RunningApp business, params PluginDefinition[] definitions)
    {
        var loader = Loader(business);
        await loader.MountAsync(definitions, Ct);
        foreach (var definition in definitions)
            Assert.True(loader.GetState(definition.Entry.Id, ScopeKey.Global) == PluginState.Ready, loader.GetFailure(definition.Entry.Id, ScopeKey.Global)?.ToString());
    }

    private static PluginDefinition SignupDefinition(object? config) => new(new PluginEntry("signup", TenancySignupDefinition.ComponentId, "1.0.0", ScopeKey.Global, 1,
        JsonSerializer.SerializeToElement(config ?? new { })), new TenancySignupPlugin(), [TenancySignupDefinition.Key]);

    private static PluginDefinition MessengerDefinition(Outbox outbox) => new(new PluginEntry("signup.messenger", "qian.tests.signup-messenger", "1.0.0", ScopeKey.Global, 0,
        JsonSerializer.SerializeToElement(new { })), new MessengerPlugin(outbox), [TenancySignupDefinition.MessengerKey]);

    private sealed record Ops(LifecyclePlatform Platform, RunningApp Business, WebApplication Management, HttpClient Client, Outbox Outbox) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await Management.StopAsync(Ct); await Management.DisposeAsync(); await Business.DisposeAsync(); Platform.Dispose();
        }
    }

    // 身份服务的公开分支：申请人接口不经业务的租户入口管线（同 IdentityBranchFilter），分支里自带 ErrorDetail 映射。
    private sealed class SignupBranch : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(http => TenancySignupEndpoints.IsApplicantPath(http.Request.Path), branch =>
            {
                branch.UseQianErrors(); branch.UseRouting();
                branch.UseEndpoints(endpoints => endpoints.MapQianTenancySignupApplicant(http =>
                    http.RequestServices.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("signup", ScopeKey.Global)));
            });
            next(app);
        };
    }

    // 测试用发件端口：记录验证码与审核结果。
    private sealed class Outbox : ISignupMessenger
    {
        internal ConcurrentQueue<SignupVerificationMessage> Codes { get; } = new();
        internal ConcurrentQueue<SignupResultMessage> Results { get; } = new();
        public ValueTask SendVerificationCodeAsync(SignupVerificationMessage message, CancellationToken cancellationToken = default) { Codes.Enqueue(message); return ValueTask.CompletedTask; }
        public ValueTask SendResultAsync(SignupResultMessage message, CancellationToken cancellationToken = default) { Results.Enqueue(message); return ValueTask.CompletedTask; }
        internal string Code(Guid challenge) => Codes.Single(item => item.Id == challenge).Code;
        internal string Phone(Guid challenge) => Codes.Single(item => item.Id == challenge).Phone;
    }

    private sealed class MessengerPlugin(Outbox outbox) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<ISignupMessenger>(TenancySignupDefinition.MessengerKey, outbox);
            return ValueTask.CompletedTask;
        }
    }

    // 一个申请人的浏览器：Cookie 容器 + 同源的 Origin（可改写成跨源或去掉）。
    private sealed class Browser(Uri address) : IDisposable
    {
        private readonly CookieContainer _cookies = new();
        private HttpClient? _client;
        private HttpClient Client => _client ??= new HttpClient(new HttpClientHandler { CookieContainer = _cookies, UseCookies = true, AllowAutoRedirect = false }) { BaseAddress = address };
        internal string? Token => _cookies.GetCookies(new Uri(address, Session)).FirstOrDefault(cookie => cookie.Name == TenancySignupEndpoints.CookieName)?.Value is { Length: > 0 } value ? value : null;
        internal void Restore(string token) => _cookies.Add(new Uri(address, Session), new Cookie(TenancySignupEndpoints.CookieName, token, TenancySignupEndpoints.CookiePath));
        internal Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? origin = null, string? fetchSite = null, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, path);
            var sameOrigin = address.GetLeftPart(UriPartial.Authority);
            if (method != HttpMethod.Get && (origin ?? sameOrigin) is { Length: > 0 } value) request.Headers.Add("Origin", value);
            if (fetchSite is not null) request.Headers.Add("Sec-Fetch-Site", fetchSite);
            request.Content = content ?? (body is null ? null : JsonContent.Create(body, body.GetType()));
            return SendAndDispose(Client, request);
        }
        public void Dispose() => _client?.Dispose();
    }

    private static async Task<HttpResponseMessage> SendAndDispose(HttpClient client, HttpRequestMessage request)
    { using (request) return await client.SendAsync(request, Ct); }

    private static Dictionary<string, object?> Body(string code, string? company = "测试科技", string? name = "申请人", string? password = Password, bool acceptTerms = true,
        string size = "1-20", string? email = "apply@example.com")
    {
        var body = new Dictionary<string, object?> { ["companyName"] = company, ["code"] = code, ["size"] = size, ["acceptTerms"] = acceptTerms, ["industry"] = "软件", ["description"] = "第一行\n第二行" };
        if (name is not null) body["displayName"] = name;
        if (password is not null) body["password"] = password;
        if (email is not null) body["email"] = email;
        return body;
    }

    private static Dictionary<string, object?> ExistingBody(string code, string company = "测试科技", string size = "21-100") =>
        new() { ["companyName"] = company, ["code"] = code, ["size"] = size, ["acceptTerms"] = true };

    private static Task<HttpResponseMessage> Submit(Browser browser, object body) => browser.Send(HttpMethod.Post, Mine, body);

    private static async Task<Guid> SendCode(Browser browser, string phone) =>
        (await Json(await browser.Send(HttpMethod.Post, Verifications, new { phone }), HttpStatusCode.Accepted)).GetProperty("id").GetGuid();

    private static async Task<JsonElement> Verify(Ops ops, Browser browser, string phone)
    {
        var challenge = await SendCode(browser, phone);
        return await Json(await browser.Send(HttpMethod.Post, Session, new { verificationId = challenge, code = ops.Outbox.Code(challenge) }), HttpStatusCode.OK);
    }

    private static async Task<(bool Available, string? Reason)> Availability(Browser browser, string code)
    {
        var result = await Json(await browser.Send(HttpMethod.Get, "/api/signup/code-availability?code=" + Uri.EscapeDataString(code)), HttpStatusCode.OK);
        Assert.Equal(code, result.GetProperty("code").GetString());
        return (result.GetProperty("available").GetBoolean(), result.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null);
    }

    private static (string?, bool) Applicant(JsonElement applicant) => (applicant.GetProperty("phone").GetString(), applicant.GetProperty("existingAccount").GetBoolean());

    private static string Mask(string phone) => phone[..3] + "****" + phone[^4..];

    // 把挑战的创建时间整体前移，模拟时间流逝（限流窗口与重发间隔按创建时间计算）。
    private Task<int> Age(string where, string interval) => Admin($"UPDATE {Schema}.verifications SET created_at = created_at - interval '{interval}' WHERE {where};");

    private static string SessionCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(TenancySignupEndpoints.CookieName + "=", StringComparison.Ordinal));

    private static async Task<HttpStatusCode> Status(Task<HttpResponseMessage> pending)
    { using var response = await pending; return response.StatusCode; }

    private static async Task<int> RateLimited(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.TooManyRequests, body);
            Assert.Equal("RATE_LIMITED", ErrorDetailJson.Deserialize(body)!.Code);
            return (int)response.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
        }
    }

    private static async Task<SignInResult> SignIn(RunningApp business, string identifier, string password)
    {
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(password));
        return await business.Identity.SignIn.SignInAsync(new LoginInput("web", null, null, null, proof) { Identifier = identifier }, new LoginContext(), Ct);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == status, (int)response.StatusCode + " " + body);
            if (body.Length == 0) return default;
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
    }

    private static async Task<ErrorDetail> Rejected(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == status, (int)response.StatusCode + " " + body);
            var error = ErrorDetailJson.Deserialize(body)!;
            Assert.Equal(code, error.Code);
            return error;
        }
    }
}

internal static class ErrorDetailPointer
{
    // 错误位置：顶层 pointer 或第一个字段错误的 pointer。
    internal static string? Field(this ErrorDetail error) => error.Pointer ?? (error.Errors is [var first, ..] ? first.Pointer : null);
}

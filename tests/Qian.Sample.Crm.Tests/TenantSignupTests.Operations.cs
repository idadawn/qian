using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Qian.Audit;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Tenancy.Signup;

namespace Qian.Sample.Crm.Tests;

// 开通申请的平台运营一侧（S1-TEN-019）：运营宿主上的列表、详情、通过、驳回、重试与状态机元数据；
// 授权只走平台超管显式旁路（每次放行在平台审计链记一条），开通经 qian.tenancy 的开通编排与生命周期端口完成。
public sealed partial class TenantSignupTests
{
    private const string Applications = "/api/signup/applications";
    private const string StateMachine = "/api/signup/meta/state-machine";
    private const string Permission = "qian.tenancy.signup:application:";
    private static readonly string[] AdministratorSteps = ["05:membership", "06:administrator", "07:owner"];

    private static string Detail(Guid id) => $"{Applications}/{id}";
    private static string Act(Guid id, string action) => $"{Applications}/{id}:{action}";

    [Fact]
    public async Task 运营审核_只收平台超管且每次放行一条平台旁路记录_列表计数筛选与自动检查_通过即建租户与管理员成员申请人可登录_租户号冲突先修正_驳回原因可见备注不可见并释放租户号_发件端口缺席不阻断()
    {
        await using var ops = await StartOps();
        var (business, client) = (ops.Business, ops.Client);
        var address = business.Client.BaseAddress!;
        var accounts = business.Identity.Accounts;
        // 运营令牌一分钟有效，每次请求现签。
        string Root() => business.Token(null, "", "root", "user");

        // 四份申请：甲（新号码）、乙（已有账号，已是租户 A 的成员）、丙（与甲同名，新号码）、丁（新号码，将被驳回）。
        using var jia = new Browser(address); var jiaPhone = NewPhone();
        await Verify(ops, jia, jiaPhone);
        var jiaId = (await Json(await Submit(jia, Body("jiafang", company: "甲方科技", name: "申请人甲")), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var (yi, yiPhone) = await NewPerson(business, "老用户乙"); await Join(business, A, yi.Id, "老用户乙");
        using var yiBrowser = new Browser(address);
        await Verify(ops, yiBrowser, yiPhone);
        var yiApplication = await Json(await Submit(yiBrowser, ExistingBody("yifang", company: "乙方科技")), HttpStatusCode.Created);
        var yiId = yiApplication.GetProperty("id").GetGuid();
        using var bing = new Browser(address);
        await Verify(ops, bing, NewPhone());
        var bingId = (await Json(await Submit(bing, Body("bingfang", company: "甲方科技", name: "申请人丙")), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var ding = new Browser(address);
        await Verify(ops, ding, NewPhone());
        var dingId = (await Json(await Submit(ding, Body("dingfang", company: "丁方科技", name: "申请人丁", size: "101-500")), HttpStatusCode.Created)).GetProperty("id").GetGuid();

        // 无凭据 401；租户主体、非超管的平台用户与 Agent 一律 403（运营入口只收人类平台超管）。
        var unknown = Guid.NewGuid();
        (HttpMethod Method, string Path, object? Body)[] endpoints =
        [
            (HttpMethod.Get, Applications, null), (HttpMethod.Get, Detail(unknown), null), (HttpMethod.Post, Act(unknown, "approve"), new { plan = "trial" }),
            (HttpMethod.Post, Act(unknown, "reject"), new { reason = "资料不全" }), (HttpMethod.Post, Act(unknown, "retry"), null), (HttpMethod.Get, StateMachine, null),
        ];
        foreach (var token in new[] { null, business.Token(A, "", "root", "user"), business.Token(null, "", "intruder", "user"), business.Token(null, "", "root", "agent") })
            foreach (var (method, path, body) in endpoints)
                await Rejected(await Operate(client, method, path, token, body), token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, token is null ? "UNAUTHENTICATED" : "AUTHZ_DENIED");
        // 申请人会话 Cookie 不是运营凭据。
        using (var request = new HttpRequestMessage(HttpMethod.Get, Applications))
        {
            request.Headers.Add("Cookie", TenancySignupEndpoints.CookieName + "=" + jia.Token);
            await Rejected(await client.SendAsync(request, Ct), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }
        // 目标只取自路径：请求体带 tenantId / tenant_id / id 在绑定时拒绝；其它输入错误同样在旁路之前拒绝。
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
        {
            await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "approve"), Root(), new Dictionary<string, object?> { ["plan"] = "trial", [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
            await Rejected(await Operate(client, HttpMethod.Post, Act(dingId, "reject"), Root(), new Dictionary<string, object?> { ["reason"] = "资料不全", [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
            await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "retry"), Root(), new Dictionary<string, object?> { [forbidden] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "approve"), Root(), new { }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "approve"), Root(), new { plan = "free" }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        Assert.Equal("#/users", (await Rejected(await Operate(client, HttpMethod.Post, Act(yiId, "approve"), Root(), new { plan = "formal", users = 0 }), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/code", (await Rejected(await Operate(client, HttpMethod.Post, Act(bingId, "approve"), Root(), new { plan = "trial", code = "Bad-Code" }), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        Assert.Equal("#/reason", (await Rejected(await Operate(client, HttpMethod.Post, Act(dingId, "reject"), Root(), new { reason = "" }), HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        foreach (var query in new[] { "?status=open", "?take=0", "?take=101", "?skip=-1", "?size=2-3", "?keyword=" + new string('x', 201) })
            await Rejected(await Operate(client, HttpMethod.Get, Applications + query, Root()), HttpStatusCode.BadRequest, "INPUT_INVALID");
        Assert.Empty(await Bypasses(business)); // 401 / 403 / 400 都在超管旁路之前拒绝，平台链上没有放行记录。

        // 列表：新的在前；计数只受关键字与规模筛选影响；手机号为掩码；每行只带最需要关注的一条检查。
        var all = await List("");
        Assert.Equal((4, 4, 4, 0), (all.GetProperty("total").GetInt32(), Counted(all, "all"), Counted(all, "submitted"), Counted(all, "approved")));
        var rows = all.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([dingId, bingId, yiId, jiaId], rows.Select(row => row.GetProperty("id").GetGuid()));
        Assert.All(rows, row => Assert.Equal("submitted", row.GetProperty("status").GetString()));
        Assert.Equal(("乙方科技", "yifang", "21-100", "老用户乙", Mask(yiPhone)), (rows[2].GetProperty("companyName").GetString(), rows[2].GetProperty("code").GetString(),
            rows[2].GetProperty("size").GetString(), rows[2].GetProperty("contactName").GetString(), rows[2].GetProperty("phone").GetString()));
        // 甲、丙同名（警告，带数量）；乙已加入租户 A（提示，带数量）；丁全部通过。
        Assert.Equal<(string?, string?, int?)>(("duplicateName", "warning", 1), Check(rows[3].GetProperty("check")));
        Assert.Equal<(string?, string?, int?)>(("existingMemberships", "info", 1), Check(rows[2].GetProperty("check")));
        Assert.Equal<(string?, string?, int?)>(("duplicateName", "warning", 1), Check(rows[1].GetProperty("check")));
        Assert.Equal<(string?, string?, int?)>(("code", "pass", null), Check(rows[0].GetProperty("check")));
        Assert.Equal((0, 4), Page(await List("?status=approved")));
        Assert.Equal((2, 2), Page(await List("?keyword=" + Uri.EscapeDataString("甲方"))));
        Assert.Equal((1, 1), Page(await List("?keyword=" + yiApplication.GetProperty("number").GetString())));
        Assert.Equal((0, 0), Page(await List("?keyword=" + Uri.EscapeDataString("%"))));
        Assert.Equal((1, 1), Page(await List("?size=21-100")));
        Assert.Equal([dingId, bingId], (await List("?take=2")).GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()));
        Assert.Equal([jiaId], (await List("?skip=3&take=2")).GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()));

        // 详情：申请人（自然人 ID、掩码）、全部自动检查与审核缺省值；未审核时没有审核结果与开通进度。
        var yiDetail = await Json(await Operate(client, HttpMethod.Get, Detail(yiId), Root()), HttpStatusCode.OK);
        var profile = yiDetail.GetProperty("applicant");
        Assert.Equal((yi.Id, "老用户乙", Mask(yiPhone), true), (profile.GetProperty("subjectId").GetGuid(), profile.GetProperty("contactName").GetString(),
            profile.GetProperty("phone").GetString(), profile.GetProperty("existingAccount").GetBoolean()));
        Assert.Equal([("code", "pass", null), ("duplicateName", "pass", null), ("existingMemberships", "info", 1)], Checks(yiDetail));
        Assert.Equal((30, 20), (yiDetail.GetProperty("defaults").GetProperty("trialDays").GetInt32(), yiDetail.GetProperty("defaults").GetProperty("users").GetInt32()));
        Assert.False(Has(yiDetail, "review") || Has(yiDetail, "provisioning"));
        await Rejected(await Operate(client, HttpMethod.Get, Detail(Guid.NewGuid()), Root()), HttpStatusCode.NotFound, "NOT_FOUND");

        // 通过（试用、全用缺省值）：开通编排建租户（试用、到期日 = 今天 + 30 天、用户配额 20）与申请人的管理员成员，tenant.owner 落在申请人；
        // 申请人用自己提交时设的密码即可登录进新租户；申请人看到已通过，结果通知发出。
        var approved = await Json(await Operate(client, HttpMethod.Post, Act(jiaId, "approve"), Root(), new { plan = "trial" }), HttpStatusCode.OK);
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        Assert.Empty(approved.GetProperty("checks").EnumerateArray());
        var review = approved.GetProperty("review");
        Assert.Equal(("root", "trial", 20), (review.GetProperty("reviewer").GetString(), review.GetProperty("plan").GetString(), review.GetProperty("users").GetInt32()));
        Assert.InRange(review.GetProperty("validUntil").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddDays(30).AddHours(-1), DateTimeOffset.UtcNow.AddDays(30).AddHours(1));
        var provisioning = approved.GetProperty("provisioning");
        var tenant = provisioning.GetProperty("tenantId").GetGuid(); var membership = provisioning.GetProperty("membershipId").GetGuid();
        Assert.Equal(1, provisioning.GetProperty("attempts").GetInt32());
        Assert.False(Has(provisioning, "failureReason"));
        foreach (var step in AdministratorSteps) Assert.Equal("succeeded", Steps(provisioning)[step]);
        Assert.Equal("jiafang|甲方科技|trial|true|20", await Text("SELECT code || '|' || name || '|' || status || '|' || (abs(extract(epoch FROM valid_until - now()) - 30 * 86400) < 3600)::text || " +
            $"'|' || (quotas->>'users') FROM plugin_qian_tenancy.tenants WHERE id = '{tenant}'"));
        var jiaPerson = await accounts.FindPersonAsync(jiaPhone, Ct) ?? throw new InvalidOperationException("没有申请人甲。");
        Assert.Equal($"{membership}|{jiaPerson.Id}|true|active", await Text($"SELECT string_agg(id::text || '|' || person_id::text || '|' || is_tenant_administrator::text || '|' || status, ',') FROM plugin_qian_identity.memberships WHERE tenant_id = '{tenant}'"));
        Assert.Equal(jiaPerson.Id.ToString("D"), await Owners(tenant));
        var signedIn = await SignIn(business, jiaPhone, Password);
        Assert.Equal((SignInStatus.Authenticated, tenant, membership), (signedIn.Status, signedIn.Session!.TenantId, signedIn.Session.MembershipId));
        var jiaView = Assert.Single((await Json(await jia.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal(("approved", "trial"), (jiaView.GetProperty("status").GetString(), jiaView.GetProperty("plan").GetString()));
        Assert.True(Has(jiaView, "validUntil") && Has(jiaView, "completedAt") && !Has(jiaView, "rejectReason"));
        var notified = Assert.Single(ops.Outbox.Results, message => message.Id == jiaId);
        Assert.Equal((SignupResultKind.Approved, "jiafang", "+86" + jiaPhone), (notified.Kind, notified.TenantCode, notified.Phone));
        // 已通过的不能再通过、驳回或重试。
        await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "approve"), Root(), new { plan = "trial" }), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "reject"), Root(), new { reason = "重复" }), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        await Rejected(await Operate(client, HttpMethod.Post, Act(jiaId, "retry"), Root()), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");

        // 租户号在审核前被占用：检查报错误；照原样通过 409（位置 #/code）；修正租户号后通过（试用、指定到期日与配额）。
        await Admin($"INSERT INTO plugin_qian_tenancy.tenants(id, code, name, isolation, status, created_at) VALUES ('{Guid.CreateVersion7()}'::uuid, 'bingfang', '别家', 'row', 'active', now());");
        var bingDetail = await Json(await Operate(client, HttpMethod.Get, Detail(bingId), Root()), HttpStatusCode.OK);
        Assert.Equal([("code", "error", null), ("duplicateName", "warning", 1), ("existingMemberships", "pass", 0)], Checks(bingDetail));
        Assert.Equal("taken", bingDetail.GetProperty("checks")[0].GetProperty("reason").GetString());
        Assert.Equal("#/code", (await Rejected(await Operate(client, HttpMethod.Post, Act(bingId, "approve"), Root(), new { plan = "trial" }), HttpStatusCode.Conflict, "SIGNUP_CODE_TAKEN")).Field());
        Assert.Equal("submitted", await Text($"SELECT status FROM {Schema}.applications WHERE id = '{bingId}'"));
        var until = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds());
        var bingApproved = await Json(await Operate(client, HttpMethod.Post, Act(bingId, "approve"), Root(), new { plan = "trial", code = "bingfang2", validUntil = until, users = 5 }), HttpStatusCode.OK);
        Assert.Equal(("approved", "bingfang2"), (bingApproved.GetProperty("status").GetString(), bingApproved.GetProperty("company").GetProperty("code").GetString()));
        Assert.Equal((until, 5), (bingApproved.GetProperty("review").GetProperty("validUntil").GetDateTimeOffset(), bingApproved.GetProperty("review").GetProperty("users").GetInt32()));
        Assert.Equal("trial|true|5", await Text($"SELECT status || '|' || (valid_until = to_timestamp({until.ToUnixTimeSeconds()}))::text || '|' || (quotas->>'users') FROM plugin_qian_tenancy.tenants WHERE code = 'bingfang2'"));

        // 驳回：原因必填且对申请人可见，审核备注只给运营；驳回释放租户号，同一申请人可以另交新申请。
        var rejected = await Json(await Operate(client, HttpMethod.Post, Act(dingId, "reject"), Root(), new { reason = "资料不全", note = "内部备注：营业执照模糊" }), HttpStatusCode.OK);
        Assert.Equal(("rejected", "资料不全", "内部备注：营业执照模糊"), (rejected.GetProperty("status").GetString(), rejected.GetProperty("review").GetProperty("rejectReason").GetString(),
            rejected.GetProperty("review").GetProperty("note").GetString()));
        Assert.True(Has(rejected, "completedAt") && !Has(rejected, "provisioning"));
        await Rejected(await Operate(client, HttpMethod.Post, Act(dingId, "reject"), Root(), new { reason = "资料不全" }), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        await Rejected(await Operate(client, HttpMethod.Post, Act(dingId, "approve"), Root(), new { plan = "trial" }), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        var dingView = Assert.Single((await Json(await ding.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal(("rejected", "资料不全"), (dingView.GetProperty("status").GetString(), dingView.GetProperty("rejectReason").GetString()));
        Assert.DoesNotContain(Strings(dingView), value => value.Contains("内部备注", StringComparison.Ordinal));
        var refusal = Assert.Single(ops.Outbox.Results, message => message.Id == dingId);
        Assert.Equal((SignupResultKind.Rejected, "资料不全", (string?)null), (refusal.Kind, refusal.Reason, refusal.TenantCode));
        Assert.Equal((true, null), await Availability(ding, "dingfang"));
        Assert.Equal("submitted", (await Json(await Submit(ding, ExistingBody("dingfang", company: "丁方科技")), HttpStatusCode.Created)).GetProperty("status").GetString());

        // 到期日须晚于当前时间（放行之后的业务校验，旁路已记录）。发件端口卸下后审核照常完成，只是没有通知。
        Assert.Equal("#/validUntil", (await Rejected(await Operate(client, HttpMethod.Post, Act(yiId, "approve"), Root(), new { plan = "formal", validUntil = DateTimeOffset.UtcNow.AddDays(-1) }),
            HttpStatusCode.BadRequest, "INPUT_INVALID")).Field());
        await Loader(business).UnmountAsync("signup.messenger", ScopeKey.Global, Ct);
        var sent = ops.Outbox.Results.Count;
        var yiApproved = await Json(await Operate(client, HttpMethod.Post, Act(yiId, "approve"), Root(), new { plan = "formal", note = "老客户" }), HttpStatusCode.OK);
        Assert.Equal(("approved", "formal", "老客户"), (yiApproved.GetProperty("status").GetString(), yiApproved.GetProperty("review").GetProperty("plan").GetString(),
            yiApproved.GetProperty("review").GetProperty("note").GetString()));
        Assert.False(Has(yiApproved.GetProperty("review"), "validUntil"));
        var yiTenant = yiApproved.GetProperty("provisioning").GetProperty("tenantId").GetGuid();
        Assert.Equal("yifang|active|true|20", await Text($"SELECT code || '|' || status || '|' || (valid_until IS NULL)::text || '|' || (quotas->>'users') FROM plugin_qian_tenancy.tenants WHERE id = '{yiTenant}'"));
        Assert.Equal(sent, ops.Outbox.Results.Count);
        // 既有账号作为新租户的管理员：原有的成员身份保留，新增一个（身份组件的成员归属查询）。
        var expectedTenants = new[] { Guid.Parse(A), yiTenant };
        Assert.Equal(expectedTenants.Order(), ((await accounts.ListPersonTenantsAsync(yi.Id, Ct)) ?? []).Order());
        Assert.Equal(yi.Id.ToString("D"), await Owners(yiTenant));

        // 机器可读的操作描述：通过、驳回只给人且要求确认，重试不对 Agent 暴露。
        var machine = await Json(await Operate(client, HttpMethod.Get, StateMachine, Root()), HttpStatusCode.OK);
        foreach (var name in new[] { "approve", "reject" })
            Assert.Equal((false, true), Agent(machine, name));
        Assert.False(Agent(machine, "retry").Expose);
        // 运营接口只在运营宿主上：业务宿主（申请人入口所在）没有这些路由。
        using (var request = new HttpRequestMessage(HttpMethod.Get, Applications))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Root());
            using var response = await business.Client.SendAsync(request, Ct);
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }

        // 平台审计链：每次放行恰好一条超管旁路记录（主体、权限点、资源 = 目标申请），放行后才得出的 409 / 400 同样留有记录；重试按 approve 授权。
        var bypasses = await Bypasses(business);
        Assert.All(bypasses, record => Assert.Equal(("superadmin", "user", "root", "api"), (record.Payload.GetProperty("bypass").GetString(), record.ActorType, record.ActorId, record.Channel)));
        int Allowed(string action, Guid? id) => bypasses.Count(record => record.Payload.GetProperty("permission").GetString() == Permission + action &&
            record.Resource == (id is null ? "signup/applications" : "signup/applications/" + id.Value.ToString("D")));
        Assert.Equal((3, 1), (Allowed("approve", jiaId), Allowed("reject", jiaId)));
        Assert.Equal((2, 0), (Allowed("approve", bingId), Allowed("reject", bingId)));
        Assert.Equal((1, 2), (Allowed("approve", dingId), Allowed("reject", dingId)));
        Assert.Equal((2, 1), (Allowed("approve", yiId), Allowed("read", yiId)));
        Assert.Equal(0, Allowed("approve", unknown) + Allowed("reject", unknown) + Allowed("read", unknown));
        Assert.Equal(9, Allowed("read", null)); // 8 次列表 + 1 次状态机元数据
        var source = Loader(business).GetContext("tenancy", ScopeKey.Global);
        Assert.True((await source.Audit().VerifyPlatformAsync(source, Ct)).Valid);

        async Task<JsonElement> List(string query) => await Json(await Operate(client, HttpMethod.Get, Applications + query, Root()), HttpStatusCode.OK);
    }

    [Fact]
    public async Task 开通中途失败转failed且申请人仍见开通中_重试沿用同一租户与管理员成员收敛到approved_终态再重试409_不重复建租户与成员()
    {
        await using var ops = await StartOps();
        var (business, client) = (ops.Business, ops.Client);
        string Root() => business.Token(null, "", "root", "user");
        using var applicant = new Browser(business.Client.BaseAddress!);
        var phone = NewPhone();
        await Verify(ops, applicant, phone);
        var id = (await Json(await Submit(applicant, Body("retryco", company: "重试科技")), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var person = await business.Identity.Accounts.FindPersonAsync(phone, Ct) ?? throw new InvalidOperationException("没有建自然人。");
        // 待审核的申请不能重试（放行后由状态机拒绝）。
        await Rejected(await Operate(client, HttpMethod.Post, Act(id, "retry"), Root()), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");

        // 注入故障：开通编排第 06 步（写管理员指针）失败；第 05 步（管理员成员）已成功。
        await Admin("CREATE FUNCTION plugin_qian_tenancy.signup_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected fault'; END $$; " +
            "CREATE TRIGGER signup_fault BEFORE INSERT ON plugin_qian_tenancy.tenant_admins FOR EACH ROW EXECUTE FUNCTION plugin_qian_tenancy.signup_fault();");
        var failed = await Json(await Operate(client, HttpMethod.Post, Act(id, "approve"), Root(), new { plan = "trial" }), HttpStatusCode.OK);
        Assert.Equal("failed", failed.GetProperty("status").GetString());
        var provisioning = failed.GetProperty("provisioning");
        Assert.Equal(("step_failed", 1), (provisioning.GetProperty("failureReason").GetString(), provisioning.GetProperty("attempts").GetInt32()));
        var tenant = provisioning.GetProperty("tenantId").GetGuid(); var membership = provisioning.GetProperty("membershipId").GetGuid();
        var steps = Steps(provisioning);
        Assert.Equal(("succeeded", "failed"), (steps["05:membership"], steps["06:administrator"]));
        Assert.False(steps.ContainsKey("07:owner"));
        Assert.Equal("pending", await Text($"SELECT status FROM plugin_qian_tenancy.tenants WHERE id = '{tenant}'"));
        // 运营列表里是开通失败（仍算进行中，带检查）；申请人看到的仍是开通中，没有内部原因；还不能登录新租户，也没有结果通知。
        var row = Assert.Single((await Json(await Operate(client, HttpMethod.Get, Applications + "?status=failed", Root()), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal(id, row.GetProperty("id").GetGuid());
        Assert.True(Has(row, "check"));
        var waiting = Assert.Single((await Json(await applicant.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray());
        Assert.Equal("provisioning", waiting.GetProperty("status").GetString());
        Assert.DoesNotContain(Strings(waiting), value => value.Contains("step_failed", StringComparison.Ordinal));
        Assert.NotEqual(SignInStatus.Authenticated, (await SignIn(business, phone, Password)).Status);
        Assert.Empty(ops.Outbox.Results);

        // 排除故障后重试：沿用同一租户与管理员成员，只续跑没成功的步骤，再激活；请求体非空在旁路之前拒绝。
        await Admin("DROP TRIGGER signup_fault ON plugin_qian_tenancy.tenant_admins; DROP FUNCTION plugin_qian_tenancy.signup_fault();");
        await Rejected(await Operate(client, HttpMethod.Post, Act(id, "retry"), Root(), new Dictionary<string, object?> { ["tenantId"] = A }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        var retried = await Json(await Operate(client, HttpMethod.Post, Act(id, "retry"), Root()), HttpStatusCode.OK);
        Assert.Equal("approved", retried.GetProperty("status").GetString());
        var after = retried.GetProperty("provisioning");
        Assert.Equal((tenant, membership, 2), (after.GetProperty("tenantId").GetGuid(), after.GetProperty("membershipId").GetGuid(), after.GetProperty("attempts").GetInt32()));
        Assert.False(Has(after, "failureReason"));
        foreach (var step in AdministratorSteps) Assert.Equal("succeeded", Steps(after)[step]);
        Assert.Equal("1|trial", await Text("SELECT count(*)::text || '|' || min(status) FROM plugin_qian_tenancy.tenants WHERE code = 'retryco'"));
        Assert.Equal($"{membership}|{person.Id}", await Text($"SELECT string_agg(id::text || '|' || person_id::text, ',') FROM plugin_qian_identity.memberships WHERE tenant_id = '{tenant}'"));
        Assert.Equal(membership.ToString(), await Text($"SELECT membership_id::text FROM plugin_qian_tenancy.tenant_admins WHERE tenant_id = '{tenant}'"));
        Assert.Equal(person.Id.ToString("D"), await Owners(tenant));
        var signedIn = await SignIn(business, phone, Password);
        Assert.Equal((SignInStatus.Authenticated, tenant, membership), (signedIn.Status, signedIn.Session!.TenantId, signedIn.Session.MembershipId));
        Assert.Equal("approved", Assert.Single((await Json(await applicant.Send(HttpMethod.Get, Mine), HttpStatusCode.OK)).GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
        Assert.Single(ops.Outbox.Results, message => message.Id == id && message.Kind == SignupResultKind.Approved);

        // 已通过再重试 409，什么都不变。平台链：重试（待审核）、通过、重试、再重试各一条，请求体被拒的那次没有。
        await Rejected(await Operate(client, HttpMethod.Post, Act(id, "retry"), Root()), HttpStatusCode.Conflict, "SIGNUP_STATE_CONFLICT");
        Assert.Equal("1", await Text($"SELECT count(*) FROM plugin_qian_identity.memberships WHERE tenant_id = '{tenant}'"));
        Assert.Equal(4, (await Bypasses(business)).Count(record => record.Payload.GetProperty("permission").GetString() == Permission + "approve" &&
            record.Resource == "signup/applications/" + id.ToString("D")));
    }

    // ---- 运营侧辅助 ----

    private static async Task<HttpResponseMessage> Operate(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        return await client.SendAsync(request, Ct);
    }

    // 平台链上本组件的超管旁路记录（引导时生命周期条目与租户主档的放行不算）。
    private static async Task<AuditRecord[]> Bypasses(RunningApp business)
    {
        var source = Loader(business).GetContext("tenancy", ScopeKey.Global);
        var records = new List<AuditRecord>(); long after = 0;
        while (true)
        {
            var page = await source.Audit().ReadPlatformAsync(source, after, 200, cancellationToken: Ct);
            records.AddRange(page.Select(entry => entry.Record));
            if (page.Count < 200) break;
            after = page[^1].Sequence;
        }
        return [.. records.Where(record => record.Payload.ValueKind == JsonValueKind.Object && record.Payload.TryGetProperty("permission", out var permission) &&
            permission.GetString()!.StartsWith(Permission, StringComparison.Ordinal))];
    }

    private Task<string?> Owners(Guid tenant) => Text("SELECT string_agg(a.subject_id, ',' ORDER BY a.subject_id) FROM plugin_qian_authz.role_assignments a " +
        $"JOIN plugin_qian_authz.roles r ON a.role_id = r.id WHERE a.tenant_id = '{tenant}' AND r.code = 'tenant.owner'");

    private static int Counted(JsonElement list, string status) => list.GetProperty("counts").GetProperty(status).GetInt32();

    private static (int Total, int All) Page(JsonElement list) => (list.GetProperty("total").GetInt32(), Counted(list, "all"));

    private static (string? Kind, string? Level, int? Count) Check(JsonElement check) =>
        (check.GetProperty("kind").GetString(), check.GetProperty("level").GetString(), Has(check, "count") ? check.GetProperty("count").GetInt32() : null);

    private static (string? Kind, string? Level, int? Count)[] Checks(JsonElement detail) => [.. detail.GetProperty("checks").EnumerateArray().Select(Check)];

    private static Dictionary<string, string?> Steps(JsonElement provisioning) =>
        provisioning.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("step").GetString()!, step => step.GetProperty("status").GetString());

    private static (bool Expose, bool RequiresConfirmation) Agent(JsonElement machine, string name)
    {
        var agent = machine.GetProperty("actions").EnumerateArray().Single(action => action.GetProperty("name").GetString() == name).GetProperty("agent");
        return (agent.GetProperty("expose").GetBoolean(), agent.GetProperty("requiresConfirmation").GetBoolean());
    }

    private static bool Has(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null;

    // 响应里出现的全部字符串值（不依赖序列化器是否转义非 ASCII 字符）。
    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => Strings(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        _ => []
    };
}

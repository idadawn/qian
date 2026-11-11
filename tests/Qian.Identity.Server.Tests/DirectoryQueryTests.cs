using System.Net;
using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 目录派生查询（S1-IDP-010）：上级链、直属主管、部门负责人、@下属 / @子孙与成员检索，全部走真实 PostgreSQL、受限角色与 Kestrel。
public sealed class DirectoryQueryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 组织：总部 → 研发中心 → 前端组；总部 → 销售部。岗位：总经理[张三] → 研发总监[李四]（跨组织父链）→ 前端组长[空缺] → 前端工程师；
    // 销售经理[钱七]、销售专员是销售部的平铺岗位（没有父岗位，上级要从组织的责任岗位回溯）。主管字段：总部张三、研发中心李四、销售部钱七，前端组未设。
    internal sealed record World(AccountsHost.Running Host, TenantDescriptor Tenant, IReadOnlyDictionary<string, Guid> Orgs, IReadOnlyDictionary<string, Guid> Positions,
        IReadOnlyDictionary<string, Membership> Members, IReadOnlyDictionary<string, string> Phones)
    {
        internal Guid M(string key) => Members[key].Id;
    }

    internal static async Task<World> Build(AccountsHost.Running host, string code)
    {
        var tenant = host.Tenants.Add(code);
        var members = new Dictionary<string, Membership>(); var phones = new Dictionary<string, string>();
        foreach (var (key, name) in new[] { ("zhangsan", "张三"), ("lisi", "李四"), ("wangwu", "王五"), ("zhaoliu", "赵六"), ("qianqi", "钱七"), ("sunba", "孙八"), ("zhoujiu", "周九"), ("nobody", "吴十") })
        {
            var (person, phone) = await CreatePerson(host, name);
            members[key] = await Join(host, tenant, person.Id, name, admin: key == "zhangsan", account: key); phones[key] = phone;
        }
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D")); var d = host.Service;
        foreach (var (type, label) in new[] { ("company", "公司"), ("dept", "部门"), ("team", "小组") }) await d.SetOrganizationTypeAsync(new(type, label), Ct);
        var hq = await d.CreateOrganizationAsync(new("总部", "company"), Ct);
        var rd = await d.CreateOrganizationAsync(new("研发中心", "dept", hq.Id), Ct);
        var fe = await d.CreateOrganizationAsync(new("前端组", "team", rd.Id), Ct);
        var sales = await d.CreateOrganizationAsync(new("销售部", "dept", hq.Id), Ct);
        var ceo = await d.CreatePositionAsync(new("总经理", hq.Id, IsResponsible: true, ResponsibleMembershipId: members["zhangsan"].Id), Ct);
        var director = await d.CreatePositionAsync(new("研发总监", rd.Id, ceo.Id, IsResponsible: true, ResponsibleMembershipId: members["lisi"].Id), Ct);
        var lead = await d.CreatePositionAsync(new("前端组长", fe.Id, director.Id, IsResponsible: true), Ct);
        var engineer = await d.CreatePositionAsync(new("前端工程师", fe.Id, lead.Id), Ct);
        var salesManager = await d.CreatePositionAsync(new("销售经理", sales.Id, IsResponsible: true, ResponsibleMembershipId: members["qianqi"].Id), Ct);
        var salesStaff = await d.CreatePositionAsync(new("销售专员", sales.Id), Ct);
        await d.UpdateOrganizationAsync(hq.Id, new("总部", "company", ManagerMembershipId: members["zhangsan"].Id, ResponsiblePositionId: ceo.Id), Ct);
        await d.UpdateOrganizationAsync(rd.Id, new("研发中心", "dept", hq.Id, ManagerMembershipId: members["lisi"].Id, ResponsiblePositionId: director.Id), Ct);
        await d.UpdateOrganizationAsync(fe.Id, new("前端组", "team", rd.Id, ResponsiblePositionId: lead.Id), Ct);
        await d.UpdateOrganizationAsync(sales.Id, new("销售部", "dept", hq.Id, ManagerMembershipId: members["qianqi"].Id, ResponsiblePositionId: salesManager.Id), Ct);
        await d.SetRelationsAsync(members["zhangsan"].Id, [new("organization", hq.Id, true), new("position", ceo.Id, true)], Ct);
        await d.SetRelationsAsync(members["lisi"].Id, [new("organization", rd.Id, true), new("position", director.Id, true)], Ct);
        await d.SetRelationsAsync(members["zhaoliu"].Id, [new("organization", fe.Id, true), new("position", engineer.Id, true), new("position", salesStaff.Id)], Ct);
        await d.SetRelationsAsync(members["qianqi"].Id, [new("organization", sales.Id, true), new("position", salesManager.Id, true)], Ct);
        await d.SetRelationsAsync(members["sunba"].Id, [new("organization", rd.Id, true)], Ct);
        await d.SetRelationsAsync(members["zhoujiu"].Id, [new("position", salesStaff.Id, true)], Ct);
        await d.SetRelationsAsync(members["wangwu"].Id, [new("organization", fe.Id, true)], Ct);
        return new World(host, tenant,
            new Dictionary<string, Guid> { ["hq"] = hq.Id, ["rd"] = rd.Id, ["fe"] = fe.Id, ["sales"] = sales.Id },
            new Dictionary<string, Guid> { ["ceo"] = ceo.Id, ["director"] = director.Id, ["lead"] = lead.Id, ["engineer"] = engineer.Id, ["salesManager"] = salesManager.Id, ["salesStaff"] = salesStaff.Id },
            members, phones);
    }

    private static (Guid Position, Guid Organization, Guid? Responsible, string? Vacancy)[] Levels(DirectorySuperiors chain) =>
        [.. chain.Items.Select((item, index) => { Assert.Equal(index + 1, item.Level); return (item.PositionId, item.OrganizationId, item.Responsible?.Id, item.Vacancy); })];

    [Fact]
    public async Task 上级链按岗位链返回责任人序列_空缺占位_跨组织去重_主岗位变化后随之变化()
    {
        await using var host = await AccountsHost.Start(postgres); var w = await Build(host, "superiors");
        using var scope = host.Tenant.BeginScope(w.Tenant.Id.ToString("D")); var d = host.Service;
        // 赵六的主岗位是前端工程师：前端组长（空缺）→ 研发总监（父岗位在研发中心）→ 总经理；总部的责任岗位已在链上，不重复。
        var chain = await d.GetSuperiorsAsync(w.M("zhaoliu"), cancellationToken: Ct);
        Assert.Equal((w.Positions["engineer"], w.Orgs["fe"]), (chain.StartPositionId!.Value, chain.StartOrganizationId!.Value));
        Assert.Equal([(w.Positions["lead"], w.Orgs["fe"], null, DirectoryVacancies.Unassigned), (w.Positions["director"], w.Orgs["rd"], w.M("lisi"), null),
            (w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)], Levels(chain));
        // 指定本人担任的另一个岗位：销售专员没有父岗位，从销售部的责任岗位回溯，再到总部。
        var other = await d.GetSuperiorsAsync(w.M("zhaoliu"), w.Positions["salesStaff"], Ct);
        Assert.Equal([(w.Positions["salesManager"], w.Orgs["sales"], w.M("qianqi"), null), (w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)], Levels(other));
        await Rejected("NOT_FOUND", () => d.GetSuperiorsAsync(w.M("zhaoliu"), w.Positions["ceo"], Ct).AsTask());
        await Rejected("NOT_FOUND", () => d.GetSuperiorsAsync(w.M("zhaoliu"), Guid.NewGuid(), Ct).AsTask());
        // 验收 1 后半句：主归属（主岗位）变了，结果随之变化。
        await d.SetRelationsAsync(w.M("zhaoliu"), [new("organization", w.Orgs["fe"], true), new("position", w.Positions["engineer"]), new("position", w.Positions["salesStaff"], true)], Ct);
        Assert.Equal(Levels(other), Levels(await d.GetSuperiorsAsync(w.M("zhaoliu"), cancellationToken: Ct)));
        Assert.Equal(w.Positions["salesStaff"], (await d.GetSuperiorsAsync(w.M("zhaoliu"), cancellationToken: Ct)).StartPositionId);
        // 责任人停用 → inactive 空缺且不返回其信息；恢复后照常出现。
        await d.UpdatePositionAsync(w.Positions["lead"], new("前端组长", w.Orgs["fe"], w.Positions["director"], IsResponsible: true, ResponsibleMembershipId: w.M("wangwu")), Ct);
        await d.SetDirectoryMemberStatusAsync(w.M("wangwu"), AccountStatuses.Disabled, "superiors", Ct);
        var inactive = await d.GetSuperiorsAsync(w.M("zhaoliu"), w.Positions["engineer"], Ct);
        Assert.Equal((w.Positions["lead"], (Guid?)null, DirectoryVacancies.Inactive), (inactive.Items[0].PositionId, inactive.Items[0].Responsible?.Id, inactive.Items[0].Vacancy));
        await d.SetDirectoryMemberStatusAsync(w.M("wangwu"), AccountStatuses.Active, "superiors", Ct);
        Assert.Equal(w.M("wangwu"), (await d.GetSuperiorsAsync(w.M("zhaoliu"), w.Positions["engineer"], Ct)).Items[0].Responsible!.Id);
        // 没有岗位、只有主组织：从主组织的责任岗位起算。没有任何归属：空序列。
        var direct = await d.GetSuperiorsAsync(w.M("sunba"), cancellationToken: Ct);
        Assert.Equal(((Guid?)null, (Guid?)w.Orgs["rd"]), (direct.StartPositionId, direct.StartOrganizationId));
        Assert.Equal([(w.Positions["director"], w.Orgs["rd"], w.M("lisi"), null), (w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)], Levels(direct));
        var empty = await d.GetSuperiorsAsync(w.M("nobody"), cancellationToken: Ct);
        Assert.Equal(((Guid?)null, (Guid?)null, 0), (empty.StartPositionId, empty.StartOrganizationId, empty.Items.Count));
        // 本人担任责任岗位：链从父岗位起，不含本人那一级；平铺岗位回溯时跳过本组织（自己就是那个责任岗位）。
        Assert.Equal([(w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)], Levels(await d.GetSuperiorsAsync(w.M("lisi"), cancellationToken: Ct)));
        Assert.Equal([(w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)], Levels(await d.GetSuperiorsAsync(w.M("qianqi"), cancellationToken: Ct)));
        Assert.Empty((await d.GetSuperiorsAsync(w.M("zhangsan"), cancellationToken: Ct)).Items);
        await Rejected("NOT_FOUND", () => d.GetSuperiorsAsync(Guid.NewGuid(), cancellationToken: Ct).AsTask());
        // 父链上的非责任岗位不算一级：销售实习生 → 销售专员（非责任）→ 到顶，从销售部的责任岗位回溯。
        var intern = await d.CreatePositionAsync(new("销售实习生", w.Orgs["sales"], w.Positions["salesStaff"]), Ct);
        await d.SetRelationsAsync(w.M("nobody"), [new("position", intern.Id, true)], Ct);
        Assert.Equal([(w.Positions["salesManager"], w.Orgs["sales"], w.M("qianqi"), null), (w.Positions["ceo"], w.Orgs["hq"], w.M("zhangsan"), null)],
            Levels(await d.GetSuperiorsAsync(w.M("nobody"), cancellationToken: Ct)));
    }

    [Fact]
    public async Task 直属主管逐级上溯_部门负责人_下属与子孙是上级链的反查()
    {
        await using var host = await AccountsHost.Start(postgres); var w = await Build(host, "managers");
        using var scope = host.Tenant.BeginScope(w.Tenant.Id.ToString("D")); var d = host.Service;
        // 前端组未设主管 → 研发中心的李四；李四本人是研发中心主管 → 总部的张三；张三在顶层 → 没有；只有主岗位的周九 → 销售部（岗位所属组织）的钱七。
        Assert.Equal((w.M("lisi"), w.Orgs["rd"]), Manager(await d.GetManagerAsync(w.M("zhaoliu"), Ct)));
        Assert.Equal((w.M("zhangsan"), w.Orgs["hq"]), Manager(await d.GetManagerAsync(w.M("lisi"), Ct)));
        Assert.Equal(((Guid?)null, (Guid?)null), ManagerOrNone(await d.GetManagerAsync(w.M("zhangsan"), Ct)));
        Assert.Equal((w.M("qianqi"), w.Orgs["sales"]), Manager(await d.GetManagerAsync(w.M("zhoujiu"), Ct)));
        Assert.Equal(((Guid?)null, (Guid?)null), ManagerOrNone(await d.GetManagerAsync(w.M("nobody"), Ct)));
        // 部门负责人：责任岗位的现任责任人与主管字段；空缺给原因；没设责任岗位时两项都没有。
        var rd = await d.GetOrganizationHeadAsync(w.Orgs["rd"], Ct);
        Assert.Equal((w.Positions["director"], w.M("lisi"), (string?)null, w.M("lisi")), (rd.ResponsiblePositionId!.Value, rd.Responsible!.Id, rd.Vacancy, rd.Manager!.Id));
        var fe = await d.GetOrganizationHeadAsync(w.Orgs["fe"], Ct);
        Assert.Equal((w.Positions["lead"], (Guid?)null, DirectoryVacancies.Unassigned, (Guid?)null), (fe.ResponsiblePositionId!.Value, fe.Responsible?.Id, fe.Vacancy, fe.Manager?.Id));
        var team = await d.CreateOrganizationAsync(new("测试组", "team", w.Orgs["rd"]), Ct);
        var bare = await d.GetOrganizationHeadAsync(team.Id, Ct);
        Assert.Equal(((Guid?)null, (Guid?)null, (string?)null, (Guid?)null), (bare.ResponsiblePositionId, bare.Responsible?.Id, bare.Vacancy, bare.Manager?.Id));
        await Rejected("NOT_FOUND", () => d.GetOrganizationHeadAsync(Guid.NewGuid(), Ct).AsTask());
        // @下属 / @子孙：张三在李四、钱七的第 1 级，孙八、周九的第 2 级，赵六与王五（直属前端组、没有岗位）的第 3 级；不含本人。
        Assert.Equal(Ids(w, "lisi", "qianqi"), Ids(await d.GetSubordinatesAsync(w.M("zhangsan"), 1, cancellationToken: Ct)));
        Assert.Equal(Ids(w, "lisi", "qianqi", "sunba", "zhoujiu"), Ids(await d.GetSubordinatesAsync(w.M("zhangsan"), 2, cancellationToken: Ct)));
        Assert.Equal(Ids(w, "lisi", "qianqi", "sunba", "zhoujiu", "zhaoliu", "wangwu"), Ids(await d.GetSubordinatesAsync(w.M("zhangsan"), cancellationToken: Ct)));
        // 赵六、王五的第 1 级是空缺的前端组长，所以只在李四的子孙里，不在李四的直属下属里。
        Assert.Equal(Ids(w, "sunba"), Ids(await d.GetSubordinatesAsync(w.M("lisi"), 1, cancellationToken: Ct)));
        Assert.Equal(Ids(w, "sunba", "zhaoliu", "wangwu"), Ids(await d.GetSubordinatesAsync(w.M("lisi"), cancellationToken: Ct)));
        Assert.Empty(await d.GetSubordinatesAsync(w.M("zhaoliu"), cancellationToken: Ct));
        // 按账号分页：lisi、qianqi、sunba、zhoujiu 的第 4 个。
        Assert.Equal(Ids(w, "zhoujiu"), Ids(await d.GetSubordinatesAsync(w.M("zhangsan"), 2, offset: 3, limit: 1, cancellationToken: Ct)));
        await Rejected("INPUT_INVALID", () => d.GetSubordinatesAsync(w.M("zhangsan"), 0, cancellationToken: Ct).AsTask());
        // 主管或责任人停用后视同未设：赵六的主管越过李四到张三；李四不再有下属。
        await d.SetDirectoryMemberStatusAsync(w.M("lisi"), AccountStatuses.Disabled, "managers", Ct);
        Assert.Equal((w.M("zhangsan"), w.Orgs["hq"]), Manager(await d.GetManagerAsync(w.M("zhaoliu"), Ct)));
        Assert.Null((await d.GetOrganizationHeadAsync(w.Orgs["rd"], Ct)).Manager);
        Assert.Equal(DirectoryVacancies.Inactive, (await d.GetOrganizationHeadAsync(w.Orgs["rd"], Ct)).Vacancy);
        Assert.Empty(await d.GetSubordinatesAsync(w.M("lisi"), cancellationToken: Ct));
    }

    private static (Guid, Guid) Manager(DirectoryManager result) => (result.Manager!.Id, result.SourceOrganizationId!.Value);
    private static (Guid?, Guid?) ManagerOrNone(DirectoryManager result) => (result.Manager?.Id, result.SourceOrganizationId);
    private static Guid[] Ids(World w, params string[] keys) => [.. keys.Select(w.M).Order()];
    private static Guid[] Ids(IReadOnlyList<DirectoryMember> members) => [.. members.Select(member => member.Membership.Id).Order()];

    [Fact]
    public async Task HTTP派生查询走真实会话_字段为snake_case_跨租户与平台会话不可见()
    {
        await using var host = await AccountsHost.Start(postgres); var w = await Build(host, "derived-http"); var b = host.Tenants.Add("derived-other");
        var (outsider, outsiderPhone) = await CreatePerson(host); await Join(host, b, outsider.Id, admin: true);
        var (platform, platformPhone) = await CreatePerson(host, platform: true);
        var member = host.NewBrowser(); await member.Login(w.Phones["zhaoliu"], Password, tenant: w.Tenant.Id);
        var chain = await member.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors");
        Assert.Equal(HttpStatusCode.OK, chain.Status); Assert.Contains("no-store", chain.Raw.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal(w.Positions["engineer"], chain.Body.GetProperty("start_position_id").GetGuid());
        var levels = chain.Body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal((1, "unassigned", false), (levels[0].GetProperty("level").GetInt32(), levels[0].GetProperty("vacancy").GetString(), levels[0].TryGetProperty("responsible", out _)));
        Assert.Equal(("李四", "lisi"), (levels[1].GetProperty("responsible").GetProperty("display_name").GetString(), levels[1].GetProperty("responsible").GetProperty("account").GetString()));
        Assert.False(levels[1].TryGetProperty("vacancy", out _));
        var picked = await member.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors?position_id={w.Positions["salesStaff"]:D}");
        Assert.Equal(w.Positions["salesManager"], picked.Body.GetProperty("items")[0].GetProperty("position_id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await member.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors?position_id=not-a-uuid")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors?position_id={w.Positions["ceo"]:D}")).Status);
        var manager = await member.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/manager");
        Assert.Equal((w.M("lisi"), w.Orgs["rd"]), (manager.Body.GetProperty("manager").GetProperty("id").GetGuid(), manager.Body.GetProperty("source_organization_id").GetGuid()));
        var head = await member.Json(HttpMethod.Get, $"api/directory/orgs/{w.Orgs["fe"]:D}/head");
        Assert.Equal(("unassigned", w.Positions["lead"]), (head.Body.GetProperty("vacancy").GetString(), head.Body.GetProperty("responsible_position_id").GetGuid()));
        var boss = host.NewBrowser(); await boss.Login(w.Phones["zhangsan"], Password, tenant: w.Tenant.Id);
        var direct = await boss.Json(HttpMethod.Get, "api/directory/me/subordinates?depth=1");
        Assert.Equal(Ids(w, "lisi", "qianqi"), direct.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("membership").GetProperty("id").GetGuid()).Order().ToArray());
        Assert.Equal(6, (await boss.Json(HttpMethod.Get, "api/directory/me/subordinates")).Body.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await boss.Json(HttpMethod.Get, "api/directory/me/subordinates?depth=0")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await boss.Json(HttpMethod.Get, "api/directory/me/subordinates?depth=x")).Status);
        // 其他租户的会话按本租户口径查：成员、组织都不存在；平台会话没有租户成员身份，一律 403；未登录 401。
        var stranger = host.NewBrowser(); await stranger.Login(outsiderPhone, Password, tenant: b.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/manager")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Json(HttpMethod.Get, $"api/directory/orgs/{w.Orgs["fe"]:D}/head")).Status);
        Assert.Empty((await stranger.Json(HttpMethod.Get, "api/directory/me/subordinates")).Body.GetProperty("items").EnumerateArray());
        var operations = host.NewBrowser(); await operations.Login(platformPhone, Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await operations.Json(HttpMethod.Get, "api/directory/me/subordinates")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await operations.Json(HttpMethod.Get, $"api/directory/members/{w.M("zhaoliu"):D}/superiors")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Json(HttpMethod.Get, $"api/directory/orgs/{w.Orgs["fe"]:D}/head")).Status);
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE person_id=@person", ("person", platform.Id)));
    }

    [Fact]
    public async Task 成员关键字检索显示名账号拼音_不搜联系方式_通配符按字面_按id批量只回本租户可见成员()
    {
        await using var host = await AccountsHost.Start(postgres); var w = await Build(host, "member-search"); var other = host.Tenants.Add("member-search-other");
        var (stranger, _) = await CreatePerson(host, "张外"); var outside = await Join(host, other, stranger.Id, "张外", account: "zhangwai");
        using (host.Tenant.BeginScope(w.Tenant.Id.ToString("D")))
            await host.Service.SetProfileAsync(w.M("sunba"), new("孙八", "sunba-pinyin", "13812345678", "sunba@example.test"), Ct);
        var browser = host.NewBrowser(); await browser.Login(w.Phones["zhaoliu"], Password, tenant: w.Tenant.Id);
        async Task<Guid[]> Find(string query)
        {
            var response = await browser.Json(HttpMethod.Get, "api/directory/" + query);
            Assert.True(response.Status == HttpStatusCode.OK, query + " → " + response.Status);
            return [.. response.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("membership").GetProperty("id").GetGuid())];
        }
        Assert.Equal([w.M("zhangsan")], await Find("members?q=" + Uri.EscapeDataString(" 张 ")));
        Assert.Equal([w.M("zhangsan")], await Find("members?q=ZHANG"));
        Assert.Equal([w.M("sunba")], await Find("members?q=PINYIN"));
        Assert.Empty(await Find("members?q=1381234"));
        Assert.Empty(await Find("members?q=example.test"));
        Assert.Empty(await Find("members?q=%25"));
        Assert.Empty(await Find("members?q=_"));
        // 按账号、成员 id 排序后分页；组织范围与关键字可同时用。
        Assert.Equal([w.M("lisi"), w.M("qianqi")], await Find("members?q=i&limit=2"));
        Assert.Equal([w.M("wangwu")], await Find($"orgs/{w.Orgs["fe"]:D}/members?q=" + Uri.EscapeDataString("王")));
        Assert.Equal([w.M("zhaoliu")], await Find($"orgs/{w.Orgs["rd"]:D}/members?descendants=true&q=zhao"));
        Assert.Empty(await Find($"orgs/{w.Orgs["rd"]:D}/members?q=zhao"));
        // 按 id 批量：本租户成员按账号排序返回；别的租户、不存在、已从目录删除的 id 直接省略。
        using (host.Tenant.BeginScope(w.Tenant.Id.ToString("D"))) await host.Service.DeleteMemberAsync(w.M("nobody"), "search", Ct);
        Assert.Equal([w.M("lisi"), w.M("zhaoliu")], await Find($"members?ids={w.M("zhaoliu"):D}&ids={outside.Id:D}&ids={Guid.NewGuid():D}&ids={w.M("nobody"):D}&ids={w.M("lisi"):D}"));
        Assert.Equal([w.M("qianqi")], await Find($"members?person_ids={w.Members["qianqi"].PersonId:D}&person_ids={stranger.Id:D}"));
        // 参数校验：条件互斥、数量与重复、格式、长度、单值参数重复。
        foreach (var bad in new[] { $"members?q=zhang&ids={w.M("lisi"):D}", $"members?ids={w.M("lisi"):D}&person_ids={w.Members["lisi"].PersonId:D}", $"members?ids={w.M("lisi"):D}&ids={w.M("lisi"):D}",
            "members?" + string.Join('&', Enumerable.Range(0, 101).Select(_ => $"ids={Guid.NewGuid():D}")), "members?q=" + Uri.EscapeDataString("   "), "members?q=" + new string('长', 65),
            $"orgs/{w.Orgs["fe"]:D}/members?q=" })
            Assert.Equal((HttpStatusCode.BadRequest, "INPUT_INVALID"), await Status(browser, bad));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_REQUEST"), await Status(browser, "members?ids=not-a-uuid"));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_REQUEST"), await Status(browser, "members?q=a&q=b"));
        Assert.Empty(await Find("members?q=" + new string('长', 64)));
    }

    // Agent 契约（S1-IDP-010 验收 4）的真实响应侧：契约里登记的每个 GET 端点都用真实会话调到，不带参数调一次、每个查询参数各单独调一次，
    // 响应都必须通过契约 Schema 的严格校验（根与嵌套对象都不许多字段）；错误响应符合 ErrorDetail。
    [Fact]
    public async Task 目录查询的真实响应符合Agent契约Schema_契约里的每个GET端点与查询参数都调到()
    {
        await using var host = await AccountsHost.Start(postgres); var w = await Build(host, "directory-contract");
        using (host.Tenant.BeginScope(w.Tenant.Id.ToString("D")))
        {
            await host.Service.SetProfileAsync(w.M("zhaoliu"), new("赵六", "zhaoliu", "13812345678", "zhaoliu@example.test", JsonSerializer.SerializeToElement(new { locale = "zh-CN" })), Ct);
            _ = await host.Service.CreateGroupAsync("项目组", Ct);
        }
        var admin = host.NewBrowser(); await admin.Login(w.Phones["zhangsan"], Password, tenant: w.Tenant.Id);
        var contract = DirectoryAgentContract.Load();
        var subjects = new Dictionary<string, Guid> { ["members"] = w.M("zhaoliu"), ["orgs"] = w.Orgs["rd"], ["positions"] = w.Positions["engineer"] };
        var queries = new Dictionary<string, string>
        {
            ["q"] = "q=zhao", ["ids"] = $"ids={w.M("zhaoliu"):D}&ids={w.M("lisi"):D}", ["person_ids"] = $"person_ids={w.Members["zhaoliu"].PersonId:D}", ["descendants"] = "descendants=true",
            ["offset"] = "offset=1", ["limit"] = "limit=2", ["position_id"] = $"position_id={w.Positions["salesStaff"]:D}", ["depth"] = "depth=2",
        };
        var exercised = 0;
        foreach (var (path, _, operation) in contract.Operations())
        {
            var concrete = path.Contains("{id}", StringComparison.Ordinal) ? path.Replace("{id}", subjects[path.Split('/')[3]].ToString("D"), StringComparison.Ordinal) : path;
            var schema = contract.ResponseSchema(operation, "200")!;
            var variants = contract.Parameters(operation).Where(parameter => parameter["in"]!.GetValue<string>() == "query").Select(parameter => queries[parameter["name"]!.GetValue<string>()]).Prepend("");
            foreach (var query in variants)
            {
                var url = concrete.TrimStart('/') + (query.Length == 0 ? "" : "?" + query);
                var response = await admin.Json(HttpMethod.Get, url);
                Assert.True(response.Status == HttpStatusCode.OK, url + " → " + response.Status);
                Assert.True(contract.Validate(schema, response.Body) is [], url + " 不符合契约：" + string.Join(" | ", contract.Validate(schema, response.Body)) + " 响应：" + response.Body.GetRawText());
                // 不带参数的那次必须有数据，免得空列表让校验空转。
                if (query.Length == 0 && response.Body.TryGetProperty("items", out var items)) Assert.True(items.GetArrayLength() > 0, url + " 没有数据");
            }
            exercised++;
        }
        Assert.Equal(13, exercised);
        // 错误响应：参数无效 400、不存在 404、未登录 401、平台会话 403，都符合契约里的 ErrorDetail。
        var (_, platformPhone) = await CreatePerson(host, platform: true);
        var operations = host.NewBrowser(); await operations.Login(platformPhone, Password);
        foreach (var (browser, url, status, template) in new[]
        {
            (admin, "api/directory/members?ids=not-a-uuid", HttpStatusCode.BadRequest, "/api/directory/members"),
            (admin, $"api/directory/members/{Guid.NewGuid():D}/superiors", HttpStatusCode.NotFound, "/api/directory/members/{id}/superiors"),
            (host.NewBrowser(), $"api/directory/orgs/{w.Orgs["fe"]:D}/head", HttpStatusCode.Unauthorized, "/api/directory/orgs/{id}/head"),
            (operations, "api/directory/me/subordinates", HttpStatusCode.Forbidden, "/api/directory/me/subordinates"),
        })
        {
            var response = await browser.Json(HttpMethod.Get, url);
            Assert.Equal(status, response.Status);
            var problem = contract.ResponseSchema(contract.Document["paths"]![template]!["get"]!.AsObject(), ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), "application/problem+json")!;
            Assert.True(contract.Validate(problem, response.Body) is [], url + "：" + string.Join(" | ", contract.Validate(problem, response.Body)));
        }
    }

    private static async Task<(HttpStatusCode, string)> Status(AccountsHost.Browser browser, string query)
    {
        var response = await browser.Json(HttpMethod.Get, "api/directory/" + query);
        return (response.Status, response.Body.ValueKind == JsonValueKind.Object ? response.Body.GetProperty("code").GetString()! : "");
    }
}

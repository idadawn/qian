using System.Net;
using System.Text.Json;
using Npgsql;
using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

public sealed class DirectoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Code(JsonElement body) => body.GetProperty("code").GetString()!;

    [Fact]
    public async Task 组织移动更新全部后代_循环深度与第二根拒绝_含成员子树结构化拒删()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("directory"); var (person, phone) = await CreatePerson(host); var member = await Join(host, tenant, person.Id, admin: true);
        var browser = host.NewBrowser(); await browser.Login(phone, Password, tenant: tenant.Id);
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D")); var directory = host.Service;
        await directory.SetOrganizationTypeAsync(new("company", "公司"), Ct); await directory.SetOrganizationTypeAsync(new("dept", "部门"), Ct);
        var root = await directory.CreateOrganizationAsync(new("总部", "company"), Ct);
        var a = await directory.CreateOrganizationAsync(new("甲", "dept", root.Id), Ct);
        var b = await directory.CreateOrganizationAsync(new("乙", "dept", root.Id), Ct);
        var child = await directory.CreateOrganizationAsync(new("甲的子部门", "dept", a.Id), Ct);
        await directory.SetRelationsAsync(member.Id, [new("organization", child.Id, true)], Ct);
        var deleted = await browser.Json(HttpMethod.Delete, $"api/directory/orgs/{a.Id:D}");
        Assert.Equal(HttpStatusCode.Conflict, deleted.Status); Assert.Equal("DIRECTORY_HAS_MEMBERS", Code(deleted.Body));
        Assert.Contains("no-store", deleted.Raw.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var moved = await directory.UpdateOrganizationAsync(a.Id, new("甲", "dept", b.Id), Ct);
        var after = await directory.GetOrganizationTreeAsync(Ct); var movedChild = Assert.Single(after, row => row.Id == child.Id);
        Assert.Equal((root.Path + $"{b.Id:D}/{a.Id:D}/", 3), (moved.Path, moved.Depth));
        Assert.Equal((moved.Path + $"{child.Id:D}/", 4), (movedChild.Path, movedChild.Depth));
        Assert.Equal(movedChild.Path, await host.Text("SELECT path FROM plugin_qian_identity.organizations WHERE id=@id", ("id", child.Id)));
        await Rejected("DIRECTORY_CYCLE", () => directory.UpdateOrganizationAsync(b.Id, new("乙", "dept", child.Id), Ct).AsTask());
        await Rejected("CONFLICT", () => directory.CreateOrganizationAsync(new("第二根", "company"), Ct).AsTask());
        var last = child;
        for (var depth = 5; depth <= DirectoryRules.MaximumDepth; depth++) last = await directory.CreateOrganizationAsync(new("层" + depth, "dept", last.Id), Ct);
        await Rejected("DIRECTORY_DEPTH_EXCEEDED", () => directory.CreateOrganizationAsync(new("超深", "dept", last.Id), Ct).AsTask());
        await directory.SetRelationsAsync(member.Id, [], Ct);
        await directory.DeleteOrganizationAsync(a.Id, Ct);
        Assert.Equal(2, (await directory.GetOrganizationTreeAsync(Ct)).Count);
        await Rejected("CONFLICT", () => directory.DeleteOrganizationTypeAsync("dept", Ct).AsTask());
    }

    [Fact]
    public async Task 类型字典隔离_岗位树默认责任岗位_多归属主归属与档案真实持久化()
    {
        await using var host = await AccountsHost.Start(postgres); var a = host.Tenants.Add("a"); var b = host.Tenants.Add("b");
        var (person, _) = await CreatePerson(host); var member = await Join(host, a, person.Id); var other = await Join(host, b, person.Id);
        var directory = host.Service; Guid rootId;
        using (host.Tenant.BeginScope(a.Id.ToString("D")))
        {
            await directory.SetOrganizationTypeAsync(new("custom", "租户甲自定义类型"), Ct);
            var root = await directory.CreateOrganizationAsync(new("甲总部", "custom", ManagerMembershipId: member.Id), Ct); rootId = root.Id;
            var position = await directory.CreatePositionAsync(new("负责人", root.Id, IsDefault: true, IsResponsible: true, ResponsibleMembershipId: member.Id), Ct);
            var worker = await directory.CreatePositionAsync(new("职员", root.Id, position.Id), Ct);
            var trainee = await directory.CreatePositionAsync(new("实习", root.Id, worker.Id), Ct);
            var another = await directory.CreatePositionAsync(new("经理", root.Id), Ct);
            await directory.UpdatePositionAsync(worker.Id, new("职员", root.Id, another.Id), Ct);
            Assert.Equal($"/{another.Id:D}/{worker.Id:D}/{trainee.Id:D}/", Assert.Single(await directory.ListPositionsAsync(Ct), row => row.Id == trainee.Id).Path);
            await Rejected("DIRECTORY_CYCLE", () => directory.UpdatePositionAsync(another.Id, new("经理", root.Id, trainee.Id), Ct).AsTask());
            await Rejected("CONFLICT", () => directory.CreatePositionAsync(new("另一个默认", root.Id, IsDefault: true), Ct).AsTask());
            await directory.UpdateOrganizationAsync(root.Id, new("甲总部", "custom", ManagerMembershipId: member.Id, ResponsiblePositionId: position.Id), Ct);
            var group = await directory.CreateGroupAsync("项目组", Ct);
            await directory.SetRelationsAsync(member.Id, [new("organization", root.Id, true), new("position", worker.Id, true), new("position", position.Id), new("group", group.Id, true)], Ct);
            Assert.Equal(4, (await directory.GetRelationsAsync(member.Id, Ct)).Count);
            await Rejected("INPUT_INVALID", () => directory.SetRelationsAsync(member.Id, [new("position", worker.Id, true), new("position", position.Id, true)], Ct).AsTask());
            Assert.Equal(member.Id, Assert.Single(await directory.GetPositionHoldersAsync(worker.Id, cancellationToken: Ct)).Membership.Id);
            await Rejected("DIRECTORY_HAS_MEMBERS", () => directory.DeletePositionAsync(worker.Id, Ct).AsTask());
            await Rejected("DIRECTORY_HAS_MEMBERS", () => directory.DeleteGroupAsync(group.Id, Ct).AsTask());
            var profile = await directory.SetProfileAsync(member.Id, new("职员甲", "zhiyuanjia", "13812345678", "employee@example.test", JsonSerializer.SerializeToElement(new { locale = "zh-CN" })), Ct);
            Assert.Equal("zhiyuanjia", profile.Pinyin); Assert.Contains("*", profile.Phone!, StringComparison.Ordinal); Assert.Contains("*", profile.Email!, StringComparison.Ordinal);
            Assert.Equal("zh-CN", (await directory.GetProfileAsync(member.Id, Ct))!.Extensions.GetProperty("locale").GetString());
            Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.user_profiles WHERE membership_id=@id", ("id", member.Id)));
            await directory.SetProfileAsync(member.Id, new("更新姓名"), Ct);
            Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.user_profiles WHERE membership_id=@id", ("id", member.Id)));
            await Rejected("NOT_FOUND", () => directory.SetRelationsAsync(other.Id, [new("organization", root.Id)], Ct).AsTask());
        }
        using (host.Tenant.BeginScope(b.Id.ToString("D")))
        {
            Assert.Empty(await directory.ListOrganizationTypesAsync(Ct));
            await directory.SetOrganizationTypeAsync(new("custom", "租户乙同码不同名"), Ct);
            Assert.Equal("租户乙同码不同名", Assert.Single(await directory.ListOrganizationTypesAsync(Ct)).Name);
            await Rejected("NOT_FOUND", () => directory.GetOrganizationMembersAsync(rootId, cancellationToken: Ct).AsTask());
            await Rejected("NOT_FOUND", () => directory.SetRelationsAsync(other.Id, [new("organization", rootId)], Ct).AsTask());
            Assert.Null(await directory.GetProfileAsync(other.Id, Ct));
        }
        // 真实受限应用角色直接 SQL：A 看不到 B 的字典；tenant 复合外键拒绝把 A 的关系指向 B 成员。
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString); await connection.OpenAsync(Ct);
        await using var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection); set.Parameters.AddWithValue("tenant", a.Id.ToString("D")); await set.ExecuteNonQueryAsync(Ct);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_identity.org_types", connection); Assert.Equal(1L, await count.ExecuteScalarAsync(Ct));
        await using var cross = new NpgsqlCommand("INSERT INTO plugin_qian_identity.user_profiles(tenant_id,membership_id,name,extensions) VALUES(@tenant,@member,'越界','{}')", connection);
        cross.Parameters.AddWithValue("tenant", a.Id.ToString("D")); cross.Parameters.AddWithValue("member", other.Id);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync(Ct))).SqlState);
        await using var forge = new NpgsqlCommand("INSERT INTO plugin_qian_identity.org_types(tenant_id,code,name) VALUES(@tenant,'forge','越界')", connection); forge.Parameters.AddWithValue("tenant", b.Id.ToString("D"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => forge.ExecuteNonQueryAsync(Ct))).SqlState);
    }

    [Fact]
    public async Task 批量十人保护两名管理员_三态回执_禁用即时撤销租户会话_删除仅禁用标记()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("batch"); var other = host.Tenants.Add("other");
        var members = new List<Membership>(); var phones = new List<string>();
        for (var i = 0; i < 10; i++) { var (person, phone) = await CreatePerson(host); phones.Add(phone); members.Add(await Join(host, tenant, person.Id, admin: i < 2)); }
        var extra = await Join(host, other, members[2].PersonId);
        var admin = host.NewBrowser(); await admin.Login(phones[0], Password, tenant: tenant.Id);
        var user = host.NewBrowser(); await user.Login(phones[2], Password, tenant: tenant.Id);
        var inOther = host.NewBrowser(); await inOther.Login(phones[2], Password, tenant: other.Id);
        await SessionSupport.StopProcessor(host);
        var response = await admin.Json(HttpMethod.Post, "api/directory/members:disable", new { membership_ids = members.Select(member => member.Id).ToArray(), reason = "directory_batch" });
        Assert.Equal(HttpStatusCode.OK, response.Status); Assert.Equal("PARTIAL_SUCCESS", response.Body.GetProperty("status").GetString());
        var items = response.Body.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(10, items.Length);
        Assert.Equal(8, items.Count(item => item.GetProperty("succeeded").GetBoolean()));
        Assert.All(items.Take(2), item => Assert.Equal("DIRECTORY_ADMINISTRATOR_PROTECTED", item.GetProperty("error_code").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Json(HttpMethod.Get, "api/directory/members")).Status);
        Assert.Equal(HttpStatusCode.OK, (await inOther.Json(HttpMethod.Get, "api/directory/members")).Status);
        await host.Service.ProcessRevocationsAsync(100, Ct);
        var revoked = Assert.Single(await SessionSupport.EventsOf(host, "identity.session_revoked.v1"));
        Assert.Equal((tenant.Id, members[0].PersonId.ToString("D"), "member_disabled"), (revoked.TenantId, revoked.Actor.Id, revoked.Payload.GetProperty("reason").GetString()));
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D")); var directory = host.Service;
        Assert.Equal("FAILED", (await directory.DisableMembersAsync(members.Take(2).Select(row => row.Id).ToArray(), "protected", Ct)).Status);
        Assert.Equal("SUCCESS", (await directory.DisableMembersAsync(members.Skip(2).Select(row => row.Id).ToArray(), "idempotent", Ct)).Status);
        await directory.SetDirectoryMemberStatusAsync(members[2].Id, AccountStatuses.Active, "reactivate", Ct);
        await Rejected("DIRECTORY_TRANSITION_INVALID", () => directory.SetDirectoryMemberStatusAsync(members[2].Id, AccountStatuses.Locked, "invalid", Ct).AsTask());
        await directory.SetDirectoryMemberStatusAsync(members[2].Id, AccountStatuses.Disabled, "disable", Ct);
        await directory.SetDirectoryMemberStatusAsync(members[2].Id, AccountStatuses.Locked, "lock", Ct);
        await Rejected("DIRECTORY_TRANSITION_INVALID", () => directory.SetDirectoryMemberStatusAsync(members[2].Id, AccountStatuses.Active, "invalid", Ct).AsTask());
        await host.Accounts.SetMembershipStatusAsync(members[2].Id, AccountStatuses.Disabled, "trusted_unlock", Ct);
        await directory.DeleteMemberAsync(members[2].Id, "remove", Ct);
        Assert.DoesNotContain(await directory.ListMembersAsync(cancellationToken: Ct), row => row.Membership.Id == members[2].Id);
        Assert.Equal("disabled:true", await host.Text("SELECT status || ':' || directory_deleted::text FROM plugin_qian_identity.memberships WHERE id=@id", ("id", members[2].Id)));
        await Rejected("NOT_FOUND", () => host.Accounts.SetMembershipStatusAsync(members[2].Id, AccountStatuses.Active, "no_revive", Ct).AsTask());
        Assert.Equal(1, int.Parse(await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE id=@id", ("id", extra.Id)), System.Globalization.CultureInfo.InvariantCulture));
        await Rejected("DIRECTORY_ADMINISTRATOR_PROTECTED", () => directory.DeleteMemberAsync(members[0].Id, "protected", Ct).AsTask());
    }

    [Fact]
    public async Task HTTP只接受有效租户会话_超管无Membership不进目录_跨租户不可见_写入防伪且卸载404()
    {
        await using var host = await AccountsHost.Start(postgres); var a = host.Tenants.Add("a"); var b = host.Tenants.Add("b");
        var (platform, platformPhone) = await CreatePerson(host, platform: true); var (admin, adminPhone) = await CreatePerson(host); var (user, phone) = await CreatePerson(host); var (other, otherPhone) = await CreatePerson(host);
        var administrator = await Join(host, a, admin.Id, admin: true); var member = await Join(host, a, user.Id); await Join(host, b, other.Id, admin: true);
        var browser = host.NewBrowser(); await browser.Login(adminPhone, Password, tenant: a.Id); var ordinary = host.NewBrowser(); await ordinary.Login(phone, Password, tenant: a.Id);
        var platformBrowser = host.NewBrowser(); await platformBrowser.Login(platformPhone, Password); var otherBrowser = host.NewBrowser(); await otherBrowser.Login(otherPhone, Password, tenant: b.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Json(HttpMethod.Get, "api/directory/orgs/tree")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await platformBrowser.Json(HttpMethod.Get, $"api/directory/members?tenant_id={a.Id:D}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.Json(HttpMethod.Put, "api/directory/org-types", new { code = "dept", name = "部门" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Json(HttpMethod.Put, "api/directory/org-types", new { code = "dept", name = "部门" }, csrf: false)).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.Json(HttpMethod.Put, "api/directory/org-types", new { code = "dept", name = "部门" })).Status);
        var created = await browser.Json(HttpMethod.Post, "api/directory/orgs", new { name = "总部", type_code = "dept" }); Assert.Equal(HttpStatusCode.OK, created.Status); var root = created.Body.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Json(HttpMethod.Post, "api/directory/orgs", new { name = "越权", type_code = "dept", tenant_id = b.Id })).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.Json(HttpMethod.Put, $"api/directory/members/{member.Id:D}/relations", new { items = new[] { new { target_type = "organization", target_id = root, is_primary = true } } })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Json(HttpMethod.Put, $"api/directory/members/{member.Id:D}/relations", new { })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Json(HttpMethod.Post, "api/directory/members:disable", new { reason = "missing_ids" })).Status);
        var list = await ordinary.Json(HttpMethod.Get, "api/directory/members");
        Assert.Equal(new[] { administrator.Id, member.Id }.Order(), list.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("membership").GetProperty("id").GetGuid()).Order());
        var inOrg = await ordinary.Json(HttpMethod.Get, $"api/directory/orgs/{root:D}/members");
        Assert.Equal(member.Id, Assert.Single(inOrg.Body.GetProperty("items").EnumerateArray()).GetProperty("membership").GetProperty("id").GetGuid());
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.memberships WHERE person_id=@person", ("person", platform.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await otherBrowser.Json(HttpMethod.Get, $"api/directory/orgs/{root:D}/members")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Json(HttpMethod.Post, $"api/directory/members/{member.Id:D}/purge")).Status);
        await host.Loader.UnmountAsync("identity", Qian.Runtime.ScopeKey.Global, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Http.GetAsync("api/directory/orgs/tree", Ct)).StatusCode);
    }


    [Fact]
    public async Task 岗位责任链可跨组织_组织删除不误删外部岗位_移动同步路径但不隐改后代组织()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("position-chain");
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D")); var directory = host.Service;
        await directory.SetOrganizationTypeAsync(new("dept", "部门"), Ct);
        var root = await directory.CreateOrganizationAsync(new("总部", "dept"), Ct);
        var department = await directory.CreateOrganizationAsync(new("部门", "dept", root.Id), Ct);
        var boss = await directory.CreatePositionAsync(new("总部负责人", root.Id, IsResponsible: true), Ct);
        var manager = await directory.CreatePositionAsync(new("部门负责人", department.Id, boss.Id, IsResponsible: true), Ct);
        var staff = await directory.CreatePositionAsync(new("职员", department.Id, manager.Id), Ct);
        Assert.Equal($"/{boss.Id:D}/{manager.Id:D}/{staff.Id:D}/", staff.Path);
        await directory.UpdateOrganizationAsync(department.Id, new("部门", "dept", root.Id, ResponsiblePositionId: manager.Id), Ct);
        await Rejected("CONFLICT", () => directory.UpdatePositionAsync(manager.Id, new("迁出", root.Id, boss.Id, IsResponsible: true), Ct).AsTask());
        await directory.UpdateOrganizationAsync(department.Id, new("部门", "dept", root.Id), Ct);
        await directory.UpdatePositionAsync(manager.Id, new("迁出", root.Id, IsResponsible: true), Ct);
        var positions = await directory.ListPositionsAsync(Ct);
        var child = Assert.Single(positions, row => row.Id == staff.Id);
        Assert.Equal(($"/{manager.Id:D}/{staff.Id:D}/", department.Id), (child.Path, child.OrganizationId));
        // 把总部的岗位挂到部门岗位下，删除部门不能同时带走总部岗位。
        await directory.UpdatePositionAsync(boss.Id, new("总部负责人", root.Id, staff.Id, IsResponsible: true), Ct);
        await Rejected("CONFLICT", () => directory.DeleteOrganizationAsync(department.Id, Ct).AsTask());
        Assert.Equal(3, (await directory.ListPositionsAsync(Ct)).Count);
    }

    [Fact]
    public async Task 并发删除与归属不产生悬空关系_并发移动保持单树()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("race"); var (person, _) = await CreatePerson(host); var member = await Join(host, tenant, person.Id);
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D")); var directory = host.Service;
        await directory.SetOrganizationTypeAsync(new("dept", "部门"), Ct); var root = await directory.CreateOrganizationAsync(new("根", "dept"), Ct);
        var a = await directory.CreateOrganizationAsync(new("甲", "dept", root.Id), Ct); var b = await directory.CreateOrganizationAsync(new("乙", "dept", root.Id), Ct);
        async Task<string> Outcome(Func<Task> action) { try { await action(); return "success"; } catch (Qian.Runtime.Errors.QianErrorException error) { return error.Error.Code.Code; } }
        var raced = await Task.WhenAll(Outcome(() => directory.DeleteOrganizationAsync(a.Id, Ct).AsTask()), Outcome(() => directory.SetRelationsAsync(member.Id, [new("organization", a.Id)], Ct).AsTask()));
        Assert.Contains("success", raced); Assert.Contains(raced, outcome => outcome is "NOT_FOUND" or "DIRECTORY_HAS_MEMBERS");
        await directory.SetRelationsAsync(member.Id, [], Ct);
        var tree = await directory.GetOrganizationTreeAsync(Ct);
        if (tree.All(row => row.Id != a.Id)) a = await directory.CreateOrganizationAsync(new("甲", "dept", root.Id), Ct);
        var moved = await Task.WhenAll(Outcome(() => directory.UpdateOrganizationAsync(a.Id, new("甲", "dept", b.Id), Ct).AsTask()), Outcome(() => directory.UpdateOrganizationAsync(b.Id, new("乙", "dept", a.Id), Ct).AsTask()));
        Assert.Equal(["DIRECTORY_CYCLE", "success"], moved.Order(StringComparer.Ordinal));
        Assert.Equal(3, (await directory.GetOrganizationTreeAsync(Ct)).Count);
    }
}

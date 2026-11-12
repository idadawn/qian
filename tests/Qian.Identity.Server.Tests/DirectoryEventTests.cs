using System.Net;
using Json.Schema;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// directory.*.v1（S1-IDP-010）：目录写入与事件同事务进入 Outbox；载荷符合随 Definition 分发的 Schema，不含名称、姓名与联系方式；失败写入不留事件。
public sealed class DirectoryEventTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static async Task<IReadOnlyList<QianEventEnvelope>> DirectoryEvents(AccountsHost.Running host, Guid tenant)
    {
        var joined = await host.Text("SELECT coalesce(string_agg(payload, E'\\n' ORDER BY occurred_at, id), '') FROM plugin_qian_identity.outbox_messages WHERE event_type_id LIKE 'directory.%'");
        return joined.Length == 0 ? [] : [.. joined.Split('\n').Select(QianEventEnvelope.Parse).Where(envelope => envelope.TenantId == tenant)];
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }

    internal static void AssertSchema(QianEventEnvelope envelope)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root(), "contracts/v0/s2s-claims.schema.json")), options);
        var result = JsonSchema.FromText(IdentitySchemas.Event(envelope.Type), options).Evaluate(envelope.Payload, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(result.IsValid, envelope.Type + "：" + envelope.Payload.GetRawText());
    }

    private static long Revision(QianEventEnvelope envelope) => envelope.Payload.GetProperty("directory_revision").GetInt64();
    private static Guid Id(QianEventEnvelope envelope, string name) => envelope.Payload.GetProperty(name).GetGuid();

    [Fact]
    public async Task 目录写入同事务发出directory事件_载荷符合Schema且不含名称姓名_失败写入不留事件()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("directory-events"); var other = host.Tenants.Add("directory-events-other");
        var (adminPerson, adminPhone) = await CreatePerson(host, "管理员甲"); var admin = await Join(host, tenant, adminPerson.Id, "管理员甲", admin: true, account: "admin");
        var (person, _) = await CreatePerson(host, "成员甲"); var member = await Join(host, tenant, person.Id, "成员甲", account: "membera");
        await Join(host, other, person.Id, "成员甲", account: "membera");
        // 成员建立：每个租户各一条 member.created，载荷不含姓名与账号。
        var joined = await DirectoryEvents(host, tenant.Id);
        Assert.Equal([DirectoryEventIds.MemberCreated, DirectoryEventIds.MemberCreated], joined.Select(e => e.Type));
        Assert.Equal([admin.Id, member.Id], joined.Select(e => Id(e, "membership_id")));
        Assert.Equal([1L, 2L], joined.Select(Revision));
        Assert.Single(await DirectoryEvents(host, other.Id));
        var browser = host.NewBrowser(); await browser.Login(adminPhone, Password, tenant: tenant.Id);
        async Task<Guid> Created(string path, object body) { var response = await browser.Json(HttpMethod.Post, path, body); Assert.Equal(HttpStatusCode.OK, response.Status); return response.Body.GetProperty("id").GetGuid(); }
        async Task Ok(HttpMethod method, string path, object? body = null) => Assert.Equal(HttpStatusCode.OK, (await browser.Json(method, path, body)).Status);
        await Ok(HttpMethod.Put, "api/directory/org-types", new { code = "dept", name = "部门" });
        var root = await Created("api/directory/orgs", new { name = "总部", type_code = "dept" });
        var rd = await Created("api/directory/orgs", new { name = "研发部", type_code = "dept", parent_id = root });
        var qa = await Created("api/directory/orgs", new { name = "测试部", type_code = "dept", parent_id = root });
        await Ok(HttpMethod.Put, $"api/directory/orgs/{rd:D}", new { name = "研发一部", type_code = "dept", parent_id = root });
        await Ok(HttpMethod.Put, $"api/directory/orgs/{rd:D}", new { name = "研发一部", type_code = "dept", parent_id = qa });
        var head = await Created("api/directory/positions", new { name = "部长", organization_id = rd, is_responsible = true, responsible_membership_id = admin.Id });
        var staff = await Created("api/directory/positions", new { name = "职员", organization_id = rd, parent_id = head });
        await Ok(HttpMethod.Put, $"api/directory/positions/{staff:D}", new { name = "职员", organization_id = rd });
        await Ok(HttpMethod.Put, $"api/directory/orgs/{rd:D}", new { name = "研发一部", type_code = "dept", parent_id = qa, responsible_position_id = head });
        var relations = new { items = new object[] { new { target_type = "organization", target_id = rd, is_primary = true }, new { target_type = "position", target_id = staff, is_primary = true } } };
        await Ok(HttpMethod.Put, $"api/directory/members/{member.Id:D}/relations", relations);
        await Ok(HttpMethod.Put, $"api/directory/members/{member.Id:D}/relations", relations);
        await Ok(HttpMethod.Put, $"api/directory/members/{member.Id:D}/profile", new { name = "成员甲", pinyin = "chengyuanjia", phone = "13812345678" });
        await Ok(HttpMethod.Put, $"api/directory/members/{member.Id:D}/profile", new { name = "成员甲", pinyin = "chengyuanjia", phone = "13812345678" });
        await Ok(HttpMethod.Post, $"api/directory/members/{member.Id:D}/status", new { status = "disabled", reason = "events" });
        // 失败写入（含成员的组织）整体回滚：不发事件，修订号不动。
        var before = (await DirectoryEvents(host, tenant.Id)).Count; var revision = await host.Text("SELECT revision::text FROM plugin_qian_identity.directory_revisions WHERE tenant_id=@t", ("t", tenant.Id.ToString("D")));
        var refused = await browser.Json(HttpMethod.Delete, $"api/directory/orgs/{qa:D}");
        Assert.Equal((HttpStatusCode.Conflict, "DIRECTORY_HAS_MEMBERS"), (refused.Status, refused.Body.GetProperty("code").GetString()));
        Assert.Equal(before, (await DirectoryEvents(host, tenant.Id)).Count);
        Assert.Equal(revision, await host.Text("SELECT revision::text FROM plugin_qian_identity.directory_revisions WHERE tenant_id=@t", ("t", tenant.Id.ToString("D"))));
        await Ok(HttpMethod.Put, $"api/directory/members/{member.Id:D}/relations", new { items = Array.Empty<object>() });
        await Ok(HttpMethod.Delete, $"api/directory/members/{member.Id:D}");
        await Ok(HttpMethod.Put, $"api/directory/positions/{head:D}", new { name = "部长", organization_id = rd, is_responsible = true });
        await Ok(HttpMethod.Delete, $"api/directory/positions/{head:D}");
        await Ok(HttpMethod.Delete, $"api/directory/orgs/{qa:D}");

        var events = (await DirectoryEvents(host, tenant.Id)).Skip(2).ToArray();
        Assert.Equal([
            DirectoryEventIds.OrganizationCreated, DirectoryEventIds.OrganizationCreated, DirectoryEventIds.OrganizationCreated,
            DirectoryEventIds.OrganizationUpdated, DirectoryEventIds.OrganizationMoved,
            DirectoryEventIds.PositionCreated, DirectoryEventIds.PositionCreated, DirectoryEventIds.PositionMoved, DirectoryEventIds.OrganizationUpdated,
            DirectoryEventIds.MemberRelationsChanged, DirectoryEventIds.MemberProfileUpdated, DirectoryEventIds.MemberStatusChanged,
            DirectoryEventIds.MemberRelationsChanged, DirectoryEventIds.MemberStatusChanged, DirectoryEventIds.PositionUpdated,
            DirectoryEventIds.OrganizationUpdated, DirectoryEventIds.PositionDeleted,
            DirectoryEventIds.PositionDeleted, DirectoryEventIds.OrganizationDeleted, DirectoryEventIds.OrganizationDeleted], events.Select(e => e.Type));
        foreach (var envelope in events)
        {
            AssertSchema(envelope);
            // HTTP 写入的信封 actor 是调用者本人；载荷里没有组织 / 岗位名称、成员姓名、拼音与联系方式。
            Assert.Equal((EventActorKind.User, adminPerson.Id.ToString("D"), "qian.identity"), (envelope.Actor.Kind, envelope.Actor.Id, envelope.Producer));
            foreach (var secret in new[] { "总部", "研发", "测试部", "部长", "职员", "成员甲", "管理员甲", "chengyuanjia", "13812345678", "membera" })
                Assert.DoesNotContain(secret, envelope.Payload.GetRawText(), StringComparison.Ordinal);
        }
        // 每次产生事件的写入修订号加一；同一次写入的多条事件共用一个修订号（删组织子树、删岗位连带解除组织引用）。
        var revisions = events.Select(Revision).ToArray();
        Assert.Equal([3L, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 18, 19, 19, 19], revisions);
        // 移动带新旧 path 与上级；后代 path 按前缀替换（这里研发部没有后代组织）。
        var moved = events[4];
        Assert.Equal((rd, qa, root), (Id(moved, "organization_id"), Id(moved, "parent_id"), Id(moved, "previous_parent_id")));
        Assert.Equal(($"/{root:D}/{qa:D}/{rd:D}/", $"/{root:D}/{rd:D}/", 3), (moved.Payload.GetProperty("path").GetString(), moved.Payload.GetProperty("previous_path").GetString(), moved.Payload.GetProperty("depth").GetInt32()));
        Assert.Equal((staff, head), (Id(events[7], "position_id"), Id(events[7], "previous_parent_id")));
        Assert.False(events[7].Payload.TryGetProperty("parent_id", out _));
        Assert.Equal(head, Id(events[8], "responsible_position_id"));
        // 归属变化给出替换前后的完整集合；再次提交相同集合不发事件（上面只出现一次）。
        var changed = events[9].Payload;
        Assert.Equal((member.Id, person.Id, 0), (changed.GetProperty("membership_id").GetGuid(), changed.GetProperty("person_id").GetGuid(), changed.GetProperty("previous_relations").GetArrayLength()));
        Assert.Equal([("organization", rd, true), ("position", staff, true)], changed.GetProperty("relations").EnumerateArray()
            .Select(item => (item.GetProperty("target_type").GetString(), item.GetProperty("target_id").GetGuid(), item.GetProperty("is_primary").GetBoolean())).OrderBy(item => item.Item1, StringComparer.Ordinal));
        Assert.Equal(2, events[12].Payload.GetProperty("previous_relations").GetArrayLength());
        Assert.Equal(("disabled", "active", false), (events[11].Payload.GetProperty("status").GetString(), events[11].Payload.GetProperty("previous_status").GetString(), events[11].Payload.GetProperty("deleted").GetBoolean()));
        Assert.Equal(("disabled", "disabled", true), (events[13].Payload.GetProperty("status").GetString(), events[13].Payload.GetProperty("previous_status").GetString(), events[13].Payload.GetProperty("deleted").GetBoolean()));
        Assert.False(events[14].Payload.TryGetProperty("responsible_membership_id", out _));
        // 删岗位：仍以它为责任岗位的组织先解除引用（updated），再发岗位删除；删组织子树：先岗位后组织、深的先删。
        Assert.Equal((rd, false), (Id(events[15], "organization_id"), events[15].Payload.TryGetProperty("responsible_position_id", out _)));
        Assert.Equal(head, Id(events[16], "position_id"));
        Assert.Equal((staff, rd, qa), (Id(events[17], "position_id"), Id(events[18], "organization_id"), Id(events[19], "organization_id")));
        // 另一个租户只有自己那一条成员事件。
        Assert.Single(await DirectoryEvents(host, other.Id));
    }

    [Fact]
    public async Task 账号入口的成员状态变化与重新启用也发事件_修订行受租户行级安全保护()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("directory-account-events");
        var (person, _) = await CreatePerson(host); var member = await Join(host, tenant, person.Id);
        using (host.Tenant.BeginScope(tenant.Id.ToString("D")))
        {
            await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Locked, "trusted_lock", Ct);
            await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Locked, "trusted_lock", Ct);
        }
        // 已存在且被锁定的成员再次「建立」= 重新启用：发 status_changed，不再发 created。
        await Join(host, tenant, person.Id);
        var events = await DirectoryEvents(host, tenant.Id);
        Assert.Equal([DirectoryEventIds.MemberCreated, DirectoryEventIds.MemberStatusChanged, DirectoryEventIds.MemberStatusChanged], events.Select(e => e.Type));
        Assert.Equal([("locked", "active"), ("active", "locked")], events.Skip(1).Select(e => (e.Payload.GetProperty("status").GetString(), e.Payload.GetProperty("previous_status").GetString())));
        foreach (var envelope in events) AssertSchema(envelope);
        // 受限应用角色只能看到本租户的修订行。
        await using var connection = new Npgsql.NpgsqlConnection(postgres.AppConnectionString); await connection.OpenAsync(Ct);
        await using var set = new Npgsql.NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection); set.Parameters.AddWithValue("tenant", Guid.NewGuid().ToString("D")); await set.ExecuteNonQueryAsync(Ct);
        await using var count = new Npgsql.NpgsqlCommand("SELECT count(*) FROM plugin_qian_identity.directory_revisions", connection); Assert.Equal(0L, await count.ExecuteScalarAsync(Ct));
    }

    // 宿主漏装 AddQianIdentityEvents（没有把目录事件写进 Outbox 的处理器）时，会发目录事件的四个写入入口（目录写入、成员新建、
    // 状态变更、重新启用）都在提交前抛错回滚：业务数据、修订行与 Outbox 一条都不留，不会静默丢事件（S1-IDP-010 第一轮复核补的用例）。
    [Fact]
    public async Task 宿主漏装目录事件处理器时_目录写入_成员新建_状态变更与重新启用都整体回滚()
    {
        await using var host = await AccountsHost.Start(postgres, identityEvents: false); var tenant = host.Tenants.Add("directory-no-handler");
        var scopeId = tenant.Id.ToString("D");
        Task<string> Count(string table) => host.Text($"SELECT count(*)::text FROM plugin_qian_identity.{table} WHERE tenant_id=@t", ("t", scopeId));
        Task<string> Status(Guid id) => host.Text("SELECT status FROM plugin_qian_identity.memberships WHERE id=@id", ("id", id));
        var (person, _) = await CreatePerson(host, "成员乙");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Join(host, tenant, person.Id, "成员乙"));
        Assert.Equal("0", await Count("memberships"));
        using (host.Tenant.BeginScope(scopeId))
        {
            // 组织类型字典不发事件，照常写入；建组织要发 directory.org.created.v1，整体回滚。
            await host.Service.SetOrganizationTypeAsync(new("dept", "部门"), Ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.CreateOrganizationAsync(new("总部", "dept"), Ct).AsTask());
        }
        Assert.Equal(("1", "0"), (await Count("org_types"), await Count("organizations")));
        // 这个宿主上建立入口走不通，成员行由超级用户直接写入，再走状态变更与重新启用。
        var id = Guid.CreateVersion7();
        await host.Exec("""
            INSERT INTO plugin_qian_identity.memberships (id, tenant_id, person_id, account, display_name, status, is_tenant_administrator, identity_revision, directory_deleted, created_at)
            VALUES (@id, @t, @person, 'memberb', '成员乙', 'active', false, 1, false, now())
            """, ("id", id), ("t", scopeId), ("person", person.Id));
        using (host.Tenant.BeginScope(scopeId))
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Accounts.SetMembershipStatusAsync(id, AccountStatuses.Locked, "no_handler", Ct).AsTask());
        Assert.Equal("active", await Status(id));
        await host.Exec("UPDATE plugin_qian_identity.memberships SET status = 'locked' WHERE id=@id", ("id", id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Join(host, tenant, person.Id, "成员乙"));
        Assert.Equal("locked", await Status(id));
        Assert.Equal("0", await Count("directory_revisions"));
        Assert.Empty(await DirectoryEvents(host, tenant.Id));
    }
}

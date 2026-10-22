using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// S1-AGT-002 验收 3（UT）：清单 policy.permissions 与 policy.egress 五类随制品快照落库，经注册表端口按摘要读出；租户管理员批准 API（S1 只有 API），
// 以及开通、升级在权益之后的第二道门。真实 PostgreSQL、Registry、Audit 与 Lifecycle；宿主的权限判定端口与 Authz 超管旁路是替身，
// 真实 RBAC、JWT 与 HTTP 见 Qian.Sample.Crm.Tests 的 PermissionApprovalHttpTests。S1-AGT-006 补：批准入租户审计链（ADR-009 D9.8）、开通续跑核对批准。
public sealed class PermissionApprovalTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string TenantA = "01900000-0000-7000-8000-00000000000a";
    private const string TenantB = "01900000-0000-7000-8000-00000000000b";
    private const string Plugin = "acme.connector";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly object FullPermissions = new
    {
        platform = new[] { "tenant:read", "notify:send" }, data = new[] { "qian.sample.crm:customer" },
        secrets = new[] { new { name = "oss/*", scope = "platform" }, new { name = "db/token", scope = "tenant" } }, ui = new { connectSrc = new[] { "https://api.example.test" } }
    };
    private static readonly string[] FullEgress = ["api.example.test", "*.files.example.test:443"];
    private static readonly string[] TenantRead = ["tenant:read"];
    private static readonly string[] TenantReadNotify = ["tenant:read", "notify:send"];
    private static readonly string[] TenantScope = ["tenant"];
    // 规范化的批准内容（每类去重并按序数排序，秘密按名称再按 scope），即审计载荷里的 approved / previous 与输入哈希的原文。
    private const string PartialCanonical = """{"platform":["tenant:read"],"data":[],"egress":["*.files.example.test:443","api.example.test"],"secrets":[],"ui":{"connectSrc":[]}}""";
    private const string FullCanonical = """{"platform":["notify:send","tenant:read"],"data":["qian.sample.crm:customer"],"egress":["*.files.example.test:443","api.example.test"]""" +
        ""","secrets":[{"name":"db/token","scope":"tenant"},{"name":"oss/*","scope":"platform"}],"ui":{"connectSrc":["https://api.example.test"]}}""";
    private const string ApprovalAuditCapability = "qian.lifecycle/permissions.approve";

    [Fact]
    public async Task 五类反向权限随制品快照落库并可读_未批准的开通被拒且不写记录_逐项批准后开通_重复批准不改记录()
    {
        await using var host = await Start(new Permissions(("approver", TenantA), ("other-admin", TenantA)));
        var installed = await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        // 落库：plugin_artifacts.manifest 保存的 policy.permissions 与 policy.egress 与原清单逐项相等。
        using var stored = JsonDocument.Parse((await LifecycleHost.ScalarAsync(postgres,
            $"SELECT manifest::text FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{installed.ArtifactDigest}'", Ct))!);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(FullPermissions), stored.RootElement.GetProperty("policy").GetProperty("permissions")));
        Assert.Equal(FullEgress, stored.RootElement.GetProperty("policy").GetProperty("egress").EnumerateArray().Select(item => item.GetString()));
        // 可读：注册表的受信端口按摘要读出五类，顺序同清单。
        var declared = (await host.Registry.GetArtifactPermissionsAsync(installed.ArtifactDigest, Ct))!;
        Assert.Equal((Plugin, "1.0.0", installed.ArtifactDigest), (declared.PluginId, declared.Version, declared.ArtifactDigest));
        Assert.Equal(["tenant:read", "notify:send"], declared.Platform); Assert.Equal(["qian.sample.crm:customer"], declared.Data); Assert.Equal(FullEgress, declared.Egress);
        Assert.Equal([("oss/*", "platform"), ("db/token", "tenant")], declared.Secrets.Select(secret => (secret.Name, secret.Scope)));
        Assert.Equal(["https://api.example.test"], declared.Ui.ConnectSrc);
        Assert.Null(await host.Registry.GetArtifactPermissionsAsync("sha256:" + new string('0', 64), Ct));

        // 权益（买没买）之后还有批准（同不同意）：未批准时开通被拒，安装行停在 Entitled，不认领执行记录。
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        var denied = await AssertCode(LifecycleErrors.PermissionsUnapproved, () => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask());
        Assert.Equal(422, LifecycleErrors.PermissionsUnapproved.Status);
        Assert.Equal("platform notify:send, tenant:read; data qian.sample.crm:customer; egress *.files.example.test:443, api.example.test; " +
            "secrets tenant:db/token, platform:oss/*; ui.connectSrc https://api.example.test", denied.Args["missing"]);
        Assert.Equal(TenantInstallStates.Entitled, (await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!.State);
        Assert.Empty(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct));

        var approver = new TestUser("approver", TenantA);
        using (host.Tenant.BeginScope(TenantA))
        {
            Assert.Null(await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", approver, Ct));
            // 逐项批准：只批准一部分时记录照存，开通仍被拒，错误只列剩下的项。
            var partial = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: ["tenant:read"], egress: FullEgress), approver, Ct);
            Assert.Equal(TenantRead, partial.Approved.Platform);
            Assert.Equal((PermissionApprovalChannels.Tenant, "approver"), (partial.ApprovedVia, partial.ApprovedBy));
        }
        var remaining = await AssertCode(LifecycleErrors.PermissionsUnapproved, () => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask());
        Assert.Equal("platform notify:send; data qian.sample.crm:customer; secrets tenant:db/token, platform:oss/*; ui.connectSrc https://api.example.test", remaining.Args["missing"]);

        PermissionApprovalView approval;
        using (host.Tenant.BeginScope(TenantA))
        {
            // 整份替换：顺序与重复不影响内容，记录按序数排序去重保存。
            approval = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(["notify:send", "tenant:read", "tenant:read"], ["qian.sample.crm:customer"],
                [.. FullEgress.Reverse()], [new SecretGrant("db/token", "tenant"), new SecretGrant("oss/*", "platform")], ["https://api.example.test"]), approver, Ct);
            Assert.Equal(["notify:send", "tenant:read"], approval.Approved.Platform);
            Assert.Equal(["*.files.example.test:443", "api.example.test"], approval.Approved.Egress);
            Assert.Equal([("db/token", "tenant"), ("oss/*", "platform")], approval.Approved.Secrets.Select(secret => (secret.Name, secret.Scope)));
            var rowVersion = await LifecycleHost.ScalarAsync(postgres, $"SELECT row_version FROM plugin_qian_lifecycle.permission_approvals WHERE tenant_id = '{TenantA}'", Ct);
            // 重复批准同一内容：不改记录（批准时间、批准人、行版本都不变）。
            host.Time.Now = host.Time.Now.AddMinutes(5);
            var repeated = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(["tenant:read", "notify:send"], ["qian.sample.crm:customer"], FullEgress,
                [new SecretGrant("oss/*", "platform"), new SecretGrant("db/token", "tenant")], ["https://api.example.test"]), new TestUser("other-admin", TenantA), Ct);
            Assert.Equal((approval.ApprovedAt, "approver"), (repeated.ApprovedAt, repeated.ApprovedBy));
            Assert.Equal(rowVersion, await LifecycleHost.ScalarAsync(postgres, $"SELECT row_version FROM plugin_qian_lifecycle.permission_approvals WHERE tenant_id = '{TenantA}'", Ct));
            Assert.Equal(approval.ApprovedAt, (await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", approver, Ct))!.ApprovedAt);
        }
        Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick(await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct)));
        Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct));
        // 受信读取端口（供秘密解析等可信平台插件取审批记录）与管理面读到同一份。
        var trusted = (await host.Lifecycle.GetApprovedPermissionsAsync(TenantA, Plugin, "1.0.0", Ct))!;
        Assert.Equal(approval.Approved.Secrets, trusted.Approved.Secrets);

        // B 的批准与 A 无关：B 同样已获授权，但没有批准记录时照样被拒。
        await host.Lifecycle.EntitleAsync(TenantB, Plugin, "operator", Ct);
        await AssertCode(LifecycleErrors.PermissionsUnapproved, () => host.Lifecycle.ProvisionAsync(TenantB, Plugin, "operator", Ct).AsTask());
        Assert.Null(await host.Lifecycle.GetApprovedPermissionsAsync(TenantB, Plugin, "1.0.0", Ct));
    }

    [Fact]
    public async Task 批准管理面_无权限跨租户委托与Agent与不存在同为404_内容只在判定之后校验且只能批准声明过的项_失败不写记录()
    {
        // 判定替身也放行一个超长主体：它仍须被挡下（批准人与 Authz 同样不超过 200 字符），不能变成数据库错误。
        var overlong = new string('a', 201);
        await using var host = await Start(new Permissions(("approver", TenantA), ("approver", TenantB), (overlong, TenantA)));
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        var full = Grants(["tenant:read"], [], [], [], []);
        using (host.Tenant.BeginScope(TenantA))
        {
            await AssertCode(PlatformErrors.Unauthenticated, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser("approver", TenantA, authenticated: false), Ct).AsTask());
            // 无权限的租户主体、跨租户、委托、Agent、超长主体、格式不合格的标识与未安装的版本都是同一个 404；无权限者的坏请求体也只得到 404。
            foreach (var attempt in new Func<Task>[]
            {
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser("reader", TenantA), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", null, new TestUser("reader", TenantA), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantB, Plugin, "1.0.0", full, new TestUser("approver", TenantB), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantB, Plugin, "1.0.0", full, new TestUser("approver", TenantA), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser("approver", TenantA, actor: true), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser("approver", TenantA, PrincipalKind.Agent), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync("not-a-tenant", Plugin, "1.0.0", full, new TestUser("approver", TenantA), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "9.9.9", full, new TestUser("approver", TenantA), Ct).AsTask(),
                () => host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", new TestUser("reader", TenantA), Ct).AsTask(),
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser(overlong, TenantA), Ct).AsTask(),
                // 平台主体只经超管旁路：宿主没有装配旁路端口时同样 404。
                () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", full, new TestUser("root", null), Ct).AsTask(),
            })
                await AssertCode(PlatformErrors.NotFound, attempt);
            // 判定通过之后才看内容：读不成、形状不对、未声明的项都是 INPUT_INVALID，pointer 指向请求体中的位置。
            var approver = new TestUser("approver", TenantA);
            Assert.Equal(["#"], Pointers(await AssertCode(PlatformErrors.InputInvalid, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", null, approver, Ct).AsTask())));
            Assert.Equal(["#/platform/1", "#/secrets/0/scope", "#/ui/connectSrc/0"], Pointers(await AssertCode(PlatformErrors.InputInvalid, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0",
                Grants(["tenant:read", " "], [], [], [new SecretGrant("oss/*", "k8s")], ["line\nbreak"]), approver, Ct).AsTask())));
            Assert.Equal(["#/platform/1", "#/egress/0", "#/secrets/0"], Pointers(await AssertCode(PlatformErrors.InputInvalid, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0",
                Grants(["tenant:read", "tenant:write"], [], ["other.example.test"], [new SecretGrant("oss/*", "tenant")], []), approver, Ct).AsTask())));
        }
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.permission_approvals", Ct));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NOT NULL", Ct));
    }

    [Fact]
    public async Task 平台运营代批只经Authz超管旁路_旁路收到目标租户与资源_记录为superadmin_旁路拒绝与不存在同为404()
    {
        await using var host = await Start(new Permissions());
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        var bypass = new RecordingBypass("root");
        await host.Runtime.MountAsync([new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new BypassPlugin(bypass), [AuthzDefinition.SuperadminBypassKey])], Ct);
        Assert.Equal(PluginState.Ready, host.Runtime.GetState("authz", ScopeKey.Global));
        var grants = Grants(["tenant:read", "notify:send"], ["qian.sample.crm:customer"], FullEgress,
            [new SecretGrant("oss/*", "platform"), new SecretGrant("db/token", "tenant")], ["https://api.example.test"]);
        // 运营宿主先选定已验证的目标租户（与 Registry 的平台主体入口同一口径，S1-AZ-008），旁路在该租户上下文中判定，审计随之记入该租户链。
        PermissionApprovalView approval;
        using (host.Tenant.BeginScope(TenantA))
            approval = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", grants, new TestUser("root", null), Ct);
        Assert.Equal((PermissionApprovalChannels.Superadmin, "root"), (approval.ApprovedVia, approval.ApprovedBy));
        // 代批同样以批准条目入该租户审计链：主体是平台运营，渠道 superadmin（旁路自己的判定审计由 Authz 另写，替身不写）。
        using (host.Tenant.BeginScope(TenantA))
            AssertApproval(Assert.Single(await ApprovalAudit(host)), "user", "root", approval.ApprovedAt, PermissionApprovalChannels.Superadmin, FullCanonical, null);
        var access = Assert.Single(bypass.Accesses);
        Assert.Equal(("root", (string?)null, LifecycleDefinition.InstallApprovePermission, TenantA, "lifecycle/install:" + TenantA + "/" + Plugin + "@1.0.0", "api", TenantA),
            (access.Access.Subject.Id, access.Access.Subject.TenantId, access.Access.Permission, access.Access.TenantId, access.Access.Resource, access.Access.Channel, access.Context));
        // 参数里的租户只用来核对：宿主没有选定目标、选定的是另一个租户，或主体不是超管，都与不存在同为 404，且不写记录、不调旁路。
        await AssertCode(PlatformErrors.NotFound, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", grants, new TestUser("root", null), Ct).AsTask());
        using (host.Tenant.BeginScope(TenantA))
            await AssertCode(PlatformErrors.NotFound, () => host.Lifecycle.ApprovePermissionsAsync(TenantB, Plugin, "1.0.0", grants, new TestUser("root", null), Ct).AsTask());
        Assert.Single(bypass.Accesses);
        using (host.Tenant.BeginScope(TenantB))
            await AssertCode(PlatformErrors.NotFound, () => host.Lifecycle.ApprovePermissionsAsync(TenantB, Plugin, "1.0.0", grants, new TestUser("intruder", null), Ct).AsTask());
        Assert.Equal(1, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.permission_approvals", Ct));
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        Assert.Equal(TenantInstallStates.Installed, (await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct)).State);
    }

    [Fact]
    public async Task policy_required的平台组件与没有声明反向权限的组件不经批准即可开通()
    {
        await using var host = await Start(new Permissions());
        // 平台组件（tier: system、required: true）恒视为已授权，声明了反向权限也不经批准。
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress, tier: "system", required: true), "qian.platform-probe");
        Assert.Equal(TenantInstallStates.Installed, (await host.Lifecycle.ProvisionAsync(TenantA, "qian.platform-probe", "operator", Ct)).State);
        // 不声明，或五类都为空：权益之后直接开通。
        await Install(host, "1.0.0", Policy(null, null), "acme.plain");
        await Install(host, "1.0.0", Policy(new { platform = Array.Empty<string>(), ui = new { connectSrc = Array.Empty<string>() } }, []), "acme.empty");
        foreach (var plugin in new[] { "acme.plain", "acme.empty" })
        {
            await host.Lifecycle.EntitleAsync(TenantA, plugin, "operator", Ct);
            Assert.Equal(TenantInstallStates.Installed, (await host.Lifecycle.ProvisionAsync(TenantA, plugin, "operator", Ct)).State);
        }
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.permission_approvals", Ct));
    }

    [Fact]
    public async Task 升级只在新增反向权限时须重批_连续升级沿用最近一次批准_缺失项写进错误且停在原版本()
    {
        await using var host = await Start(new Permissions(("approver", TenantA)));
        var first = await Install(host, "1.0.0", Policy(new { platform = TenantRead }, null));
        await Install(host, "1.0.1", Policy(new { platform = TenantRead }, null), activate: false);
        await Install(host, "1.0.2", Policy(new { platform = TenantRead }, null), activate: false);
        await Install(host, "1.1.0", Policy(new { platform = TenantReadNotify }, ["api.example.test"]), activate: false);
        Assert.Equal(PluginRevisionStates.Active, first.State);
        var approver = new TestUser("approver", TenantA);
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        using (host.Tenant.BeginScope(TenantA))
            await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: ["tenant:read"]), approver, Ct);
        Assert.Equal("1.0.0", (await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct)).Version);
        // 没有新增项：1.0.1、1.0.2 都没有自己的批准记录，沿用 1.0.0 的批准。
        Assert.Equal("1.0.1", (await host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.0.0", "1.0.1", "operator", Ct)).Version);
        Assert.Equal("1.0.2", (await host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.0.1", "1.0.2", "operator", Ct)).Version);
        // 1.1.0 新增两项：须重批，错误只列新增的项，租户停在 1.0.2，不认领升级钩子。
        var denied = await AssertCode(LifecycleErrors.PermissionsUnapproved, () => host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.0.2", "1.1.0", "operator", Ct).AsTask());
        Assert.Equal("platform notify:send; egress api.example.test", denied.Args["missing"]);
        Assert.Equal((TenantInstallStates.Installed, "1.0.2", (string?)null), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        Assert.DoesNotContain(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct), run => run.ToVersion == "1.1.0");
        using (host.Tenant.BeginScope(TenantA))
            await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.1.0", Grants(["tenant:read", "notify:send"], [], ["api.example.test"], [], []), approver, Ct);
        Assert.Equal("1.1.0", (await host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.0.2", "1.1.0", "operator", Ct)).Version);
        // 租户级回切到 1.0.0：它有自己的批准记录，照常回切。
        Assert.Equal("1.0.0", (await host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.1.0", "1.0.0", "operator", Ct)).Version);
    }

    [Fact]
    public async Task 批准与整份替换写入租户审计链_记批准人主体类型渠道与本次上一次内容_同一内容重复批准不再记()
    {
        await using var host = await Start(new Permissions(("approver", TenantA), ("service-admin", TenantA), ("other-admin", TenantA)));
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        using var scope = host.Tenant.BeginScope(TenantA);
        // 首次批准：上一次为 null；批准人是已验证的用户主体，渠道为租户管理员。
        var partial = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: ["tenant:read"], egress: FullEgress), new TestUser("approver", TenantA), Ct);
        AssertApproval(Assert.Single(await ApprovalAudit(host)), "user", "approver", partial.ApprovedAt, PermissionApprovalChannels.Tenant, PartialCanonical, null);
        // 整份替换（服务主体批准）：新条目接在链上，上一次是被替换的那一份。
        host.Time.Now = host.Time.Now.AddMinutes(1);
        var full = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", FullGrants(), new TestUser("service-admin", TenantA, PrincipalKind.Service), Ct);
        var entries = await ApprovalAudit(host);
        Assert.Equal(2, entries.Length);
        Assert.Equal(entries[0].Sequence + 1, entries[1].Sequence);
        AssertApproval(entries[1], "service", "service-admin", full.ApprovedAt, PermissionApprovalChannels.Tenant, FullCanonical, PartialCanonical);
        // 同一内容（顺序不同）由另一位管理员重复批准：记录不变，也不再记审计。
        host.Time.Now = host.Time.Now.AddMinutes(1);
        await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants([.. FullPermissionsPlatform.Reverse()], ["qian.sample.crm:customer"], FullEgress,
            [new SecretGrant("oss/*", "platform"), new SecretGrant("db/token", "tenant")], ["https://api.example.test"]), new TestUser("other-admin", TenantA), Ct);
        Assert.Equal(2, (await ApprovalAudit(host)).Length);
        Assert.Equal((full.ApprovedAt, "service-admin"), Pick((await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", new TestUser("approver", TenantA), Ct))!));
    }

    [Fact]
    public async Task 批准与审计同一事务_审计写入失败或提交失败时两者都不留_宿主没有审计时与不存在同为404()
    {
        await using var host = await Start(new Permissions(("approver", TenantA)));
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        var approver = new TestUser("approver", TenantA);
        var narrower = Grants(platform: ["tenant:read"], egress: FullEgress);
        using var scope = host.Tenant.BeginScope(TenantA);
        var approved = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", FullGrants(), approver, Ct);
        host.Time.Now = host.Time.Now.AddMinutes(1);
        // 审计条目写不进去：批准随同一事务回滚，记录仍是上一份。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_audit.reject_approval() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced audit failure'; END $$; " +
            "CREATE TRIGGER reject_approval BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_approval();", Ct);
        Assert.Equal("forced audit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() =>
            host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", narrower, approver, Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_approval ON plugin_qian_audit.audit_entries; DROP FUNCTION plugin_qian_audit.reject_approval();", Ct);
        Assert.Equal((approved.ApprovedAt, "approver"), Pick((await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", approver, Ct))!));
        // 审计已追加、批准在提交时失败：审计条目随同一事务回滚，链上不留孤儿条目。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_lifecycle.reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced commit failure'; END $$; " +
            "CREATE CONSTRAINT TRIGGER reject_commit AFTER INSERT OR UPDATE ON plugin_qian_lifecycle.permission_approvals DEFERRABLE INITIALLY DEFERRED FOR EACH ROW " +
            "EXECUTE FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal("forced commit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() =>
            host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", narrower, approver, Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_commit ON plugin_qian_lifecycle.permission_approvals; DROP FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal((approved.ApprovedAt, "approver"), Pick((await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", approver, Ct))!));
        AssertApproval(Assert.Single(await ApprovalAudit(host)), "user", "approver", approved.ApprovedAt, PermissionApprovalChannels.Tenant, FullCanonical, null);
        // 宿主撤下审计：批准与缺判定端口一样 fail-closed 为 404，不写记录。
        await host.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        await AssertCode(PlatformErrors.NotFound, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", narrower, approver, Ct).AsTask());
        Assert.Equal((approved.ApprovedAt, "approver"), Pick((await host.Lifecycle.GetPermissionApprovalAsync(TenantA, Plugin, "1.0.0", approver, Ct))!));
        Assert.Equal(1, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NOT NULL", Ct));
    }

    [Fact]
    public async Task 两份批准内容合起来超出审计载荷上限时只记各自的SHA256_批准照常生效()
    {
        await using var host = await Start(new Permissions(("approver", TenantA)));
        // 160 项、每项 483 字符：单是本次内容就超过 64 KiB。
        var platform = Enumerable.Range(0, 160).Select(index => "platform:" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ":" + new string('x', 470)).ToArray();
        await Install(host, "1.0.0", Policy(new { platform }, null));
        var approver = new TestUser("approver", TenantA);
        using var scope = host.Tenant.BeginScope(TenantA);
        var everything = await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: platform), approver, Ct);
        Assert.Equal(platform, everything.Approved.Platform);
        var large = "{\"platform\":[" + string.Join(",", platform.Select(item => "\"" + item + "\"")) + "],\"data\":[],\"egress\":[],\"secrets\":[],\"ui\":{\"connectSrc\":[]}}";
        AssertDigests(Assert.Single(await ApprovalAudit(host)), large, null);
        // 收窄到一项：本次内容很小，但带上被替换的上一份仍超限，两份都只记哈希。
        host.Time.Now = host.Time.Now.AddMinutes(1);
        await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: [platform[0]]), approver, Ct);
        var narrowed = "{\"platform\":[\"" + platform[0] + "\"],\"data\":[],\"egress\":[],\"secrets\":[],\"ui\":{\"connectSrc\":[]}}";
        AssertDigests((await ApprovalAudit(host))[1], narrowed, large);

        static void AssertDigests(AuditChainEntry entry, string approved, string? previous)
        {
            Assert.Equal(Sha256(approved), entry.Record.InputHash);
            var expected = JsonSerializer.SerializeToElement(new
            {
                pluginId = Plugin, version = "1.0.0", via = PermissionApprovalChannels.Tenant, approved = new { sha256 = Sha256(approved) },
                previous = previous is null ? null : new { sha256 = Sha256(previous) }
            });
            Assert.True(JsonElement.DeepEquals(expected, entry.Record.Payload), entry.Record.Payload.GetRawText());
        }
    }

    [Fact]
    public async Task 同一租户的两次批准同时提交_审计链冲突的一方换新会话重做_两份批准与对应审计都在()
    {
        var barrier = new SaveBarrier();
        await using var host = await Start(new Permissions(("approver", TenantA)),
            services: services => services.ConfigureDbContext<LifecycleDbContext>((_, options) => options.AddInterceptors(barrier)));
        await Install(host, "1.0.0", Policy(new { platform = TenantRead }, null));
        await Install(host, "1.0.0", Policy(new { platform = TenantRead }, null), "acme.second");
        var approver = new TestUser("approver", TenantA);
        using var scope = host.Tenant.BeginScope(TenantA);
        // 链上先有一条：两次并发追加要推进同一个已存在的链头。
        await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: ["tenant:read"]), approver, Ct);
        // 两次批准（不同组件，安装锁不同）各自取得快照后在保存处会合：先提交的一方推进链头，另一方追加时撞上可串行化冲突，只能从新会话重做。
        barrier.Armed = true;
        var views = await Task.WhenAll(
            Task.Run(() => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(), approver, Ct).AsTask(), Ct),
            Task.Run(() => host.Lifecycle.ApprovePermissionsAsync(TenantA, "acme.second", "1.0.0", Grants(platform: ["tenant:read"]), approver, Ct).AsTask(), Ct));
        Assert.True(barrier.Arrivals >= 3, "没有发生重做：" + barrier.Arrivals);
        Assert.Empty(views[0].Approved.Platform); Assert.Equal(TenantRead, views[1].Approved.Platform);
        var entries = await ApprovalAudit(host);
        Assert.Equal(3, entries.Length);
        Assert.Equal(["acme.connector@1.0.0", "acme.connector@1.0.0", "acme.second@1.0.0"],
            entries.Select(entry => entry.Record.Resource!.Split('/')[^1]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task 开通续跑按原目标版本重新核对批准_两次尝试之间激活声明更多权限的新版本并收窄批准后续跑被拒且不认领执行记录_重批后续跑仍开通原目标版本()
    {
        var hooks = new FlakyProvision();
        await using var host = await Start(new Permissions(("approver", TenantA)), (plugin, _) => plugin == Plugin ? hooks : null);
        await Install(host, "1.0.0", Policy(FullPermissions, FullEgress));
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        var approver = new TestUser("approver", TenantA);
        using (host.Tenant.BeginScope(TenantA)) await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", FullGrants(), approver, Ct);
        // 第一次开通：钩子失败，停在 Provisioning(1.0.0)，执行记录失败、已尝试一次。
        await AssertCode(LifecycleErrors.HookFailed, () => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask());
        Assert.Equal((TenantInstallStates.Provisioning, (string?)null, (string?)"1.0.0"), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        Assert.Equal((LifecycleRunStates.Failed, 1), PickRun(Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct))));
        // 两次尝试之间：平台激活了声明更多反向权限的 1.0.1（Active 修订换了），租户的批准又被整份替换为更小的内容。
        // 续跑核对的是原目标版本 1.0.0 的声明，不是 Active 修订（1.0.1）的：按 Active 核对会把 1.0.1 新增的 platform tenant:write 也列为缺失。
        var wider = new
        {
            platform = new[] { "tenant:read", "notify:send", "tenant:write" }, data = new[] { "qian.sample.crm:customer" },
            secrets = new[] { new { name = "oss/*", scope = "platform" }, new { name = "db/token", scope = "tenant" } }, ui = new { connectSrc = new[] { "https://api.example.test" } }
        };
        Assert.Equal((PluginRevisionStates.Active, "1.0.1"), ((await Install(host, "1.0.1", Policy(wider, FullEgress))).State, "1.0.1"));
        Assert.Equal(PluginRevisionStates.Installed, (await host.Lifecycle.GetRevisionsAsync(Plugin, Ct)).Single(revision => revision.Version == "1.0.0").State);
        using (host.Tenant.BeginScope(TenantA))
            await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", Grants(platform: ["tenant:read"], egress: FullEgress), approver, Ct);
        // 续跑先过第二道门：缺失项只有 1.0.0 声明过的四项，仍停在 Provisioning(1.0.0)，执行记录不被认领，钩子没有再执行。
        var denied = await AssertCode(LifecycleErrors.PermissionsUnapproved, () => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask());
        Assert.Equal("platform notify:send; data qian.sample.crm:customer; secrets tenant:db/token, platform:oss/*; ui.connectSrc https://api.example.test", denied.Args["missing"]);
        Assert.Equal((TenantInstallStates.Provisioning, (string?)null, (string?)"1.0.0"), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        Assert.Equal((LifecycleRunStates.Failed, 1), PickRun(Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct))));
        Assert.Equal(1, hooks.Calls);
        // 重新批准 1.0.0（不含 1.0.1 新增的项）后续跑沿用原目标版本：钩子第二次以 1.0.0 执行成功，开通完成在 1.0.0 而不是 Active 的 1.0.1。
        using (host.Tenant.BeginScope(TenantA)) await host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", FullGrants(), approver, Ct);
        Assert.Equal((TenantInstallStates.Installed, (string?)"1.0.0", (string?)null), Pick3(await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct)));
        var run = Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct));
        Assert.Equal((LifecycleRunStates.Succeeded, 2, "1.0.0"), (run.Status, run.Attempts, run.ToVersion));
        Assert.Equal(["1.0.0", "1.0.0"], hooks.Versions);
    }

    private async Task<LifecycleHost> Start(Permissions permissions, Func<string, string, IPluginLifecycleHooks?>? hooks = null, Action<IServiceCollection>? services = null)
    {
        var host = await LifecycleHost.StartAsync(postgres, hooks: hooks, services: collection =>
        {
            collection.AddSingleton<IPermissionChecker>(permissions);
            services?.Invoke(collection);
        }, ct: Ct);
        host.Trust(0, "k1", Key, host.Time.Now.AddDays(-1));
        return host;
    }

    private static readonly SigningKey Key = new();

    private static async Task<PluginRevisionView> Install(LifecycleHost host, string version, object policy, string plugin = Plugin, bool activate = true)
    {
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            [".qian-plugin/plugin.json"] = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
            {
                ["$schema"] = "https://qian.dev/schemas/plugin/v2", ["id"] = plugin, ["version"] = version, ["description"] = "反向权限批准验收使用的声明式组件制品。",
                ["author"] = new { name = "Acme" }, ["license"] = "Apache-2.0", ["interface"] = new { displayName = plugin, category = "testing" },
                ["inject"] = Array.Empty<string>(), ["provides"] = Array.Empty<string>(), ["policy"] = policy, ["scopes"] = TenantScope
            })
        };
        var revision = await host.Lifecycle.InstallAsync(Artifacts.Signed(files, Key), "operator", Ct);
        return activate ? await host.Lifecycle.ActivateAsync(plugin, revision.Revision, "operator", Ct) : revision;
    }

    private static Dictionary<string, object?> Policy(object? permissions, string[]? egress, string tier = "official", bool required = false)
    {
        var policy = new Dictionary<string, object?> { ["tier"] = tier, ["sandbox"] = "none", ["authentication"] = "ON_INSTALL", ["required"] = required };
        if (permissions is not null) policy["permissions"] = permissions;
        if (egress is not null) policy["egress"] = egress;
        return policy;
    }

    private static PermissionGrants Grants(string[]? platform = null, string[]? data = null, string[]? egress = null, SecretGrant[]? secrets = null, string[]? connectSrc = null) =>
        new(platform ?? [], data ?? [], egress ?? [], secrets ?? [], new UiGrants(connectSrc ?? []));

    private static readonly string[] FullPermissionsPlatform = ["tenant:read", "notify:send"];
    private static PermissionGrants FullGrants() => Grants(FullPermissionsPlatform, ["qian.sample.crm:customer"], FullEgress,
        [new SecretGrant("db/token", "tenant"), new SecretGrant("oss/*", "platform")], ["https://api.example.test"]);

    // 当前租户链上的批准条目（调用方已进入租户上下文）；读之前先验整条链。
    private static async Task<AuditChainEntry[]> ApprovalAudit(LifecycleHost host)
    {
        var audit = host.Runtime.GetContext("audit", ScopeKey.Global).Audit();
        Assert.True((await audit.VerifyAsync(Ct)).Valid);
        return [.. (await audit.ReadAsync(0, 100, cancellationToken: Ct)).Where(entry => entry.Record.CapabilityId == ApprovalAuditCapability)];
    }

    // 批准条目：租户链、主体即批准人、资源同判定资源、通道 api；输入哈希是规范化本次内容的 SHA-256，载荷带组件、版本、渠道与本次、上一次的内容。
    private static void AssertApproval(AuditChainEntry entry, string actorType, string actor, DateTimeOffset approvedAt, string via, string approved, string? previous)
    {
        var record = entry.Record;
        Assert.Equal((TenantA, actorType, actor, (string?)null, ApprovalAuditCapability, "v1", "api", (string?)("lifecycle/install:" + TenantA + "/" + Plugin + "@1.0.0"), "success", approvedAt),
            (record.TenantId, record.ActorType, record.ActorId, record.OnBehalfOf, record.CapabilityId, record.ContractVersion, record.Channel, record.Resource, record.Result, record.OccurredAt));
        Assert.Equal(Sha256(approved), record.InputHash);
        using var current = JsonDocument.Parse(approved);
        using var before = previous is null ? null : JsonDocument.Parse(previous);
        var expected = JsonSerializer.SerializeToElement(new { pluginId = Plugin, version = "1.0.0", via, approved = current.RootElement, previous = before?.RootElement });
        Assert.True(JsonElement.DeepEquals(expected, record.Payload), record.Payload.GetRawText());
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static PostgresException Failure(Exception error)
    {
        for (var current = (Exception?)error; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        Assert.Fail("期望数据库异常：" + error);
        return null!;
    }

    private static string[] Pointers(QianError error) => [.. error.Errors!.Select(field => field.Pointer)];
    private static (string, string?) Pick(TenantPluginInstallView view) => (view.State, view.Version);
    private static (DateTimeOffset, string) Pick(PermissionApprovalView view) => (view.ApprovedAt, view.ApprovedBy);
    private static (string, string?, string?) Pick3(TenantPluginInstallView view) => (view.State, view.Version, view.TargetVersion);
    private static (string, int) PickRun(LifecycleRunView run) => (run.Status, run.Attempts);

    private static async Task<QianError> AssertCode(ErrorCode code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(action);
        Assert.Equal(code.Code, error.Error.Code.Code);
        return error.Error;
    }

    // 第一次开通钩子失败，之后成功；记下开通钩子的调用次数与每次的目标版本。
    private sealed class FlakyProvision : IPluginLifecycleHooks
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        internal List<string> Versions { get; } = [];
        public ValueTask<JsonElement?> InvokeAsync(LifecycleHookContext context, CancellationToken cancellationToken)
        {
            if (context.Hook != LifecycleHooks.TenantProvision) return ValueTask.FromResult<JsonElement?>(null);
            lock (Versions) Versions.Add(context.ToVersion);
            if (Interlocked.Increment(ref _calls) == 1) throw new QianErrorException(new QianError(PlatformErrors.Unavailable));
            return ValueTask.FromResult<JsonElement?>(null);
        }
    }

    // 武装后，前两次带批准记录的保存互相等待（两个事务都已取得快照），之后的保存直接通过；Arrivals 计入全部这类保存。
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Armed { get; set; }
        internal int Arrivals => Volatile.Read(ref _arrivals);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context?.ChangeTracker.Entries<PermissionApprovalRow>().Any() == true && Interlocked.Increment(ref _arrivals) <= 2)
            {
                if (Volatile.Read(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    // 已由身份层校验的主体快照；tenant 为 null 表示平台主体。
    private sealed class TestUser(string subject, string? tenant, PrincipalKind kind = PrincipalKind.User, bool authenticated = true, bool actor = false) : ICurrentUser
    {
        private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public Principal? Principal { get; } = new()
        {
            Issuer = "qian-lifecycle-test", Audience = ["lifecycle"], Subject = subject, Kind = kind, TenantId = tenant, Roles = [], Scope = "lifecycle",
            SessionId = Guid.CreateVersion7().ToString(), IssuedAt = Now, NotBefore = Now, ExpiresAt = Now + 300, TokenId = Guid.CreateVersion7().ToString(),
            Actor = actor ? new DelegationActor { Subject = "delegate", Kind = PrincipalKind.Service } : null
        };
        public string? UserId => subject;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => authenticated;
    }

    // 宿主权限判定端口的替身：只给列出的 (主体, 租户) 批准权限点。
    private sealed class Permissions(params (string Subject, string Tenant)[] grants) : IPermissionChecker
    {
        public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
        {
            var allow = request.Permission == LifecycleDefinition.InstallApprovePermission && grants.Contains((request.Subject.Id, request.Subject.TenantId!));
            return ValueTask.FromResult(new PermissionDecision(allow, allow ? "granted" : "not-granted", []));
        }
        public async ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default)
        {
            var decisions = new List<PermissionDecision>();
            foreach (var request in requests) decisions.Add(await CheckAsync(request, cancellationToken));
            return decisions;
        }
        public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => CheckAsync(request, cancellationToken);
    }

    // Authz 超管旁路的替身：只放行指定主体，并记下请求与判定时的租户上下文（真实旁路在该上下文中写审计）。
    private sealed class RecordingBypass(string superadmin) : ISuperadminBypass
    {
        internal List<(SuperadminAccess Access, string? Context)> Accesses { get; } = [];
        internal Kun.Core.MultiTenancy.ITenantContext? Tenant { get; set; }
        public ValueTask<PermissionDecision> AuthorizeAsync(SuperadminAccess access, CancellationToken cancellationToken = default)
        {
            Accesses.Add((access, Tenant?.TenantId));
            var allow = access.Subject.Id == superadmin;
            return ValueTask.FromResult(new PermissionDecision(allow, allow ? "superadmin_bypass" : "not_superadmin", []));
        }
    }

    private sealed class BypassPlugin(RecordingBypass bypass) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            bypass.Tenant = ctx.Services.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>();
            ctx.Provide<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey, bypass);
            return ValueTask.CompletedTask;
        }
    }
}

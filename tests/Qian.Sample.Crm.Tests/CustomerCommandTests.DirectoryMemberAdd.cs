using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Data;
using Qian.Identity.Server;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Sample.Crm.Tests;

// 租户管理员新增成员（S1-IDP-017）在参考宿主上的验收：真实 Authz（目录、默认角色、RBAC 判定与授予端口）、真实 Audit 与标准能力目录。
// 身份宿主用例（Qian.Identity.Server.Tests 的 DirectoryMemberAddTests）覆盖 HTTP 入口、配额与资格；这里验初始角色、能力契约与审计。
public sealed partial class IdentityAuthzTests
{
    [Fact]
    public async Task 新增成员能力经真实授权与审计执行_新成员获初始角色tenant_user_能力标write与confirm且不对Agent暴露_非管理员与跨租户无写入()
    {
        const string id = "qian.identity.server/directory.member.add";
        const string initial = "Initial-Pass-2026";
        const string MemberCreate = "qian.identity.server:member:create";
        await using var host = await Start(capabilities: true, identity: true);
        // 代码派生的元数据与发布表面一致：权限目录、工具契约与默认角色（member:create 给 tenant.owner / tenant.admin）。
        var metadata = ((IComponentCodeMetadataFactory)Activator.CreateInstance(typeof(IdentityServerComponent).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType)!).Create();
        var contract = Assert.Single(metadata.Capabilities, item => item.Id == id);
        var artifact = Path.Combine(RepositoryRoot(), "plugins/qian.identity.server");
        using var actualPermissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/permissions.json")));
        Assert.True(JsonElement.DeepEquals(metadata.Permissions, actualPermissions.RootElement));
        var tool = Directory.EnumerateFiles(Path.Combine(artifact, "tools"), "*.json").Select(file => JsonElement.Parse(File.ReadAllText(file)))
            .Single(item => item.GetProperty("name").GetString() == contract.Name);
        Assert.Equal(("write", "confirm", true, "pii"), (tool.GetProperty("sideEffect").GetString(), tool.GetProperty("policyHint").GetString(),
            tool.GetProperty("idempotent").GetBoolean(), tool.GetProperty("dataClassification").GetString()));
        Assert.False(tool.GetProperty("agent").GetProperty("expose").GetBoolean()); Assert.True(tool.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean());
        Assert.Equal(MemberCreate, tool.GetProperty("requiredPermission").GetString());
        var input = tool.GetProperty("inputSchema");
        Assert.Contains(input.GetProperty("required").EnumerateArray(), field => field.GetString() == "idempotencyKey");
        Assert.True(input.GetProperty("properties").GetProperty("initialPassword").GetProperty("sensitive").GetBoolean());
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using var actualRoles = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/roles.json")));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(metadata.ComponentId, "1.0.0", metadata.Permissions, actualRoles.RootElement);
        Assert.Equal(["tenant.admin", "tenant.owner"], catalog.Roles.Select(role => role.Role));
        Assert.All(catalog.Roles, role => Assert.Equal(["qian.identity.server:member:create", "qian.identity.server:member:disable"], role.Grants.Order(StringComparer.Ordinal)));
        Assert.Equal("Medium", Assert.Single(catalog.Permissions, item => item.Code == MemberCreate).Risk.ToString());
        using var registration = host.App.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var (admin, adminPhone) = await NewPerson(host, "目录管理员"); await Join(host, A, admin.Id, "管理员", administrator: true);
        var (member, memberPhone) = await NewPerson(host, "普通成员"); await Join(host, A, member.Id, "普通成员");
        var (outsider, _) = await NewPerson(host, "乙方老员工"); await Join(host, B, outsider.Id, "乙方老员工");
        using var tenantScope = tenant.BeginScope(A);
        var roles = new PostgresTenantRoleStore(data, tenant);
        await roles.InstallDefaultsAsync(metadata.ComponentId, "1.0.0", null, "member-add-acceptance", Ct);
        await roles.AssignAsync("tenant.admin", "user", admin.Id.ToString("D"), "member-add-acceptance", Ct);
        // 对照：普通成员虽经真实 RBAC 持有 member:create，仍不能越过 Identity 的租户管理员守卫。
        await roles.AssignAsync("tenant.admin", "user", member.Id.ToString("D"), "member-add-acceptance", Ct);
        var administrator = await DirectoryUser(host, adminPhone, A); var nonAdministrator = await DirectoryUser(host, memberPhone, A);
        var loader = host.Identity.Loader!; var caller = loader.GetContext("capabilities", ScopeKey.Global).Scoped(new ScopeKey("tenant", A));
        var service = caller.Capabilities(); var binding = caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, id);
        Assert.True(JsonElement.DeepEquals(contract.Metadata, binding.Contract.Metadata));
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/capability-entry.schema.json")), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        var permissions = host.App.Services.GetRequiredService<IPermissionChecker>();
        async Task<bool> Allowed(Guid person, string permission) =>
            (await permissions.CheckAsync(new PermissionRequest(new AuthorizationSubject(person.ToString("D"), A, "user"), permission), Ct)).Allow;
        async Task<string> Roles(Guid person, string tenantId) => await Text("SELECT coalesce(string_agg(r.code, ',' ORDER BY r.code), '') FROM plugin_qian_authz.role_assignments a " +
            $"JOIN plugin_qian_authz.roles r ON a.role_id = r.id WHERE a.tenant_id = '{tenantId}' AND a.subject_type = 'user' AND a.subject_id = '{person:D}'") ?? "";
        async Task<string> Persons(string phone) => await Text($"SELECT count(*) FROM plugin_qian_identity.person_identifiers WHERE value = '+86{phone}'") ?? "";
        await using var scope = host.App.Services.CreateAsyncScope();
        var accessor = host.App.Services.GetRequiredService<IHttpContextAccessor>(); var before = accessor.HttpContext;
        var current = new DirectoryCurrentUser(tenant, administrator.Principal!);
        accessor.HttpContext = new DefaultHttpContext { RequestServices = new DirectoryInvocationServices(scope.ServiceProvider, current) };
        try
        {
            var entry = Assert.Single((await service.ListAsync(caller, current, cancellationToken: Ct)).Items, item => item.GetProperty("id").GetString() == id);
            var check = schema.Evaluate(entry, new EvaluationOptions { OutputFormat = OutputFormat.List }); Assert.True(check.IsValid, JsonSerializer.Serialize(check));
            var phone = NewPhone(); var key = Guid.CreateVersion7();
            var request = JsonSerializer.SerializeToElement(new { idempotencyKey = key, identifier = phone, displayName = "新同事", initialPassword = initial });
            // 非管理员（有权限点）与跨租户作用域：被拒，不建自然人与成员，不写审计。
            current.Snapshot = nonAdministrator.Principal!;
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, request, Ct).AsTask())).Error.Code.Code);
            current.Snapshot = administrator.Principal!;
            Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller.Scoped(new ScopeKey("tenant", B)), request, Ct).AsTask())).Error.Code.Code);
            Assert.Equal("0", await Persons(phone));
            Assert.DoesNotContain((await caller.Audit().ReadAsync(0, 200, cancellationToken: Ct)), row => row.Record.CapabilityId == id);
            // 管理员经标准入口新增：新建自然人，初始角色 tenant.user 已经授予——有参考应用的 customer:read，没有 customer:write。
            var first = await service.InvokeAsync(caller, current, id, request, key, Ct);
            Assert.False(first.Replayed);
            Assert.True(first.Output.GetProperty("personCreated").GetBoolean());
            Assert.Equal(["tenant.user"], first.Output.GetProperty("grantedRoles").EnumerateArray().Select(role => role.GetString()));
            var newPerson = first.Output.GetProperty("personId").GetGuid(); var newMember = first.Output.GetProperty("membershipId").GetGuid();
            Assert.Equal("tenant.user", await Roles(newPerson, A));
            Assert.True(await Allowed(newPerson, "qian.sample.crm:customer:read")); Assert.False(await Allowed(newPerson, CustomerWrite));
            Assert.False(await Allowed(newPerson, MemberCreate));
            // 能力标 pii：回执只记「已执行」不存响应体（组件规格 §9.5），同键同输入得到 409 CAPABILITY_REPLAY_UNAVAILABLE、不再执行；改输入 409 CONFLICT。
            var replay = await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id, request, key, Ct).AsTask());
            Assert.Equal(("CAPABILITY_REPLAY_UNAVAILABLE", "data_classification"), (replay.Error.Code.Code, replay.Error.Args["reason"]));
            Assert.Equal("1", await Persons(phone));
            var changed = JsonSerializer.SerializeToElement(new { idempotencyKey = key, identifier = NewPhone(), displayName = "另一个人", initialPassword = initial });
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id, changed, key, Ct).AsTask())).Error.Code.Code);
            // 绑定乙方的老员工：不新建自然人，同样授予 tenant.user（只在甲方），他在乙方的角色不受影响。
            var outsiderPhone = await Text($"SELECT value FROM plugin_qian_identity.person_identifiers WHERE person_id = '{outsider.Id:D}'");
            var bound = await service.InvokeAsync(caller, current, id, JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), identifier = outsiderPhone, displayName = "老员工（甲方）" }), null, Ct);
            Assert.False(bound.Output.GetProperty("personCreated").GetBoolean()); Assert.Equal(outsider.Id, bound.Output.GetProperty("personId").GetGuid());
            Assert.Equal(("tenant.user", ""), (await Roles(outsider.Id, A), await Roles(outsider.Id, B)));
            // 重复新增：结构化冲突，错误位置换成能力输入的字段；什么都不写。
            var duplicate = await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id,
                JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), identifier = phone, displayName = "再加一次" }), null, Ct).AsTask());
            Assert.Equal(("DIRECTORY_MEMBER_EXISTS", "#/identifier"), (duplicate.Error.Code.Code, duplicate.Error.Pointer));
            var missing = await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id,
                JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), identifier = NewPhone(), displayName = "缺密码" }), null, Ct).AsTask());
            Assert.Equal(("INPUT_INVALID", "#/initialPassword"), (missing.Error.Code.Code, Assert.Single(missing.Error.Errors).Pointer));
            // 审计进甲方的链：操作人是管理员，成员在载荷里；成功两次（新建、绑定）各有开始与成功记录，失败一次有开始与失败记录；任何记录都不含初始密码。
            var records = (await caller.Audit().ReadAsync(0, 200, cancellationToken: Ct)).Where(row => row.Record.CapabilityId == id).Select(row => row.Record).ToArray();
            Assert.All(records, record => Assert.Equal((A, "user", admin.Id.ToString("D"), "api"), (record.TenantId!, record.ActorType, record.ActorId, record.Channel)));
            var successes = records.Where(record => record.Result == "success").ToArray();
            Assert.Equal(2, successes.Length);
            Assert.Contains(successes, record => record.Payload.GetProperty("membershipId").GetGuid() == newMember && record.Payload.GetProperty("personId").GetGuid() == newPerson &&
                record.Payload.GetProperty("personCreated").GetBoolean());
            Assert.Equal(4, records.Count(record => record.Result == "started"));
            Assert.Equal(2, records.Count(record => record.Result == "failure"));
            Assert.DoesNotContain(initial, JsonSerializer.Serialize(records.Select(record => new { record.Payload, record.InputHash })), StringComparison.Ordinal);
        }
        finally { accessor.HttpContext = before; }
    }
}

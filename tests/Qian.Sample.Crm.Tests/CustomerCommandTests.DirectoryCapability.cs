using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Sample.Crm.Tests;

public sealed partial class IdentityAuthzTests
{
    [Fact]
    public async Task 目录禁用标准生成能力经真实授权与PG回执执行_重放不重新吊销_绑定不能绕过管理员与会话()
    {
        const string id = "qian.identity.server/directory.member.disable";
        await using var host = await Start(capabilities: true, identity: true);
        var metadata = ((IComponentCodeMetadataFactory)Activator.CreateInstance(typeof(IdentityServerComponent).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType)!).Create();
        var contract = Assert.Single(metadata.Capabilities, item => item.Id == id);
        var artifact = Path.Combine(RepositoryRoot(), "plugins/qian.identity.server");
        using var actualPermissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/permissions.json")));
        Assert.True(JsonElement.DeepEquals(metadata.Permissions, actualPermissions.RootElement));
        var tool = Directory.EnumerateFiles(Path.Combine(artifact, "tools"), "*.json").Select(file => JsonElement.Parse(File.ReadAllText(file)))
            .Single(item => item.GetProperty("name").GetString() == contract.Name);
        Assert.Equal(("write", "confirm", true), (tool.GetProperty("sideEffect").GetString(), tool.GetProperty("policyHint").GetString(), tool.GetProperty("idempotent").GetBoolean()));
        Assert.True(tool.GetProperty("agent").GetProperty("expose").GetBoolean()); Assert.True(tool.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean());
        Assert.Contains(tool.GetProperty("inputSchema").GetProperty("required").EnumerateArray(), field => field.GetString() == "idempotencyKey");
        Assert.DoesNotContain(metadata.Capabilities, item => item.Name.Contains("purge", StringComparison.Ordinal));

        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var permissions = host.App.Services.GetRequiredService<IPermissionRegistry>();
        using var actualRoles = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/roles.json")));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(metadata.ComponentId, "1.0.0", metadata.Permissions, actualRoles.RootElement);
        Assert.Equal(["tenant.admin", "tenant.owner"], catalog.Roles.Select(role => role.Role));
        Assert.All(catalog.Roles, role => Assert.Equal(["qian.identity.server:member:disable"], role.Grants));
        using var registration = permissions.Register(catalog.Version, catalog.Permissions);
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var (admin, adminPhone) = await NewPerson(host, "目录管理员"); var adminMember = await Join(host, A, admin.Id, "管理员", administrator: true);
        var (member, memberPhone) = await NewPerson(host, "普通成员"); var regular = await Join(host, A, member.Id, "普通成员");
        var (target, targetPhone) = await NewPerson(host, "禁用目标"); var targetMember = await Join(host, A, target.Id, "禁用目标");
        using var tenantScope = tenant.BeginScope(A);
        var roles = new PostgresTenantRoleStore(data, tenant);
        await roles.InstallDefaultsAsync(metadata.ComponentId, "1.0.0", null, "directory-acceptance", Ct);
        await roles.AssignAsync("tenant.admin", "user", admin.Id.ToString("D"), "directory-acceptance", Ct);
        // 对照：普通成员虽经真实 RBAC 授权，仍不能越过 Identity 的租户管理员守卫。
        await roles.AssignAsync("tenant.admin", "user", member.Id.ToString("D"), "directory-acceptance", Ct);
        var administrator = await DirectoryUser(host, adminPhone, A);
        var nonAdministrator = await DirectoryUser(host, memberPhone, A);
        _ = await DirectoryUser(host, targetPhone, A);
        var loader = host.Identity.Loader!; var caller = loader.GetContext("capabilities", ScopeKey.Global).Scoped(new ScopeKey("tenant", A));
        var service = caller.Capabilities(); var binding = caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, id);
        Assert.Equal(id, binding.Contract.Id);
        Assert.True(JsonElement.DeepEquals(contract.Metadata, binding.Contract.Metadata));
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/capability-entry.schema.json")), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        await using var scope = host.App.Services.CreateAsyncScope();
        var accessor = host.App.Services.GetRequiredService<IHttpContextAccessor>(); var before = accessor.HttpContext;
        var current = new DirectoryCurrentUser(tenant, administrator.Principal!);
        accessor.HttpContext = new DefaultHttpContext { RequestServices = new DirectoryInvocationServices(scope.ServiceProvider, current) };
        try
        {
            var entry = Assert.Single((await service.ListAsync(caller, current, cancellationToken: Ct)).Items, item => item.GetProperty("id").GetString() == id);
            var check = schema.Evaluate(entry, new EvaluationOptions { OutputFormat = OutputFormat.List }); Assert.True(check.IsValid, JsonSerializer.Serialize(check));
            var key = Guid.CreateVersion7(); var input = JsonSerializer.SerializeToElement(new { idempotencyKey = key, membershipIds = new[] { adminMember.Id, targetMember.Id }, reason = "private-reason-sentinel" });
            current.Snapshot = nonAdministrator.Principal!;
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
            current.Snapshot = administrator.Principal!;
            Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller.Scoped(new ScopeKey("tenant", B)), input, Ct).AsTask())).Error.Code.Code);
            // 已认证主体快照里的会话必须确实属于此人，不能仅凭几个已知 UUID 通过身份校验。
            var forged = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(administrator.Principal, IdentityJson.PrincipalTypeInfo))!;
            forged["session_id"] = nonAdministrator.Principal.SessionId;
            current.Snapshot = forged.Deserialize(IdentityJson.PrincipalTypeInfo)!;
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
            current.Snapshot = administrator.Principal!;
            var smuggled = JsonSerializer.SerializeToElement(new { idempotencyKey = key, membershipIds = new[] { targetMember.Id }, reason = "test", tenantId = B, sessionId = administrator.Principal.SessionId });
            Assert.Equal("INPUT_INVALID", (await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id, smuggled, key, Ct).AsTask())).Error.Code.Code);
            Assert.Empty(await AdminRows("SELECT idempotency_key FROM plugin_qian_capabilities.invoke_receipts WHERE capability_id = '" + id + "'"));
            var first = await service.InvokeAsync(caller, current, id, input, key, Ct);
            Assert.False(first.Replayed); Assert.Equal("PARTIAL_SUCCESS", first.Output.GetProperty("status").GetString());
            Assert.Equal(2, first.Output.GetProperty("items").GetArrayLength());
            Assert.Equal(AccountStatuses.Disabled, (await loader.GetContext("identity", ScopeKey.Global).Identity().GetMembershipAsync(targetMember.Id, Ct))!.Status);
            // 别的有效管理操作把目标重新启用后，原幂等键仍回放首次结果，不能再次禁用或多发吊销事件。
            await host.Identity.Accounts.SetMembershipStatusAsync(targetMember.Id, AccountStatuses.Active, "reactivate", Ct);
            var replay = await service.InvokeAsync(caller, current, id, input, key, Ct);
            Assert.True(replay.Replayed); Assert.True(JsonElement.DeepEquals(first.Output, replay.Output));
            Assert.Equal(AccountStatuses.Active, (await loader.GetContext("identity", ScopeKey.Global).Identity().GetMembershipAsync(targetMember.Id, Ct))!.Status);
            var changed = JsonSerializer.SerializeToElement(new { idempotencyKey = key, membershipIds = new[] { regular.Id }, reason = "changed" });
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, current, id, changed, key, Ct).AsTask())).Error.Code.Code);
            Assert.Single(await AdminRows("SELECT idempotency_key FROM plugin_qian_capabilities.invoke_receipts WHERE capability_id = '" + id + "'"));
            var records = (await caller.Audit().ReadAsync(0, 100, cancellationToken: Ct)).Where(row => row.Record.CapabilityId == id).ToArray();
            Assert.Equal(2, records.Length); Assert.DoesNotContain("private-reason-sentinel", JsonSerializer.Serialize(records), StringComparison.Ordinal);
            await WaitDirectoryRevocation();
            Assert.Single(await AdminRows("SELECT id FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.session_revoked.v1'"));
            // 即使可信宿主仍持旧快照，已被服务器撤销的管理员会话也不能再次执行新键。
            await loader.GetContext("identity", ScopeKey.Global).Identity().RevokeSessionsAsync(new SessionRevocation(admin.Id, Guid.Parse(administrator.Principal!.SessionId), "test_revoke", Guid.NewGuid().ToString()), Ct);
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
            await loader.UnmountAsync("identity", ScopeKey.Global, Ct);
            Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
        }
        finally { accessor.HttpContext = before; }

        async Task WaitDirectoryRevocation()
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while ((await AdminRows("SELECT id FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.session_revoked.v1'")).Count == 0)
            { Assert.True(DateTimeOffset.UtcNow < deadline, "成员吊销旁路未写入同租户 Outbox"); await Task.Delay(50, Ct); }
        }
    }
}

public abstract partial class CustomerCommandTests
{
    // 先以真实密码进入生产 SignInPipeline；这里只把认证结果接入测试宿主的 ICurrentUser，未从请求体生成身份。
    private protected static async Task<DirectoryCurrentUser> DirectoryUser(RunningApp host, string phone, string tenant)
    {
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(IdentityPassword));
        var login = await host.Identity.SignIn.SignInAsync(new LoginInput("web", Guid.Parse(tenant), null, null, proof) { Identifier = phone }, new LoginContext(null, null), Ct);
        Assert.True(login.Succeeded, login.Error?.Code.Code); var session = login.Session!; var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new DirectoryCurrentUser(host.App.Services.GetRequiredService<ITenantContext>(), new Principal
        {
            Issuer = host.Identity.Issuer, Audience = [IdentityResource], Subject = session.PersonId.ToString("D"), Kind = PrincipalKind.User,
            TenantId = session.TenantId!.Value.ToString("D"), MembershipId = session.MembershipId!.Value.ToString("D"), IdentityRevision = session.IdentityRevision,
            Roles = [], Scope = "qian.api", SessionId = session.SessionId.ToString("D"), TokenId = Guid.NewGuid().ToString("D"), IssuedAt = now, NotBefore = now, ExpiresAt = now + 60
        });
    }
    private protected sealed class DirectoryCurrentUser(ITenantContext tenant, Principal principal) : ICurrentUser
    {
        internal Principal Snapshot { get; set; } = principal;
        public Principal Principal => Snapshot;
        public bool IsAuthenticated => tenant.TenantId == Snapshot.TenantId;
        public string UserId => Snapshot.Subject;
        public IReadOnlyList<string> Roles => Snapshot.Roles;
    }
    private protected sealed class DirectoryInvocationServices(IServiceProvider services, ICurrentUser user) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ICurrentUser) ? user : services.GetService(serviceType);
    }
}

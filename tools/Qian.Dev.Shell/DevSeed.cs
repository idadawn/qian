using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Lifecycle;
using Qian.Runtime;
using Qian.Tenancy;

namespace Qian.Dev.Shell;

internal sealed record SeedPerson(string Key, string DisplayName, string Username, string Password, bool Platform);
internal sealed record SeedMember(string Person, string Role, string DisplayName);
internal sealed record SeedTenant(string Code, string Name, string Administrator, IReadOnlyList<SeedMember> Members);
internal sealed record SeedFile(IReadOnlyList<SeedPerson> Persons, IReadOnlyList<SeedTenant> Tenants);
internal sealed record SeededTenant(string Code, string Name, Guid Id);

// 种子：自然人经身份账号端口创建（用户名 + 初始密码，不要求首登改密）；租户经真实开通编排创建——第 03 步经 Lifecycle 为租户开通 system 组件（含参考应用），
// 第 05 步经身份的成员开户端口建管理员成员，第 07 步授予 tenant.owner；随后激活。开通只登记安装并执行 tenant-provision 钩子、不挂载（与 Lifecycle 验收一致），
// 所以种子再把参考应用条目挂进租户 scope（与运营「挂载组件」等价），能力目录与菜单才可见。第二租户的额外成员与角色由种子直接补齐（与真实运营「邀请成员」等价）。
internal static class DevSeed
{
    private const string Actor = "dev-bootstrap";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    internal static SeedFile Load(string path)
    {
        var seed = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), Json) ?? throw new InvalidOperationException("种子文件为空：" + path);
        if (seed.Persons.Count == 0 || seed.Tenants.Count == 0) throw new InvalidOperationException("种子文件至少要有一个自然人与一个租户：" + path);
        foreach (var tenant in seed.Tenants)
            if (!seed.Persons.Any(person => person.Key == tenant.Administrator) || tenant.Members.Any(member => !seed.Persons.Any(person => person.Key == member.Person)))
                throw new InvalidOperationException($"租户 {tenant.Code} 引用了不存在的自然人。");
        return seed;
    }

    internal static async Task<IReadOnlyList<SeededTenant>> ApplyAsync(SeedFile seed, PluginLoader loader, IServiceProvider services, CancellationToken ct)
    {
        var accounts = loader.GetContext("identity", ScopeKey.Global).IdentityAccounts();
        var tenancy = loader.GetContext("tenancy", ScopeKey.Global);
        var provisioning = tenancy.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        var lifecycle = tenancy.Inject<ITenantLifecycle>(TenantLifecycle.Key);
        var components = loader.GetContext("lifecycle", ScopeKey.Global).Lifecycle();
        var tenantContext = services.GetRequiredService<ITenantContext>();
        var persons = new Dictionary<string, Person>(StringComparer.Ordinal);
        foreach (var item in seed.Persons)
        {
            using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(item.Password));
            persons[item.Key] = await accounts.CreatePersonAsync(new CreatePerson(item.DisplayName, [new IdentifierInput(IdentifierKind.Username, item.Username, true)])
                { InitialPassword = proof, RequirePasswordChange = false, PlatformAccess = item.Platform }, ct).ConfigureAwait(false);
        }
        var result = new List<SeededTenant>();
        foreach (var tenant in seed.Tenants)
        {
            var administrator = persons[tenant.Administrator];
            var descriptor = await provisioning.ProvisionAsync(new CreateTenant(tenant.Code, tenant.Name), new TenantAdministrator(Guid.CreateVersion7(), administrator.Id.ToString("D")), Actor, ct).ConfigureAwait(false);
            var steps = await provisioning.GetStepsAsync(descriptor.Id, ct).ConfigureAwait(false);
            var failed = steps.Where(step => step.Status == "failed").ToList();
            if (failed.Count > 0) throw new InvalidOperationException($"租户 {tenant.Code} 开通失败：" + string.Join("；", failed.Select(step => step.Step + " " + step.Reason)));
            await lifecycle.ActivateAsync(descriptor.Id, new ActivateTenant(), Actor, ct).ConfigureAwait(false);
            var id = descriptor.Id.ToString("D");
            var mounted = await components.MountAsync(new ScopeKey("tenant", id), ReferenceEntry, Actor, ct).ConfigureAwait(false);
            if (mounted.State != MountStates.Ready) throw new InvalidOperationException($"租户 {tenant.Code} 挂载参考应用失败：{mounted.ReasonCode}");
            using (tenantContext.BeginScope(id))
            {
                var grants = tenancy.Scoped(new ScopeKey("tenant", id)).Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
                foreach (var member in tenant.Members)
                {
                    var person = persons[member.Person];
                    await accounts.CreateMembershipAsync(new CreateMembership(person.Id, member.DisplayName), ct).ConfigureAwait(false);
                    await grants.AssignAsync(member.Role, "user", person.Id.ToString("D"), Actor, ct).ConfigureAwait(false);
                }
            }
            result.Add(new SeededTenant(tenant.Code, tenant.Name, descriptor.Id));
        }
        return result;
    }
    // 与 Lifecycle 验收里的租户条目同形：条目 id crm，引用已激活的参考应用修订；Registry 按组件名解析到带菜单清单的定义（DevPlatform.Resolve）。
    private static JsonElement ReferenceEntry => JsonSerializer.SerializeToElement(new { id = "crm", plugin = "qian.sample.crm@1.0.0", config = new { } });
}

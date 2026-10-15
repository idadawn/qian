using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Data;
using Qian.Events;
using Qian.Lifecycle;
using Qian.Lifecycle.AspNetCore;
using Qian.Plugin.Sdk;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;
using Qian.Audit;
using Npgsql;

namespace Qian.Sample.Crm.Tests;

// S1-RT-005 验收 1、2、4、5 的端到端部分：参考应用 qian.sample.crm 不挂全局，只由 qian.lifecycle 经真实 HTTP 宿主装进租户 scope。
// 平台宿主 = Data + Tenancy + Authz + Audit + Registry + Capabilities（契约准入）+ Config（信任根经 K8s Secret 挂载）+ Lifecycle；
// 业务请求经 SDK 插件端点门禁与 CRM 命令处理器，管理面诊断经 GET /api/lifecycle/diagnostics。网关与外壳两处拦截归 S1-ENT-002。
public sealed partial class CustomerCommandTests
{
    private static readonly EventOrigin Operator = new("qian.lifecycle", EventActor.User("platform-operator")) { CorrelationId = Guid.CreateVersion7() };

    [Fact]
    public async Task 参考应用在A和B分别开通并挂载_各一条成功的开通记录_重复开通无副作用_卸载后五秒内拦截恢复且不改安装状态()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        using (var before = await host.Get(A, "/api/customers")) Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        using (EventEnvelopeScope.Begin(Operator))
        {
            Assert.Equal(1, (await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "operator", Ct)).Revision);
            Assert.Equal(PluginRevisionStates.Active, (await lifecycle.ActivateAsync("qian.sample.crm", 1, "operator", Ct)).State);
            foreach (var tenant in new[] { A, B })
            {
                // 未授权的租户不能开通；授权后开通执行一次 tenant-provision 钩子，开通不挂载。
                await AssertCode(LifecycleErrors.NotEntitled, () => lifecycle.ProvisionAsync(tenant, "qian.sample.crm", "operator", Ct).AsTask());
                await lifecycle.EntitleAsync(tenant, "qian.sample.crm", "operator", Ct);
                Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick(await lifecycle.ProvisionAsync(tenant, "qian.sample.crm", "operator", Ct)));
                using (var unmounted = await host.Get(tenant, "/api/customers")) Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
                var mounted = await lifecycle.MountAsync(new ScopeKey("tenant", tenant), Entry("1.0.0"), "operator", Ct);
                Assert.Equal((MountStates.Ready, 1, (string?)null), (mounted.State, mounted.Revision, mounted.ReasonCode));
            }
        }
        foreach (var tenant in new[] { A, B })
        {
            var page = await WithinFiveSeconds(host, tenant, HttpStatusCode.OK);
            // 开通钩子种下的示例客户以幂等键为主键：每个租户一条，两租户互不可见。
            var seeded = Assert.Single(page!.Items);
            Assert.Equal((CrmPlugin.SampleCustomer, LifecycleController.IdempotencyKey(tenant, "qian.sample.crm", LifecycleHooks.TenantProvision, "1.0.0")), (seeded.Name, seeded.Id));
            var run = Assert.Single(await lifecycle.GetRunsAsync(tenant, "qian.sample.crm", Ct));
            Assert.Equal((LifecycleHooks.TenantProvision, LifecycleRunStates.Succeeded, 1, "1.0.0", (string?)null), (run.Hook, run.Status, run.Attempts, run.ToVersion, run.FromVersion));
        }
        Assert.Equal("{\"seeded\": true}", await Text($"SELECT result::text FROM plugin_qian_lifecycle.lifecycle_runs WHERE tenant_id = '{A}'"));

        // 对 A 重复开通：直接返回，不认领执行记录、不调钩子、不写任何行。
        var installBefore = await InstallRow(A);
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick(await lifecycle.ProvisionAsync(A, "qian.sample.crm", "operator", Ct)));
        Assert.Equal(installBefore, await InstallRow(A));
        var repeated = Assert.Single(await lifecycle.GetRunsAsync(A, "qian.sample.crm", Ct));
        Assert.Equal((1, LifecycleRunStates.Succeeded), (repeated.Attempts, repeated.Status));
        Assert.Single((await (await host.Get(A, "/api/customers")).Content.ReadFromJsonAsync<CustomerPage>(Ct))!.Items);

        // 验收 4（Lifecycle 层）：卸载写墓碑并随事务发 unmounted 事件，5 秒内 SDK 拦截恢复；安装记录不变，B 不受影响，再次挂载数据仍在。
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal((MountStates.Unmounted, 2), Pick(await lifecycle.UnmountAsync(new ScopeKey("tenant", A), "crm", "operator", Ct)));
        await WithinFiveSeconds(host, A, HttpStatusCode.NotFound);
        using (var other = await host.Get(B, "/api/customers")) Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(installBefore, await InstallRow(A));
        Assert.Equal("True", await Text($"SELECT is_deleted FROM plugin_qian_registry.scope_patches WHERE tenant_id = '{A}' AND entry_id = 'crm'"));
        Assert.Equal([RegistryEvents.ScopeMounted, RegistryEvents.ScopeUnmounted], await EventTypes("plugin_qian_registry", A));
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(MountStates.Ready, (await lifecycle.MountAsync(new ScopeKey("tenant", A), Entry("1.0.0"), "operator", Ct)).State);
        Assert.Single((await WithinFiveSeconds(host, A, HttpStatusCode.OK))!.Items);
    }

    [Fact]
    public async Task 发布修订n加1并激活_A升级B保持n_A回切到n后请求由n处理且升级期间写入的数据可读()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        await ProvisionAndMount(platform, host, A, B);

        using (EventEnvelopeScope.Begin(Operator))
        {
            Assert.Equal(2, (await lifecycle.InstallAsync(platform.Artifacts["1.0.1"], "operator", Ct)).Revision);
            Assert.Equal(PluginRevisionStates.Active, (await lifecycle.ActivateAsync("qian.sample.crm", 2, "operator", Ct)).State);
            Assert.Equal(PluginRevisionStates.Installed, (await lifecycle.GetRevisionsAsync("qian.sample.crm", Ct))[0].State);
            Assert.Equal((TenantInstallStates.Installed, "1.0.1"), Pick(await lifecycle.UpgradeAsync(A, "qian.sample.crm", "1.0.0", "1.0.1", "operator", Ct)));
        }
        // A 的条目改写为 n+1 并合入：A 的请求由 1.0.1 的组件处理（菜单来自 1.0.1 的 ui/manifest.json），B 仍是 1.0.0。
        Assert.Equal("1.0.1", await ServingVersion(host, A));
        Assert.Equal("1.0.0", await ServingVersion(host, B));
        Assert.Equal("租户专属客户", Assert.Single(await Menus(host, A), item => item.Route == "/customers").Title);
        Assert.Equal("客户", Assert.Single(await Menus(host, B), item => item.Route == "/customers").Title);
        Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick((await lifecycle.GetInstallAsync(B, "qian.sample.crm", Ct))!));
        var upgrade = Assert.Single(await lifecycle.GetRunsAsync(A, "qian.sample.crm", Ct), run => run.Hook == LifecycleHooks.TenantUpgrade);
        Assert.Equal(("1.0.0", "1.0.1", LifecycleRunStates.Succeeded), (upgrade.FromVersion, upgrade.ToVersion, upgrade.Status));
        Assert.DoesNotContain(await lifecycle.GetRunsAsync(B, "qian.sample.crm", Ct), run => run.Hook == LifecycleHooks.TenantUpgrade);

        // n+1 下写入的客户，回切到 n 后仍由 n 读出（表结构前向兼容）。
        using (var created = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "升级期间的客户")))
            Assert.True(created.StatusCode == HttpStatusCode.Created, host.FailureEvidence + await created.Content.ReadAsStringAsync(Ct));
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick(await lifecycle.UpgradeAsync(A, "qian.sample.crm", "1.0.1", "1.0.0", "operator", Ct)));
        Assert.Equal("1.0.0", await ServingVersion(host, A));
        Assert.Equal("客户", Assert.Single(await Menus(host, A), item => item.Route == "/customers").Title);
        var page = (await (await host.Get(A, "/api/customers")).Content.ReadFromJsonAsync<CustomerPage>(Ct))!;
        Assert.Equal(["升级期间的客户", CrmPlugin.SampleCustomer], page.Items.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("qian.sample.crm@1.0.0", await Text($"SELECT plugin || '@' || plugin_version FROM plugin_qian_registry.scope_patches WHERE tenant_id = '{A}' AND entry_id = 'crm'"));

        // 平台级回切：Active 回到修订 1；已开通租户不受影响。两类事件各随状态写入同一事务进入 Outbox。
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(PluginRevisionStates.Active, (await lifecycle.RollbackAsync("qian.sample.crm", 1, "operator", Ct)).State);
        Assert.Equal([LifecycleEvents.PluginActivated, LifecycleEvents.PluginActivated, LifecycleEvents.PluginActivated], await EventTypes("plugin_qian_lifecycle", null));
        Assert.Equal([LifecycleEvents.TenantPluginUpgraded, LifecycleEvents.TenantPluginUpgraded], await EventTypes("plugin_qian_lifecycle", A));
        Assert.Empty(await EventTypes("plugin_qian_lifecycle", B));
        Assert.Equal("qian.sample.crm|1.0.1|1.0.0|platform-operator", await Text("SELECT concat_ws('|', payload::jsonb #>> '{payload,pluginId}', payload::jsonb #>> '{payload,fromVersion}', " +
            $"payload::jsonb #>> '{{payload,toVersion}}', payload::jsonb #>> '{{actor,id}}') FROM plugin_qian_lifecycle.outbox_messages WHERE tenant_id = '{A}' ORDER BY occurred_at DESC LIMIT 1"));
    }

    [Fact]
    public async Task 破坏性契约的挂载为Failed_条目撤回且原因进入诊断端点_Reconciler同样拦截_升级失败留在原版本()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        var scopeA = new ScopeKey("tenant", A);
        using (EventEnvelopeScope.Begin(Operator))
        {
            Assert.Equal(1, (await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "operator", Ct)).Revision);
            // 1.1.0 删除了全部 HTTP 路径：安装只验签与静态校验，契约兼容性在挂载时由平台契约准入判定（R76）。
            Assert.Equal(2, (await lifecycle.InstallAsync(platform.Artifacts["1.1.0"], "operator", Ct)).Revision);
            Assert.Equal(PluginRevisionStates.Active, (await lifecycle.ActivateAsync("qian.sample.crm", 2, "operator", Ct)).State);
            await lifecycle.EntitleAsync(A, "qian.sample.crm", "operator", Ct);
            Assert.Equal((TenantInstallStates.Installed, "1.1.0"), Pick(await lifecycle.ProvisionAsync(A, "qian.sample.crm", "operator", Ct)));
            var failed = await lifecycle.MountAsync(scopeA, Entry("1.1.0"), "operator", Ct);
            Assert.Equal((MountStates.Failed, PlatformErrors.PreconditionFailed.Code, 1), (failed.State, failed.ReasonCode, failed.Revision));
        }
        // 撤回：本 scope 没有残留条目，业务表面仍是 404；诊断端点给出条目、组件修订与原因码。
        await WithinFiveSeconds(host, A, HttpStatusCode.NotFound);
        Assert.Equal("True", await Text($"SELECT is_deleted FROM plugin_qian_registry.scope_patches WHERE tenant_id = '{A}' AND entry_id = 'crm'"));
        var recorded = Assert.Single(await Diagnostics(host, A));
        Assert.Equal(("tenant:" + A, "crm", "qian.sample.crm@1.1.0", 1, PlatformErrors.PreconditionFailed.Code),
            (recorded.Scope, recorded.EntryId, recorded.Plugin, recorded.Revision, recorded.ReasonCode));
        // 没有诊断权限（B 的调用者只是使用者）或跨租户：与不存在同为 404。
        using (var denied = await DiagnosticsRequest(host, B, null)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var crossTenant = await DiagnosticsRequest(host, A, "tenant:" + B)) Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);

        // Registry scope Reconciler 同一包装：绕过 Lifecycle 直接写入的破坏性条目同样装配失败，scope 收紧，诊断给出当前失败。
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var registry = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("registry", ScopeKey.Global).Registry();
        using (tenant.BeginScope(A))
        using (EventEnvelopeScope.Begin(Operator))
            await registry.MountAsync(scopeA, JsonSerializer.SerializeToElement(new { id = "crm-direct", plugin = "qian.sample.crm@1.1.0", config = new { } }), 0, "operator", Ct);
        var current = await Diagnostics(host, A);
        Assert.Contains(current, item => item is { EntryId: "crm-direct", Plugin: "qian.sample.crm@1.1.0", ReasonCode: "PRECONDITION_FAILED" });
        Assert.Contains(current, item => item.EntryId == "crm");
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(MountStates.Unmounted, (await lifecycle.UnmountAsync(scopeA, "crm-direct", "operator", Ct)).State);

        // 平台回切到修订 1，A 回到 1.0.0 后挂载成功：同一条目的旧诊断随之清除。
        using (EventEnvelopeScope.Begin(Operator))
        {
            await lifecycle.RollbackAsync("qian.sample.crm", 1, "operator", Ct);
            Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick(await lifecycle.UpgradeAsync(A, "qian.sample.crm", "1.1.0", "1.0.0", "operator", Ct)));
            Assert.Equal(MountStates.Ready, (await lifecycle.MountAsync(scopeA, Entry("1.0.0"), "operator", Ct)).State);
        }
        Assert.Single((await WithinFiveSeconds(host, A, HttpStatusCode.OK))!.Items);
        Assert.Empty(await Diagnostics(host, A));

        // 升级路径：改写到 1.1.0 后装配失败，条目整体改回 1.0.0，租户留在原版本、请求不中断，原因进入诊断。
        using (EventEnvelopeScope.Begin(Operator))
        {
            var error = await Assert.ThrowsAsync<QianErrorException>(() => lifecycle.UpgradeAsync(A, "qian.sample.crm", "1.0.0", "1.1.0", "operator", Ct).AsTask());
            Assert.Equal((LifecycleErrors.MountFailed.Code, "crm", PlatformErrors.PreconditionFailed.Code), (error.Error.Code.Code, error.Error.Args["entryId"], error.Error.Args["reasonCode"]));
        }
        Assert.Equal((TenantInstallStates.Installed, "1.0.0"), Pick((await lifecycle.GetInstallAsync(A, "qian.sample.crm", Ct))!));
        Assert.Equal("1.0.0", await ServingVersion(host, A));
        using (var serving = await host.Get(A, "/api/customers")) Assert.Equal(HttpStatusCode.OK, serving.StatusCode);
        Assert.Equal(("crm", "qian.sample.crm@1.1.0", "PRECONDITION_FAILED"), Single(await Diagnostics(host, A)));
        // 只有 1.1.0 → 1.0.0 那次成功的改版发了 upgraded 事件；失败的升级不发。
        Assert.Equal([LifecycleEvents.TenantPluginUpgraded], await EventTypes("plugin_qian_lifecycle", A));
        static (string?, string?, string) Single(IReadOnlyList<LifecycleDiagnosticView> items) { var item = Assert.Single(items); return (item.EntryId, item.Plugin, item.ReasonCode); }
    }

    private static JsonElement Entry(string version) => JsonSerializer.SerializeToElement(new { id = "crm", plugin = "qian.sample.crm@" + version, config = new { } });
    private static (string, string?) Pick(TenantPluginInstallView view) => (view.State, view.Version);
    private static (string, int) Pick(MountResult result) => (result.State, result.Revision);

    private static async Task ProvisionAndMount(LifecyclePlatform platform, RunningApp host, params string[] tenants)
    {
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        using (EventEnvelopeScope.Begin(Operator))
        {
            await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "operator", Ct);
            await lifecycle.ActivateAsync("qian.sample.crm", 1, "operator", Ct);
            foreach (var tenant in tenants)
            {
                await lifecycle.EntitleAsync(tenant, "qian.sample.crm", "operator", Ct);
                await lifecycle.ProvisionAsync(tenant, "qian.sample.crm", "operator", Ct);
                Assert.Equal(MountStates.Ready, (await lifecycle.MountAsync(new ScopeKey("tenant", tenant), Entry("1.0.0"), "operator", Ct)).State);
            }
        }
        foreach (var tenant in tenants) await WithinFiveSeconds(host, tenant, HttpStatusCode.OK);
    }

    // 业务请求经 SDK 插件端点门禁：每 100ms 重试一次，5 秒内达到预期状态码。
    private static async Task<CustomerPage?> WithinFiveSeconds(RunningApp host, string tenant, HttpStatusCode expected)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            using var response = await host.Get(tenant, "/api/customers");
            if (response.StatusCode == expected) return expected == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<CustomerPage>(Ct) : null;
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) Assert.Fail($"5 秒内没有得到 {expected}，最后为 {response.StatusCode}：{host.FailureEvidence}");
            await Task.Delay(100, Ct);
        }
    }

    // 处理该租户业务请求的组件版本：SDK 门禁与命令处理器读取的同一个 scope 视图（Registry 合入后的已挂载条目）。
    private static async Task<string> ServingVersion(RunningApp host, string tenant)
    {
        var context = host.App.Services.GetRequiredService<ITenantContext>();
        var registry = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("registry", ScopeKey.Global).Registry();
        using var scope = context.BeginScope(tenant);
        var entries = await registry.GetScopeEntriesAsync(new ScopeKey("tenant", tenant), CapabilityBindingRegistration.Key, Ct);
        return entries.Where(entry => entry.EntryId == "crm").Select(entry => entry.Version).Distinct().Single();
    }

    private static async Task<IReadOnlyList<LifecycleDiagnosticView>> Diagnostics(RunningApp host, string tenant)
    {
        using var response = await DiagnosticsRequest(host, tenant, null);
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true }, response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("items").Deserialize<LifecycleDiagnosticView[]>(JsonSerializerOptions.Web)!;
    }

    private static Task<HttpResponseMessage> DiagnosticsRequest(RunningApp host, string tenant, string? scope) =>
        CatalogRequest(host, tenant, "/api/lifecycle/diagnostics" + (scope is null ? "" : "?scope=" + Uri.EscapeDataString(scope)));

    private async Task<(string?, string?, string?, string?)> InstallRow(string tenant)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT state, version, updated_by, updated_at::text FROM plugin_qian_lifecycle.tenant_plugin_installs WHERE tenant_id = '{tenant}'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    private async Task<string?> Text(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync(Ct))?.ToString();
    }

    private async Task<string[]> EventTypes(string schema, string? tenant)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT event_type_id FROM {schema}.outbox_messages WHERE tenant_id {(tenant is null ? "IS NULL" : "= '" + tenant + "'")} ORDER BY occurred_at", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var types = new List<string>();
        while (await reader.ReadAsync(Ct)) types.Add(reader.GetString(0));
        return [.. types];
    }

    private static async Task AssertCode(Qian.Runtime.Errors.ErrorCode code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(action);
        Assert.Equal(code.Code, error.Error.Code.Code);
    }

    // 生命周期接线：上下文随包迁移；Registry / Lifecycle 的事件类型与领域事件处理器经参考宿主的合成入口并进同一次 Kun 登记（Start）；
    // 信任根照生产形状经配置节 Trust:PlatformKeys 引用 K8s Secret 挂载里的公钥。
    private static void ConfigureLifecycle(WebApplicationBuilder builder, string connection, LifecyclePlatform platform)
    {
        builder.Services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(LifecycleDbContext.Schema));
        builder.Services.AddSingleton<ISecretAccessObserver>(platform);
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = platform.SecretRoot,
            Bindings = [new SecretBinding("secretRef://platform/trust/platform", new KubernetesSecretLocation("qian", "trust-platform", "public-key"))],
            Policies = [new SecretAccessPolicy("qian.lifecycle", [new("trust/*", "platform")], [new("trust/*", "platform")])]
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trust:PlatformKeys:0:PublicKey"] = "secretRef://platform/trust/platform", ["Trust:PlatformKeys:0:NotBefore"] = "2026-01-01T00:00:00Z"
        });
    }

    private static void MapLifecycle(WebApplication app) =>
        app.MapQianLifecycle(http => http.RequestServices.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("lifecycle", ScopeKey.Global));

    // 平台插件树：CRM 撤下全局条目，只预装 1.0.0 / 1.0.1 / 1.1.0（module 制品随宿主镜像发布，摘要即安装时核对的摘要），由 Lifecycle 装进租户 scope。
    private static async Task AttachLifecycle(WebApplication app, LifecyclePlatform platform)
    {
        var loader = app.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-lifecycle-crm-" + Guid.NewGuid().ToString("N"))).FullName;
        app.Lifetime.ApplicationStopped.Register(() => Directory.Delete(directory, true));
        var validator = new SurfaceValidator(); var installed = new List<RegistryComponentBinding>();
        var options = new ProfileLoadOptions { WorkingDirectory = directory, Profile = "platform", IncludeHomePatch = false };
        var definitions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        var versions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        Add("data", "qian.data", new DataPlugin(), [DataDefinition.Key]);
        Add("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey]);
        Add("authz", "qian.authz", new AuthzPlugin([CrmPermissions.Compile()]), [CommandPolicies.AuthorizationKey, AuthzDefinition.Key]);
        Add("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]);
        Add("config", "qian.config", new ConfigurationPlugin(), [ConfigDefinition.Key]);
        var registry = new RegistryPlugin(new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = loader, Profiles = new ProfileLoader(validator), Profile = options, Surfaces = validator,
            Resolve = entry =>
            {
                var reference = entry.Plugin.Split('@');
                var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
                var definition = reference[0] == "qian.sample.crm" ? versions[reference[1]] : definitions[entry.Id];
                return definition with { Entry = new PluginEntry(entry.Id, reference[0], reference[1], scope, entry.Layer, entry.Config) };
            }
        });
        Add("registry", "qian.registry", registry, [RegistryDefinition.Key], new { store = "pg" });
        Add("capabilities", "qian.capabilities", new CapabilitiesPlugin(), [CapabilitiesDefinition.Key, CapabilitiesDefinition.ContractsKey, CapabilitiesDefinition.NavigationKey]);
        Add("lifecycle", "qian.lifecycle", new LifecyclePlugin(new LifecycleRuntimeOptions { Hooks = (id, _) => id == "qian.sample.crm" ? new CrmPlugin() : null }), [LifecycleDefinition.Key]);
        foreach (var id in new[] { "registry", "capabilities", "lifecycle" })
            Write($"plugins/{id}/schemas/config.schema.json", File.ReadAllText(Path.Combine(RepositoryRoot(), $"plugins/qian.{id}/schemas/config.schema.json")));
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = item.Entry.Config })));
        // CRM 组件文件夹：清单声明 module 形态；契约索引两份 OpenAPI；1.0.1 只改菜单标题，1.1.0 删除全部 HTTP 路径（破坏性契约）。
        var crm = new CrmPlugin();
        Write("plugins/crm/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
        {
            id = "qian.sample.crm", version = "1.0.0", description = "参考应用：客户管理，由生命周期装进租户 scope。", author = new { name = "Qian" }, license = "Apache-2.0",
            @interface = new { displayName = "crm", category = "sample" }, inject = crm.Inject.Select(key => key.Name), provides = new[] { CrmDefinition.Key.Name },
            policy = new { tier = "official", sandbox = "none", authentication = "ON_INSTALL", required = false }, scopes = CatalogScopes,
            runtime = new { form = "module", entry = "Qian.Sample.Crm" }
        }));
        var contract = CapabilityContractCompiler.CreateOpenApi([CreateCustomerCommand.QianCapability, ListCustomersQuery.QianCapability, GetCustomerQuery.QianCapability], validator, new ToolContractHasher());
        Write("plugins/crm/metadata/contracts.json", JsonSerializer.Serialize(new[]
            { new { id = "qian.sample.crm.api/v1", kind = "openapi", path = "./contracts/http-v1.openapi.json" },
              new { id = "qian.sample.crm.http/v1", kind = "openapi", path = "./contracts/rest-v1.openapi.json" } }));
        Write("plugins/crm/contracts/http-v1.openapi.json", contract.GetRawText());
        Write("plugins/crm/contracts/rest-v1.openapi.json", (await app.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.sample.crm", Ct)).GetRawText());
        Write("plugins/crm/ui/manifest.json", """{"pages":[{"route":"/customers","expose":"CustomersPage","permission":"qian.sample.crm:customer:read","nav":{"title":"客户","icon":"users","order":10}}]}""");
        foreach (var definition in definitions.Values)
        {
            var validation = validator.ValidateComponent(Path.Combine(directory, "plugins", definition.Entry.Id),
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
            Assert.True(validation.IsValid, definition.Entry.Id + ": " + JsonSerializer.Serialize(validation.Diagnostics));
            var files = Directory.EnumerateFiles(Path.Combine(directory, "plugins", definition.Entry.Id), "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(Path.Combine(directory, "plugins", definition.Entry.Id), file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            var digest = LifecycleArtifactDigest(files);
            installed.Add(RegistryComponentBinding.FromArtifact(definition, files, digest, validator, new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true }));
            platform.SystemArtifacts[definition.Entry.Plugin] = new PluginArtifact(files, digest, platform.Sign(digest));
        }
        foreach (var version in new[] { "1.0.0", "1.0.1", "1.1.0" })
        {
            var files = CrmFiles(Path.Combine(directory, "plugins/crm"), version);
            // 装配器按 plugins/ 下的组件文件夹解析 组件@版本，每个预装版本各有一个文件夹。
            if (version != "1.0.0")
                foreach (var file in files) { var target = Path.Combine(directory, "plugins", "crm-" + version, file.Key); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, file.Value.ToArray()); }
            var digest = LifecycleArtifactDigest(files);
            var definition = new PluginDefinition(new PluginEntry("crm", "qian.sample.crm", version, ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new CrmPlugin(), [CrmDefinition.Key]);
            var binding = RegistryComponentBinding.FromArtifact(definition, files, digest, validator,
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
            installed.Add(binding);
            versions[version] = definition with { Plugin = new NavigationManifestPlugin(definition.Plugin, binding.Metadata, validator) };
            platform.Artifacts[version] = new PluginArtifact(files, digest, platform.Sign(digest));
        }
        await loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        await loader.MountAsync([definitions["config"], definitions["registry"], definitions["capabilities"], definitions["lifecycle"]], Ct);
        foreach (var id in new[] { "config", "registry", "capabilities", "lifecycle" })
            Assert.True(loader.GetState(id, ScopeKey.Global) == PluginState.Ready, id + ": " + loader.GetFailure(id, ScopeKey.Global));
        await loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(Ct);
        await GrantDiagnostics(app);
        return;

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides, object? config = null)
        {
            var definition = new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
            definitions.Add(id, definition);
            Write($"plugins/{id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = component, version = "1.0.0", description = "生命周期端到端验收使用实际 Provider 的平台组合。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = id, category = "testing" }, inject = plugin.Inject.Select(key => key.Name), provides = provides.Select(key => key.Name),
                policy = new { tier = "system", sandbox = "none", authentication = "ON_INSTALL", required = true }, scopes = CatalogScopes,
                runtime = new { form = "module", entry = plugin.GetType().FullName + ", " + plugin.GetType().Assembly.GetName().Name }
            }));
        }
        void Write(string path, string content)
        {
            var file = Path.Combine(directory, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content);
        }
    }

    // 同一份字节既是宿主预装绑定，也是交给 Lifecycle 安装、经平台签名的制品。
    private static Dictionary<string, ReadOnlyMemory<byte>> CrmFiles(string source, string version)
    {
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(files[".qian-plugin/plugin.json"].Span)!;
        manifest["version"] = version;
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        if (version == "1.0.1")
        {
            var ui = System.Text.Json.Nodes.JsonNode.Parse(files["ui/manifest.json"].Span)!;
            ui["pages"]![0]!["nav"]!["title"] = "租户专属客户"; files["ui/manifest.json"] = Encoding.UTF8.GetBytes(ui.ToJsonString());
        }
        if (version == "1.1.0")
            foreach (var name in new[] { "contracts/http-v1.openapi.json", "contracts/rest-v1.openapi.json" })
            {
                var document = System.Text.Json.Nodes.JsonNode.Parse(files[name].Span)!;
                document["paths"] = new System.Text.Json.Nodes.JsonObject(); files[name] = Encoding.UTF8.GetBytes(document.ToJsonString());
            }
        return files;
    }

    private static string LifecycleArtifactDigest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }

    // 生命周期的管理面权限照仓库 metadata 编译登记：两租户安装角色模板，A 的调用者是 Owner（持诊断读取权限），B 的调用者只是使用者。
    private static async Task GrantDiagnostics(WebApplication app)
    {
        var root = RepositoryRoot();
        using var permissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins/qian.lifecycle/metadata/permissions.json")));
        using var roles = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins/qian.lifecycle/metadata/roles.json")));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.lifecycle", "1.0.0", permissions.RootElement, roles.RootElement);
        Assert.Equal([LifecycleDefinition.DiagnosticsReadPermission], catalog.Permissions.Select(permission => permission.Code));
        var data = app.Services.GetRequiredService<IDataService>(); var tenant = app.Services.GetRequiredService<ITenantContext>();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var active = app.Services.GetRequiredService<IPermissionRegistry>().Register("1.0.0", catalog.Permissions);
        app.Lifetime.ApplicationStopped.Register(active.Dispose);
        foreach (var id in new[] { A, B })
        {
            using var current = tenant.BeginScope(id);
            await new PostgresTenantRoleStore(data, tenant).InstallDefaultsAsync("qian.lifecycle", "1.0.0", null, "test-bootstrap", Ct);
        }
    }

    // 平台签名钥与 K8s Secret 挂载目录；公钥 PEM 写在挂载里，信任根配置引用它。也是秘密访问的审计接收端。
    private sealed class LifecyclePlatform : IDisposable, ISecretAccessObserver
    {
        private readonly Ed25519PrivateKeyParameters _private = new(new SecureRandom());
        internal LifecyclePlatform()
        {
            var spki = Convert.FromHexString("302a300506032b6570032100").Concat(_private.GeneratePublicKey().GetEncoded()).ToArray();
            Fingerprint = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(spki));
            var target = Directory.CreateDirectory(Path.Combine(SecretRoot, "qian", "trust-platform")).FullName;
            File.WriteAllText(Path.Combine(target, "public-key"), "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(spki) + "\n-----END PUBLIC KEY-----\n");
        }
        internal string SecretRoot { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-lifecycle-secrets-" + Guid.NewGuid().ToString("N"))).FullName;
        internal string Fingerprint { get; }
        internal Dictionary<string, PluginArtifact> SystemArtifacts { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, PluginArtifact> Artifacts { get; } = new(StringComparer.Ordinal);
        internal static IPluginLifecycleController Lifecycle(RunningApp host) =>
            host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("lifecycle", ScopeKey.Global).Lifecycle();
        internal ArtifactSignature Sign(string digest)
        {
            var message = Convert.FromHexString(digest["sha256:".Length..]);
            var signer = new Ed25519Signer(); signer.Init(true, _private); signer.BlockUpdate(message, 0, message.Length);
            return new ArtifactSignature(Fingerprint, signer.GenerateSignature());
        }
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void Dispose() { try { Directory.Delete(SecretRoot, true); } catch (IOException) { } }
    }
}

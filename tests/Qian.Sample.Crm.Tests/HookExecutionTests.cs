using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Data;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Surfaces.Hosting;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    private static readonly string[] HookProvides = ["Crm"];
    private static readonly string[] HookTargets = ["qian.sample.crm/seedCustomer", "qian.sample.crm/listCustomers"];
    [Fact]
    public async Task 真实运行时事件经声明hook调用实际客户命令_审计通道hook_拒绝仍由真实权限负责()
    {
        await using var host = await Start(hooks: true);
        var command = new CreateCustomerCommand(Guid.CreateVersion7(), "hook private customer");
        using var response = await HookPost(host, A, command); Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenant.BeginScope(A))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Single(await session.Context.Customers.ToListAsync(Ct)); Assert.Single(await session.Context.OutboxMessages.ToListAsync(Ct));
            var audit = host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit();
            var record = Assert.Single(await audit.ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal("hook", record.Record.Channel); Assert.Equal("qian.sample.crm/customer.create", record.Record.CapabilityId); Assert.Equal(Writer, record.Record.ActorId);
        }
        using var denied = await HookPost(host, B, new CreateCustomerCommand(Guid.CreateVersion7(), "forbidden")); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (tenant.BeginScope(B))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Empty(await session.Context.Customers.ToListAsync(Ct));
        }
        using var ordinary = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "ordinary request")); Assert.Equal(HttpStatusCode.Created, ordinary.StatusCode);
        using (tenant.BeginScope(A))
        {
            var records = await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().ReadAsync(0, 10, cancellationToken: Ct);
            Assert.Equal("api", records[1].Record.Channel); // hook 通道没有泄漏到下一次普通请求。
        }
    }
    [Fact]
    public async Task 实际hook声明不可见时不执行_目标不存在时Apply失败()
    {
        await using var host = await Start(hooks: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (runtime.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != SurfaceKeys.Hooks))
        {
            using var response = await HookPost(host, A, new CreateCustomerCommand(Guid.CreateVersion7(), "hidden")); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct); Assert.Empty(await session.Context.Customers.ToListAsync(Ct));
        }
        var snapshot = HookSnapshot("missing"); // 语法有效不等于实际 handler 已登记。
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(new PluginEntry("crm", "qian.sample.crm", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new HookBridgePlugin(new CrmPlugin(), snapshot), [CrmDefinition.Key])], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("crm", ScopeKey.Global));
    }
    [Fact]
    public async Task hooks文件更新后实际桥接行为五秒内改变_代码与业务服务不重启()
    {
        await using var host = await Start(hooks: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = runtime.GetContext("crm", ScopeKey.Global);
        var original = source.Inject<ICrmCommands>(CrmDefinition.Key);
        var directory = (string)((IApplicationBuilder)host.App).Properties["qian.tests.hooks"]!;
        var file = Path.Combine(directory, "hooks/hooks.json");
        File.WriteAllText(file, """{"hooks":{"tenant/provisioned":[{"matcher":"^never-match$","hooks":[{"type":"plugin","plugin":"qian.sample.crm","handler":"seedCustomer"}]}]}}""");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (source.Inject<SurfaceDocument>(SurfaceKeys.Hooks, "qian.sample.crm/hooks").Data.GetProperty("hooks").GetProperty("tenant/provisioned")[0].TryGetProperty("matcher", out var matcher) == false || matcher.GetString() != "^never-match$")
            await Task.Delay(20, deadline.Token);
        Assert.Same(original, source.Inject<ICrmCommands>(CrmDefinition.Key));
        using var ignored = await HookPost(host, A, new CreateCustomerCommand(Guid.CreateVersion7(), "not called")); Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
        using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
        await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
        Assert.Empty(await session.Context.Customers.ToListAsync(Ct));
        File.WriteAllText(file, """{"hooks":{"tenant/provisioned":[{"hooks":[{"type":"plugin","plugin":"qian.sample.crm","handler":"listCustomers"}]}]}}""");
        using var updated = CancellationTokenSource.CreateLinkedTokenSource(Ct); updated.CancelAfter(TimeSpan.FromSeconds(5));
        while (source.Inject<SurfaceDocument>(SurfaceKeys.Hooks, "qian.sample.crm/hooks").Data.GetProperty("hooks").GetProperty("tenant/provisioned")[0].GetProperty("hooks")[0].GetProperty("handler").GetString() != "listCustomers")
            await Task.Delay(20, updated.Token);
        using var queried = await HookPost(host, A, new CreateCustomerCommand(Guid.CreateVersion7(), "query event")); Assert.Equal(HttpStatusCode.OK, queried.StatusCode);
        Assert.Same(original, source.Inject<ICrmCommands>(CrmDefinition.Key));
        var record = Assert.Single(await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().ReadAsync(0, 10, cancellationToken: Ct));
        Assert.Equal("hook", record.Record.Channel); Assert.Equal("qian.sample.crm/customer.list", record.Record.CapabilityId);
    }

    private static void MapHookEntry(WebApplication app)
    {
        // 仅验证已有真实生产方上下文的事件桥接；这不是租户开户/开通 API 的替代实现。
        app.MapPost("/api/acceptance/tenant-provisioned", async (CreateCustomerCommand input, HttpContext http, CrmCommandRuntime runtime, ITenantContext tenant) =>
        {
            var source = runtime.Loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant.TenantId!));
            var result = await source.DispatchAsync(SurfaceHookEvents.TenantProvisioned,
                new HookNotification(source, tenant.TenantId!, JsonSerializer.SerializeToElement(input, CrmCommandJson.Default.CreateCustomerCommand), http.RequestAborted), http.RequestAborted);
            return Results.Json(result);
        }).RequireAuthorization();
    }
    private static async Task AttachHooks(WebApplication app)
    {
        var runtime = app.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var entry = runtime.DumpConfig().Single(item => item.Id == "crm");
        var directory = Directory.CreateTempSubdirectory("qian-live-hooks-");
        ((IApplicationBuilder)app).Properties["qian.tests.hooks"] = directory.FullName;
        app.Lifetime.ApplicationStopped.Register(() => directory.Delete(true));
        var snapshot = HookSnapshot("seedCustomer", directory.FullName);
        var bridge = new HookBridgePlugin(new CrmPlugin(), snapshot);
        var watching = new HotReloadSurfacePlugin(directory.FullName, new SurfaceValidator(),
            new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server", Capabilities = HookTargets }, bridge);
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry, watching, [CrmDefinition.Key, .. watching.SurfaceProvides])], Ct);
        Assert.True(runtime.GetState("crm", ScopeKey.Global) == PluginState.Ready, runtime.GetFailure("crm", ScopeKey.Global)?.ToString());
    }
    private static SurfaceValidationResult HookSnapshot(string handler, string? existingDirectory = null)
    {
        var directory = existingDirectory is null ? Directory.CreateTempSubdirectory("qian-hook-proof-") : new DirectoryInfo(existingDirectory);
        try
        {
            Directory.CreateDirectory(Path.Combine(directory.FullName, ".qian-plugin")); Directory.CreateDirectory(Path.Combine(directory.FullName, "hooks"));
            var manifest = new { id = "qian.sample.crm", version = "1.0.0", description = "真实事件到实际客户命令的桥接验收组件。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = "客户", category = "testing" }, inject = new CrmPlugin().Inject.Select(key => key.Name), provides = HookProvides, runtime = new { form = "module", entry = typeof(CrmPlugin).FullName + ", " + typeof(CrmPlugin).Assembly.GetName().Name }, policy = new { tier = "official", sandbox = "none", authentication = "ON_INSTALL" }, scopes = CatalogScopes };
            File.WriteAllText(Path.Combine(directory.FullName, ".qian-plugin/plugin.json"), JsonSerializer.Serialize(manifest));
            File.WriteAllText(Path.Combine(directory.FullName, "hooks/hooks.json"), JsonSerializer.Serialize(new { hooks = new Dictionary<string, object> { ["tenant/provisioned"] = new[] { new { hooks = new[] { new { type = "plugin", plugin = "qian.sample.crm", handler } } } } } }));
            var validation = new SurfaceValidator().ValidateComponent(directory.FullName, new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server", Capabilities = handler == "seedCustomer" ? HookTargets : ["qian.sample.crm/missing"] });
            Assert.True(validation.IsValid, JsonSerializer.Serialize(validation.Diagnostics)); return validation;
        }
        finally { if (existingDirectory is null) directory.Delete(true); }
    }
    private static Task<HttpResponseMessage> HookPost(RunningApp host, string tenant, CreateCustomerCommand input)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/acceptance/tenant-provisioned") { Content = JsonContent.Create(input) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write")); return host.Send(request);
    }
}

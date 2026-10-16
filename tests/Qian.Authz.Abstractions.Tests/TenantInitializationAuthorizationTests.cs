using System.Text.Json;
using Qian.Testing;
using System.Runtime.CompilerServices;
using Kun.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.Abstractions.Tests;

public sealed class TenantInitializationAuthorizationTests
{
    [Fact]
    public async Task 初始化凭据绑定生产方租户能力与请求类型_重放及越权拒绝()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        await loader.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new Producer(), [])], TestContext.Current.CancellationToken);
        var source = loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", Guid.NewGuid().ToString()));
        var wrong = source.Scoped(new ScopeKey("tenant", Guid.NewGuid().ToString()));
        Assert.False(TenantInitializationAccess.Allows("qian.test/init", source.Scope));
        using (TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { "qian.test/init" }))
        {
            Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Enter(wrong, "qian.test/init", typeof(Request), Request.RequiredPermissions));
            Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Enter(source, "qian.test/other", typeof(Request), Request.RequiredPermissions));
            using (TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions))
            {
                Assert.True(TenantInitializationAccess.Allows<Request>());
                Assert.False(TenantInitializationAccess.Allows<Other>());
                Assert.Equal("operator", TenantInitializationAccess.Actor);
                Assert.Equal(source.Scope.Id, TenantInitializationAccess.Tenant);
                Assert.False(TenantInitializationAccess.Allows("qian.test/init", wrong.Scope));
                TenantInitializationAccess.Claim<Request>("qian.test/init", source.Scope);
                Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Claim<Request>("qian.test/init", source.Scope));
            }
            Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions));
        }
        Assert.False(TenantInitializationAccess.Allows("qian.test/init", source.Scope));
    }

    [Fact]
    public async Task 子任务捕获的开户上下文在派发结束后失效()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        await loader.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new Producer(), [])], TestContext.Current.CancellationToken);
        var source = loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", Guid.NewGuid().ToString()));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task escaped;
        using (TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { "qian.test/init" }))
            escaped = Task.Run(async () =>
            {
                await gate.Task;
                Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions));
            }, TestContext.Current.CancellationToken);
        gate.SetResult(); await escaped;
    }

    [Fact]
    public void 发布程序集只暴露三个受信消费者_不向测试程序集开放()
    {
        var friends = typeof(AuthzDefinition).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName).Order().ToArray();
        Assert.Equal("Qian.Authz,Qian.Plugin.Sdk,Qian.Tenancy", string.Join(",", friends));
    }

    [Fact]
    public async Task 已打开凭据拒绝嵌套Begin与嵌套Enter()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var source = await Source(loader);
        using var grant = TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { "qian.test/init", "qian.test/other" });
        Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Begin(source, "operator", new HashSet<string>()));
        using var invocation = TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions);
        Assert.Throws<QianErrorException>(() => TenantInitializationAccess.Enter(source, "qian.test/other", typeof(Request), Request.RequiredPermissions));
    }

    [Fact]
    public async Task 已打开凭据并发消费同一能力恰好一个成功()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var source = await Source(loader);
        using var grant = TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { "qian.test/init" });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { using var invocation = TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions); return true; }
            catch (QianErrorException error) { Assert.Equal("AUTHZ_DENIED", error.Error.Code.Code); return false; }
        }, TestContext.Current.CancellationToken)).ToArray();
        start.SetResult();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(allowed => allowed));
    }

    [Fact]
    public async Task 已打开能力调用的并发Claim恰好一个成功()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var source = await Source(loader);
        using var grant = TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { "qian.test/init" });
        using var invocation = TenantInitializationAccess.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { TenantInitializationAccess.Claim<Request>("qian.test/init", source.Scope); return true; }
            catch (QianErrorException error) { Assert.Equal("AUTHZ_DENIED", error.Error.Code.Code); return false; }
        }, TestContext.Current.CancellationToken)).ToArray();
        start.SetResult();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(allowed => allowed));
    }

    private static async Task<IPluginContext> Source(PluginLoader loader)
    {
        await loader.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new Producer(), [])], TestContext.Current.CancellationToken);
        return loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", Guid.NewGuid().ToString()));
    }

    private sealed class Producer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class Request : IPermissionProtectedRequest { public static IReadOnlyList<string> RequiredPermissions => ["qian.test:seed:write"]; }
    private sealed class Other : IPermissionProtectedRequest { public static IReadOnlyList<string> RequiredPermissions => ["qian.test:seed:write"]; }
}

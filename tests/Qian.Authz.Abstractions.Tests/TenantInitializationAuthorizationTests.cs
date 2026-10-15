using System.Text.Json;
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
        Assert.False(TenantInitializationAuthorization.Allows("qian.test/init", source.Scope));
        using (TenantInitializationAuthorization.Begin(source, "operator", new HashSet<string> { "qian.test/init" }))
        {
            Assert.Throws<QianErrorException>(() => TenantInitializationAuthorization.Enter(wrong, "qian.test/init", typeof(Request), Request.RequiredPermissions));
            Assert.Throws<QianErrorException>(() => TenantInitializationAuthorization.Enter(source, "qian.test/other", typeof(Request), Request.RequiredPermissions));
            using (TenantInitializationAuthorization.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions))
            {
                Assert.True(TenantInitializationAuthorization.Allows<Request>());
                Assert.False(TenantInitializationAuthorization.Allows<Other>());
                Assert.Equal("operator", TenantInitializationAuthorization.Actor);
                Assert.Equal(source.Scope.Id, TenantInitializationAuthorization.Tenant);
                Assert.False(TenantInitializationAuthorization.Allows("qian.test/init", wrong.Scope));
                TenantInitializationAuthorization.Claim<Request>("qian.test/init", source.Scope);
                Assert.Throws<QianErrorException>(() => TenantInitializationAuthorization.Claim<Request>("qian.test/init", source.Scope));
            }
            Assert.Throws<QianErrorException>(() => TenantInitializationAuthorization.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions));
        }
        Assert.False(TenantInitializationAuthorization.Allows("qian.test/init", source.Scope));
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
        using (TenantInitializationAuthorization.Begin(source, "operator", new HashSet<string> { "qian.test/init" }))
            escaped = Task.Run(async () =>
            {
                await gate.Task;
                Assert.Throws<QianErrorException>(() => TenantInitializationAuthorization.Enter(source, "qian.test/init", typeof(Request), Request.RequiredPermissions));
            }, TestContext.Current.CancellationToken);
        gate.SetResult(); await escaped;
    }

    private sealed class Producer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class Request : IPermissionProtectedRequest { public static IReadOnlyList<string> RequiredPermissions => ["qian.test:seed:write"]; }
    private sealed class Other : IPermissionProtectedRequest { public static IReadOnlyList<string> RequiredPermissions => ["qian.test:seed:write"]; }
}

using System.Text.Json;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk.Tests;

// SDK 协议边界测试；不替代真实身份入口、生产权限策略或 CRM 写入验收。
public sealed class CommandBoundaryTests
{
    [Fact]
    public async Task 实际Mediator缺少强制监听器时在数据连接和handler之前拒绝()
    {
        var ct = TestContext.Current.CancellationToken; var tenant = new AsyncLocalTenantContext(); var source = new SourcePlugin();
        var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(tenant); services.AddSingleton<ICurrentUser>(new VerifiedPort());
        services.AddSingleton<HandlerCounter>();
        services.AddKunMediator(builder => builder.AddQianCapability<CreateRequest, CustomerResponse, BoundaryHandler, BoundaryContext>(_ => source.Context!));
        using var provider = services.BuildServiceProvider(); await using var runtime = new PluginLoader(provider);
        await runtime.MountAsync([Define("source", "qian.sdk-tests", source)], ct);
        using var scope = tenant.BeginScope(Tenant);
        var error = await Assert.ThrowsAsync<QianErrorException>(() => provider.GetRequiredService<ISender>().Send(new CreateRequest(Guid.CreateVersion7(), "never execute"), ct).AsTask());
        Assert.Equal("AUTHZ_DENIED", error.Error.Code.Code); Assert.Equal(0, provider.GetRequiredService<HandlerCounter>().Calls); Assert.Equal(0, provider.GetRequiredService<HandlerCounter>().Constructors);
        // 故意不登记数据 Provider；若管道提前打开业务连接，这里会得到不同的错误。
    }

    // 框架当前主体端口被匿名兜底占住时，命令管道会在权限引擎之前判拒一切执行（403、无决策审计）；能力宿主改为启动即失败（S1-KUN-017）。
    [Fact]
    public async Task 框架当前主体端口是匿名兜底时能力宿主启动即失败_端口由身份端口桥接时照常启动()
    {
        var ct = TestContext.Current.CancellationToken; var source = new SourcePlugin();
        ServiceProvider Host(Action<IServiceCollection> port)
        {
            var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); services.AddSingleton<ICurrentUser>(new VerifiedPort());
            port(services);
            services.AddKunMediator(builder => builder.AddQianCapability<CreateRequest, CustomerResponse, BoundaryHandler, BoundaryContext>(_ => source.Context!));
            return services.BuildServiceProvider();
        }
        // Kun 2026.10.25 之前数据约定的形状：匿名兜底先登记，AddQianCapability 的桥接 TryAdd 随之落空。
        using (var anonymous = Host(services => services.AddScoped<Kun.Core.Security.ICurrentUser>(_ => Kun.Core.Security.AnonymousCurrentUser.Instance)))
        {
            var check = Assert.Single(anonymous.GetServices<IHostedService>());
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(ct));
            Assert.Contains("Kun.Core.Security.ICurrentUser", error.Message, StringComparison.Ordinal);
        }
        using var bridged = Host(_ => { });
        await Assert.Single(bridged.GetServices<IHostedService>()).StartAsync(ct);
    }

    [Fact]
    public async Task 普通组件不能登记或伪装基础授权监听器()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = new ServiceCollection().BuildServiceProvider(); await using var runtime = new PluginLoader(provider);
        await runtime.MountAsync([Define("foreign", "qian.sdk-tests", new ForeignPolicy())], ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("foreign", ScopeKey.Global));
        Assert.Equal("AUTHZ_DENIED", Assert.IsType<QianErrorException>(runtime.GetFailure("foreign", ScopeKey.Global)).Error.Code.Code);
    }

    private const string Tenant = "01900000-0000-7000-8000-000000000001";
    private static PluginDefinition Define(string id, string pluginId, IQianPlugin plugin) => new(
        new PluginEntry(id, pluginId, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, []);
    private sealed class SourcePlugin : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }
    private sealed class ForeignPolicy : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        { CommandPolicies.Register(ctx, CommandPolicyKind.Authorization, (_, next) => next()); return ValueTask.CompletedTask; }
    }
    private sealed class VerifiedPort : ICurrentUser
    {
        public Principal Principal { get; } = new()
        {
            Issuer = "test-port", Audience = ["qian.sdk-tests"], Subject = "client", Kind = PrincipalKind.Service, TenantId = Tenant,
            Roles = [], Scope = "qian.sdk-tests:write", SessionId = Guid.CreateVersion7().ToString(), TokenId = Guid.CreateVersion7().ToString(),
            IssuedAt = 1, NotBefore = 1, ExpiresAt = 300
        };
        public string UserId => Principal.Subject;
        public IReadOnlyList<string> Roles => Principal.Roles;
        public bool IsAuthenticated => true;
    }
    private sealed class HandlerCounter { public int Calls { get; set; } public int Constructors { get; set; } }
    private sealed class BoundaryHandler : IRequestHandler<CreateRequest, CustomerResponse>
    {
        private readonly HandlerCounter _counter;
        public BoundaryHandler(HandlerCounter counter) { _counter = counter; counter.Constructors++; }
        public ValueTask<CustomerResponse> Handle(CreateRequest request, CancellationToken cancellationToken)
        { _counter.Calls++; return ValueTask.FromResult(new CustomerResponse(Guid.CreateVersion7(), request.Name)); }
    }
    private sealed class BoundaryContext(DbContextOptions<BoundaryContext> options, ITenantContext tenant) : QianDbContext(options, tenant);
}

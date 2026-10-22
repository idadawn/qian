using System.Runtime.CompilerServices;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Gateway.DependencyInjection;
using Kun.Gateway.Endpoints;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Identity.Client;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using StackExchange.Redis;
using Yarp.ReverseProxy.Configuration;

// 这是完整依赖的技术探针，不是 GW-001 的认证验收替身。只有 Linux NativeAOT 发布成功后才能执行运行阶段。
// 运行所需的数据库 schema、受限角色、Apollo 服务和签名密钥由测试准备；探针不迁移、不关闭 RLS、不创建空 Provider。
if (RuntimeFeature.IsDynamicCodeSupported || !OperatingSystem.IsLinux())
    throw new InvalidOperationException("必须运行 Linux NativeAOT 可执行文件。");

var configuration = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
{
    BasePath = AppContext.BaseDirectory,
    Arguments = args,
    EnvironmentPrefix = "QIAN_PROBE_"
}).Build();
using var configurationOwner = (IDisposable)configuration;
var builder = WebApplication.CreateSlimBuilder(args);
builder.Configuration.AddConfiguration(configuration);
var tenant = new AsyncLocalTenantContext();
builder.Services.AddSingleton<ITenantContext>(tenant);
builder.Services.AddKunEventBus();
builder.Services.AddQianConfiguration(configuration);
builder.Services.AddQianData<AuditDbContext>((_, options) => options
    .UseNpgsql(Required("Audit:Connection")).UseKunSchema(AuditDbContext.Schema));
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(Required("Redis:Connection")));
builder.Services.AddQianIdentityClient(new IdentityClientOptions
{
    Authority = new Uri(Required("Identity:Issuer")), ClientId = Required("Identity:ClientId"),
    Resource = Required("Identity:Resource"), RedirectUri = new Uri(Required("Identity:RedirectUri"))
});
builder.Services.AddSingleton<IProxyConfigProvider>(new InMemoryConfigProvider(
    [new RouteConfig { RouteId = "probe", ClusterId = "qian-platform", Match = new RouteMatch { Path = "/api/{**rest}" } }],
    [new ClusterConfig { ClusterId = "qian-platform", Destinations = new Dictionary<string, DestinationConfig>
        { ["platform"] = new() { Address = Required("Platform:Address") } } }]));
builder.Services.AddKunGateway(options =>
{
    options.ExternalIssuer = Required("Identity:Issuer"); options.ExternalAudience = Required("Identity:Resource");
    options.ExternalJwksUri = Required("Identity:JwksUri"); options.InternalIssuer = Required("Gateway:Issuer");
    options.InternalAudience = "qian-platform"; options.SigningKeyPath = Required("Gateway:SigningKeyPath");
    options.PublicKeyPath = Required("Gateway:PublicKeyPath"); options.KeyOverlapPeriod = TimeSpan.FromMinutes(10);
});
await using var app = builder.Build();
app.UseRouting(); app.UseQianIdentityClient(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapQianIdentityClient(); app.MapKunJwks(); app.MapReverseProxy();
await using var loader = new PluginLoader(app.Services);
using var empty = JsonDocument.Parse("{}");
using var apollo = JsonDocument.Parse(Required("Apollo:PluginConfiguration"));
using var identity = JsonDocument.Parse(Required("Identity:PluginConfiguration"));
await loader.MountAsync([
    Definition("data", "qian.data", new DataPlugin(), empty.RootElement, DataDefinition.Key),
    Definition("config", "qian.config.apollo", new ApolloConfigurationPlugin(), apollo.RootElement, ConfigDefinition.Key),
    Definition("identity", "qian.identity.client", new IdentityClientComponent(), identity.RootElement, IdentityDefinition.Key),
    Definition("audit", "qian.audit", new AuditComponent(), empty.RootElement, AuditDefinition.Key, CommandPolicies.AuditKey)
]);
foreach (var id in new[] { "data", "config", "identity", "audit" })
    if (loader.GetState(id, ScopeKey.Global) != PluginState.Ready)
        throw new InvalidOperationException("组件未就绪：" + id, loader.GetFailure(id, ScopeKey.Global));
await app.StartAsync();
using (tenant.BeginScope(Required("Audit:Tenant")))
{
    var audit = loader.GetContext("audit", ScopeKey.Global).Audit();
    var record = new AuditRecord(Guid.NewGuid(), tenant.TenantId, "service", "qian.gateway.aot-probe", null,
        "qian.gateway:aot:probe", "1", new string('a', 64), "api", null, "success", new string('a', 32),
        DateTimeOffset.UtcNow, empty.RootElement);
    var appended = await audit.AppendAsync(record);
    var read = await audit.ReadAsync(appended.Sequence - 1, 1);
    if (read.Count != 1 || read[0].Record.Id != record.Id || !(await audit.VerifyAsync()).Valid)
        throw new InvalidOperationException("真实审计读写或链校验失败。");
}
await app.StopAsync();
Console.WriteLine("完整 Config / Identity / Data / Audit Linux NativeAOT 运行通过。");

string Required(string key) => configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少探针配置：" + key);
static PluginDefinition Definition(string id, string plugin, IQianPlugin implementation, JsonElement config, params ServiceKey[] provides) =>
    new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, config.Clone()), implementation, provides);

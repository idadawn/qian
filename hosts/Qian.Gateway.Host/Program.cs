using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Data;
using Qian.Gateway;
using Qian.Identity.Abstractions;
using Qian.Identity.Client;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var secretDirectory = builder.Configuration["Secrets:Directory"];
builder.Configuration.Sources.Clear();
builder.Configuration.AddQianConfigurationSources(new QianConfigurationOptions
{
    BasePath = builder.Environment.ContentRootPath, EnvironmentName = builder.Environment.EnvironmentName,
    SecretDirectory = secretDirectory, EnvironmentPrefix = "QIAN_", Arguments = args,
    ResolveAccessKey = reference => reference == "secretRef://platform/apollo/access-key"
        ? ReadCredential(Required("ConnectionFiles:ApolloAccessKey")) : throw new InvalidOperationException("Apollo 引导密钥引用未登记。")
});
var gateway = new GatewayConfiguration(Required("Gateway:IdentityIssuer"), Required("Gateway:IdentityAudience"), Required("Gateway:Issuer"),
    Required("Gateway:SigningKeyPath"), Required("Gateway:PublicKeyPath"),
    builder.Configuration.GetSection("Gateway:TrustedProxyNetworks").GetChildren().Select(item => item.Value ?? "").ToArray(),
    builder.Configuration.GetSection("Gateway:Routes").GetChildren().Select(item => new GatewayRoute(item["Id"] ?? "", item["Path"] ?? "", item["Cluster"] ?? "")).ToArray(),
    builder.Configuration.GetSection("Gateway:Clusters").GetChildren().Select(item => new GatewayCluster(item["Id"] ?? "", item["Address"] ?? "")).ToArray());
var gatewayJson = JsonSerializer.SerializeToElement(gateway, HostJson.Default.GatewayConfiguration);
gateway = GatewayHosting.ReadConfiguration(gatewayJson);
builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
builder.Services.AddKunEventBus();
builder.Services.AddQianConfiguration(builder.Configuration);
builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(ReadCredential(Required("ConnectionFiles:Audit"))).UseKunSchema(AuditDbContext.Schema));
builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(ReadCredential(Required("ConnectionFiles:PlatformAudit"))).UseKunSchema(AuditDbContext.Schema));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(ReadCredential(Required("ConnectionFiles:Redis"))));
builder.Services.AddDataProtection().SetApplicationName("qian.gateway")
    .PersistKeysToFileSystem(new DirectoryInfo(Required("Identity:DataProtectionKeysDirectory")));
var observer = new GatewaySecretAudit();
builder.Services.AddSingleton<ISecretAccessObserver>(observer);
const string identitySecret = "secretRef://platform/identity/client-secret";
builder.Services.AddQianSecrets(new SecretResolverOptions
{
    MountRoot = Required("Identity:SecretMountRoot"),
    Bindings = [new(identitySecret, new KubernetesSecretLocation("qian", "identity", "client-secret"))],
    Policies = [new("qian.identity.client", [new("identity/*", "platform")], [new("identity/*", "platform")])]
});
builder.Services.AddQianIdentityClient(new IdentityClientOptions
{
    Authority = new Uri(gateway.IdentityIssuer), ClientId = Required("Identity:ClientId"), Resource = gateway.IdentityAudience,
    RedirectUri = new Uri(Required("Identity:RedirectUri")), Scopes = ["openid", "offline_access", "qian.api"],
    PathPrefix = "/auth", RequireHttpsMetadata = !builder.Environment.IsDevelopment(),
    AllowedReturnPaths = ["/"], DefaultReturnPath = "/", SessionKeyPrefix = "qian:gateway:sessions:"
});
builder.Services.AddQianGateway(gateway, IdentityClientDefaults.SessionScheme);
await using var app = builder.Build();
app.UseForwardedHeaders(); app.UseRouting(); app.UseQianIdentityClient(); app.UseQianGateway();
app.MapQianIdentityClient(); app.MapQianGateway();
await using var loader = new PluginLoader(app.Services);
observer.Source = () => loader.GetContext("config", ScopeKey.Global);
using var empty = JsonDocument.Parse("{}");
using var identityConfig = JsonDocument.Parse(new JsonObject
{
    ["issuer"] = gateway.IdentityIssuer, ["clientId"] = Required("Identity:ClientId"), ["clientSecret"] = identitySecret
}.ToJsonString());
using var auditConfig = JsonDocument.Parse(new JsonObject
{
    ["chain"] = new JsonObject { ["store"] = "pg", ["platformRole"] = Required("Audit:PlatformRole") }
}.ToJsonString());
using var apolloConfig = JsonDocument.Parse(new JsonObject
{
    ["appId"] = Required("Apollo:AppId"), ["env"] = Required("Apollo:Env"),
    ["meta"] = Required("Apollo:ConfigServer:0"),
    ["namespaces"] = new JsonArray(builder.Configuration.GetSection("Apollo:Namespaces").GetChildren().Select(item => (JsonNode?)JsonValue.Create(item.Value)).ToArray())
}.ToJsonString());
await loader.MountAsync([
    Definition("data", "qian.data", new DataPlugin(), empty.RootElement, DataDefinition.Key),
    Definition("config", "qian.config.apollo", new ApolloConfigurationPlugin(), apolloConfig.RootElement, ConfigDefinition.Key),
    Definition("audit", "qian.audit", new AuditComponent(), auditConfig.RootElement, AuditDefinition.Key, CommandPolicies.AuditKey),
    Definition("identity", "qian.identity.client", new IdentityClientComponent(), identityConfig.RootElement, IdentityDefinition.Key),
    Definition("gateway", "qian.gateway", new GatewayComponent(), gatewayJson, GatewayDefinition.Key)
]);
foreach (var id in new[] { "data", "config", "audit", "identity", "gateway" })
    if (loader.GetState(id, ScopeKey.Global) != PluginState.Ready)
        throw new InvalidOperationException("gateway 依赖未就绪：" + id, loader.GetFailure(id, ScopeKey.Global));
await app.RunAsync();

string Required(string key) => builder.Configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少网关宿主配置：" + key);
static string ReadCredential(string path)
{
    if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("引导凭据必须来自指定的 Secret 文件。");
    var value = File.ReadAllText(path).Trim();
    return value.Length > 0 ? value : throw new InvalidOperationException("引导凭据文件为空。");
}
static PluginDefinition Definition(string id, string plugin, IQianPlugin implementation, JsonElement config, params ServiceKey[] provides) =>
    new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, config.Clone()), implementation, provides);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GatewayConfiguration))]
internal sealed partial class HostJson : JsonSerializerContext;

internal sealed class GatewaySecretAudit : ISecretAccessObserver
{
    internal Func<IPluginContext>? Source { get; set; }
    public async ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default)
    {
        var source = Source?.Invoke() ?? throw new InvalidOperationException("密钥访问审计尚未就绪。");
        if (access.TenantId is not null) throw new InvalidOperationException("网关客户端密钥仅允许平台域。");
        using var payload = JsonDocument.Parse(new JsonObject { ["reference"] = access.Reference, ["allowed"] = access.Allowed }.ToJsonString());
        var inputHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(access.Reference)));
        var trace = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
        await source.Audit().AppendPlatformAsync(source, new AuditRecord(Guid.CreateVersion7(), null, "service", access.Caller, null,
            "qian.config:secret:resolve", "1.0.0", inputHash, "api", null, access.Outcome, trace, DateTimeOffset.UtcNow, payload.RootElement), cancellationToken);
    }
}

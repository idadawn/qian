using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Audit;
using Qian.Audit.Native;
using Qian.Data.Native;
using Qian.Data.Native.Postgres;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Shell.TestHost;
using Qian.Testing.Containers;
using Qian.Testing.NativeAudit;

// 独立浏览器验收宿主：使用真实 PostgreSQL 与正式审计 Provider，不属于生产 profile。
if (args.Length != 2 || args[0] != "--state") throw new ArgumentException("需要 --state 指定临时验收状态文件。");
var state = Path.GetFullPath(args[1]);
var repository = FindRepository();
var yueVersion = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "packages/yue/core/package.json"))).RootElement.GetProperty("version").GetString()!;
await using var postgres = new PostgresFixture();
await postgres.InitializeAsync();
var database = await NativeAuditDatabase.Create(postgres, CancellationToken.None);
var builder = WebApplication.CreateBuilder([]);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
var tenant = new AsyncLocalTenantContext();
builder.Services.AddSingleton<ITenantContext>(tenant);
builder.Services.AddSingleton(ErrorCatalog.CreateDefault());
builder.Services.AddQianErrors();
var guard = NativeAuditStorage.ExpectedGuard(database.Roles);
NativeDataTable[] tables = [new(NativeAuditStorage.Schema, "audit_entries", NativeAuditStorage.GuardPolicy, guard, guard),
    new(NativeAuditStorage.Schema, "audit_chain_state", NativeAuditStorage.GuardPolicy, guard, guard)];
builder.Services.AddQianNativeData(new("audit_tenant", database.Roles.TenantLogin, NativeDataAccess.Tenant, _ => ValueTask.FromResult(database.TenantConnection), tables));
builder.Services.AddQianNativeData(new("audit_platform", database.Roles.PlatformLogin!, NativeDataAccess.Platform, _ => ValueTask.FromResult(database.PlatformConnection), tables));
await using var app = builder.Build();
await using var runtime = new PluginLoader(app.Services);
var consumer = new AuditConsumer();
await runtime.MountAsync([
    Define("data", "qian.data.native", new NativeDataComponent(), new { connections = AuditConsumer.Bindings }, NativeDataDefinition.Key),
    Define("audit", "qian.audit.native", new NativeAuditComponent(), new
    {
        tenantBinding = "audit_tenant", platformBinding = "audit_platform", tenantRole = database.Roles.TenantLogin,
        platformRole = database.Roles.PlatformLogin, functionOwner = database.Roles.FunctionOwner, maxAttempts = 6
    }, AuditDefinition.Key, CommandPolicies.AuditKey),
    Define("shell", "test.shell-audit", consumer, new { })]);
foreach (var id in new[] { "data", "audit", "shell" })
    if (runtime.GetState(id, ScopeKey.Global) != PluginState.Ready) throw new InvalidOperationException("真实审计验收组合未就绪：" + id);
using var rsa = RSA.Create(2048);
var key = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
var tokens = new JsonWebTokenHandler { MapInboundClaims = false };
var validation = new TokenValidationParameters
{
    ValidateIssuer = true, ValidIssuer = "qian-shell-acceptance", ValidateAudience = true, ValidAudience = "qian-shell-audit",
    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true,
    IssuerSigningKey = key, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero
};
app.UseQianErrors();
ShellAuditEndpoints.Map(app, consumer, tenant, tokens, validation, yueVersion);
await app.StartAsync();
Directory.CreateDirectory(Path.GetDirectoryName(state)!);
var temporary = state + ".tmp";
var payload = JsonSerializer.Serialize(new
{
    url = app.Urls.Single(),
    tenantA = ShellAuditEndpoints.TenantA, tenantB = ShellAuditEndpoints.TenantB,
    writerToken = Token(ShellAuditEndpoints.TenantA, "report"), writerTokenB = Token(ShellAuditEndpoints.TenantB, "report"),
    readerToken = Token(ShellAuditEndpoints.TenantA, "read"), yueVersion
});
try
{
    await File.WriteAllTextAsync(temporary, payload);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    File.Move(temporary, state, overwrite: true);
    Console.WriteLine("真实外壳审计验收宿主已就绪；临时凭据仅写入受限状态文件。");
    await app.WaitForShutdownAsync();
}
finally { File.Delete(temporary); File.Delete(state); }

string Token(string tenantId, string purpose) => tokens.CreateToken(new SecurityTokenDescriptor
{
    Issuer = validation.ValidIssuer, Audience = validation.ValidAudience, IssuedAt = DateTime.UtcNow, NotBefore = DateTime.UtcNow.AddSeconds(-1),
    Expires = DateTime.UtcNow.AddHours(1), SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
    Subject = new ClaimsIdentity([new Claim("sub", "shell-acceptance-user"), new Claim("tenant_id", tenantId),
        new Claim("principal_kind", "user"), new Claim("purpose", purpose)])
});
static PluginDefinition Define(string id, string plugin, IQianPlugin implementation, object config, params ServiceKey[] provides) =>
    new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config)), implementation, provides);
static string FindRepository()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
    throw new InvalidOperationException("找不到验收仓库根目录。");
}

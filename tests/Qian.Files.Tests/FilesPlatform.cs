using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities.AspNetCore;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy.AspNetCore;

namespace Qian.Files.Tests;

// 平台模式（S1-FILE-004）：Registry 是真实 Provider（pg 存储），按临时 platform profile 预装组件。qian.files 用仓库里已去骨架的组件文件夹，
// 其余平台组件的清单由实际 Provider 派生（照 tools/Qian.Dev.TenantOps 的 DevPlatform）；贡献方组件只带 metadata/upload-policies.json。
// TEN-004 的设置端点挂在独立运营宿主（照参考运营宿主 TenantOperationsHost）：平台用户令牌，超管旁路由 authz 的平台登录角色提供。
internal sealed class FilesPlatform : IAsyncDisposable
{
    internal const string Operator = "root";
    private static readonly string[] CatalogScopes = ["global", "tenant"];
    private static readonly string[] GlobalScopes = ["global"];
    // 完整目录：工具绑定只能引用目录里已有的能力，平台目录含 qian.files 的六个能力。
    private static readonly SurfaceValidationOptions Surface = new()
    {
        Profile = "platform", ExecutionPlane = "server", CatalogComplete = true,
        Capabilities = [FilesCapabilities.PresignUpload, FilesCapabilities.CompleteUpload, FilesCapabilities.Get, FilesCapabilities.DownloadUrl, FilesCapabilities.Delete, FilesCapabilities.Quota]
    };
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("qian-files-platform-");
    private readonly IReadOnlyDictionary<string, object> _contributors;
    private readonly string _password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private WebApplication? _operations;

    // contributors：组件 ID → metadata/upload-policies.json 的内容。
    internal FilesPlatform(string app, IReadOnlyDictionary<string, object>? contributors)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        AuthzRole = "files_authz_" + suffix; AuditRole = "files_audit_" + suffix; _contributors = contributors ?? new Dictionary<string, object>();
        AuthzConnection = new NpgsqlConnectionStringBuilder(app) { Username = AuthzRole, Password = _password }.ConnectionString;
        AuditConnection = new NpgsqlConnectionStringBuilder(app) { Username = AuditRole, Password = _password }.ConnectionString;
    }

    internal string AuthzRole { get; }
    internal string AuditRole { get; }
    internal string AuthzConnection { get; }
    internal string AuditConnection { get; }
    internal HttpClient Client { get; private set; } = null!;

    // 平台授权与审计各一个独立登录（NOSUPERUSER / NOBYPASSRLS），只经 *PlatformAccess 迁移授予的函数访问平台数据。
    internal string LoginSql => string.Concat(new[] { AuthzRole, AuditRole }.Select(role =>
        $"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{_password}';\n"));

    // 超管旁路按授权目录判定运营权限点：平台模式的 authz 另登记 qian.tenancy 与 qian.registry 的权限声明（与参考运营宿主相同）。
    internal static IEnumerable<PermissionCatalog> Catalogs()
    {
        var compiler = new PermissionCatalogCompiler(new SurfaceValidator());
        yield return compiler.FromMetadata("qian.tenancy", "1.0.0", Metadata("qian.tenancy", "permissions.json"), JsonSerializer.SerializeToElement(Array.Empty<object>()));
        yield return compiler.FromMetadata("qian.registry", "1.0.0", Metadata("qian.registry", "permissions.json"), Metadata("qian.registry", "roles.json"));
        static JsonElement Metadata(string component, string file) =>
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(FilesMetadata.RepositoryRoot(), "plugins", component, "metadata", file)));
    }

    internal void Configure(WebApplicationBuilder builder, string app)
    {
        builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(app).UseKunSchema(RegistryDbContext.Schema));
        builder.Services.AddQianData<PlatformAuthzDbContext>((_, options) => options.UseNpgsql(AuthzConnection).UseKunSchema(AuthzDbContext.Schema));
        builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(AuditConnection).UseKunSchema(AuditDbContext.Schema));
    }

    internal static string SchemaSql(IServiceProvider services) =>
        $"DROP SCHEMA IF EXISTS {RegistryDbContext.Schema} CASCADE;\n" + services.GetRequiredService<RegistryDbContext>().Database.GenerateCreateScript();

    internal string AccessSql =>
        $"GRANT USAGE ON SCHEMA {RegistryDbContext.Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {RegistryDbContext.Schema} TO qian_app;\n" +
        "BEGIN;" + AuthzPlatformAccess.CreateMigrationSql(AuthzRole) + AuditPlatformAccess.CreateMigrationSql(AuditRole) + "COMMIT;";

    // 写临时 platform profile 与各组件文件夹，按制品字节建立预装绑定，返回挂在最后的 Registry 定义。
    internal PluginDefinition Registry(PluginLoader loader, IReadOnlyList<PluginDefinition> providers)
    {
        var validator = new SurfaceValidator(); var installed = new List<RegistryComponentBinding>();
        var definitions = providers.ToDictionary(definition => definition.Entry.Id, StringComparer.Ordinal);
        var registry = new PluginDefinition(new PluginEntry("registry", "qian.registry", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { store = "pg" })),
            new RegistryPlugin(new RegistryRuntimeOptions
            {
                InstalledComponents = installed, Runtime = loader, Profiles = new ProfileLoader(validator), Surfaces = validator,
                Profile = new ProfileLoadOptions { WorkingDirectory = _directory.FullName, Profile = "platform", IncludeHomePatch = false },
                Resolve = entry =>
                {
                    var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
                    var reference = entry.Plugin.Split('@');
                    return definitions[entry.Id] with { Entry = new PluginEntry(entry.Id, reference[0], reference[1], scope, entry.Layer, entry.Config) };
                }
            }), [RegistryDefinition.Key]);
        definitions.Add(registry.Entry.Id, registry);
        foreach (var definition in definitions.Values)
        {
            if (definition.Entry.Plugin == "qian.files")
            { Copy(Path.Combine(FilesMetadata.RepositoryRoot(), "plugins", "qian.files"), Path.Combine(_directory.FullName, "plugins", definition.Entry.Id)); continue; }
            Manifest(definition, definition.Entry.Plugin is "qian.tenancy" ? GlobalScopes : CatalogScopes, system: true);
            var schema = Path.Combine(FilesMetadata.RepositoryRoot(), "plugins", definition.Entry.Plugin, "schemas", "config.schema.json");
            if (File.Exists(schema)) Write($"plugins/{definition.Entry.Id}/schemas/config.schema.json", File.ReadAllText(schema));
        }
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = item.Entry.Config })));
        // 贡献方组件只预装、不挂载：Registry 的预装快照里有它的 metadata，qian.files 据此汇总上传策略。
        var bound = definitions.Values.ToList();
        foreach (var (id, policies) in _contributors)
        {
            var contributor = new PluginDefinition(new PluginEntry(id, id, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new ContributorPlugin(), []);
            Manifest(contributor, CatalogScopes, system: false);
            Write($"plugins/{id}/metadata/upload-policies.json", JsonSerializer.Serialize(policies, JsonSerializerOptions.Web));
            bound.Add(contributor);
        }
        foreach (var definition in bound)
        {
            var folder = Path.Combine(_directory.FullName, "plugins", definition.Entry.Id);
            var validation = validator.ValidateComponent(folder, Surface);
            Assert.True(validation.IsValid, definition.Entry.Id + "：" + JsonSerializer.Serialize(validation.Diagnostics));
            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            installed.Add(RegistryComponentBinding.FromArtifact(definition, files, ArtifactDigest(files), validator, Surface));
        }
        return registry;
    }

    // Registry 只能在初始插件树 Ready 后启动监听；平台超管经 authz 的平台登录授予，运营宿主随后启动。
    internal async Task StartAsync(IServiceProvider services, PluginLoader loader, SecurityKey key, string issuer, string audience, CancellationToken ct)
    {
        await loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(ct);
        await new PostgresPlatformRoleStore(services.GetRequiredService<IDataService>(), services.GetRequiredService<ITenantContext>(), AuthzRole)
            .AssignSuperadminAsync(Operator, "operator", ct);
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(services.GetRequiredService<ITenantContext>());
        builder.Services.AddSingleton(services.GetRequiredService<ErrorCatalog>());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, PlatformOperator>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, IssuerSigningKey = key, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents { OnTokenValidated = context => { PlatformOperator.Capture(context); return Task.CompletedTask; } };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        _operations = builder.Build(); _operations.UseQianErrors(); _operations.UseAuthentication(); _operations.UseAuthorization();
        _operations.MapQianTenancy(_ => loader.GetContext("tenancy", ScopeKey.Global));
        await _operations.StartAsync(ct);
        Client = new HttpClient { BaseAddress = new Uri(Assert.Single(_operations.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses)) };
    }

    internal Task<HttpResponseMessage> Manage(string token, HttpMethod method, string path, object? input = null)
    {
        var message = new HttpRequestMessage(method, path); message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (input is not null) message.Content = JsonContent.Create(input, input.GetType());
        return Client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_operations is not null) { await _operations.StopAsync(TestContext.Current.CancellationToken); await _operations.DisposeAsync(); }
        foreach (var connection in new[] { AuthzConnection, AuditConnection }) { using var pool = new NpgsqlConnection(connection); NpgsqlConnection.ClearPool(pool); }
        try { _directory.Delete(true); } catch (IOException) { }
    }

    private void Manifest(PluginDefinition definition, string[] scopes, bool system) => Write($"plugins/{definition.Entry.Id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
    {
        id = definition.Entry.Plugin, version = definition.Entry.Version, description = "文件验收平台宿主装载的实际 Provider。", author = new { name = "Qian" }, license = "Apache-2.0",
        @interface = new { displayName = definition.Entry.Id, category = "testing" }, inject = definition.Plugin.Inject.Select(key => key.Name), provides = definition.Provides.Select(key => key.Name),
        policy = new { tier = system ? "system" : "official", sandbox = "none", authentication = "ON_INSTALL", required = system }, scopes,
        runtime = new { form = "module", entry = definition.Plugin.GetType().FullName + ", " + definition.Plugin.GetType().Assembly.GetName().Name }
    }));

    // 与 Lifecycle 核对的摘要算法相同：按路径排序的「路径、长度、SHA-256」清单再取 SHA-256。
    private static string ArtifactDigest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }

    private void Write(string path, string content)
    {
        var file = Path.Combine(_directory.FullName, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content);
    }

    private static void Copy(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
        }
    }

    // 只声明 metadata 的贡献方组件：没有依赖、不提供服务。
    private sealed class ContributorPlugin : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }

    // 平台用户（无租户、user 主体）；主体快照取自已验签令牌的载荷，与参考运营宿主同一做法。
    private sealed class PlatformOperator(IHttpContextAccessor accessor) : ICurrentUser
    {
        private static readonly object Snapshot = new();
        public Principal? Principal => accessor.HttpContext?.Items[Snapshot] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;
        internal static void Capture(TokenValidatedContext context)
        {
            try
            {
                if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind) || kind != "user")
                { context.Fail("缺少已验证的平台用户。"); return; }
                using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
                var principal = document.RootElement.Deserialize(IdentityJson.PrincipalTypeInfo);
                if (principal is null || principal.Kind != PrincipalKind.User || principal.Subject != subject || principal.TenantId != tenant)
                { context.Fail("主体快照不一致。"); return; }
                context.HttpContext.Items[Snapshot] = principal;
            }
            catch (JsonException) { context.Fail("身份快照无效。"); }
        }
    }
}

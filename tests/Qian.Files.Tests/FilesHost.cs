using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy.DependencyInjection;
using Kun.MultiTenancy.Middleware;
using Kun.Security.Authentication;
using Kun.Security.DependencyInjection;
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
using Qian.Authz.AspNetCore;
using Qian.Capabilities;
using Qian.Data;
using Qian.Events;
using Qian.Files.AspNetCore;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;
using Qian.Testing.Containers;

namespace Qian.Files.Tests;

// 验收宿主：与参考宿主（samples/Qian.Sample.Crm）同一装配——真实 S2S 令牌、生产模式租户解析、Tenancy / Authz（含生产模式 HTTP PEP）/ Audit / Events 组件。
// 文件元数据经包内迁移由受限属主角色建表，业务连接是独立的 NOSUPERUSER / NOBYPASSRLS 登录；qian.files 在 Kestrel 起来后挂载，local 后端的 endpoint 才有端口。
internal sealed class FilesHost : IAsyncDisposable
{
    internal const string A = "01900000-0000-7000-8000-0000000000a1";
    internal const string B = "01900000-0000-7000-8000-0000000000b2";
    // A 里是 tenant.owner（读、写、删），B 里只是 tenant.user（读、写）：同一主体跨租户的权限差异由 PEP 判定。
    internal const string Subject = "files-acceptance-client";
    internal const string Purpose = "acme.crm/attachment";
    internal const string Avatar = "acme.crm/avatar";
    internal const long Quota = 10L * 1024 * 1024;
    private const string Issuer = "files-acceptance";
    private const string Audience = "qian.files";
    private static readonly string[] TokenRoles = ["tenant.owner"];

    private readonly WebApplication _app;
    private readonly FilesHostRuntime _runtime;
    private readonly RSA _signing;
    private readonly RsaSecurityKey _key;
    private readonly string _admin;
    private readonly string _business;
    private readonly string _migrations;

    private FilesHost(WebApplication app, FilesHostRuntime runtime, RSA signing, RsaSecurityKey key, HttpClient client, ManualClock clock, string storage,
        string admin, string business, string migrations)
    {
        _app = app; _runtime = runtime; _signing = signing; _key = key; Client = client; Clock = clock; StorageRoot = storage;
        _admin = admin; _business = business; _migrations = migrations;
    }

    internal HttpClient Client { get; }
    internal ManualClock Clock { get; }
    internal string StorageRoot { get; }
    internal IServiceProvider Services => _app.Services;
    internal PluginLoader Loader => _runtime.Loader;
    // 受限业务登录（NOSUPERUSER / NOBYPASSRLS），用来从数据库侧核对 RLS 的实际效果。
    internal string BusinessConnection => _business;
    // 平台模式下的真实 Registry 与 TEN-004 运营宿主；默认宿主为 null。
    internal FilesPlatform? Platform { get; private init; }

    // installed 不为 null 时另挂只提供预装快照的 Registry 替身，模拟平台宿主里各组件 metadata/upload-policies.json 的声明；
    // platform 为 true 时改挂真实 Registry 并起 TEN-004 运营宿主（FilesPlatform），contributors 是只带上传策略声明的预装贡献方组件。
    internal static async Task<FilesHost> StartAsync(PostgresFixture postgres, object? policies = null, long quota = Quota,
        IReadOnlyList<InstalledComponentMetadata>? installed = null, bool platform = false, IReadOnlyDictionary<string, object>? contributors = null,
        CancellationToken ct = default)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var role = "qian_files_app_" + suffix; var owner = "qian_audit_fn_" + suffix; var migrator = "qian_files_owner_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin(postgres.ConnectionString, $"""
            CREATE ROLE "{role}" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';
            CREATE ROLE "{owner}" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
            CREATE ROLE "{migrator}" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';
            GRANT CONNECT, CREATE ON DATABASE {PostgresFixture.DatabaseName} TO "{migrator}";
            """, ct);
        var operations = platform ? new FilesPlatform(postgres.AppConnectionString, contributors) : null;
        if (operations is not null) await Admin(postgres.ConnectionString, operations.LoginSql, ct);
        var business = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
        var migrations = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = migrator, Password = password }.ConnectionString;
        var storage = Directory.CreateTempSubdirectory("qian-files-").FullName;
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var signing = RSA.Create(2048); using var publicKey = RSA.Create(); publicKey.ImportParameters(signing.ExportParameters(false));
        var key = new RsaSecurityKey(signing) { KeyId = "files-acceptance-key" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicKey) { KeyId = key.KeyId });
        var keys = new JsonWebKeySet(JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } }));

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddQianErrors();
        // 身份适配器先于数据约定登记，避免框架的匿名兜底占住当前主体端口。
        builder.Services.AddKunSecurity();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, FilesCurrentUser>();
        builder.Services.AddKunMultiTenancy(options => { options.Production = true; options.FallbackTenantId = null; });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddKunServiceAuthentication(options =>
        {
            options.Issuer = Issuer; options.Audience = Audience; options.StaticKeySet = keys;
        });
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (previous is not null) await previous(context).ConfigureAwait(false);
                if (context.Result is not null && !context.Result.Succeeded) return;
                FilesCurrentUser.Capture(context);
            };
        });
        builder.Services.AddKunEventBus(registrar =>
        {
            registrar.RegisterFilesEvents().RegisterTenancyEvents().RegisterAuthzEvents();
            if (operations is not null) registrar.RegisterRegistryEvents();
        });
        builder.Services.AddQianEvents();
        builder.Services.AddQianData<FilesDbContext>((_, options) => options.UseNpgsql(business).UseKunSchema(FilesDbContext.Schema));
        builder.Services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuthzDbContext.Schema));
        builder.Services.AddQianData<TenancyDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(TenancyDbContext.Schema));
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        operations?.Configure(builder, postgres.AppConnectionString);
        // 与 --migrate-only 迁移任务同一入口：包内迁移由属主角色在 advisory lock 内执行，没有迁移的上下文照常跳过。
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, _) => migrations);
        // 能力契约的登记期检查：ProvideCapability 时生成目录条目并预编译输入校验器，不合格的契约挂载即失败。
        builder.Services.AddSingleton<ISurfaceReader, SurfaceValidator>();
        builder.Services.AddSingleton<IToolContractHasher, ToolContractHasher>();
        builder.Services.AddQianCapabilities();
        // 命令管道里 Kun 的 PermissionBehavior 读宿主的 IPermissionChecker（缺失即拒绝）；与参考宿主一样接真实 RBAC，登记表与 authz 组件共用同一实例。
        builder.Services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
        builder.Services.AddSingleton<Kun.Authorization.IPermissionChecker>(sp => new RbacPermissionChecker(sp.GetRequiredService<IDataService>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IPermissionRegistry>()));
        builder.Services.AddSingleton<FilesHostRuntime>();
        // HTTP PEP：判定、identity_revision 比对与决策审计都经 authz 组件按请求租户收窄的上下文；生产模式下未声明权限点的组件端点缺省拒绝。
        builder.Services.AddQianAuthzEnforcement(http => http.RequestServices.GetRequiredService<FilesHostRuntime>().AuthzSource(), options => options.Production = true);
        builder.Services.AddKunMediator(registry =>
        {
            registry.AddQianTenancyEvents().AddQianAuthzEvents();
            if (operations is not null) registry.AddQianRegistryEvents();
            registry.AddQianFiles(sp => sp.GetRequiredService<FilesHostRuntime>().Source(), options =>
            {
                options.Clock = clock; options.LocalSigningKey = RandomNumberGenerator.GetBytes(32);
            });
        });
        var app = builder.Build();
        app.UseQianErrors();
        app.UseAuthentication();
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch => branch.UseKunMultiTenancy());
        app.UseAuthorization();
        app.UseQianAuthzEnforcement();
        var runtime = app.Services.GetRequiredService<FilesHostRuntime>();
        app.MapQianFiles(http => http.RequestServices.GetRequiredService<FilesHostRuntime>().Source(), (endpoint, permission) => endpoint.RequireQianPermission(permission),
            new FilesEndpointOptions { TransferSource = http => http.RequestServices.GetRequiredService<FilesHostRuntime>().TransferSource() });
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var schemas = new[] { FilesDbContext.Schema, AuthzDbContext.Schema, TenancyDbContext.Schema, AuditDbContext.Schema };
                var sql = string.Join('\n', schemas.Select(schema => "DROP SCHEMA IF EXISTS " + schema + " CASCADE;"));
                sql += scope.ServiceProvider.GetRequiredService<AuthzDbContext>().Database.GenerateCreateScript();
                sql += scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Database.GenerateCreateScript();
                sql += scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.GenerateCreateScript();
                await Admin(postgres.ConnectionString, sql, ct);
                await Admin(postgres.ConnectionString, "GRANT USAGE ON SCHEMA plugin_qian_authz, plugin_qian_tenancy, plugin_qian_audit TO qian_app; " +
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_authz, plugin_qian_tenancy, plugin_qian_audit TO qian_app;", ct);
                await Admin(postgres.ConnectionString, "BEGIN;" + AuditTransactionAccess.CreateMigrationSql(owner, role) + "COMMIT;", ct);
                if (operations is not null)
                {
                    await Admin(postgres.ConnectionString, FilesPlatform.SchemaSql(scope.ServiceProvider), ct);
                    await Admin(postgres.ConnectionString, operations.AccessSql, ct);
                }
            }
            var reports = await app.Services.GetRequiredService<IDataMigrator>().MigrateAsync(ct);
            Assert.NotEmpty(Assert.Single(reports, report => report.ContextType == typeof(FilesDbContext)).Applied);
            await Admin(postgres.ConnectionString, $"GRANT USAGE ON SCHEMA {FilesDbContext.Schema} TO \"{role}\"; " +
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {FilesDbContext.Schema} TO \"{role}\";", ct);
            foreach (var (id, code) in new[] { (A, "a"), (B, "b") })
                await Admin(postgres.ConnectionString, "INSERT INTO plugin_qian_tenancy.tenants(id, code, name, isolation, status, created_at) " +
                    $"VALUES ('{id}'::uuid, '{code}', 'files tenant', 'row', 'active', now());", ct);
            await app.StartAsync(ct);
            var address = Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);
            await runtime.MountAsync(Configuration(storage, address + "/files/blobs/", quota, policies), installed, operations, ct);
            if (operations is not null) await operations.StartAsync(app.Services, runtime.Loader, key, Issuer, Audience, ct);
            await runtime.ProvisionAsync(A, "tenant.owner", Subject, ct);
            await runtime.ProvisionAsync(B, "tenant.user", Subject, ct);
            return new FilesHost(app, runtime, signing, key, new HttpClient { BaseAddress = new Uri(address) }, clock, storage,
                postgres.ConnectionString, business, migrations) { Platform = operations };
        }
        catch
        {
            if (operations is not null) await operations.DisposeAsync();
            await app.DisposeAsync(); signing.Dispose(); Directory.Delete(storage, recursive: true);
            throw;
        }
    }

    internal static object DefaultPolicies() => new[]
    {
        new { purpose = Purpose, allowedTypes = new[] { "text/plain", "text/html", "image/png", "application/pdf" }, maxSize = 4L * 1024 * 1024 },
        new { purpose = Avatar, allowedTypes = new[] { "image/png" }, maxSize = 1L * 1024 * 1024 }
    };

    private static JsonElement Configuration(string storage, string endpoint, long quota, object? policies) => JsonSerializer.SerializeToElement(new
    {
        storage = new { kind = "local", root = storage, endpoint },
        quota = new { perTenantBytes = quota },
        policies = policies ?? DefaultPolicies()
    });

    internal string Token(string tenant, string? subject = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Issuer, ["aud"] = new[] { Audience }, ["sub"] = subject ?? Subject, ["principal_kind"] = "service", ["tenant_id"] = tenant,
            ["roles"] = TokenRoles, ["scope"] = "files", ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
    }

    // 平台用户令牌（无租户的 user 主体），只给平台模式的运营宿主用。
    internal string PlatformToken(string subject = FilesPlatform.Operator)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Issuer, ["aud"] = new[] { Audience }, ["sub"] = subject, ["principal_kind"] = "user", ["tenant_id"] = null,
            ["roles"] = Array.Empty<string>(), ["scope"] = "", ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
    }

    internal Task<HttpResponseMessage> Send(string tenant, HttpMethod method, string path, HttpContent? content = null, Guid? idempotencyKey = null, string? subject = null)
    {
        var message = new HttpRequestMessage(method, path) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(tenant, subject));
        if (idempotencyKey is { } key) message.Headers.Add("Idempotency-Key", key.ToString());
        return Send(message);
    }

    internal async Task<HttpResponseMessage> Send(HttpRequestMessage message)
    {
        using (message) return await Client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    internal Task<HttpResponseMessage> Presign(string tenant, PresignUploadRequest request, string? subject = null) =>
        Send(tenant, HttpMethod.Post, "/api/files/uploads:presign", JsonContent.Create(request, FilesContractJson.PresignUploadRequestTypeInfo), subject: subject);

    // 按签发结果直传：方法、地址与必需请求头都来自服务端回执，客户端不拼存储键。
    internal Task<HttpResponseMessage> Put(PresignedUpload upload, byte[] content, string? contentType = null)
    {
        var message = new HttpRequestMessage(new HttpMethod(upload.Method), upload.Url) { Content = new ByteArrayContent(content) };
        foreach (var (name, value) in upload.RequiredHeaders)
            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase)) message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? value);
            else message.Headers.TryAddWithoutValidation(name, value);
        return Send(message);
    }

    // 申请、直传、完成三步走完，返回完成后的文件。
    internal async Task<FileDescriptor> Upload(string tenant, byte[] content, string contentType = "text/plain", string name = "note.txt", string purpose = Purpose)
    {
        using var presigned = await Presign(tenant, Request(content, contentType, name, purpose));
        Assert.True(presigned.StatusCode == System.Net.HttpStatusCode.Created, await presigned.Content.ReadAsStringAsync());
        var upload = (await presigned.Content.ReadFromJsonAsync(FilesContractJson.PresignedUploadTypeInfo))!;
        using var put = await Put(upload, content);
        Assert.True(put.StatusCode == System.Net.HttpStatusCode.Created, await put.Content.ReadAsStringAsync());
        using var completed = await Send(tenant, HttpMethod.Post, "/api/files/" + upload.FileId + ":complete");
        Assert.True(completed.StatusCode == System.Net.HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        return (await completed.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo))!;
    }

    internal static PresignUploadRequest Request(byte[] content, string contentType = "text/plain", string name = "note.txt", string purpose = Purpose) =>
        new(Guid.CreateVersion7(), purpose, name, content.LongLength, contentType, Convert.ToHexStringLower(SHA256.HashData(content)));

    internal Task<int> Admin(string sql) => Admin(_admin, sql, TestContext.Current.CancellationToken);

    internal async Task<T?> AdminScalar<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is null or DBNull ? default : (T)result;
    }

    internal async Task<IReadOnlyList<string[]>> AdminRows(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture)!).ToArray());
        return rows;
    }

    // 绕过 EF 与应用层过滤，以受限业务登录直连：会话租户不同的行看不到。
    internal async Task<long> RestrictedCount(string tenant, Guid id)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(_business); await connection.OpenAsync(ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection))
        {
            set.Parameters.AddWithValue("tenant", tenant); await set.ExecuteScalarAsync(ct);
        }
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {FilesDbContext.Schema}.file_objects WHERE id = @id", connection);
        count.Parameters.AddWithValue("id", id);
        return (long)(await count.ExecuteScalarAsync(ct))!;
    }

    private static async Task<int> Admin(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (Platform is not null) await Platform.DisposeAsync();
        await _app.StopAsync(TestContext.Current.CancellationToken); await _runtime.DisposeAsync(); await _app.DisposeAsync(); _signing.Dispose();
        // 每个宿主都有独立受限登录，池不能积压到 PostgreSQL 的连接上限；只清理本宿主的池。
        using (var business = new NpgsqlConnection(_business)) NpgsqlConnection.ClearPool(business);
        using (var migration = new NpgsqlConnection(_migrations)) NpgsqlConnection.ClearPool(migration);
        try { Directory.Delete(StorageRoot, recursive: true); } catch (IOException) { }
    }
}

// 宿主的实际插件树。files 条目挂在 global，请求经按租户收窄的上下文进入能力管道；直传网关用 global 上下文（签名即授权，不读租户）。
internal sealed class FilesHostRuntime(IServiceProvider services, ITenantContext tenant) : IAsyncDisposable
{
    private readonly PluginLoader _loader = new(services);
    internal PluginLoader Loader => _loader;

    internal async Task MountAsync(JsonElement files, IReadOnlyList<InstalledComponentMetadata>? installed, FilesPlatform? platform, CancellationToken ct)
    {
        IReadOnlyList<PluginDefinition> registry = installed is null ? [] : [Define("registry", "qian.registry", new InstalledSnapshotPlugin(installed), [RegistryDefinition.Key])];
        IReadOnlyList<PluginDefinition> providers = [.. registry,
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("events", "qian.events", new EventsPlugin(), [EventsDefinition.Key]),
            Define("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey],
                new { resolver = new { mode = "production", allowHeaderFallback = false }, initializationCapabilities = Array.Empty<string>() }),
            // 授权组件持久化并登记 qian.files 的权限目录（plugins/qian.files/metadata），监听 tenant/provisioned 实例化内置角色与组件缺省策略。
            // 平台模式下 authz / audit 另配平台登录角色：前者提供超管显式旁路，后者承接平台审计。
            Define("authz", "qian.authz", new AuthzPlugin(platform is null ? [FilesMetadata.Catalog()] : [FilesMetadata.Catalog(), .. FilesPlatform.Catalogs()]),
                [CommandPolicies.AuthorizationKey, AuthzDefinition.Key],
                platform is null ? new { decisionLogSampling = 0d } : (object)new { decisionLogSampling = 0d, platformRole = platform.AuthzRole }),
            Define("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey],
                platform is null ? null : new { chain = new { platformRole = platform.AuditRole } }),
            // provides 取自仓库清单（含 [Contributes] 派生的 Files.Backends:local）：装载器在 Apply 后核对每个声明的服务键都已提供。
            Define("files", "qian.files", new FilesPlugin(), FilesMetadata.Provides(), files)];
        if (platform is not null) providers = [.. providers, platform.Registry(_loader, providers)];
        await _loader.MountAsync(providers, ct);
        foreach (var definition in providers)
            if (_loader.GetState(definition.Entry.Id, definition.Entry.Scope) != PluginState.Ready)
                throw new InvalidOperationException("验收宿主组件未就绪：" + definition.Entry.Id, _loader.GetFailure(definition.Entry.Id, definition.Entry.Scope));
    }

    // 与 Tenancy 开通编排的第 04 步相同：在目标租户 scope 派发 tenant/provisioned，再经授予端口给主体绑定角色。
    internal async Task ProvisionAsync(string id, string role, string subject, CancellationToken ct)
    {
        var scope = _loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", id));
        await scope.DispatchAsync(SurfaceHookEvents.TenantProvisioned, new HookNotification(scope, id, JsonSerializer.SerializeToElement(new { code = id[^2..] }), ct), ct);
        using var _ = tenant.BeginScope(id);
        await scope.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync(role, "service", subject, "test-bootstrap", ct);
    }

    internal IPluginContext Source()
    {
        if (!Guid.TryParseExact(tenant.TenantId, "D", out var id) || id == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try
        {
            var context = _loader.GetContext("files", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant.TenantId!));
            _ = context.Inject<IFileService>(FilesDefinition.Key); return context;
        }
        catch (Exception error) when (error is ServiceNotReadyException or KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    internal IPluginContext TransferSource()
    {
        try { return _loader.GetContext("files", ScopeKey.Global); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    internal IPluginContext AuthzSource()
    {
        if (!Guid.TryParseExact(tenant.TenantId, "D", out var id) || id == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try { return _loader.GetContext("authz", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant.TenantId!)); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    public ValueTask DisposeAsync() => _loader.DisposeAsync();

    private static PluginDefinition Define(string id, string component, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides, object? config = null) => new(
        new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, config as JsonElement? ?? JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
}

// 仓库里 qian.files 的权限与角色声明（plugins/qian.files/metadata），按授权组件的同一编译器编成目录。
internal static class FilesMetadata
{
    internal static PermissionCatalog Catalog()
    {
        var root = Path.Combine(RepositoryRoot(), "plugins", "qian.files", "metadata");
        using var permissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "permissions.json")));
        using var roles = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "roles.json")));
        return new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.files", "1.0.0", permissions.RootElement, roles.RootElement);
    }

    // 清单的 provides（具名项形如 Files.Backends:local，与 ctx.Provide(key, name, …) 提供的服务键同名）。
    internal static IReadOnlyList<ServiceKey> Provides()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins", "qian.files", ".qian-plugin", "plugin.json")));
        return [.. manifest.RootElement.GetProperty("provides").EnumerateArray().Select(item => new ServiceKey(item.GetString()!))];
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

// 预签名有效期与完成时限的可控时钟；令牌仍按真实时间签发。
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    internal void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

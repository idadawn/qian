using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Audit;
using Qian.Config.Apollo;
using Qian.Data;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

// S1-CFG-003 验收 3 后半句：真实 Apollo 2.5.2（AdminService 编辑并发布）一次发布 → 平台审计链多一条记录、链校验通过、记录不含配置值。
// 平台链按 Qian.Audit.Tests 的 AuditPlatformTests 装配：受限 NOINHERIT 登录角色、两条独立凭据、global AuditPlugin 显式配置 chain.platformRole。
// 与另外三个真实 Apollo 类同一集合、共用一套栈（RealApolloGroup）：计数只取本用例 appId 的配置发布记录，平台链在本类自己的 PostgreSQL 里。
[Collection(RealApolloGroup.Name)]
public sealed class RealApolloReleaseAuditTests(RealApolloPortalFixture apollo, PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] s_application = ["application"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 真实Apollo发布一次_平台审计链多一条已应用记录_链校验通过_持久化记录不含配置值()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-release-real-" + Guid.NewGuid().ToString("N"))).FullName;
        var appId = "qian-release-" + Guid.NewGuid().ToString("N")[..12];
        var role = "qian_audit_platform_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var platformConnection = await CreatePlatformRole(role);
            await apollo.CreateAppAsync(appId, Ct);
            await apollo.PublishAsync(appId, "Sample:PageSize", "10", Ct);
            var configuration = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
            {
                BasePath = directory,
                Arguments = ["--Apollo:AppId=" + appId, "--Apollo:Env=UAT", "--Apollo:Cluster=default", "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri.AbsoluteUri]
            }).Build();
            using var owner = (IDisposable)configuration;

            var tenant = new AsyncLocalTenantContext();
            var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
            builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus();
            builder.Services.AddQianConfiguration(configuration);
            builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
            builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(platformConnection).UseKunSchema(AuditDbContext.Schema));
            using var host = builder.Build(); await host.StartAsync(Ct);
            await Prepare(host);
            await Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;");

            await using var runtime = new PluginLoader(host.Services); var reader = new AuditReader();
            await runtime.MountAsync([
                Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key], new { }),
                Define("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg", platformRole = role } }),
                Define("config", "qian.config.apollo", new ApolloConfigurationPlugin(), [ConfigDefinition.Key],
                    new { appId, env = "UAT", meta = apollo.ConfigServiceUri.AbsoluteUri, namespaces = s_application }),
                Define("reader", "test.audit-reader", reader, [], new { })], Ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("config", ScopeKey.Global));
            var audit = reader.Context!.Audit();

            // 启动时实例拉取的快照先入链（回读信号），随后真实发布一次。
            var initial = await WaitForAsync(audit, reader.Context!, appId, 1);
            await apollo.PublishAsync(appId, "Sample:PageSize", "25", Ct);
            var entries = await WaitForAsync(audit, reader.Context!, appId, initial.Count + 1);
            Assert.Equal("25", configuration["Sample:PageSize"]);
            var released = entries[^1].Record;
            Assert.Equal((null, "service", appId, ConfigEvents.ReleaseAppliedCapabilityId, "event", "applied"),
                (released.TenantId, released.ActorType, released.ActorId, released.CapabilityId, released.Channel, released.Result));
            Assert.Equal($"apollo://{appId}/UAT/default/application", released.Resource);
            Assert.Equal(1, released.Payload.GetProperty("changedKeys").GetInt32());
            Assert.NotEqual(initial[^1].Record.InputHash, released.InputHash);
            var verification = await audit.VerifyPlatformAsync(reader.Context!, Ct);
            Assert.Equal(new AuditChainVerification(true, entries.Count, null, null), verification);

            await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
            await using var rows = new NpgsqlCommand("SELECT string_agg(to_jsonb(e)::text, '') FROM plugin_qian_audit.audit_entries e WHERE tenant_id IS NULL", connection);
            var persisted = (string)(await rows.ExecuteScalarAsync(Ct))!;
            Assert.DoesNotContain("PageSize", persisted, StringComparison.Ordinal);
            Assert.Contains(ConfigEvents.ReleaseAppliedCapabilityId, persisted, StringComparison.Ordinal);
            TestContext.Current.TestOutputHelper?.WriteLine($"平台审计链 {entries.Count} 条，最新一条为真实 Apollo 发布 {released.Resource}。");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<IReadOnlyList<AuditChainEntry>> WaitForAsync(IAuditService audit, IPluginContext producer, string appId, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        AuditChainEntry[] entries = [];
        while (DateTime.UtcNow < deadline)
        {
            entries = (await audit.ReadPlatformAsync(producer, 0, 100, cancellationToken: Ct)).Where(entry => entry.Record.CapabilityId.StartsWith("qian.config/", StringComparison.Ordinal)
                && entry.Record.Resource?.StartsWith("apollo://" + appId + "/", StringComparison.Ordinal) == true).ToArray();
            if (entries.Length >= count) return entries;
            await Task.Delay(200, Ct);
        }
        Assert.Fail($"平台审计链在 90 秒内只有 {entries.Length} 条配置发布记录，期望 {count} 条。");
        return entries;
    }

    private async Task<string> CreatePlatformRole(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
        return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
    }

    private async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }

    // 与 Qian.Audit.Tests 相同：按 EF 模型建出审计 schema，再把表权限授予受限应用角色。
    private async Task Prepare(IHost host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        await Admin("DROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;\n" + sql +
            "\nGRANT USAGE ON SCHEMA plugin_qian_audit TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_audit TO qian_app;");
    }

    private static PluginDefinition Define(string id, string plugin, IQianPlugin implementation, IReadOnlyList<ServiceKey> provides, object config) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config)), implementation, provides);

    private sealed class AuditReader : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [AuditDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }
}

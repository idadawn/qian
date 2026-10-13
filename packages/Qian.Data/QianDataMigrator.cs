using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Qian.Data;

/// <summary>迁移协调的宿主设置。</summary>
public sealed class QianDataMigrationOptions
{
    /// <summary>
    /// 迁移所用的连接：按上下文类型返回迁移属主角色的连接串，返回 null 时用上下文自身的连接。
    /// 属主角色同样必须是 NOSUPERUSER / NOBYPASSRLS；在线应用角色通常没有建表权限。
    /// </summary>
    public Func<IServiceProvider, Type, string?>? ResolveConnection { get; set; }

    /// <summary>等待其他副本释放迁移锁的上限，超时即失败。默认 10 分钟。</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>迁移协调的宿主装配与 <c>--migrate-only</c> 入口。</summary>
public static class QianDataMigrationExtensions
{
    /// <summary>K8s 迁移 Job 的启动参数：只执行迁移，随后退出。</summary>
    public const string MigrateOnlyArgument = "--migrate-only";

    /// <summary>设置迁移连接等选项；<see cref="IDataMigrator"/> 本身随 AddQianData 登记。</summary>
    public static IServiceCollection AddQianDataMigrations(this IServiceCollection services, Action<QianDataMigrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        return services;
    }

    /// <summary>启动参数是否要求只执行迁移。</summary>
    public static bool IsMigrateOnly(IEnumerable<string> args) => args.Contains(MigrateOnlyArgument, StringComparer.Ordinal);
}

/// <summary>
/// 逐个业务上下文：在属主连接上取 <c>pg_advisory_lock</c>（键由 schema 派生），锁内读待应用迁移、执行，再确认已无遗漏。
/// 锁按 schema 串行化同一插件的迁移，不同插件互不阻塞；EF 自带的迁移锁在锁内不再发生竞争。
/// </summary>
internal sealed partial class QianDataMigrator(IServiceScopeFactory scopes, IDataContextCatalog catalog, IServiceProvider services,
    IOptions<QianDataMigrationOptions> settings, ILogger<QianDataMigrator> logger) : IDataMigrator
{
    public async Task<IReadOnlyList<DataMigrationReport>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var options = settings.Value;
        if (options.LockTimeout <= TimeSpan.Zero) throw new QianDataMisconfiguredException("迁移锁等待上限必须为正。");
        var reports = new List<DataMigrationReport>();
        foreach (var type in catalog.ContextTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopes.CreateAsyncScope();
            var context = (QianDbContext)scope.ServiceProvider.GetRequiredService(type);
            var schema = context.Model.GetDefaultSchema() ?? throw new QianDataMisconfiguredException("上下文缺少插件 schema。");
            if (!context.Database.GetMigrations().Any())
            {
                reports.Add(new DataMigrationReport(type, schema, [], TimeSpan.Zero));
                continue;
            }
            if (options.ResolveConnection?.Invoke(services, type) is { Length: > 0 } connection) context.Database.SetConnectionString(connection);
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var key = LockKey(schema);
                var started = Stopwatch.GetTimestamp();
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(options.LockTimeout);
                    await ExecuteAsync(context, "SELECT pg_advisory_lock(@key)", key, timeout.Token).ConfigureAwait(false);
                }
                var waited = Stopwatch.GetElapsedTime(started);
                try
                {
                    var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
                    if (pending.Length > 0) await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                    var remaining = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
                    if (remaining.Length > 0) throw new QianDataMisconfiguredException("迁移执行后仍有未应用的迁移：" + string.Join(", ", remaining));
                    Log.Migrated(logger, schema, waited.TotalMilliseconds, pending.Length);
                    reports.Add(new DataMigrationReport(type, schema, pending, waited));
                }
                finally
                {
                    // 连接随后归还连接池，必须显式释放会话级 advisory lock。
                    await ExecuteAsync(context, "SELECT pg_advisory_unlock(@key)", key, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally { await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
        }
        return reports;
    }

    // 锁键只由 schema 决定：与连接串、宿主或副本无关，同一插件的迁移在所有副本间互斥。
    internal static long LockKey(string schema) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("qian.data.migrate:" + schema)));

    private static async Task ExecuteAsync(QianDbContext context, string sql, long key, CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 0;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void Register(IServiceCollection services)
    {
        services.AddOptions<QianDataMigrationOptions>();
        services.TryAddSingleton<IDataMigrator>(sp => new QianDataMigrator(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IDataContextCatalog>(),
            sp, sp.GetRequiredService<IOptions<QianDataMigrationOptions>>(), sp.GetRequiredService<ILogger<QianDataMigrator>>()));
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "数据迁移完成：schema {Schema}，等锁 {WaitedMs} 毫秒，执行 {Count} 个迁移。")]
        public static partial void Migrated(ILogger logger, string schema, double waitedMs, int count);
    }
}

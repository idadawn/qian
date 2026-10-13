using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Qian.Config;
using Qian.Runtime.Errors;

namespace Qian.Data.Dialect.Pg;

/// <summary>PostgreSQL 方言的宿主装配。</summary>
public static class QianPgDialectExtensions
{
    /// <summary>登记 PostgreSQL 方言（Data.Dialects:pg）；配合 AddQianDataTypes / AddQianDataConnections 使用。</summary>
    public static IServiceCollection AddQianPgDialect(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataDialect, PgDialect>());
        return services;
    }
}

/// <summary>
/// PostgreSQL 方言组件（qian.data.dialect.pg）：描述数据随包嵌入并在加载时按系统基准类型核对；连通探测用 Npgsql 的连接串构建器，
/// 不池化、不拼接字符串，错误只给出结构化原因。
/// </summary>
public sealed class PgDialect : IDataDialect
{
    private static readonly DbDialect Data = Read();

    /// <inheritdoc />
    public DbDialect Descriptor => Data;

    /// <inheritdoc />
    public async ValueTask<DataConnectionProbe> ProbeAsync(DataConnectionTarget target, SecretValue password, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var seconds = (int)Math.Ceiling(timeout.TotalSeconds);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = target.Host,
            Port = target.Port,
            Database = target.Database,
            Username = target.UserName,
            Password = password.RevealUtf8(),
            // 探测连接用完即关：不进连接池，避免以租户凭据长期占用目标库的连接。
            Pooling = false,
            Timeout = seconds,
            CommandTimeout = seconds,
            ApplicationName = "qian.data connection test"
        };
        if (target.Schema is not null) builder.SearchPath = target.Schema;
        if (target.Encoding is not null) builder.ClientEncoding = target.Encoding;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
            if (target.Schema is not null)
            {
                await using var command = new NpgsqlCommand("SELECT has_schema_privilege(oid, 'USAGE') FROM pg_namespace WHERE nspname = @schema", connection);
                command.Parameters.AddWithValue("schema", target.Schema);
                var usage = await command.ExecuteScalarAsync(deadline.Token).ConfigureAwait(false);
                if (usage is not bool allowed) throw Rejected("schema", null);
                if (!allowed) throw Rejected("permission", null);
            }
            return new DataConnectionProbe(connection.ServerVersion);
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unreachable(error);
        }
        catch (PostgresException error)
        {
            throw error.SqlState switch
            {
                PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification => Rejected("authentication", error),
                PostgresErrorCodes.InvalidCatalogName => Rejected("database", error),
                PostgresErrorCodes.InsufficientPrivilege => Rejected("permission", error),
                PostgresErrorCodes.InvalidParameterValue => Rejected("parameter", error),
                // 08 连接异常、53 资源不足、57 管理员干预（如正在启动或关闭）都是暂时不可用，可以重试。
                _ when error.SqlState.StartsWith("08", StringComparison.Ordinal) || error.SqlState.StartsWith("53", StringComparison.Ordinal)
                    || error.SqlState.StartsWith("57", StringComparison.Ordinal) => Unreachable(error),
                _ => Rejected("server", error)
            };
        }
        catch (NpgsqlException error)
        {
            // 没有服务端错误码：DNS、拒绝连接、超时、TLS 或协议层失败。
            throw Unreachable(error);
        }
    }

    private static QianErrorException Unreachable(Exception inner) => new(new QianError(DataErrors.ConnectionUnreachable), inner);

    private static QianErrorException Rejected(string reason, Exception? inner) =>
        new(new QianError(DataErrors.ConnectionRejected, new Dictionary<string, string> { ["reason"] = reason }), inner);

    private static DbDialect Read()
    {
        using var stream = typeof(PgDialect).Assembly.GetManifestResourceStream("qian.data.dialect.pg.json")
            ?? throw new InvalidOperationException("PostgreSQL 方言数据未打包。");
        return DataTypeContracts.ReadDialect(stream);
    }
}

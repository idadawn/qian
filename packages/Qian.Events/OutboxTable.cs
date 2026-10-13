using System.Collections.Concurrent;
using System.Data.Common;
using Kun.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Qian.Data;

namespace Qian.Events;

/// <summary>某个业务上下文的 outbox_messages 表；表名与列名取自生效的 EF 模型，SQL 按上下文类型缓存。</summary>
/// <remarks>
/// 领取用一条语句完成：<c>FOR UPDATE SKIP LOCKED</c> 选出到期行并把 next_attempt_at 推到租约到期时刻，随即提交。
/// 已确认的消息逐条标记发布，持有者崩溃时只有未确认的行在租约到期后被其他副本接管；租约到期时刻同时充当这次领取的持有凭据。
/// 到期判断与租约都取数据库时钟，副本之间的时钟偏差不会让租约提前失效。
/// </remarks>
internal sealed class OutboxTable
{
    private static readonly ConcurrentDictionary<(Type Context, string Schema), OutboxTable> Cache = new();

    private OutboxTable(string schema, string claim, string published, string retry, string release)
    {
        Schema = schema;
        ClaimSql = claim;
        PublishedSql = published;
        RetrySql = retry;
        ReleaseSql = release;
    }

    public string Schema { get; }
    private string ClaimSql { get; }
    private string PublishedSql { get; }
    private string RetrySql { get; }
    private string ReleaseSql { get; }

    // 同一上下文类型在不同宿主可以绑定不同的插件 schema，缓存键带上生效 schema。
    public static OutboxTable For(QianDbContext context) =>
        Cache.GetOrAdd((context.GetType(), context.Model.GetDefaultSchema() ?? ""), _ => Build(context));

    private static OutboxTable Build(QianDbContext context)
    {
        if (context.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            throw new QianEventsException(EventsErrors.Misconfigured, "Outbox 投递要求 PostgreSQL 上下文。");
        var entity = context.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new QianEventsException(EventsErrors.Misconfigured, "上下文模型缺少 Outbox 表。");
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var sql = context.GetService<ISqlGenerationHelper>();
        string Column(string property) => sql.DelimitIdentifier(entity.FindProperty(property)?.GetColumnName(store)
            ?? throw new QianEventsException(EventsErrors.Misconfigured, "Outbox 表缺少列映射：" + property));
        var table = sql.DelimitIdentifier(store.Name, store.Schema);
        var id = Column(nameof(OutboxMessage.Id));
        var status = Column(nameof(OutboxMessage.Status));
        var next = Column(nameof(OutboxMessage.NextAttemptAt));
        var attempts = Column(nameof(OutboxMessage.AttemptCount));
        var error = Column(nameof(OutboxMessage.LastError));
        var claim = $"""
            UPDATE {table} SET {next} = now() + CAST(@lease AS interval)
            WHERE {id} IN (
              SELECT {id} FROM {table}
              WHERE {status} = 0 AND ({next} IS NULL OR {next} <= now())
              ORDER BY {Column(nameof(OutboxMessage.OccurredAt))}, {id}
              LIMIT @batch
              FOR UPDATE SKIP LOCKED)
            RETURNING {id}, {Column(nameof(OutboxMessage.EventTypeId))}, {Column(nameof(OutboxMessage.Payload))}, {Column(nameof(OutboxMessage.TenantId))},
              {Column(nameof(OutboxMessage.TraceParent))}, {Column(nameof(OutboxMessage.TraceState))}, {Column(nameof(OutboxMessage.OccurredAt))}, {attempts}, {next}
            """;
        var published = $"UPDATE {table} SET {status} = 1, {Column(nameof(OutboxMessage.PublishedAt))} = now(), {next} = NULL, {error} = NULL WHERE {id} = @id AND {status} = 0";
        var retry = $"UPDATE {table} SET {attempts} = @attempts, {status} = @status, {next} = now() + CAST(@delay AS interval), {error} = @error WHERE {id} = @id AND {status} = 0";
        // 只交还仍由本次领取持有的行：租约到期后已被其他副本重新领取的行，next_attempt_at 已是对方的租约。
        var release = $"UPDATE {table} SET {next} = NULL WHERE {id} = ANY(@ids) AND {status} = 0 AND {next} = @lease_until";
        return new OutboxTable(store.Schema ?? "public", claim, published, retry, release);
    }

    public async Task<IReadOnlyList<ClaimedMessage>> ClaimAsync(DbConnection connection, TimeSpan lease, int batch, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, ClaimSql, ("lease", lease), ("batch", batch));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ClaimedMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ClaimedMessage(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), Text(reader, 3), Text(reader, 4), Text(reader, 5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8)));
        }
        // RETURNING 不保证顺序，按 Outbox 的发生顺序发布。
        return rows.OrderBy(row => row.OccurredAt).ThenBy(row => row.Id).ToArray();
    }

    public Task MarkPublishedAsync(DbConnection connection, Guid id, CancellationToken cancellationToken) =>
        Execute(Command(connection, PublishedSql, ("id", id)), cancellationToken);

    /// <summary>记一次失败；<paramref name="delay"/> 为 null 时行标为失败，不再领取。</summary>
    public Task MarkRetryAsync(DbConnection connection, Guid id, int attempts, TimeSpan? delay, string error, CancellationToken cancellationToken) =>
        Execute(Command(connection, RetrySql, ("id", id), ("attempts", attempts), ("status", delay is null ? 2 : 0), ("delay", delay),
            ("error", error.Length <= 2000 ? error : error[..2000])), cancellationToken);

    /// <summary>交还同一次领取中尚未发布的行；它们共用一个租约到期时刻。</summary>
    public Task ReleaseAsync(DbConnection connection, IReadOnlyList<ClaimedMessage> rows, CancellationToken cancellationToken) =>
        rows.Count == 0 ? Task.CompletedTask
            : Execute(Command(connection, ReleaseSql, ("ids", rows.Select(row => row.Id).ToArray()), ("lease_until", rows[0].LeaseUntil)), cancellationToken);

    private static async Task Execute(DbCommand command, CancellationToken cancellationToken)
    {
        await using (command) await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbCommand Command(DbConnection connection, string text, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = text;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static string? Text(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}

/// <summary>已领取、尚未确认的一行；<see cref="LeaseUntil"/> 是数据库写入的租约到期时刻。</summary>
internal sealed record ClaimedMessage(Guid Id, string EventTypeId, string Payload, string? TenantId, string? TraceParent, string? TraceState,
    DateTimeOffset OccurredAt, int AttemptCount, DateTimeOffset LeaseUntil)
{
    public OutboxMessage ToMessage() => new(Id, EventTypeId, Payload, TenantId, TraceParent, TraceState, OccurredAt);
}

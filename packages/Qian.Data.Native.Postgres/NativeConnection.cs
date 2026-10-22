using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Qian.Data.Native.Postgres;

internal sealed class NativeConnection(NativeSession session, NpgsqlConnection raw) : DbConnection
{
    [AllowNull] public override string ConnectionString { get => string.Empty; set => throw NativeErrors.Denied(); }
    public override string Database => raw.Database;
    public override string DataSource => raw.DataSource;
    public override string ServerVersion => raw.ServerVersion;
    public override ConnectionState State => raw.State;
    public override int ConnectionTimeout => raw.ConnectionTimeout;
    public override void ChangeDatabase(string databaseName) => throw NativeErrors.Denied();
    public override void Open() => throw NativeErrors.Denied();
    public override Task OpenAsync(CancellationToken cancellationToken) => throw NativeErrors.Denied();
    public override void Close() => session.DisposeAsync().AsTask().GetAwaiter().GetResult();
    public override Task CloseAsync() => session.DisposeAsync().AsTask();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => session.BeginTransactionAsync(isolationLevel).AsTask().GetAwaiter().GetResult();
    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => session.BeginTransactionAsync(isolationLevel, cancellationToken);
    protected override DbCommand CreateDbCommand() => new NativeCommand(session, this, session.Command());
    protected override void Dispose(bool disposing) { if (disposing) Close(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await session.DisposeAsync().ConfigureAwait(false); await base.DisposeAsync().ConfigureAwait(false); }
    public override void EnlistTransaction(System.Transactions.Transaction? transaction) => throw NativeErrors.Denied();
}

internal sealed class NativeTransaction(NativeSession session, NpgsqlTransaction transaction, IsolationLevel level) : DbTransaction
{
    internal NativeSession Session => session;
    internal NpgsqlTransaction Raw => transaction;
    internal bool Completed { get; set; }
    public override IsolationLevel IsolationLevel => level;
    protected override DbConnection? DbConnection => Completed ? null : session.Connection;
    public override void Commit() => CommitAsync().GetAwaiter().GetResult();
    public override Task CommitAsync(CancellationToken cancellationToken = default)
    { if (Completed) throw NativeErrors.Denied(); return session.CommitAsync(cancellationToken).AsTask(); }
    public override void Rollback() => RollbackAsync().GetAwaiter().GetResult();
    public override Task RollbackAsync(CancellationToken cancellationToken = default) => Completed ? Task.CompletedTask : session.RollbackAsync(cancellationToken).AsTask();
    protected override void Dispose(bool disposing) { if (disposing && !Completed) Rollback(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { if (!Completed) await session.RollbackAsync().ConfigureAwait(false); await base.DisposeAsync().ConfigureAwait(false); }
}

internal sealed class NativeCommand(NativeSession session, NativeConnection connection, NpgsqlCommand raw) : DbCommand
{
    [AllowNull] public override string CommandText { get => raw.CommandText; set => raw.CommandText = value; }
    public override int CommandTimeout { get => raw.CommandTimeout; set => raw.CommandTimeout = value; }
    public override CommandType CommandType { get => raw.CommandType; set => raw.CommandType = value; }
    public override bool DesignTimeVisible { get => raw.DesignTimeVisible; set => raw.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => raw.UpdatedRowSource; set => raw.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => connection; set { if (!ReferenceEquals(connection, value)) throw NativeErrors.Denied(); } }
    protected override DbTransaction? DbTransaction
    {
        get => session.Transaction;
        set { if (value is not null && (value is not NativeTransaction transaction || transaction.Session != session || transaction.Completed)) throw NativeErrors.Denied(); }
    }
    protected override DbParameterCollection DbParameterCollection => raw.Parameters;
    protected override DbParameter CreateDbParameter() => new NpgsqlParameter();
    public override void Cancel() => raw.Cancel();
    public override int ExecuteNonQuery() => ExecuteNonQueryAsync().GetAwaiter().GetResult();
    public override object? ExecuteScalar() => ExecuteScalarAsync().GetAwaiter().GetResult();
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => session.RunAsync(ct => { Bind(); return raw.ExecuteNonQueryAsync(ct); }, cancellationToken);
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => session.RunAsync(ct => { Bind(); return raw.ExecuteScalarAsync(ct); }, cancellationToken);
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => ExecuteDbDataReaderAsync(behavior, CancellationToken.None).GetAwaiter().GetResult();
    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        if ((behavior & CommandBehavior.CloseConnection) != 0) throw NativeErrors.Denied(); // 连接只能由 session 的清理路径归还。
        return await session.RunAsync<DbDataReader>(async ct => { Bind(); return new NativeReader(session, await raw.ExecuteReaderAsync(behavior, ct).ConfigureAwait(false)); }, cancellationToken).ConfigureAwait(false);
    }
    public override void Prepare() => PrepareAsync().GetAwaiter().GetResult();
    public override async Task PrepareAsync(CancellationToken cancellationToken = default) =>
        await session.RunAsync(async ct => { Bind(); await raw.PrepareAsync(ct).ConfigureAwait(false); return 0; }, cancellationToken).ConfigureAwait(false);
    private void Bind() => raw.Transaction = session.RawTransaction;
    protected override void Dispose(bool disposing) { if (disposing) raw.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await raw.DisposeAsync().ConfigureAwait(false); await base.DisposeAsync().ConfigureAwait(false); }
}

internal sealed class NativeReader(NativeSession session, DbDataReader raw) : DbDataReader
{
    private T ReadValue<T>(Func<T> read) { session.Guard(); return read(); }
    public override object this[int ordinal] => ReadValue(() => raw[ordinal]);
    public override object this[string name] => ReadValue(() => raw[name]);
    public override int Depth => raw.Depth;
    public override int FieldCount => raw.FieldCount;
    public override bool HasRows => ReadValue(() => raw.HasRows);
    public override bool IsClosed => raw.IsClosed;
    public override int RecordsAffected => raw.RecordsAffected;
    public override bool GetBoolean(int ordinal) => ReadValue(() => raw.GetBoolean(ordinal));
    public override byte GetByte(int ordinal) => ReadValue(() => raw.GetByte(ordinal));
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => ReadValue(() => raw.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length));
    public override char GetChar(int ordinal) => ReadValue(() => raw.GetChar(ordinal));
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => ReadValue(() => raw.GetChars(ordinal, dataOffset, buffer, bufferOffset, length));
    public override string GetDataTypeName(int ordinal) => ReadValue(() => raw.GetDataTypeName(ordinal));
    public override DateTime GetDateTime(int ordinal) => ReadValue(() => raw.GetDateTime(ordinal));
    public override decimal GetDecimal(int ordinal) => ReadValue(() => raw.GetDecimal(ordinal));
    public override double GetDouble(int ordinal) => ReadValue(() => raw.GetDouble(ordinal));
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) { session.Guard(); return raw.GetFieldType(ordinal); }
    public override float GetFloat(int ordinal) => ReadValue(() => raw.GetFloat(ordinal));
    public override Guid GetGuid(int ordinal) => ReadValue(() => raw.GetGuid(ordinal));
    public override short GetInt16(int ordinal) => ReadValue(() => raw.GetInt16(ordinal));
    public override int GetInt32(int ordinal) => ReadValue(() => raw.GetInt32(ordinal));
    public override long GetInt64(int ordinal) => ReadValue(() => raw.GetInt64(ordinal));
    public override string GetName(int ordinal) => raw.GetName(ordinal);
    public override int GetOrdinal(string name) => raw.GetOrdinal(name);
    public override string GetString(int ordinal) => ReadValue(() => raw.GetString(ordinal));
    public override object GetValue(int ordinal) => ReadValue(() => raw.GetValue(ordinal));
    public override int GetValues(object[] values) => ReadValue(() => raw.GetValues(values));
    public override bool IsDBNull(int ordinal) => ReadValue(() => raw.IsDBNull(ordinal));
    public override bool NextResult() => ReadValue(raw.NextResult);
    public override bool Read() => ReadValue(raw.Read);
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    { session.Guard(); var value = await raw.ReadAsync(cancellationToken).ConfigureAwait(false); session.Guard(); return value; }
    public override async Task<bool> NextResultAsync(CancellationToken cancellationToken)
    { session.Guard(); var value = await raw.NextResultAsync(cancellationToken).ConfigureAwait(false); session.Guard(); return value; }
    public override System.Collections.IEnumerator GetEnumerator() => new DbEnumerator(this, false);
    public override void Close() => raw.Close();
    protected override void Dispose(bool disposing) { if (disposing) raw.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await raw.DisposeAsync().ConfigureAwait(false); await base.DisposeAsync().ConfigureAwait(false); }
}

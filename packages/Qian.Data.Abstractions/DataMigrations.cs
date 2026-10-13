namespace Qian.Data;

/// <summary>
/// 多副本迁移协调：逐个业务上下文，在按 schema 取得的 PostgreSQL advisory lock 内执行未应用的 EF 迁移。
/// 同时启动的副本只有一个执行 DDL，其余副本等锁，拿到锁后发现已无待应用迁移即只做校验。
/// K8s 迁移 Job 以 <c>--migrate-only</c> 调用它；在线副本启动时只校验、不迁移。
/// </summary>
public interface IDataMigrator
{
    /// <summary>迁移宿主登记的全部业务上下文；返回每个上下文的执行结果，迁移后仍有待应用项即抛出。</summary>
    Task<IReadOnlyList<DataMigrationReport>> MigrateAsync(CancellationToken cancellationToken = default);
}

/// <summary>一个业务上下文的迁移结果。</summary>
/// <param name="ContextType">业务上下文类型。</param>
/// <param name="Schema">插件 schema，也是 advisory lock 的作用范围。</param>
/// <param name="Applied">本副本实际执行的迁移；为空表示其他副本已执行或本来就没有待应用的迁移。</param>
/// <param name="LockWait">等待 advisory lock 的时长。</param>
public sealed record DataMigrationReport(Type ContextType, string Schema, IReadOnlyList<string> Applied, TimeSpan LockWait);

using System.Data.Common;
using Qian.Runtime;

namespace Qian.Audit;

/// <summary>组件间的审计 seam，不是客户端可直接调用的审计写入 HTTP API。</summary>
public static class AuditDefinition
{
    /// <summary>当前 scope 可见的审计服务键。</summary>
    public static readonly ServiceKey Key = new("Audit");
    /// <summary>解析当前插件可见的审计服务。</summary>
    public static IAuditService Audit(this IPluginContext context) => context.Inject<IAuditService>(Key);
}
/// <summary>可信组件生成记录后的追加端口；独立追加不替代业务同事务写入。</summary>
public interface IAuditWriter
{
    /// <summary>追加当前租户的记录；缺失租户拒绝。</summary>
    Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    /// <summary>将租户审计加入现有 PostgreSQL 可串行化业务事务；返回节点只有在外层提交后才持久化，冲突须由调用方重试整个业务事务。</summary>
    Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default);
    /// <summary>显式追加平台记录；需提供仍有效的 global 生产方上下文，并显式配置专用平台角色；租户调用拒绝。</summary>
    Task<AuditChainEntry> AppendPlatformAsync(IPluginContext producer, AuditRecord record, CancellationToken cancellationToken = default);
}
/// <summary>组件内读取端口；面向用户的查询必须由宿主另行鉴权并审计。</summary>
public interface IAuditReader
{
    /// <summary>分页读取当前租户的审计记录。</summary>
    Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default);
    /// <summary>读取平台链，不自动跨租户。</summary>
    Task<IReadOnlyList<AuditChainEntry>> ReadPlatformAsync(IPluginContext producer, long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default);
    /// <summary>验证当前租户的链。</summary>
    Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default);
    /// <summary>验证显式平台链。</summary>
    Task<AuditChainVerification> VerifyPlatformAsync(IPluginContext producer, CancellationToken cancellationToken = default);
}
/// <summary>Audit seam 的读写服务。</summary>
public interface IAuditService : IAuditWriter, IAuditReader;

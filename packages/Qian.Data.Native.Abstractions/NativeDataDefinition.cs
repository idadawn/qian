using System.Data;
using System.Data.Common;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Data.Native;

/// <summary>原生连接与事务的独立 seam，不实现或替代 EF Data。</summary>
public static class NativeDataDefinition
{
    /// <summary>NativeData 服务键。</summary>
    public static readonly ServiceKey Key = new("NativeData");
    /// <summary>解析当前可见的原生数据服务。</summary>
    public static INativeDataService NativeData(this IPluginContext context) => context.Inject<INativeDataService>(Key);
}

/// <summary>仅访问受信任宿主登记的连接绑定；不接受连接串或任意角色名。</summary>
public interface INativeDataService
{
    /// <summary>按当前已验证租户打开连接；缺租户、错误绑定或服务已卸载时拒绝。</summary>
    ValueTask<INativeDataSession> OpenTenantAsync(string binding, CancellationToken cancellationToken = default);
    /// <summary>显式打开专用平台登录连接；调用方必须仍有效、处于 global，且当前没有租户。不能以租户调用或角色成员关系冒充。</summary>
    ValueTask<INativeDataSession> OpenPlatformAsync(string binding, IPluginContext caller, CancellationToken cancellationToken = default);
    /// <summary>校验受管或外部真实租户事务的同库绑定、登录角色、GUC、当前租户与 SERIALIZABLE 隔离级别。既不提交、回滚，也不重试外部事务。</summary>
    ValueTask ValidateTenantTransactionAsync(string binding, DbTransaction transaction, CancellationToken cancellationToken = default);
}

/// <summary>拥有连接与事务的会话。命令只能来自可信组件；释放回滚未完成事务、清理会话并释放连接，组件卸载同样释放活动会话。</summary>
public interface INativeDataSession : IAsyncDisposable
{
    /// <summary>带生命周期、角色与租户守卫的已打开连接；可以先获取 session advisory lock，再开启事务。</summary>
    DbConnection Connection { get; }
    /// <summary>当前未完成事务；没有事务时为 null。</summary>
    DbTransaction? Transaction { get; }
    /// <summary>会话捕获的租户 UUID；显式平台会话为 null，不表示自动取得平台权限。</summary>
    string? TenantId { get; }
    /// <summary>开始事务时再次校验调用上下文，并设置事务级租户 GUC；嵌套事务拒绝。</summary>
    ValueTask<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
    /// <summary>提交当前事务；取消或提交失败时会话不得继续执行。</summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    /// <summary>回滚当前事务；没有事务时为空操作。</summary>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
    /// <summary>验证会话仍有效，当前上下文、数据库登录与 GUC 仍符合绑定。消费者不需要也不得自行改租户 GUC。</summary>
    ValueTask ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>不含连接凭据的原生数据错误词汇。</summary>
public static class NativeDataErrors
{
    /// <summary>连接绑定、受限角色、表或 RLS 保护不符合要求。</summary>
    public static readonly ErrorCode Misconfigured = ErrorCode.Protocol("NATIVE_DATA_MISCONFIGURED", 500, "qian.data.native",
        new Dictionary<string, string> { ["zh-CN"] = "原生数据装配无效", ["en"] = "Invalid native data configuration" },
        new Dictionary<string, string> { ["zh-CN"] = "原生数据的连接绑定、角色或租户隔离不满足要求。", ["en"] = "The native data binding, role or tenant isolation does not meet its requirements." });
}

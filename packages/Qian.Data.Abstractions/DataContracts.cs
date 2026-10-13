using Kun.EntityFrameworkCore;
using Kun.EventBus.Outbox;
using Qian.Runtime;

namespace Qian.Data;

/// <summary>数据 seam 的稳定服务键；业务组件只依赖 Definition。</summary>
public static class DataDefinition
{
    /// <summary>当前 scope 可见的数据 Provider。</summary>
    public static readonly ServiceKey Key = new("Data");

    /// <summary>解析已挂载的数据 Provider。</summary>
    public static IDataService Data(this IPluginContext context) => context.Inject<IDataService>(Key);
}

/// <summary>创建独立工作单元；不把不同业务上下文绑定到一个全局 KunDbContext。</summary>
public interface IDataService
{
    /// <summary>打开已由组件注册的上下文；调用方负责释放会话。</summary>
    ValueTask<IDataSession<TContext>> OpenAsync<TContext>(CancellationToken cancellationToken = default) where TContext : QianDbContext;
}

/// <summary>同一上下文中的业务变更、领域通知与 Outbox 保存边界。</summary>
public interface IDataSession<out TContext> : IUnitOfWork, IAsyncDisposable where TContext : QianDbContext
{
    /// <summary>此会话独占的业务上下文；不能跨线程并发使用。</summary>
    TContext Context { get; }
    /// <summary>暂存到此会话的 Outbox；只有 SaveChangesAsync 成功后才落库。</summary>
    IOutboxWriter Outbox { get; }
}

/// <summary>
/// 替换 Kun 默认累加器的 Outbox 写入端（如事件组件的信封写入端）声明自己写入的缓冲器；
/// 数据会话只接受写入当前 scope 缓冲器的实现，保证事件随同一次保存落库。
/// </summary>
public interface IOutboxBufferBinding
{
    /// <summary>写入端追加消息的缓冲器，必须是当前 scope 的实例。</summary>
    IntegrationEventBuffer Buffer { get; }
}

/// <summary>宿主经 AddQianData 登记的全部业务上下文；Outbox 投递等宿主级组件据此逐个处理各上下文的表。</summary>
public interface IDataContextCatalog
{
    /// <summary>登记顺序的上下文类型，均继承 <see cref="QianDbContext"/>。</summary>
    IReadOnlyList<Type> ContextTypes { get; }
}

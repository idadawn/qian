using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore;
using Kun.EventBus;
using Kun.EventBus.Outbox;
using Kun.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

namespace Qian.Data;

internal sealed class QianDataService(IServiceScopeFactory scopes, IReadOnlySet<Type> contexts, ScopeKey ownerScope) : IDataService, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly HashSet<IAsyncDisposable> _sessions = [];
    private bool _disposed;

    public async ValueTask<IDataSession<TContext>> OpenAsync<TContext>(CancellationToken cancellationToken = default) where TContext : QianDbContext
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = scopes.CreateAsyncScope();
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!contexts.Contains(typeof(TContext)))
                    throw new QianDataMisconfiguredException("所请求上下文不属于 Data 当前连接引用的登记集合。");
                if (ownerScope.Kind == "tenant" && scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId != ownerScope.Id)
                    throw new QianDataMisconfiguredException("Data 挂载 scope 与当前已验证租户不一致。");
                var session = new Session<TContext>(scope, Remove);
                _sessions.Add(session);
                return session;
            }
        }
        catch { await scope.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        IAsyncDisposable[] sessions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            sessions = _sessions.ToArray();
            _sessions.Clear();
        }
        List<Exception> errors = [];
        foreach (var session in sessions)
        {
            try { await session.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0) throw new AggregateException("数据会话释放失败。", errors);
    }

    private void Remove(IAsyncDisposable session) { lock (_gate) _sessions.Remove(session); }

    private sealed class Session<TContext> : IDataSession<TContext> where TContext : QianDbContext
    {
        private readonly AsyncServiceScope _scope;
        private readonly Action<IAsyncDisposable> _remove;
        private readonly TContext _context;
        private readonly IOutboxWriter _outbox;
        private readonly EfUnitOfWork _unit;
        private readonly IntegrationEventBuffer _buffer;
        private readonly ITenantContext _tenant;
        private readonly string? _tenantId;
        private readonly SemaphoreSlim _operation = new(1, 1);
        private bool _failed;
        private bool _disposed;

        public Session(AsyncServiceScope scope, Action<IAsyncDisposable> remove)
        {
            _scope = scope;
            _remove = remove;
            var services = scope.ServiceProvider;
            _context = services.GetService<TContext>()
                ?? throw new QianDataMisconfiguredException("业务组件没有登记所请求的数据上下文。");
            _tenant = services.GetRequiredService<ITenantContext>();
            _tenantId = _tenant.TenantId;
            _buffer = services.GetRequiredService<IntegrationEventBuffer>();
            var writer = services.GetRequiredService<IOutboxWriter>();
            // 默认是 AddQianData 登记的 Kun 累加器；事件组件可换成写同一缓冲器的信封写入端。
            var bound = writer switch
            {
                IOutboxBufferBinding binding => ReferenceEquals(binding.Buffer, _buffer),
                IntegrationEventOutboxAccumulator accumulator => ReferenceEquals(accumulator, services.GetService<IntegrationEventOutboxAccumulator>()),
                _ => false
            };
            if (!bound || !ReferenceEquals(writer, services.GetRequiredService<IIntegrationEventBus>()))
                throw new QianDataMisconfiguredException("数据会话的事件总线和 Outbox 写入端口必须是写入当前缓冲器的同一个实例。");
            _outbox = writer;
            _unit = new EfUnitOfWork(_context, new SessionDispatcher(services, EnsureBoundary), _buffer);
        }

        public TContext Context { get { EnsureUsable(); return _context; } }
        public IOutboxWriter Outbox { get { EnsureUsable(); return _outbox; } }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureUsable();
                try
                {
                    EnsureBoundary();
                    return await _unit.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch { _failed = true; throw; }
            }
            finally { _operation.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            await _operation.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                _disposed = true;
                try { await _scope.DisposeAsync().ConfigureAwait(false); }
                finally { _remove(this); }
            }
            finally { _operation.Release(); }
        }

        private void EnsureUsable()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_failed) throw new InvalidOperationException("保存失败后必须释放数据会话，从新作用域重试。");
            if (_tenant.TenantId != _tenantId || _context.TenantIdForModelCacheKey != _tenantId)
                throw new QianDataMisconfiguredException("数据会话不得在创建后切换租户上下文。");
        }

        private void EnsureBoundary()
        {
            EnsureUsable();
            if (_buffer.GetPendingMessages().Any(message => message.TenantId != _tenantId))
                throw new QianDataMisconfiguredException("Outbox 消息必须属于当前数据会话的租户。");
        }
    }

    private sealed class SessionDispatcher(IServiceProvider services, Action validate) : IDomainEventDispatcher
    {
        public async ValueTask DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count == 0) return;
            validate();
            if (events.Any(item => item is not INotification))
                throw new QianDataMisconfiguredException("数据会话的领域事件必须实现 Kun INotification，不能静默丢弃通知。");
            var publisher = services.GetService<IPublisher>()
                ?? throw new QianDataMisconfiguredException("有领域通知时必须装配 Kun Mediator。");
            await new InProcessDomainEventDispatcher(publisher, services).DispatchAsync(events, cancellationToken).ConfigureAwait(false);
            validate();
        }
    }
}

using System.Collections.Concurrent;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Sample.Crm;

/// <summary>
/// 参考宿主自己的订阅（S1-EVT-002）：客户建档事件到达后，在订阅器按信封 tenant_id 回灌的租户里经 EF 读回该客户，记一条内存回执。
/// 租户过滤与行级安全都按回灌的租户生效，别的租户的客户在这里查不到；查不到属于确定性失败，消息直接进死信队列。
/// </summary>
internal sealed class CustomerCreatedHandler(IDataService data, ITenantContext tenant, CustomerCreatedReceipts receipts) : IIntegrationEventHandler<CrmCustomerCreated>
{
    public async ValueTask HandleAsync(CrmCustomerCreated @event, MessageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        await using var session = await data.OpenAsync<CrmDbContext>(cancellationToken).ConfigureAwait(false);
        if (!await session.Context.Customers.AsNoTracking().AnyAsync(customer => customer.Id == @event.CustomerId, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("客户不在事件所属租户内。");
        receipts.Add(new CustomerCreatedReceipt(context.MessageId, tenant.TenantId!, @event.CustomerId));
    }
}

/// <summary>已处理的客户建档事件回执，只保留最近 1000 条；放在进程内存里，供联调与演示查询，重启即清空。</summary>
public sealed class CustomerCreatedReceipts
{
    private readonly ConcurrentQueue<CustomerCreatedReceipt> _items = new();

    /// <summary>按处理顺序的回执快照。</summary>
    public IReadOnlyList<CustomerCreatedReceipt> Items => [.. _items];

    internal void Add(CustomerCreatedReceipt receipt)
    {
        _items.Enqueue(receipt);
        while (_items.Count > 1000) _items.TryDequeue(out _);
    }
}

/// <summary>一条回执：事件 id（信封 event_id）、handler 内的租户与客户 id。</summary>
public sealed record CustomerCreatedReceipt(Guid EventId, string TenantId, Guid CustomerId);

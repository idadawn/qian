using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Kun.Core.Cqrs;
using Kun.EventBus;
using Microsoft.EntityFrameworkCore;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm;

// 客户名称是个人信息：响应含它的 customer.create / list / get 都标为 pii，通用执行入口的幂等回执不保存其响应体，同键重放返回 409 CAPABILITY_REPLAY_UNAVAILABLE。
[Capability("customer.create", typeof(CustomerItem), typeof(CrmCommandJson), Description = "为当前租户创建客户；重复提交同一幂等键会返回原创建结果。",
    SideEffect = "write", Idempotent = true, DataClassification = "pii", RequiredPermission = "qian.sample.crm:customer:write", RequiredScopes = ["crm.write"], ContractVersion = "crm-v1", AgentExpose = true)]
public sealed partial record CreateCustomerCommand(Guid IdempotencyKey, [property: Sensitive] string Name);

internal sealed class CreateCustomerHandler(CommandDataSession<CrmDbContext> work) : IRequestHandler<CreateCustomerCommand, CustomerItem>
{
    public async ValueTask<CustomerItem> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || request.Name.Any(char.IsControl))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var name = request.Name.Trim(); var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
        var db = work.Context;
        var receipt = await db.CustomerReceipts.SingleOrDefaultAsync(row => row.IdempotencyKey == request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (receipt.RequestHash != fingerprint) throw new QianErrorException(new QianError(PlatformErrors.Conflict));
            return new CustomerItem(receipt.CustomerId, receipt.TenantId!, receipt.Name);
        }
        var customer = new Customer(Guid.CreateVersion7(), name);
        db.Customers.Add(customer);
        db.CustomerReceipts.Add(new CustomerReceipt { TenantId = db.TenantIdForModelCacheKey, IdempotencyKey = request.IdempotencyKey, CustomerId = customer.Id, Name = name, RequestHash = fingerprint });
        work.Outbox.Enqueue(new CrmCustomerCreated(customer.Id));
        return new CustomerItem(customer.Id, db.TenantIdForModelCacheKey!, name);
    }
}

[Capability("customer.list", typeof(CustomerPage), typeof(CrmCommandJson), Description = "分页查看当前租户的客户列表，默认不产生查询审计。", DataClassification = "pii",
    RequiredPermission = "qian.sample.crm:customer:read", RequiredScopes = ["crm.read"], ContractVersion = "crm-v1", AgentExpose = true)]
public sealed partial record ListCustomersQuery(int Skip = 0, int Take = 100);

[Capability("customer.get", typeof(CustomerItem), typeof(CrmCommandJson), Description = "查看当前租户一个客户的详情，并记录这次详情访问。", Audit = true, DataClassification = "pii",
    RequiredPermission = "qian.sample.crm:customer:read", RequiredScopes = ["crm.read"], ContractVersion = "crm-v1", AgentExpose = true)]
public sealed partial record GetCustomerQuery(Guid Id);

// Agent 字段探针（S1-AGT-006、R112）：每个 Agent 字段都取与上面三条不同的值，policyHint 取 destructive 的非缺省值 deny（缺省为 confirm），
// 目录读取时按其它字段派生或写死任一字段都会与声明和提交的工具文件不符。只在 crm.admin scope 下可见；S1 没有异步任务的执行器（Operation 资源，R8），
// 调用一律 503 UNAVAILABLE，不删除任何数据。
[Capability("customer.purge", typeof(CustomerPurgeResult), typeof(CrmCommandJson), Description = "清除当前租户的全部客户且不可恢复；只供租户管理员人工确认后发起，S1 只登记契约、不执行。",
    SideEffect = "destructive", PolicyHint = "deny", Idempotent = true, Async = "task", RequiredSecrets = ["crm-archive-key"], DataClassification = "internal", CostTier = "high",
    RequiredPermission = "qian.sample.crm:customer:write", RequiredScopes = ["crm.admin"], ContractVersion = "crm-admin-v1", AgentExpose = false, RequiresConfirmation = true)]
public sealed partial record PurgeCustomersCommand(Guid IdempotencyKey);

/// <summary>清除完成后的结果：async 为 task 时输出 Schema 描述 Operation 完成后的 result。</summary>
public sealed record CustomerPurgeResult(int Purged);

internal sealed class PurgeCustomersHandler : IRequestHandler<PurgeCustomersCommand, CustomerPurgeResult>
{
    public ValueTask<CustomerPurgeResult> Handle(PurgeCustomersCommand request, CancellationToken cancellationToken) =>
        throw new QianErrorException(new QianError(PlatformErrors.Unavailable));
}

internal sealed class ListCustomersHandler(CommandDataSession<CrmDbContext> work) : IRequestHandler<ListCustomersQuery, CustomerPage>
{
    public async ValueTask<CustomerPage> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        if (request.Skip < 0 || request.Take is < 1 or > 100) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var query = work.Context.Customers.AsNoTracking(); var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.TagWith(CrmApplication.ListQueryTag).OrderBy(customer => customer.Id).Skip(request.Skip).Take(request.Take)
            .Select(customer => new CustomerItem(customer.Id, customer.TenantId!, customer.Name)).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new CustomerPage(total, rows);
    }
}
internal sealed class GetCustomerHandler(CommandDataSession<CrmDbContext> work) : IRequestHandler<GetCustomerQuery, CustomerItem>
{
    public async ValueTask<CustomerItem> Handle(GetCustomerQuery request, CancellationToken cancellationToken) =>
        await work.Context.Customers.AsNoTracking().Where(customer => customer.Id == request.Id)
            .Select(customer => new CustomerItem(customer.Id, customer.TenantId!, customer.Name)).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
}

/// <summary>参考应用的稳定事件 ID；破坏性载荷改动必须升版本，不能原地改名。</summary>
public static class CrmEvents
{
    /// <summary>客户已创建。</summary>
    public const string CustomerCreated = "crm.customer.created.v1";

    /// <summary>登记参考应用的全部集成事件及其源生成 JsonTypeInfo；宿主与契约测试共用这一处。</summary>
    public static IEventTypeRegistrar RegisterCrmEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        return registrar.Register(CustomerCreated, CrmCommandJson.Default.CrmCustomerCreated);
    }
}
[IntegrationEventId(CrmEvents.CustomerCreated)]
internal sealed record CrmCustomerCreated(Guid CustomerId);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(CreateCustomerCommand))]
[JsonSerializable(typeof(ListCustomersQuery))]
[JsonSerializable(typeof(GetCustomerQuery))]
[JsonSerializable(typeof(PurgeCustomersCommand))]
[JsonSerializable(typeof(CustomerPurgeResult))]
[JsonSerializable(typeof(CustomerPage))]
[JsonSerializable(typeof(CustomerItem))]
[JsonSerializable(typeof(CrmCustomerCreated))]
internal sealed partial class CrmCommandJson : JsonSerializerContext;

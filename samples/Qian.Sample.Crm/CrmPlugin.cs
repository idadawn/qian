using System.Text.Json;
using Kun.Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm;

public static class CrmDefinition
{
    public static readonly ServiceKey Key = new("Crm");
}
public interface ICrmCommands
{
    Task<CustomerItem> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default);
    Task<CustomerPage> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken = default);
    Task<CustomerItem> GetAsync(GetCustomerQuery query, CancellationToken cancellationToken = default);
}

[QianPlugin("qian.sample.crm")]
[QianConfigSchema("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}""")]
public sealed class CrmPlugin : IQianPlugin, IPluginLifecycleHooks
{
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, CommandPolicies.TenancyKey, CommandPolicies.AuthorizationKey, CommandPolicies.AuditKey];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        ctx.Provide<ICrmCommands>(CrmDefinition.Key, new CrmCommands(ctx.Services.GetRequiredService<ISender>()));
        ctx.ProvideCapability<CreateCustomerCommand, CustomerItem>();
        ctx.ProvideCapability<ListCustomersQuery, CustomerPage>();
        ctx.ProvideCapability<GetCustomerQuery, CustomerItem>();
        ctx.ProvideCapability<PurgeCustomersCommand, CustomerPurgeResult>();
        ctx.ExposeCapabilityHook<CreateCustomerCommand, CustomerItem>("seedCustomer", notification =>
            new CreateCustomerCommand(notification.Payload.GetProperty("idempotencyKey").GetGuid(), notification.Payload.GetProperty("name").GetString()!));
        ctx.ExposeCapabilityHook<ListCustomersQuery, CustomerPage>("listCustomers", _ => new ListCustomersQuery());
        return ValueTask.CompletedTask;
    }

    // 生命周期钩子（qian.lifecycle.hook/v1）：开通时为租户种一条示例客户，主键取幂等键，重试或重复调用都不会多写；
    // 1.0.x 之间表结构不变，升级与回切没有数据迁移。种子数据不经命令处理器，不发客户创建事件。
    public async ValueTask<JsonElement?> InvokeAsync(LifecycleHookContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Hook != LifecycleHooks.TenantProvision) return null;
        await using var session = await context.Context.Data().OpenAsync<CrmDbContext>(cancellationToken).ConfigureAwait(false);
        var seeded = !await session.Context.Customers.AnyAsync(customer => customer.Id == context.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (seeded)
        {
            session.Context.Customers.Add(new Customer(context.IdempotencyKey, SampleCustomer));
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return JsonSerializer.SerializeToElement(new { seeded });
    }

    public const string SampleCustomer = "示例客户";
}
internal sealed class CrmCommands(ISender sender) : ICrmCommands
{
    public async Task<CustomerItem> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await sender.Send(command, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(10 * (attempt + 1), cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (Retryable(error)) { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        }
    }
    public Task<CustomerPage> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken = default) => sender.Send(query, cancellationToken).AsTask();
    public Task<CustomerItem> GetAsync(GetCustomerQuery query, CancellationToken cancellationToken = default) => sender.Send(query, cancellationToken).AsTask();
    private static bool Retryable(Exception error)
    {
        // Npgsql 的默认执行策略会把瞬态数据库异常再包一层 InvalidOperationException。
        // 只拆已知框架包装，仍按明确 SQLSTATE 决定是否重试整笔命令。
        var current = error;
        while (current is DbUpdateException or InvalidOperationException && current.InnerException is { } inner) current = inner;
        var postgres = current as PostgresException;
        return postgres is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } ||
            postgres is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_customer_receipts" };
    }
}

using System.Text.Json;
using Kun.Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Data;
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
public sealed class CrmPlugin : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, CommandPolicies.TenancyKey, CommandPolicies.AuthorizationKey, CommandPolicies.AuditKey];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ctx.Provide<ICrmCommands>(CrmDefinition.Key, new CrmCommands(ctx.Services.GetRequiredService<ISender>()));
        ctx.ProvideCapability<CreateCustomerCommand, CustomerItem>();
        ctx.ProvideCapability<ListCustomersQuery, CustomerPage>();
        ctx.ProvideCapability<GetCustomerQuery, CustomerItem>();
        return ValueTask.CompletedTask;
    }
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
        var postgres = error as PostgresException ?? (error as DbUpdateException)?.InnerException as PostgresException;
        return postgres is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } ||
            postgres is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_customer_receipts" };
    }
}

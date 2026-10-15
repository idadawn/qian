using System.Text.Json;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Kun.EventBus.DependencyInjection;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Events.Tests;

/// <summary>qian.record.changed.v1 按本组件的模型发布：登记的是载荷，信封由写入端生成，after Hook 视图从收到的信封派生。</summary>
public sealed class RecordEventEnvelopeTests
{
    private const string Tenant = "0190f3a2-7c41-7b3e-9d2a-5c8e1f4a6b20";
    private static readonly EvaluationOptions Strict = new() { OutputFormat = OutputFormat.List, RequireFormatValidation = true };
    private static readonly Lazy<JsonSchema> RecordEnvelope = new(() =>
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(DataContractSchemas.S2sClaimsV0, options);
        JsonSchema.FromText(DataContractSchemas.EventEnvelopeV0, options);
        JsonSchema.FromText(DataContractSchemas.RecordChangedPayload, options);
        return JsonSchema.FromText(DataContractSchemas.RecordChangedEnvelope, options);
    });

    [Fact]
    public void 记录变更载荷经写入端只生成一层v1信封_通过记录信封Schema并派生after_Hook视图()
    {
        using var provider = Services();
        using var scope = provider.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var origin = new EventOrigin("qian.dynamic-data", EventActor.Agent("agent-client-1", "person-1")) { CausationId = Guid.CreateVersion7() };
        using var before = JsonDocument.Parse("{\"name\":\"张三\"}");
        using var after = JsonDocument.Parse("{\"name\":\"张四\"}");
        using (tenant.BeginScope(Tenant))
        using (EventEnvelopeScope.Begin(origin))
        {
            var payload = new RecordChangedPayload("crm.customer", "customer-1", "updated", Guid.Parse(Tenant), RecordActor.From(origin.Actor),
                ["name"], before.RootElement.Clone(), after.RootElement.Clone());
            payload.EnsureConsistentContext(tenant);
            scope.ServiceProvider.GetRequiredService<IOutboxWriter>().Enqueue(payload);
        }
        var message = Assert.Single(scope.ServiceProvider.GetRequiredService<IntegrationEventBuffer>().GetPendingMessages());
        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;
        Assert.Equal(RecordChangedPayload.EventType, message.EventTypeId);
        Assert.False(root.GetProperty("payload").TryGetProperty("event_id", out _));
        Assert.Empty(EnvelopeSchemas.ValidateQian(root));
        Assert.True(RecordEnvelope.Value.Evaluate(root, Strict).IsValid, message.Payload);
        var change = RecordChangedEvent.FromEnvelope(QianEventEnvelope.Parse(message.Payload));
        Assert.Equal(message.Id, change.EventId);
        Assert.Equal(Guid.Parse(Tenant), change.TenantId);
        Assert.Equal("qian.dynamic-data", change.Envelope.Producer);
        Assert.Equal(origin.CausationId, change.Envelope.CausationId);
        Assert.Equal(("crm.customer", "customer-1", "updated"), (change.Payload.EntityCode, change.Payload.RecordId, change.Payload.Op));
        Assert.Equal("张四", change.Payload.After!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public void 主体与事件来源不一致的记录载荷_入队前核对拒绝_消费端也拒绝派生视图()
    {
        using var provider = Services();
        using var scope = provider.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        // Agent 代表用户行事，载荷却只写了被代表的用户、丢了委托关系。
        var payload = new RecordChangedPayload("crm.customer", "customer-1", "created", Guid.Parse(Tenant), new RecordActor("user", "person-1"), ["name"]);
        using (tenant.BeginScope(Tenant))
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.dynamic-data", EventActor.Agent("agent-client-1", "person-1"))))
        {
            Assert.Throws<InvalidOperationException>(() => payload.EnsureConsistentContext(tenant));
            // 跳过核对直接入队时写入端照常生成信封，消费端派生视图时仍会拒绝。
            scope.ServiceProvider.GetRequiredService<IOutboxWriter>().Enqueue(payload);
        }
        var message = Assert.Single(scope.ServiceProvider.GetRequiredService<IntegrationEventBuffer>().GetPendingMessages());
        Assert.Throws<InvalidOperationException>(() => RecordChangedEvent.FromEnvelope(QianEventEnvelope.Parse(message.Payload)));
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus(events => events.RegisterRecordEvents());
        services.AddQianEvents();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}

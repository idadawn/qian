using Json.Schema;
using Kun.Core.MultiTenancy;
using Kun.EventBus.DependencyInjection;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;

namespace Qian.Events.Tests;

/// <summary>身份事件按本组件的模型发布（S1-DEC-020）：event_id、occurred_at 与 tenant_id 只在写入端生成的信封上，载荷只有身份投影。</summary>
public sealed class IdentityEventEnvelopeTests
{
    private const string Tenant = "0190f3a2-7c41-7b3e-9d2a-5c8e1f4a6b20";

    [Fact]
    public void 身份事件的event_id_occurred_at与tenant_id只出现在信封上_平台级事件tenant_id为null()
    {
        using var provider = Services();
        using var scope = provider.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var person = Guid.CreateVersion7();
        using (tenant.BeginScope(Tenant))
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.identity.server", EventActor.User(person.ToString("D")))))
            writer.Enqueue(new IdentityLoginEvent(person, 3, Guid.CreateVersion7(), "web", ["pwd"]));
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.identity.server", EventActor.Service("qian-identity"))))
            writer.Enqueue(new IdentityLockedEvent(person, 4, "policy"));
        var messages = scope.ServiceProvider.GetRequiredService<IntegrationEventBuffer>().GetPendingMessages().ToArray();
        Assert.Equal(2, messages.Length);
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(File.ReadAllText(Path.Combine(EnvelopeSchemas.RepositoryRoot, "contracts/v0/s2s-claims.schema.json")), options);
        foreach (var message in messages)
        {
            var envelope = QianEventEnvelope.Parse(message.Payload);
            Assert.Equal(message.Id, envelope.EventId);
            Assert.Equal(7, envelope.EventId.Version);
            Assert.DoesNotContain(envelope.Payload.EnumerateObject(), member => member.Name is "event_id" or "occurred_at" or "tenant_id");
            Assert.True(JsonSchema.FromText(IdentitySchemas.Event(envelope.Type), options).Evaluate(envelope.Payload).IsValid, message.Payload);
        }
        Assert.Equal((IdentityEventIds.Login, Guid.Parse(Tenant)), (messages[0].EventTypeId, QianEventEnvelope.Parse(messages[0].Payload).TenantId));
        Assert.Equal((IdentityEventIds.Locked, (Guid?)null), (messages[1].EventTypeId, QianEventEnvelope.Parse(messages[1].Payload).TenantId));
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus(events => events.RegisterIdentityEvents());
        services.AddQianEvents();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}

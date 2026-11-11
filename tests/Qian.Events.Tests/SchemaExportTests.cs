using System.Text.Json;
using Json.Schema;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;

namespace Qian.Events.Tests;

public sealed class SchemaExportTests
{
    [Fact]
    public void 身份组件的每个IntegrationEventId都能由JsonTypeInfo导出payload_Schema且实例通过校验()
    {
        var registry = Registry(builder => builder.RegisterIdentityEvents());
        var catalog = IntegrationEventSchemas.Describe(registry);
        var declared = typeof(IdentityLoginEvent).Assembly.GetTypes()
            .Select(type => type.GetCustomAttributes(typeof(IntegrationEventIdAttribute), false).OfType<IntegrationEventIdAttribute>().SingleOrDefault()?.EventType)
            .OfType<string>().Order(StringComparer.Ordinal).ToArray();
        // 身份组件程序集声明的事件 = 身份事件 + 目录事件（S1-IDP-010），两者都随 RegisterIdentityEvents 登记。
        Assert.Equal(IdentityEventIds.All.Concat(DirectoryEventIds.All).Order(StringComparer.Ordinal), declared);
        Assert.Equal(declared, catalog.Select(item => item.Type));
        foreach (var item in catalog)
        {
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", item.PayloadSchema.GetProperty("$schema").GetString());
            Assert.Equal("object", item.PayloadSchema.GetProperty("type").GetString());
            Assert.True(item.PayloadSchema.GetProperty("properties").EnumerateObject().Any(), item.Type);
            Assert.True(IntegrationEventTypes.TryParse(item.Type, out var subject, out var version));
            Assert.Equal((subject, version), (item.Subject, item.Version));
        }
        var login = new IdentityLoginEvent(Guid.CreateVersion7(), 3, Guid.CreateVersion7(), "web", ["pwd"]);
        var schema = JsonSchema.FromText(catalog.Single(item => item.Type == IdentityEventIds.Login).PayloadSchema.GetRawText());
        var instance = JsonSerializer.SerializeToElement(login, IdentityJson.EventTypeInfo(IdentityEventIds.Login)!);
        Assert.True(schema.Evaluate(instance).IsValid);
        var created = new DirectoryMemberCreatedEvent(7, Guid.CreateVersion7(), Guid.CreateVersion7(), "active", 3);
        var createdSchema = JsonSchema.FromText(catalog.Single(item => item.Type == DirectoryEventIds.MemberCreated).PayloadSchema.GetRawText());
        Assert.True(createdSchema.Evaluate(JsonSerializer.SerializeToElement(created, IdentityJson.EventTypeInfo(DirectoryEventIds.MemberCreated)!)).IsValid);
    }

    [Fact]
    public void 事件ID与类型声明不一致或缺少JsonTypeInfo时拒绝建目录()
    {
        var mismatch = Registry(builder => builder.Register("tests.ping.renamed.v1", TestEventJson.Default.PingSent));
        Assert.Equal(EventsErrors.Misconfigured, Assert.Throws<QianEventsException>(() => IntegrationEventSchemas.Describe(mismatch)).Error.Code);
        var reflected = Registry(builder => builder.Register<Reflected>("tests.reflected.created.v1"));
        Assert.Equal(EventsErrors.Misconfigured, Assert.Throws<QianEventsException>(() => IntegrationEventSchemas.Describe(reflected)).Error.Code);
        var badToken = Registry(builder => builder.Register("tests.ping", TestEventJson.Default.PingSent));
        Assert.Equal(EventsErrors.Misconfigured, Assert.Throws<QianEventsException>(() => IntegrationEventSchemas.Describe(badToken)).Error.Code);
    }

    private static IEventTypeRegistry Registry(Action<IEventTypeRegistrar> register)
    {
        var services = new ServiceCollection();
        services.AddKunEventBus(builder => register(builder));
        return services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
    }
}

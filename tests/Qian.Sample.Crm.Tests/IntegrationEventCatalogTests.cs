using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Events;
using Qian.Identity.Abstractions;

namespace Qian.Sample.Crm.Tests;

public sealed class IntegrationEventCatalogTests
{
    [Fact]
    public void 仓库内每个IntegrationEventId都经组件登记入口带源生成JsonTypeInfo_并能导出payload_Schema()
    {
        // 扫描本测试输出里的全部 Qian 程序集；新组件声明了事件却没有登记入口时这里失败，提醒在下方补上它的入口。
        var declared = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "Qian.*.dll"))
        {
            Type?[] types;
            try { types = Assembly.LoadFrom(file).GetTypes(); }
            catch (ReflectionTypeLoadException error) { types = error.Types; }
            foreach (var type in types.OfType<Type>())
                if (type.GetCustomAttribute<IntegrationEventIdAttribute>() is { } attribute) Assert.True(declared.TryAdd(attribute.EventType, type), attribute.EventType);
        }
        Assert.Contains(CrmEvents.CustomerCreated, declared.Keys);
        Assert.Contains(IdentityEventIds.Login, declared.Keys);
        Assert.Same(typeof(RecordChangedPayload), declared[RecordChangedPayload.EventType]);
        var services = new ServiceCollection();
        services.AddKunEventBus(events => events.RegisterIdentityEvents().RegisterRecordEvents().RegisterCrmEvents());
        var catalog = IntegrationEventSchemas.Describe(services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>());
        Assert.Equal(declared.Keys, catalog.Select(item => item.Type));
        foreach (var item in catalog)
        {
            Assert.Equal("object", item.PayloadSchema.GetProperty("type").GetString());
            Assert.Equal(QianEventTopology.RoutingKey(item.Type), item.Subject);
            // payload 不能再嵌一封信封：trace_parent 与 payload 只出现在写入端生成的外层信封里。
            Assert.DoesNotContain(Members(item.PayloadSchema), member => member is "trace_parent" or "payload");
        }
        var created = catalog.Single(item => item.Type == CrmEvents.CustomerCreated);
        Assert.Equal(("crm.customer.created", 1), (created.Subject, created.Version));
        Assert.True(created.PayloadSchema.GetProperty("properties").TryGetProperty("customerId", out _));
        // 记录事件登记的是载荷：目录导出的 payload Schema 与随包发布的手写 Schema 成员、必填项一致，并接受真实载荷。
        var record = catalog.Single(item => item.Type == RecordChangedPayload.EventType);
        using var published = JsonDocument.Parse(DataContractSchemas.RecordChangedPayload);
        Assert.Equal(Members(published.RootElement), Members(record.PayloadSchema));
        Assert.Equal(Required(published.RootElement), Required(record.PayloadSchema));
        using var before = JsonDocument.Parse("{\"name\":\"张三\"}");
        var payload = new RecordChangedPayload("crm.customer", "customer-1", "deleted", Guid.CreateVersion7(), new RecordActor("agent", "agent-1", "person-1"),
            ["name"], before.RootElement.Clone());
        Assert.True(JsonSchema.FromText(record.PayloadSchema.GetRawText()).Evaluate(JsonSerializer.SerializeToElement(payload, DataContractJson.RecordChangedPayloadTypeInfo)).IsValid);
    }

    private static string[] Members(JsonElement schema) =>
        schema.GetProperty("properties").EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToArray();

    private static string[] Required(JsonElement schema) => schema.TryGetProperty("required", out var required)
        ? required.EnumerateArray().Select(member => member.GetString()!).Order(StringComparer.Ordinal).ToArray()
        : [];
}

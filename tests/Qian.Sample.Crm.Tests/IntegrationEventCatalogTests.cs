using System.Reflection;
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
        Assert.Contains(RecordChangedEvent.EventType, declared.Keys);
        var services = new ServiceCollection();
        services.AddKunEventBus(events => events.RegisterIdentityEvents().RegisterRecordEvents().RegisterCrmEvents());
        var catalog = IntegrationEventSchemas.Describe(services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>());
        Assert.Equal(declared.Keys, catalog.Select(item => item.Type));
        foreach (var item in catalog)
        {
            Assert.Equal("object", item.PayloadSchema.GetProperty("type").GetString());
            Assert.Equal(QianEventTopology.RoutingKey(item.Type), item.Subject);
        }
        var created = catalog.Single(item => item.Type == CrmEvents.CustomerCreated);
        Assert.Equal(("crm.customer.created", 1), (created.Subject, created.Version));
        Assert.True(created.PayloadSchema.GetProperty("properties").TryGetProperty("customerId", out _));
    }
}

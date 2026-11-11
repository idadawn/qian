using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Kun.Core.Domain;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Identity.Abstractions.Tests;

// directory.*.v1（S1-IDP-010 验收 3）：稳定 ID、类型标记、源生成 JSON 与随 Definition 分发的 JSON Schema 一一对应。
public sealed class DirectoryEventContractsTests
{
    private static readonly string Root = FindRoot();
    private static readonly Guid A = Guid.Parse("0193a000-0000-7000-8000-000000000001"), B = Guid.Parse("0193a000-0000-7000-8000-000000000002"),
        C = Guid.Parse("0193a000-0000-7000-8000-000000000003"), D = Guid.Parse("0193a000-0000-7000-8000-000000000004");
    private static string PathOf(params Guid[] ids) => "/" + string.Concat(ids.Select(id => id.ToString("D") + "/"));

    // 每个事件一条「可选字段都给」的样例和一条「可选字段都不给」的样例。
    private static IEnumerable<(DirectoryIntegrationEvent Full, DirectoryIntegrationEvent Minimal)> Samples()
    {
        MemberRelation[] relations = [new("organization", A, true), new("position", B), new("group", C, true)];
        yield return (new DirectoryOrganizationCreatedEvent(1, B, A, "dept", PathOf(A, B), 2, C, D), new DirectoryOrganizationCreatedEvent(1, A, null, "company", PathOf(A), 1));
        yield return (new DirectoryOrganizationUpdatedEvent(2, B, A, "dept", PathOf(A, B), 2, C, D), new DirectoryOrganizationUpdatedEvent(2, A, null, "company", PathOf(A), 1));
        yield return (new DirectoryOrganizationMovedEvent(3, C, B, A, PathOf(A, B, C), PathOf(A, C), 3), new DirectoryOrganizationMovedEvent(3, C, null, null, PathOf(C), PathOf(A, C), 1));
        yield return (new DirectoryOrganizationDeletedEvent(4, C, B, PathOf(A, B, C)), new DirectoryOrganizationDeletedEvent(4, A, null, PathOf(A)));
        yield return (new DirectoryPositionCreatedEvent(5, B, A, C, PathOf(C, B), 2, true, true, D), new DirectoryPositionCreatedEvent(5, B, A, null, PathOf(B), 1, false, false));
        yield return (new DirectoryPositionUpdatedEvent(6, B, A, C, PathOf(C, B), 2, true, true, D), new DirectoryPositionUpdatedEvent(6, B, A, null, PathOf(B), 1, false, false));
        yield return (new DirectoryPositionMovedEvent(7, B, A, C, D, PathOf(C, B), PathOf(D, B), 2), new DirectoryPositionMovedEvent(7, B, A, null, null, PathOf(B), PathOf(D, B), 1));
        yield return (new DirectoryPositionDeletedEvent(8, B, A, C, PathOf(C, B)), new DirectoryPositionDeletedEvent(8, B, A, null, PathOf(B)));
        yield return (new DirectoryMemberCreatedEvent(9, A, B, "active", 0), new DirectoryMemberCreatedEvent(9, A, B, "active", 0));
        yield return (new DirectoryMemberRelationsChangedEvent(10, A, B, 3, relations, [relations[0]]), new DirectoryMemberRelationsChangedEvent(10, A, B, 3, [], []));
        yield return (new DirectoryMemberStatusChangedEvent(11, A, B, "disabled", "active", true, 4), new DirectoryMemberStatusChangedEvent(11, A, B, "locked", "disabled", false, 5));
        yield return (new DirectoryMemberProfileUpdatedEvent(12, A, B), new DirectoryMemberProfileUpdatedEvent(12, A, B));
    }

    private static string IdOf(Type type) => type.GetCustomAttribute<IntegrationEventIdAttribute>()!.EventType;

    [Fact]
    public void 目录事件ID稳定唯一_类型标记一一对应_随身份事件入口登记源生成元数据()
    {
        var constants = typeof(DirectoryEventIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToArray();
        Assert.Equal(12, constants.Length);
        Assert.Equal(constants.Order(), DirectoryEventIds.All.Order());
        Assert.Equal(DirectoryEventIds.All.Count, DirectoryEventIds.All.Distinct().Count());
        Assert.All(DirectoryEventIds.All, id => Assert.Matches("^directory\\.(org|position|member)\\.[a-z_]+\\.v1$", id));
        Assert.Empty(DirectoryEventIds.All.Intersect(IdentityEventIds.All));
        var types = typeof(DirectoryIntegrationEvent).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(DirectoryIntegrationEvent)) && !type.IsAbstract).ToArray();
        Assert.Equal(DirectoryEventIds.All.Order(), types.Select(IdOf).Order());
        Assert.Equal(DirectoryEventIds.All.Order(), Samples().Select(sample => IdOf(sample.Full.GetType())).Order());
        var services = new ServiceCollection();
        services.AddKunEventBus(builder => builder.RegisterIdentityEvents());
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IEventTypeRegistry>();
        foreach (var type in types)
        {
            Assert.Same(type, registry.Resolve(IdOf(type)));
            Assert.NotNull(registry.GetJsonTypeInfo(type));
            Assert.Same(IdentityJson.EventTypeInfo(IdOf(type)), registry.GetJsonTypeInfo(type));
        }
        // 载荷类型不是 Kun 的 IIntegrationEvent：event_id、occurred_at、tenant_id 只在写入端生成的信封上（S1-DEC-020）。
        Assert.False(typeof(IIntegrationEvent).IsAssignableFrom(typeof(DirectoryIntegrationEvent)));
        Assert.DoesNotContain(A.ToString(), Samples().First().Full.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void 目录事件Schema与源生成JSON逐字段一致_样例通过_缺字段与多字段被拒_不含信封字段与个人信息()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "contracts/v0/s2s-claims.schema.json")), options);
        string[] forbidden = ["event_id", "occurred_at", "tenant_id", "name", "display_name", "account", "pinyin", "phone", "email", "password", "token"];
        foreach (var (full, minimal) in Samples())
        {
            var id = IdOf(full.GetType());
            var text = IdentitySchemas.Event(id);
            Assert.Equal(text, File.ReadAllText(Path.Combine(Root, "contracts/identity/v1", id + ".schema.json")));
            using var published = JsonDocument.Parse(text);
            var root = published.RootElement;
            Assert.Equal($"https://qian.dev/contracts/identity/v1/{id}.schema.json", root.GetProperty("$id").GetString());
            Assert.False(root.GetProperty("additionalProperties").GetBoolean());
            Assert.True(root.GetProperty("description").GetString()!.Length >= 10, id);
            var typeInfo = IdentityJson.EventTypeInfo(id)!;
            var members = root.GetProperty("properties").EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(typeInfo.Properties.Select(property => property.Name).Order(StringComparer.Ordinal), members);
            // 可空的 id 字段是可选的，其余都必填；修订号每条都有。
            var required = root.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(typeInfo.Properties.Where(property => Nullable.GetUnderlyingType(property.PropertyType) is null).Select(property => property.Name).Order(StringComparer.Ordinal), required);
            Assert.Contains("directory_revision", required);
            Assert.Empty(members.Intersect(forbidden));
            var schema = JsonSchema.FromText(text, options);
            foreach (var sample in new[] { full, minimal })
            {
                using var payload = JsonDocument.Parse(JsonSerializer.Serialize(sample, typeInfo));
                Assert.True(schema.Evaluate(payload.RootElement).IsValid, id + "：" + payload.RootElement.GetRawText());
                var withoutRevision = JsonNode.Parse(payload.RootElement.GetRawText())!.AsObject(); withoutRevision.Remove("directory_revision");
                Assert.False(schema.Evaluate(JsonSerializer.SerializeToElement(withoutRevision)).IsValid, id);
                var extra = JsonNode.Parse(payload.RootElement.GetRawText())!.AsObject(); extra["display_name"] = "张三";
                Assert.False(schema.Evaluate(JsonSerializer.SerializeToElement(extra)).IsValid, id);
            }
            using var all = JsonDocument.Parse(JsonSerializer.Serialize(full, typeInfo));
            Assert.Equal(members, all.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

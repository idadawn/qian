using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Events;
using Qian.Runtime.Errors;

namespace Qian.Data.Abstractions.Tests;

public sealed class DataContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly IReadOnlyDictionary<string, string> Fields =
        JsonSerializer.Deserialize<Dictionary<string, string>>(Fixture("field-types.json"))!;

    [Fact]
    public void PublishedFilterFixture_PassesSchemaAndSemanticValidation_AndRoundTrips()
    {
        var raw = Fixture("filter-valid.json");
        AssertValid(DataContractSchemas.Filter, raw);
        var filter = JsonSerializer.Deserialize(raw, DataContractJson.FilterTypeInfo)!;
        Assert.Empty(FilterContracts.Validate(filter, Fields));
        AssertValid(DataContractSchemas.Filter, JsonSerializer.Serialize(filter, DataContractJson.FilterTypeInfo));
        Assert.Equal("currentUserAndAllSubordinates", filter.Groups[0].Conditions[1].SysVar);
    }

    [Fact]
    public void BooleanBetween_IsRejectedBySharedFieldTypeMatrix()
    {
        var raw = Fixture("filter-invalid-between-bool.json");
        AssertValid(DataContractSchemas.Filter, raw); // 结构有效，类型信息来自可信字段目录。
        var filter = JsonSerializer.Deserialize(raw, DataContractJson.FilterTypeInfo)!;
        var error = Assert.Single(FilterContracts.Validate(filter, Fields));
        Assert.Equal("#/groups/0/conditions/0/op", error.Path);
        Assert.DoesNotContain("between", FilterContracts.Operators["bool"]);
    }

    [Theory]
    [InlineData("eq", "1", null, true)]
    [InlineData("eq", "\"1\"", null, false)]
    [InlineData("in", "[1,2]", null, true)]
    [InlineData("in", "[]", null, false)]
    [InlineData("in", "[1,\"x\"]", null, false)]
    [InlineData("between", "[1,2]", null, true)]
    [InlineData("between", "[1]", null, false)]
    [InlineData("between", "[1,2,3]", null, false)]
    [InlineData("eq", "null", null, false)]
    [InlineData("isNull", null, null, true)]
    [InlineData("notNull", "1", null, false)]
    [InlineData("eq", null, "currentUser", false)]
    [InlineData("eq", "1", "currentUser", false)]
    [InlineData("EQ", "1", null, false)]
    public void OperatorArityAndTypedValues_FailClosed(string op, string? raw, string? variable, bool valid)
    {
        var value = raw is null ? (JsonElement?)null : JsonDocument.Parse(raw).RootElement.Clone();
        var errors = FilterContracts.Validate(One(new("age", op, value, variable)), Fields);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("tenant_id")]
    [InlineData("TENANT_ID")]
    [InlineData("nested.tenantId")]
    [InlineData("name;DROP TABLE records")]
    [InlineData("name--")]
    public void UntrustedFieldIdentifiers_AreRejectedEvenIfCallerListsThem(string field)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { [field] = "uuid" };
        if (field == "missing") fields.Clear();
        var errors = FilterContracts.Validate(One(new(field, "isNull")), fields);
        Assert.Equal("#/groups/0/conditions/0/field", Assert.Single(errors).Path);
    }

    [Theory]
    [InlineData("eq", "currentUser", true)]
    [InlineData("in", "currentUserAndSubordinates", true)]
    [InlineData("eq", "currentUserAndSubordinates", false)]
    [InlineData("in", "currentUser", false)]
    [InlineData("eq", "currentTenant", false)]
    [InlineData("eq", "keyword", false)]
    public void SystemVariables_RespectTrustedCatalogAndCardinality(string op, string variable, bool valid)
    {
        var errors = FilterContracts.Validate(One(new("owner_id", op, SysVar: variable)), Fields);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData("{\"field\":\"age\",\"op\":\"eq\"}")]
    [InlineData("{\"field\":\"age\",\"op\":\"eq\",\"value\":1,\"sysVar\":\"currentUser\"}")]
    [InlineData("{\"field\":\"age\",\"op\":\"isNull\",\"value\":null}")]
    [InlineData("{\"field\":\"age\",\"op\":\"between\",\"sysVar\":\"currentUserAndSubordinates\"}")]
    [InlineData("{\"field\":\"age\",\"op\":\"in\",\"value\":[]}")]
    [InlineData("{\"field\":\"age\",\"op\":\"eq\",\"value\":1,\"sql\":\"1=1\"}")]
    public void StructuralSchema_RejectsAmbiguousOrInjectedConditions(string condition)
    {
        var json = "{\"logic\":\"and\",\"groups\":[{\"logic\":\"and\",\"conditions\":[" + condition + "]}]}";
        Assert.False(Schema(DataContractSchemas.Filter).Evaluate(JsonDocument.Parse(json).RootElement).IsValid);
    }

    [Fact]
    public void EmptyFilter_IsExplicit_EmptyGroupAndUnknownLogicAreRejected()
    {
        Assert.Empty(FilterContracts.Validate(new("and", []), Fields));
        Assert.Empty(FilterContracts.Validate(new("or", []), Fields));
        Assert.NotEmpty(FilterContracts.Validate(new("xor", []), Fields));
        Assert.NotEmpty(FilterContracts.Validate(new("and", [new("and", [])]), Fields));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"logic\":\"and\"}", DataContractJson.FilterTypeInfo));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"logic\":\"and\",\"groups\":[],\"sql\":\"1=1\"}", DataContractJson.FilterTypeInfo));
    }

    [Theory]
    [InlineData("xor", "and", "#/logic")]
    [InlineData("and", "xor", "#/groups/0/logic")]
    [InlineData("and", "AND", "#/groups/0/logic")]
    public void InvalidLogic_ReportsResolvableJsonPointer(string rootLogic, string groupLogic, string path)
    {
        var filter = new FilterExpression(rootLogic, [new(groupLogic, [new("age", "isNull")])]);
        var error = Assert.Single(FilterContracts.Validate(filter, Fields));
        Assert.Equal(path, error.Path);
        var raw = JsonSerializer.Serialize(filter, DataContractJson.FilterTypeInfo);
        Assert.False(Schema(DataContractSchemas.Filter).Evaluate(JsonDocument.Parse(raw).RootElement).IsValid);
        var target = JsonNode.Parse(raw)!;
        foreach (var segment in error.Path[2..].Split('/'))
            target = target is JsonArray ? target[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]! : target[segment]!;
        Assert.Equal(path == "#/logic" ? rootLogic : groupLogic, target.GetValue<string>());
    }

    [Fact]
    public void PublishedDataSchemas_AreExplicitlyFrozen()
    {
        var schemas = Directory.GetFiles(Path.Combine(Root, "contracts/data/v1"), "*.schema.json");
        Assert.NotEmpty(schemas);
        foreach (var file in schemas)
            Assert.Equal("frozen", JsonNode.Parse(File.ReadAllText(file))!["x-status"]!.GetValue<string>());
    }

    [Fact]
    public void MatrixAndVariables_ArePublishedFromSingleCatalog()
    {
        AssertValid(DataContractSchemas.OperatorMatrixSchema, DataContractSchemas.OperatorMatrix);
        Assert.Equal(10, FilterContracts.Operators.Count);
        Assert.Equal(13, FilterContracts.Operators.Values.SelectMany(items => items).Distinct().Count());
        Assert.Equal(15, FilterContracts.SystemVariables.Count);
        Assert.All(FilterContracts.SystemVariables.Values, variable => Assert.False(string.IsNullOrWhiteSpace(variable.Description)));
        var changed = JsonNode.Parse(DataContractSchemas.OperatorMatrix)!;
        changed["bool"]!.AsArray().Add("between");
        Assert.False(Schema(DataContractSchemas.OperatorMatrixSchema).Evaluate(JsonDocument.Parse(changed.ToJsonString()).RootElement).IsValid);
    }

    [Fact]
    public void RecordEvent_RegistersPayloadAsEventType_AndDerivesHookViewFromFrozenEnvelope()
    {
        var raw = Fixture("record-changed-envelope.json");
        AssertValid(DataContractSchemas.RecordChangedEnvelope, raw);
        var change = RecordChangedEvent.FromEnvelope(QianEventEnvelope.Parse(raw));
        Assert.Equal(Guid.Parse("01900000-0000-7000-8000-000000000002"), change.EventId);
        Assert.Equal(change.Envelope.TenantId, change.TenantId);
        Assert.Equal(RecordActor.From(change.Envelope.Actor), change.Payload.Actor);
        Assert.Equal("name", Assert.Single(change.Payload.ChangedFields));
        AssertValid(DataContractSchemas.RecordChangedPayload, JsonSerializer.Serialize(change.Payload, DataContractJson.RecordChangedPayloadTypeInfo));
        Assert.DoesNotContain("张三", change.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("张三", change.Payload.ToString(), StringComparison.Ordinal);
        // 集成事件类型是载荷：写入端把它放进信封的 payload，视图不登记、不单独上线。
        Assert.Equal(RecordChangedPayload.EventType, typeof(RecordChangedPayload).GetCustomAttribute<IntegrationEventIdAttribute>()!.EventType);
        Assert.Null(typeof(RecordChangedEvent).GetCustomAttribute<IntegrationEventIdAttribute>());
        var services = new ServiceCollection();
        services.AddKunEventBus(builder => builder.RegisterRecordEvents());
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IEventTypeRegistry>();
        Assert.Same(typeof(RecordChangedPayload), registry.Resolve(RecordChangedPayload.EventType));
        Assert.Same(DataContractJson.RecordChangedPayloadTypeInfo, registry.GetJsonTypeInfo(typeof(RecordChangedPayload)));
    }

    [Theory]
    [InlineData("tenant", "租户或主体")]
    [InlineData("actor", "租户或主体")]
    [InlineData("delegation", "租户或主体")]
    [InlineData("platform", "租户或主体")]
    [InlineData("type", "信封不是")]
    [InlineData("nested-envelope", "不符合")]
    [InlineData("missing-member", "不符合")]
    public void RecordEvent_FromEnvelope_RejectsMismatchedContextOrMalformedPayload(string mutation, string reason)
    {
        var node = JsonNode.Parse(Fixture("record-changed-envelope.json"))!;
        var payload = node["payload"]!.AsObject();
        switch (mutation)
        {
            case "tenant": payload["tenantId"] = "01900000-0000-7000-8000-000000000009"; break;
            case "actor": payload["actor"] = new JsonObject { ["kind"] = "service", ["id"] = "test-user" }; break;
            case "delegation": payload["actor"]!["on_behalf_of"] = "someone-else"; break;
            case "platform": node["tenant_id"] = null; break;
            case "type": (node["type"], node["subject"]) = ("qian.record.other.v1", "qian.record.other"); break;
            case "nested-envelope": node["payload"] = node.DeepClone(); break;
            case "missing-member": payload.Remove("changedFields"); break;
        }
        var envelope = QianEventEnvelope.Parse(node.ToJsonString());
        Assert.Contains(reason, Assert.Throws<InvalidOperationException>(() => RecordChangedEvent.FromEnvelope(envelope)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordPayload_EnsureConsistentContext_RequiresCurrentTenantAndEventScopeActor()
    {
        var payload = RecordChangedEvent.FromEnvelope(QianEventEnvelope.Parse(Fixture("record-changed-envelope.json"))).Payload;
        var tenant = new FixedTenant(payload.TenantId.ToString("D"));
        Assert.Throws<InvalidOperationException>(() => payload.EnsureConsistentContext(tenant));
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.dynamic-data", EventActor.User("test-user"))))
        {
            payload.EnsureConsistentContext(tenant);
            Assert.Throws<InvalidOperationException>(() => payload.EnsureConsistentContext(new FixedTenant(null)));
            Assert.Throws<InvalidOperationException>(() => payload.EnsureConsistentContext(new FixedTenant("01900000-0000-7000-8000-000000000009")));
            Assert.Throws<InvalidOperationException>(() => (payload with { Actor = new("user", "test-user", "someone-else") }).EnsureConsistentContext(tenant));
        }
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.dynamic-data", EventActor.Agent("test-user"))))
            Assert.Throws<InvalidOperationException>(() => payload.EnsureConsistentContext(tenant));
    }

    [Theory]
    [InlineData("type", "qian.record.changed.v2")]
    [InlineData("subject", "qian.record.other")]
    [InlineData("tenant_id", "not-a-uuid")]
    [InlineData("trace_parent", "untrusted")]
    public void RecordEnvelope_InvalidFrozenFieldsFail(string property, string value)
    {
        var node = JsonNode.Parse(Fixture("record-changed-envelope.json"))!;
        node[property] = value;
        Assert.False(Schema(DataContractSchemas.RecordChangedEnvelope).Evaluate(JsonDocument.Parse(node.ToJsonString()).RootElement).IsValid);
    }

    [Theory]
    [InlineData("created", false, true, true)]
    [InlineData("created", true, true, true)]
    [InlineData("updated", true, true, true)]
    [InlineData("updated", false, true, true)]
    [InlineData("deleted", true, false, true)]
    [InlineData("deleted", true, true, true)]
    [InlineData("imported", false, true, true)]
    [InlineData("updated", false, false, true)]
    [InlineData("unknown", true, true, false)]
    public void RecordSnapshotShape_FollowsOperation(string operation, bool before, bool after, bool valid)
    {
        var node = JsonNode.Parse(Fixture("record-changed-envelope.json"))!["payload"]!.DeepClone();
        node["op"] = operation;
        if (!before) node.AsObject().Remove("before");
        if (!after) node.AsObject().Remove("after");
        Assert.Equal(valid, Schema(DataContractSchemas.RecordChangedPayload).Evaluate(JsonDocument.Parse(node.ToJsonString()).RootElement).IsValid);
    }

    [Fact]
    public async Task HookContract_CanRewriteSnapshotAndRejectWithUnifiedError()
    {
        var change = RecordChangedEvent.FromEnvelope(QianEventEnvelope.Parse(Fixture("record-changed-envelope.json")));
        IRecordHook hook = new NormalizeNameHook();
        var context = new RecordHookContext(change.TenantId, change.Payload.Actor, change.Payload.EntityCode,
            change.Payload.RecordId, "update", change.Payload.Before, change.Payload.After);
        var result = await hook.BeforeAsync(context, TestContext.Current.CancellationToken);
        Assert.True(result.Allowed);
        Assert.Null(result.Error);
        Assert.Equal("normalized", result.Payload!.Value.GetProperty("name").GetString());
        var error = new QianError(PlatformErrors.AuthzDenied);
        var rejected = RecordHookResult.Reject(error);
        Assert.False(rejected.Allowed);
        Assert.Same(error, rejected.Error);
        Assert.Null(rejected.Payload);
        Assert.Throws<ArgumentException>(() => RecordHookResult.Accept(JsonDocument.Parse("[]").RootElement));
        await hook.AfterAsync(change, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DataRulePolicy_RoundTripsPublishedSchema()
    {
        var raw = Fixture("data-rule-policy.json");
        AssertValid(DataContractSchemas.RulePolicy, raw);
        var policy = JsonSerializer.Deserialize(raw, DataContractJson.RulePolicyTypeInfo)!;
        AssertValid(DataContractSchemas.RulePolicy, JsonSerializer.Serialize(policy, DataContractJson.RulePolicyTypeInfo));
        var invalid = JsonNode.Parse(raw)!;
        invalid["primaryKeyStrategy"] = "rawSql";
        Assert.False(Schema(DataContractSchemas.RulePolicy).Evaluate(JsonDocument.Parse(invalid.ToJsonString()).RootElement).IsValid);
    }

    private sealed class NormalizeNameHook : IRecordHook
    {
        public ValueTask<RecordHookResult> BeforeAsync(RecordHookContext context, CancellationToken cancellationToken = default)
        {
            using var document = JsonDocument.Parse("{\"name\":\"normalized\"}");
            return ValueTask.FromResult(RecordHookResult.Accept(document.RootElement));
        }
        public ValueTask AfterAsync(RecordChangedEvent change, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FixedTenant(string? tenantId) : ITenantContext
    {
        public string? TenantId => tenantId;
        public bool IsMultiTenant => true;
    }

    private static FilterExpression One(FilterCondition condition) => new("and", [new("and", [condition])]);
    private static string Fixture(string file) => File.ReadAllText(Path.Combine(Root, "contracts/data/v1/fixtures", file));
    private static JsonSchema Schema(string text)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(DataContractSchemas.FrozenS2sClaims, options);
        JsonSchema.FromText(DataContractSchemas.FrozenEventEnvelope, options);
        var payload = JsonSchema.FromText(DataContractSchemas.RecordChangedPayload, options);
        return text == DataContractSchemas.RecordChangedPayload ? payload : JsonSchema.FromText(text, options);
    }
    private static void AssertValid(string schema, string json) => Assert.True(Schema(schema).Evaluate(JsonDocument.Parse(json).RootElement).IsValid, json);
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }
}

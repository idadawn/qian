using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors;

namespace Qian.Data.Abstractions.Tests;

public sealed class DataContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly IReadOnlyDictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["age"] = "int", ["owner_id"] = "uuid", ["active"] = "bool", ["name"] = "varchar", ["tenant_id"] = "uuid"
    };

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
    public void RecordEnvelope_MatchesFrozenV0AndPayloadSchemas_AndFrameworkRegistration()
    {
        var raw = Fixture("record-changed-envelope.json");
        AssertValid(DataContractSchemas.RecordChangedEnvelope, raw);
        var envelope = JsonSerializer.Deserialize(raw, DataContractJson.RecordChangedEventTypeInfo)!;
        var serialized = JsonSerializer.Serialize(envelope, DataContractJson.RecordChangedEventTypeInfo);
        AssertValid(DataContractSchemas.RecordChangedEnvelope, serialized);
        envelope.EnsureConsistentContext();
        Assert.Equal(envelope.TenantId, envelope.Payload.TenantId);
        Assert.Equal(envelope.Actor, envelope.Payload.Actor);
        Assert.DoesNotContain("张三", envelope.ToString(), StringComparison.Ordinal);
        Assert.Equal(RecordChangedEvent.EventType, typeof(RecordChangedEvent).GetCustomAttribute<IntegrationEventIdAttribute>()!.EventType);
        var services = new ServiceCollection();
        services.AddKunEventBus(builder => builder.RegisterRecordEvents());
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IEventTypeRegistry>();
        Assert.Same(typeof(RecordChangedEvent), registry.Resolve(RecordChangedEvent.EventType));
        Assert.Same(DataContractJson.RecordChangedEventTypeInfo, registry.GetJsonTypeInfo(typeof(RecordChangedEvent)));
    }

    [Fact]
    public void RecordEnvelope_RejectsConflictingTenantActorAndNonUtcTime()
    {
        var valid = JsonSerializer.Deserialize(Fixture("record-changed-envelope.json"), DataContractJson.RecordChangedEventTypeInfo)!;
        Assert.Throws<InvalidOperationException>(() => (valid with { TenantId = Guid.NewGuid() }).EnsureConsistentContext());
        Assert.Throws<InvalidOperationException>(() => (valid with { Actor = new("service", "other") }).EnsureConsistentContext());
        Assert.Throws<InvalidOperationException>(() => (valid with { OccurredAt = DateTime.SpecifyKind(valid.OccurredAt, DateTimeKind.Unspecified) }).EnsureConsistentContext());
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
        var change = JsonSerializer.Deserialize(Fixture("record-changed-envelope.json"), DataContractJson.RecordChangedEventTypeInfo)!;
        IRecordHook hook = new NormalizeNameHook();
        var context = new RecordHookContext(change.TenantId, change.Actor, change.Payload.EntityCode,
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

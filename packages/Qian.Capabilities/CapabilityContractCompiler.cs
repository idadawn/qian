using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities;

/// <summary>制品构建期从完整生成契约派生文档；不读取当前用户权限，也不授予运行资格。</summary>
public static class CapabilityContractCompiler
{
    /// <summary>为一个组件的完整生成契约生成 OpenAPI 3.1 制品，供版本化仓库作为兼容基线。</summary>
    public static JsonElement CreateOpenApi(IReadOnlyList<CapabilityContract> contracts, ISurfaceReader schemas, IToolContractHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(contracts); ArgumentNullException.ThrowIfNull(schemas); ArgumentNullException.ThrowIfNull(hasher);
        if (contracts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != contracts.Count ||
            contracts.Select(item => item.ComponentId).Distinct(StringComparer.Ordinal).Count() > 1) throw Error(PlatformErrors.InputInvalid);
        return CapabilityOpenApi.Build(contracts.Select(contract => Describe(contract, schemas, hasher)));
    }
    internal static JsonElement Describe(CapabilityContract contract, ISurfaceReader schemas, IToolContractHasher hasher)
    {
        var tool = JsonNode.Parse(contract.Metadata.GetRawText())!.AsObject();
        tool["inputSchema"] = contract.InputSchema; tool["outputSchema"] = contract.OutputSchema;
        using (var json = JsonDocument.Parse(tool.ToJsonString())) tool["schemaSha256"] = hasher.Compute(json.RootElement);
        using (var json = JsonDocument.Parse(tool.ToJsonString()))
            if (schemas.ValidateSchema("tool", contract.Id, json.RootElement).Any(diagnostic => diagnostic.Severity == "error")) throw Error(PlatformErrors.InputInvalid);
        tool.Remove("binding"); tool.Remove("$schema");
        tool["id"] = contract.Id; tool["plugin"] = contract.ComponentId;
        tool["kind"] = tool["sideEffect"]!.GetValue<string>() == "read" ? "query" : "command";
        using var result = JsonDocument.Parse(tool.ToJsonString());
        if (schemas.ValidateSchema("capability-entry", contract.Id, result.RootElement).Any(diagnostic => diagnostic.Severity == "error") ||
            HasTenantField(result.RootElement.GetProperty("inputSchema"))) throw Error(PlatformErrors.InputInvalid);
        return result.RootElement.Clone();
    }
    private static bool HasTenantField(JsonElement schema)
    {
        if (schema.ValueKind == JsonValueKind.Array) return schema.EnumerateArray().Any(HasTenantField);
        if (schema.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in schema.EnumerateObject())
        {
            if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object &&
                property.Value.EnumerateObject().Any(field => field.Name.Equals("tenant", StringComparison.OrdinalIgnoreCase) ||
                    field.Name.Equals("tenantId", StringComparison.OrdinalIgnoreCase) || field.Name.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
                    field.Name.Equals("tenant-id", StringComparison.OrdinalIgnoreCase))) return true;
            if (HasTenantField(property.Value)) return true;
        }
        return false;
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}

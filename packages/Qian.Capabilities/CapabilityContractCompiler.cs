using System.Globalization;
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
        try { return CapabilityOpenApi.Build(contracts.Select(contract => Describe(contract, schemas, hasher))); }
        catch (CapabilityContractException error) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid), error); }
    }

    // 由契约生成目录条目；不合格时抛 CapabilityContractException，位置以能力 ID 为文件指向条目内的出错处。
    internal static JsonElement Describe(CapabilityContract contract, ISurfaceReader schemas, IToolContractHasher hasher)
    {
        var tool = JsonNode.Parse(contract.Metadata.GetRawText())!.AsObject();
        tool["inputSchema"] = contract.InputSchema; tool["outputSchema"] = contract.OutputSchema;
        using (var json = JsonDocument.Parse(tool.ToJsonString())) tool["schemaSha256"] = hasher.Compute(json.RootElement);
        using (var json = JsonDocument.Parse(tool.ToJsonString()))
        {
            // 租户字段先于结构校验检查：契约 Schema 只能在 properties 这一级报出顶层租户字段，这里给出字段本身的位置，嵌套层也一并覆盖。
            if (TenantField(json.RootElement.GetProperty("inputSchema"), ["inputSchema"]) is { } field)
                throw new CapabilityContractException(contract.ComponentId, contract.Name, Locate(contract.Id, field), "tenant-field");
            if (Rejected(contract, schemas.ValidateSchema("tool", contract.Id, json.RootElement), "tool-invalid") is { } rejected) throw rejected;
        }
        tool.Remove("binding"); tool.Remove("$schema");
        tool["id"] = contract.Id; tool["plugin"] = contract.ComponentId;
        tool["kind"] = tool["sideEffect"]!.GetValue<string>() == "read" ? "query" : "command";
        using var result = JsonDocument.Parse(tool.ToJsonString());
        if (Rejected(contract, schemas.ValidateSchema("capability-entry", contract.Id, result.RootElement), "entry-invalid") is { } invalid) throw invalid;
        return result.RootElement.Clone();
    }

    // 以能力 ID 为文件的 JSON Pointer；超过 contracts/v0 的 1024 字符上限时退到最近的合法上级。
    internal static string Locate(string capabilityId, IReadOnlyList<string> segments)
    {
        for (var count = segments.Count; count >= 0; count--)
        {
            object[] parts = [.. segments.Take(count)];
            string pointer;
            try { pointer = ErrorPointer.InFile(capabilityId, parts); }
            catch (ArgumentException) { pointer = ErrorPointer.Input(parts); }
            if (ErrorPointer.IsValid(pointer)) return pointer;
        }
        return ErrorPointer.Input();
    }

    private static CapabilityContractException? Rejected(CapabilityContract contract, IReadOnlyList<SurfaceDiagnostic> diagnostics, string rule) =>
        diagnostics.FirstOrDefault(diagnostic => diagnostic.Severity == "error") is { } error
            ? new CapabilityContractException(contract.ComponentId, contract.Name,
                error.Error.Pointer is { } pointer && ErrorPointer.IsValid(pointer) ? pointer : Locate(contract.Id, []), rule)
            : null;

    private static readonly HashSet<string> TenantNames = new(StringComparer.OrdinalIgnoreCase) { "tenant", "tenantId", "tenant_id", "tenant-id" };

    // 输入 Schema 里任何层级的租户字段（PRD 7.2.1：inputSchema 不含租户字段），返回其在条目内的位置段。
    private static string[]? TenantField(JsonElement schema, string[] path)
    {
        if (schema.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in schema.EnumerateArray())
                if (TenantField(item, [.. path, (index++).ToString(CultureInfo.InvariantCulture)]) is { } found) return found;
            return null;
        }
        if (schema.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in schema.EnumerateObject())
        {
            if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                foreach (var field in property.Value.EnumerateObject())
                    if (TenantNames.Contains(field.Name)) return [.. path, "properties", field.Name];
            if (TenantField(property.Value, [.. path, property.Name]) is { } found) return found;
        }
        return null;
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}

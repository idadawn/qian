using System.Text.Json;

namespace Qian.Capabilities;

/// <summary>真实契约检查器返回的兼容性结论；规则 ID 不含模型内容或请求值。</summary>
public sealed record ContractCompatibility(bool IsCompatible, IReadOnlyList<string> BreakingRules);

/// <summary>OpenAPI 3.1 向后兼容性探测端口；无法完成检测必须失败，不能当作兼容。</summary>
public interface IOpenApiCompatibilityProbe
{
    /// <summary>检查新契约是否保持基线调用者所依赖的接口与 Schema。</summary>
    ValueTask<ContractCompatibility> CompareAsync(JsonElement baseline, JsonElement candidate, CancellationToken cancellationToken = default);
}

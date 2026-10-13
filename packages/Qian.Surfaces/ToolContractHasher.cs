using System.Text.Json;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

/// <summary>复用已验证的工具契约规范化实现。</summary>
public sealed class ToolContractHasher : IToolContractHasher
{
    /// <inheritdoc />
    public string Compute(JsonElement tool) => ToolContractHash.Compute(tool);
}

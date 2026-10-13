using System.Text.Json;

namespace Qian.Surfaces.Abstractions;

/// <summary>冻结工具契约的 JCS/SHA-256 端口，供能力目录复用，避免复制或变更哈希规则。</summary>
public interface IToolContractHasher
{
    /// <summary>对 description、inputSchema、name 三个成员按冻结规范计算摘要。</summary>
    string Compute(JsonElement tool);
}

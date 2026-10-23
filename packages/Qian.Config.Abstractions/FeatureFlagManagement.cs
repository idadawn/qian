using System.Text.Json.Serialization;
using Qian.Runtime;

namespace Qian.Config;

/// <summary>可选平台管理组件的声明；只保存 Apollo 草稿，发布仍由 Portal 操作员完成。</summary>
public static class FeatureFlagManagement
{
    /// <summary>平台草稿管理端口，与 Config 只读端口分开。</summary>
    public static readonly ServiceKey Key = new("FeatureFlagDrafts");
    /// <summary>草稿编辑权限；声明不等于授予。</summary>
    public const string SetPermission = "qian.config.flags:flag:set";
    /// <summary>草稿编辑能力标识。</summary>
    public const string SetCapabilityId = "qian.config.flags/flags.set";
}

/// <summary>平台专用草稿写入；实现每次重新检查已认证主体、确认、权限与审计。</summary>
public interface IFeatureFlagDrafts
{
    /// <summary>保存受限规则草稿；成功不代表已发布或对读者生效。</summary>
    ValueTask<FeatureFlagDraftResult> SetAsync(FeatureFlagDraft request, CancellationToken cancellationToken = default);
}

/// <summary>固定灰度规则；没有表达式、外部地址或任意配置键。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FeatureFlagPolicy
{
    /// <summary>未命中白名单或百分比时的结果。</summary>
    public required bool Default { get; init; }
    /// <summary>非空且不重复的租户 UUID，最多 256 项。</summary>
    public IReadOnlyList<Guid> Tenants { get; set; } = [];
    /// <summary>0 至 100 的整数；同名开关使用固定哈希桶。</summary>
    public int Percentage { get; set; }
}

/// <summary>草稿操作输入；目标环境、应用、namespace、操作员与秘密引用均由可信宿主固定。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FeatureFlagDraft
{
    /// <summary>必填且非空的持久幂等键；同一目标和键仅执行一次，相同键不同主体或输入拒绝。</summary>
    public required Guid IdempotencyKey { get; init; }
    /// <summary>小写开关名称，实际 Apollo 键为 flag 加点号再加此名称。</summary>
    public required string Name { get; init; }
    /// <summary>经过固定 Schema 校验的灰度规则。</summary>
    public required FeatureFlagPolicy Rule { get; init; }
    /// <summary>调用者明确确认保存草稿；不代替身份或权限检查，也不是 S3 审批票据。</summary>
    public required bool Confirmed { get; init; }
}

/// <summary>草稿写入回执，不含 Token 或规则内容。</summary>
public sealed class FeatureFlagDraftResult
{
    /// <summary>开关名称。</summary>
    public required string Name { get; init; }
    /// <summary>固定 pending-publish；不得解释为运行时已生效。</summary>
    public string Status { get; } = "pending-publish";
    /// <summary>规范化规则的 SHA-256，供操作员核对。</summary>
    public required string RuleHash { get; init; }
}

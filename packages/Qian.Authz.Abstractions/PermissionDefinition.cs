using System.Text.Json.Serialization;

namespace Qian.Authz.Abstractions;

/// <summary>权限元数据的风险等级；具体放行和 step-up 规则由 Provider 求值。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionRisk>))]
public enum PermissionRisk
{
    /// <summary>低风险。</summary>
    [JsonStringEnumMemberName("low")] Low,
    /// <summary>中风险。</summary>
    [JsonStringEnumMemberName("medium")] Medium,
    /// <summary>高风险。</summary>
    [JsonStringEnumMemberName("high")] High,
    /// <summary>关键管理风险。</summary>
    [JsonStringEnumMemberName("critical")] Critical
}

/// <summary>权限点声明；资源和动作保留清单短名，Code 由组件前缀派生。</summary>
public sealed class PermissionDefinition
{
    /// <summary>创建经过命名校验且复制终端集合的声明；元数据本身不授予任何权限。</summary>
    [JsonConstructor]
    public PermissionDefinition(string pluginId, string resource, string action, PermissionRisk risk,
        bool requiresStepUp, IReadOnlyList<string> terminals)
    {
        Code = PermissionCode.Format(pluginId, resource, action);
        if (!Enum.IsDefined(risk)) throw new ArgumentOutOfRangeException(nameof(risk));
        ArgumentNullException.ThrowIfNull(terminals);
        if (terminals.Any(string.IsNullOrWhiteSpace) || terminals.Distinct(StringComparer.Ordinal).Count() != terminals.Count)
            throw new ArgumentException("终端集合不得包含空值或重复项。", nameof(terminals));
        PluginId = pluginId; Resource = resource; Action = action; Risk = risk;
        RequiresStepUp = requiresStepUp; Terminals = Array.AsReadOnly(terminals.ToArray());
    }

    /// <summary>全局唯一权限点。</summary>
    public string Code { get; }
    /// <summary>所属组件 id。</summary>
    public string PluginId { get; }
    /// <summary>清单中的资源短名，不携带组件前缀。</summary>
    public string Resource { get; }
    /// <summary>动作短名。</summary>
    public string Action { get; }
    /// <summary>风险元数据。</summary>
    public PermissionRisk Risk { get; }
    /// <summary>是否声明需要提升认证强度。</summary>
    public bool RequiresStepUp { get; }
    /// <summary>声明的终端标签；Provider 负责检查可信终端上下文，不能从客户端输入直接放行。</summary>
    public IReadOnlyList<string> Terminals { get; }
}

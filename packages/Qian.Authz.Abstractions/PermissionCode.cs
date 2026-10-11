using System.Text.RegularExpressions;

namespace Qian.Authz.Abstractions;

/// <summary>权限点的不可混淆全名：组件 id、插件内资源短名、动作短名。</summary>
public static partial class PermissionCode
{
    /// <summary>按 R9 拼接权限点；不纠正大小写，不接受资源自带插件前缀。</summary>
    public static string Format(string pluginId, string resource, string action)
    {
        Validate(pluginId, ComponentPattern(), nameof(pluginId));
        Validate(resource, SegmentPattern(), nameof(resource));
        Validate(action, SegmentPattern(), nameof(action));
        return pluginId + ":" + resource + ":" + action;
    }

    /// <summary>检查完整权限点格式；缺失、空白、额外分隔符都不是有效权限点。</summary>
    public static bool IsValid(string? value)
    {
        if (value is null) return false;
        var parts = value.Split(':');
        return parts.Length == 3 && ComponentPattern().IsMatch(parts[0]) &&
            SegmentPattern().IsMatch(parts[1]) && SegmentPattern().IsMatch(parts[2]);
    }

    private static void Validate(string value, Regex pattern, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (!pattern.IsMatch(value)) throw new ArgumentException("权限点必须使用规范的组件 id 与插件内短名。", parameter);
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ComponentPattern();
    [GeneratedRegex(@"\A[a-z][a-z0-9_-]*\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SegmentPattern();
}

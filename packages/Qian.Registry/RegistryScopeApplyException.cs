using System.Globalization;
using Qian.Runtime;

namespace Qian.Registry;

// scope 条目 Apply 失败：带条目 ID、组件@版本、scope patch 修订与原因码（失败为 QianErrorException 时取其错误码，否则 INTERNAL）。
// 同样的值写入 Data，供管理面诊断出口（S1-RT-005）读取；不包含配置、连接或底层异常文本。
internal sealed class RegistryScopeApplyException : InvalidOperationException
{
    internal RegistryScopeApplyException(ScopeKey scope, string entryId, string plugin, int? revision, string reasonCode, Exception? failure)
        : base(string.Create(CultureInfo.InvariantCulture,
            $"scope {scope.Kind}:{scope.Id} 的条目 {entryId}（组件 {plugin}，scope patch 修订 {(revision is { } value ? value.ToString(CultureInfo.InvariantCulture) : "无")}）Apply 失败，原因码 {reasonCode}。"), failure)
    {
        Scope = scope; EntryId = entryId; Plugin = plugin; Revision = revision; ReasonCode = reasonCode;
        Data["scope"] = scope.Kind + ":" + scope.Id; Data["entryId"] = entryId; Data["plugin"] = plugin;
        Data["revision"] = revision; Data["reasonCode"] = reasonCode;
    }
    internal ScopeKey Scope { get; }
    internal string EntryId { get; }
    // 组件 ID 与版本，形如 qian.sample.crm@1.0.1。
    internal string Plugin { get; }
    // 该条目在 scope_patches 中的修订号；条目不来自 scope patch 时为 null。
    internal int? Revision { get; }
    internal string ReasonCode { get; }
}

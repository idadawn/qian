using Microsoft.Extensions.Logging;

namespace Qian.Runtime;

public sealed partial class PluginLoader
{
    private static readonly Action<ILogger, string, string, Exception?> s_pendingWarning = LoggerMessage.Define<string, string>(
        LogLevel.Warning, new EventId(4, "DependencyPending"), "条目 {EntryId} 已等待依赖超过 30 秒：{ServiceKeys}");

    private void ReportPending()
    {
        List<(string Entry, string Keys)> warnings = [];
        lock (Sync)
        {
            if (_disposed) return;
            foreach (var node in _nodes.Where(n => n.State == PluginState.Pending && !n.PendingReported
                && DateTimeOffset.UtcNow - n.PendingSince >= TimeSpan.FromSeconds(30)))
            {
                node.PendingReported = true;
                warnings.Add((node.Entry.Id, string.Join(", ", node.Inject.Where(k => Find(k, node.Entry.Scope) is null).Select(k => k.Name))));
            }
        }
        foreach (var warning in warnings) s_pendingWarning(Logger, warning.Entry, warning.Keys, null);
    }
}

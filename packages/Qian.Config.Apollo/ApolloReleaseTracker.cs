using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Qian.Config.Apollo;

// 一次 Apollo 快照被实例接受或拒绝的记录（PRD 7.8「Apollo 发布事件进审计」、ADR-007 D7.13「实例拉取审计作为回读信号」）。
// 只含 namespace、变更键数、原因码与脱敏快照的哈希，绝不含配置值。
internal sealed record ApolloReleaseRecord(string Namespace, string Resource, bool Rejected, string InputHash, int ChangedKeys,
    IReadOnlyList<string> Reasons, DateTimeOffset OccurredAt);

// Kun 的 Apollo 客户端只暴露校验委托与重载令牌（releaseKey / notificationId 是其内部类型），这里按两处信号重建每次发布的结论：
// ValidateNamespace 登记本轮被拉取（非 304）的 namespace 候选，合并快照校验失败即「拒绝」；通过后以重载回调（合并结果变化）确认「接受」，
// 合并结果未变时 Kun 不发重载，直接按已生效记；离线缓存启动没有重载回调，Load 返回时确认。
// 被拒绝的候选在客户端每个重试周期都会重拉，同一 namespace 同一内容只记一次。没有审计写入方时记录暂存在有界队列，满了丢最旧的。
internal sealed class ApolloReleaseTracker(string appId, string environment, string cluster)
{
    private const int Capacity = 256;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, NamespaceSnapshot> _validated = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NamespaceSnapshot> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NamespaceSnapshot> _applied = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Hash, bool Rejected)> _recorded = new(StringComparer.Ordinal);
    private readonly Channel<ApolloReleaseRecord> _records = Channel.CreateBounded<ApolloReleaseRecord>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private string? _pendingMerged;
    private string? _appliedMerged;

    internal string AppId => appId;
    internal ChannelReader<ApolloReleaseRecord> Records => _records.Reader;

    // ValidateNamespace：namespace 原始快照扫描未通过即拒绝；本轮刷新随之中止，丢弃已登记的候选。
    internal bool Namespace(string name, IReadOnlyDictionary<string, string?> values, IReadOnlyList<SecretDiagnostic> diagnostics)
    {
        var snapshot = NamespaceSnapshot.Capture(values, diagnostics);
        lock (_gate)
        {
            if (diagnostics.Count == 0) { _validated[name] = snapshot; return true; }
            _validated.Clear();
            Reject(name, snapshot, Codes(diagnostics));
            return false;
        }
    }

    // Validate：合并快照扫描或宿主追加的校验未通过时，本轮全部候选记为拒绝。
    internal bool Merged(IReadOnlyDictionary<string, string?> values, IReadOnlyList<SecretDiagnostic> diagnostics, bool accepted)
    {
        var merged = NamespaceSnapshot.Capture(values, diagnostics).Hash;
        lock (_gate)
        {
            var candidates = _validated.ToArray();
            _validated.Clear();
            if (diagnostics.Count != 0 || !accepted)
            {
                var reasons = diagnostics.Count != 0 ? Codes(diagnostics) : [ConfigErrors.ReloadRejected.Code];
                foreach (var (name, snapshot) in candidates) Reject(name, snapshot, reasons);
                return false;
            }
            foreach (var (name, snapshot) in candidates) _pending[name] = snapshot;
            _pendingMerged = merged;
            if (merged == _appliedMerged) Confirm();
            return true;
        }
    }

    // Kun 在合并结果变化、数据已替换后触发重载令牌。
    internal void Reloaded()
    {
        lock (_gate) Confirm();
    }

    // 离线缓存启动：缓存快照已通过同一组校验并生效，但不触发重载令牌。
    internal void Loaded()
    {
        lock (_gate) if (_pending.Count != 0) Confirm();
    }

    private void Confirm()
    {
        _appliedMerged = _pendingMerged;
        foreach (var (name, snapshot) in _pending)
        {
            var changed = snapshot.Changed(_applied.GetValueOrDefault(name));
            _applied[name] = snapshot;
            Emit(name, snapshot, rejected: false, changed, []);
        }
        _pending.Clear();
    }

    private void Reject(string name, NamespaceSnapshot snapshot, IReadOnlyList<string> reasons) =>
        Emit(name, snapshot, rejected: true, snapshot.Changed(_applied.GetValueOrDefault(name)), reasons);

    private void Emit(string name, NamespaceSnapshot snapshot, bool rejected, int changed, IReadOnlyList<string> reasons)
    {
        if (_recorded.TryGetValue(name, out var last) && last == (snapshot.Hash, rejected)) return;
        _recorded[name] = (snapshot.Hash, rejected);
        _records.Writer.TryWrite(new ApolloReleaseRecord(name, Resource(name), rejected, snapshot.Hash, changed, reasons, DateTimeOffset.UtcNow));
    }

    private string Resource(string name) => "apollo://" + string.Join('/', new[] { appId, environment, cluster, name }.Select(Uri.EscapeDataString));
    private static string[] Codes(IReadOnlyList<SecretDiagnostic> diagnostics) => [.. diagnostics.Select(item => item.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    // 规范化快照：键按序数排序后与值写成 JSON 数组；扫描标出的位置换成 [REDACTED] 再取 SHA-256，未通过扫描的明文不进哈希。
    // 逐键只保留值的哈希，用于统计变更键数；键名大小写不敏感，与 Kun 合并配置一致。
    private sealed class NamespaceSnapshot(string hash, IReadOnlyDictionary<string, string> keys)
    {
        private const string Redacted = "[REDACTED]";
        internal string Hash { get; } = hash;

        internal static NamespaceSnapshot Capture(IReadOnlyDictionary<string, string?> values, IReadOnlyList<SecretDiagnostic> diagnostics)
        {
            var flagged = new HashSet<string>(diagnostics.Where(item => item.Location.StartsWith("#/", StringComparison.Ordinal))
                .Select(item => item.Location[2..].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)), StringComparer.OrdinalIgnoreCase);
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartArray();
                foreach (var pair in values.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    var value = flagged.Contains(pair.Key) ? Redacted : pair.Value;
                    writer.WriteStartArray(); writer.WriteStringValue(pair.Key);
                    if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(value);
                    writer.WriteEndArray();
                    keys[pair.Key] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value is null ? "n" : "s" + value)));
                }
                writer.WriteEndArray();
            }
            return new NamespaceSnapshot(Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray())), keys);
        }

        // 新增、删除与改值的键数；没有基线（首次加载）时全部键算变更。
        internal int Changed(NamespaceSnapshot? baseline)
        {
            if (baseline is null) return keys.Count;
            var changed = keys.Count(pair => !baseline.Keys.TryGetValue(pair.Key, out var value) || value != pair.Value);
            return changed + baseline.Keys.Keys.Count(key => !keys.ContainsKey(key));
        }

        private IReadOnlyDictionary<string, string> Keys => keys;
    }
}

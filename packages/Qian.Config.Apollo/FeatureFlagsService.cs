using System.Collections.Frozen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

// 配置根由宿主拥有。本实例只拥有监听器和已校验的不可变快照。
internal sealed partial class FeatureFlagsService : IFeatureFlags, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ConfigReloadNotifier _notifier;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly IDisposable _reload;
    private FrozenDictionary<string, FeatureFlagRule> _rules = FrozenDictionary<string, FeatureFlagRule>.Empty;
    private bool _disposed;

    internal FeatureFlagsService(IConfiguration configuration, ConfigReloadNotifier notifier, ILogger logger)
    {
        _configuration = configuration; _notifier = notifier; _logger = logger;
        // 先订阅后读，关闭初始化读取和下一次通知之间的丢变更窗口。
        _reload = ChangeToken.OnChange(configuration.GetReloadToken, Reload);
        try { lock (_gate) _rules = Read(); }
        catch (InvalidDataException)
        {
            _reload.Dispose();
            throw new QianErrorException(new QianError(ConfigErrors.ReloadRejected,
                new Dictionary<string, string> { ["section"] = FeatureFlagRules.Namespace }));
        }
        catch { _reload.Dispose(); throw; }
    }

    public bool IsEnabled(string name, Guid tenantId)
    {
        if (!FeatureFlagRules.ValidName(name)) throw new ArgumentException("特性开关名称无效。", nameof(name));
        if (tenantId == Guid.Empty) throw new ArgumentException("租户标识不能为空。", nameof(tenantId));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _rules.TryGetValue(name, out var rule) && rule.Evaluate(name, tenantId);
        }
    }

    private FrozenDictionary<string, FeatureFlagRule> Read() => FeatureFlagRules.Read(_configuration.AsEnumerable());

    private void Reload()
    {
        lock (_gate)
        {
            if (_disposed) return;
            try { _rules = Read(); return; }
            catch (InvalidDataException) { }
        }
        ConfigTelemetry.ReloadFailed.Add(1, new KeyValuePair<string, object?>("options.type", typeof(IFeatureFlags).FullName),
            new KeyValuePair<string, object?>("options.name", ""), new KeyValuePair<string, object?>("plugin", "qian.config"));
        LogRejected(_logger);
        _notifier.Publish(new ConfigReloadFailed(typeof(IFeatureFlags).FullName!, "", FeatureFlagRules.Namespace, 1,
            ConfigErrors.ReloadRejected.Code) { Plugin = "qian.config" }, _logger);
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _reload.Dispose();
    }

    [LoggerMessage(EventId = 2121, Level = LogLevel.Warning, Message = "特性开关规则重载被拒绝，保留上一份有效快照。")]
    private static partial void LogRejected(ILogger logger);
}

using Qian.Runtime.Errors;

namespace Qian.Registry;

/// <summary>注册表拥有的错误码；R48 的原因通过协议错误码表达，不扩展 ErrorDetail。</summary>
public static class RegistryErrors
{
    /// <summary>管理面可解释的未授权状态。</summary>
    public static readonly ErrorCode NotEntitled = Hidden("PLUGIN_NOT_ENTITLED", "组件未获授权");
    /// <summary>管理面可解释的未挂载状态。</summary>
    public static readonly ErrorCode NotMounted = Hidden("PLUGIN_NOT_MOUNTED", "组件未挂载");
    /// <summary>管理面可解释的受限状态。</summary>
    public static readonly ErrorCode Restricted = Hidden("PLUGIN_RESTRICTED", "组件被限制过滤");
    /// <summary>管理面可解释的依赖等待状态。</summary>
    public static readonly ErrorCode DependencyPending = Hidden("PLUGIN_DEPENDENCY_PENDING", "组件依赖未就绪");
    /// <summary>条件写入的修订已变化。</summary>
    public static readonly ErrorCode RevisionConflict = ErrorCode.Business("REGISTRY_REVISION_CONFLICT", 409, "qian.registry",
        new Dictionary<string, string> { ["zh-CN"] = "注册表修订冲突", ["en"] = "Registry revision conflict" },
        new Dictionary<string, string> { ["zh-CN"] = "条目修订已变化，请重新读取后再提交。", ["en"] = "The entry revision changed. Read it again before submitting." });
    /// <summary>注册表字段校验错误；放在 INPUT_INVALID 的 errors 中。</summary>
    public static readonly ErrorCode Invalid = ErrorCode.Field("REGISTRY_INPUT_INVALID", "qian.registry",
        new Dictionary<string, string> { ["zh-CN"] = "注册表输入无效", ["en"] = "Invalid registry input" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });

    /// <summary>登记全部组件错误，释放时逆序撤销。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var code in new[] { NotEntitled, NotMounted, Restricted, DependencyPending, RevisionConflict, Invalid })
                registrations.Add(catalog.Register(code));
            return new Registration(registrations);
        }
        catch { foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose(); throw; }
    }

    private static ErrorCode Hidden(string code, string title) => ErrorCode.Protocol(code, 403, "qian.registry",
        new Dictionary<string, string> { ["zh-CN"] = title, ["en"] = code },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });

    private sealed class Registration(List<IDisposable> registrations) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose();
        }
    }
}

using System.Text.Json;
using Qian.Runtime;

namespace Qian.Surfaces.Abstractions;

/// <summary>实际监视器提供的当前状态；不包含配置或文件正文。</summary>
public interface ISurfaceReloadDiagnosticSource
{
    /// <summary>读取当前挂载状态；卸载后的引用不能继续读取。</summary>
    JsonElement Read();
}

/// <summary>诊断也通过实际运行时注册、scope 遮蔽和 restrict 过滤。</summary>
public static class SurfaceRuntimeDiagnostics
{
    /// <summary>按组件名登记的监视器集合。</summary>
    public static readonly ServiceKey Key = new("SurfaceReloadDiagnostics");

    /// <summary>给宿主管理面导出当前可见监视器，既不扫描目录也不读取旧报告。</summary>
    public static JsonElement Read(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entries = context.List(Key).OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => ((ISurfaceReloadDiagnosticSource)item.Value).Read()).ToArray();
        return JsonSerializer.SerializeToElement(new { protocol = "qian.surface-diagnostics/v1", scope = ScopeName(context.Scope), entries });
    }

    /// <summary>与 CLI 和 TS 运行时一致的 scope 表示。</summary>
    public static string ScopeName(ScopeKey scope) => scope == ScopeKey.Global ? "global" : scope.Kind + ":" + scope.Id;
}

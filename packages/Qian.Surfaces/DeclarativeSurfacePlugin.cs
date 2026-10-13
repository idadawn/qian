using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

/// <summary>把已经校验的静态快照注册到运行时；注册沿用条目 scope 与 Effect 逆序释放。</summary>
public sealed class DeclarativeSurfacePlugin : IQianPlugin
{
    private readonly ParsedComponent _component;
    private readonly IReadOnlyList<(ServiceKey Key, string Name, SurfaceDocument Document)> _registrations;
    /// <summary>必须传入有效的校验结果；错误时拒绝创建可挂载插件。</summary>
    public DeclarativeSurfacePlugin(SurfaceValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsValid) throw new ArgumentException("组件尚未通过表面校验。", nameof(result));
        _component = result.Component!;
        Inject = _component.Manifest.GetProperty("inject").EnumerateArray().Select(e => new ServiceKey(e.GetString()!)).ToArray();
        _registrations = SurfaceRegistrationSession.Plan(result).Select(item => (item.Key.Key, item.Key.Name, item.Value)).ToArray();
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject { get; }
    /// <summary>为依赖图提供本次快照实际注册的具名目录项。</summary>
    public IReadOnlyList<ServiceKey> Provides => _registrations.Select(r => new ServiceKey(r.Key.Name + ":" + r.Name)).ToArray();
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_component.Manifest.GetProperty("scopes").EnumerateArray().Any(scope => scope.GetString() == ctx.Scope.Kind))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        foreach (var registration in _registrations) ctx.Provide(registration.Key, registration.Name, registration.Document);
        return ValueTask.CompletedTask;
    }
}

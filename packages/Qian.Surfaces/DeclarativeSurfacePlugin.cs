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
        var entries = new List<(ServiceKey, string, SurfaceDocument)>();
        foreach (var document in _component.Documents)
        {
            var key = document.Kind switch
            {
                "skill" => SurfaceKeys.Skills, "agent" => SurfaceKeys.Agents, "command" => SurfaceKeys.Commands,
                "hooks" => SurfaceKeys.Hooks, "mcp" => SurfaceKeys.McpServers, "tool" => SurfaceKeys.Tools, _ => (ServiceKey?)null
            };
            if (key is null) continue;
            if (document.Kind == "mcp")
            {
                foreach (var server in document.Data.GetProperty("mcpServers").EnumerateObject())
                    if (!server.Value.TryGetProperty("profiles", out var profiles) || profiles.EnumerateArray().Any(p => p.GetString() == _component.Profile))
                        entries.Add((key.Value, _component.Id + "/" + server.Name, document with { Name = server.Name, Data = server.Value.Clone() }));
            }
            else entries.Add((key.Value, _component.Id + "/" + document.Name, document));
        }
        _registrations = entries;
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

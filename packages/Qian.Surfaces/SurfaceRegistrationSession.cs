using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

/// <summary>同一次 Apply 中可独立刷新和释放的声明式注册，不重放代码插件效果。</summary>
public sealed class SurfaceRegistrationSession : IDisposable
{
    private readonly IPluginContext _context;
    private readonly Lock _gate = new();
    private readonly Dictionary<(ServiceKey Key, string Name), Active> _active = [];
    private bool _disposed;
    /// <summary>初始快照必须已经通过共享校验，并与实际条目身份一致。</summary>
    public SurfaceRegistrationSession(IPluginContext context, SurfaceValidationResult initial)
    {
        ArgumentNullException.ThrowIfNull(context); _context = context;
        Replace(initial);
    }
    /// <summary>
    /// 只新增、原位替换或撤销变化的目录项，返回这三类注册的总数；不改动代码服务、监听器或其它表面的注册引用。
    /// 新旧快照都有的项经注册句柄原子替换，读取方只看到旧文档或新文档；失败时撤销本次新增并恢复已替换的旧文档。
    /// </summary>
    public int Replace(SurfaceValidationResult next)
    {
        var plan = Plan(next, _context);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var added = plan.Where(item => !_active.ContainsKey(item.Key)).ToArray();
            var changed = plan.Where(item => _active.TryGetValue(item.Key, out var old) && !Same(old.Document, item.Value)).ToArray();
            var removed = _active.Where(item => !plan.ContainsKey(item.Key)).ToArray();
            // 注册按 key/name 独占（R38），不能先登记后撤销；先撤销后登记会让读取方在两步之间看到缺失。
            // 因此先新增，再原位替换，最后撤销已删除的项，回滚也不必重新登记。
            var installed = new List<(ServiceKey Key, string Name)>();
            var replaced = new List<((ServiceKey Key, string Name) Key, Active Old)>();
            try
            {
                foreach (var item in added)
                {
                    _active.Add(item.Key, new Active(item.Value, _context.Provide(item.Key.Key, item.Key.Name, item.Value)));
                    installed.Add(item.Key);
                }
                foreach (var item in changed)
                {
                    var old = _active[item.Key];
                    old.Handle.Replace(item.Value);
                    _active[item.Key] = old with { Document = item.Value };
                    replaced.Add((item.Key, old));
                }
            }
            catch
            {
                foreach (var (key, old) in replaced.AsEnumerable().Reverse())
                {
                    _active[key] = old;
                    try { old.Handle.Replace(old.Document); }
                    catch (ObjectDisposedException) { } // 上下文已卸载：注册随效果栈撤销，无须恢复。
                }
                foreach (var key in installed.AsEnumerable().Reverse()) { _active[key].Handle.Dispose(); _active.Remove(key); }
                throw;
            }
            foreach (var item in removed.Reverse()) { item.Value.Handle.Dispose(); _active.Remove(item.Key); }
            return added.Length + changed.Length + removed.Length;
        }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var item in _active.Values.Reverse()) item.Handle.Dispose();
            _active.Clear();
        }
    }
    internal static Dictionary<(ServiceKey Key, string Name), SurfaceDocument> Plan(SurfaceValidationResult result, IPluginContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsValid || result.Component is not { } component) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (context is not null && (component.Id != context.Entry.Plugin || component.Manifest.GetProperty("version").GetString() != context.Entry.Version ||
            !component.Manifest.GetProperty("scopes").EnumerateArray().Any(value => value.GetString() == context.Scope.Kind))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var entries = new Dictionary<(ServiceKey, string), SurfaceDocument>();
        foreach (var document in component.Documents)
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
                    if (!server.Value.TryGetProperty("profiles", out var profiles) || profiles.EnumerateArray().Any(profile => profile.GetString() == component.Profile))
                        Add(key.Value, component.Id + "/" + server.Name, document with { Name = server.Name, Data = server.Value.Clone() });
            }
            else
            {
                Add(key.Value, component.Id + "/" + document.Name, document);
                // 技能既有专属入口，也属于面向模型的工具集合；两个视图使用同一不可变文档。
                if (document.Kind == "skill") Add(SurfaceKeys.Tools, component.Id + "/" + document.Name, document);
            }
        }
        return entries;
        void Add(ServiceKey key, string name, SurfaceDocument document)
        {
            if (!entries.TryAdd((key, name), document)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        }
    }
    private static bool Same(SurfaceDocument first, SurfaceDocument second) => first.Kind == second.Kind && first.Name == second.Name && first.File == second.File && first.Body == second.Body && JsonElement.DeepEquals(first.Data, second.Data);
    private sealed record Active(SurfaceDocument Document, IRegistrationHandle<SurfaceDocument> Handle);
}

using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Authz;

/// <summary>进程内活动权限与资源注册表，按版本隔离并通过释放句柄撤销；持久目录与主体授权仍各自管理。</summary>
public sealed class PermissionRegistry : IPermissionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Version, string Code), Entry<PermissionDefinition>> _permissions = [];
    private readonly Dictionary<(string Version, string Code), Entry<ResourceTypeDefinition>> _resources = [];

    /// <inheritdoc />
    public IDisposable Register(string version, IReadOnlyList<PermissionDefinition> permissions)
    {
        Version(version); ArgumentNullException.ThrowIfNull(permissions);
        var values = permissions.ToArray();
        if (values.Any(value => value is null) || values.Select(value => value.Code).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw Invalid();
        return RegisterCore(version, values, _permissions, value => value.Code, SamePermission);
    }
    /// <inheritdoc />
    public PermissionDefinition? Find(string code, string version)
    {
        Version(version);
        lock (_gate) return _permissions.GetValueOrDefault((version, code))?.Value;
    }
    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> List(string pluginId, string version)
    {
        Version(version);
        lock (_gate) return Array.AsReadOnly(_permissions.Where(pair => pair.Key.Version == version && pair.Value.Value.PluginId == pluginId)
            .Select(pair => pair.Value.Value).OrderBy(value => value.Code, StringComparer.Ordinal).ToArray());
    }
    /// <inheritdoc />
    public IDisposable RegisterResources(string version, IReadOnlyList<ResourceTypeDefinition> resources)
    {
        Version(version); ArgumentNullException.ThrowIfNull(resources);
        var values = resources.ToArray();
        if (values.Any(value => value is null) || values.Select(value => value.Code).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw Invalid();
        return RegisterCore(version, values, _resources, value => value.Code,
            (left, right) => left.Code == right.Code && left.Description == right.Description);
    }
    /// <inheritdoc />
    public IReadOnlyList<ResourceTypeDefinition> ListResources(string pluginId, string version)
    {
        Version(version);
        lock (_gate)
        {
            var resources = _permissions.Where(pair => pair.Key.Version == version && pair.Value.Value.PluginId == pluginId)
                .Select(pair => pair.Value.Value.Resource).Distinct(StringComparer.Ordinal)
                .ToDictionary(resource => pluginId + ":" + resource, resource => new ResourceTypeDefinition(pluginId, resource), StringComparer.Ordinal);
            foreach (var resource in _resources.Where(pair => pair.Key.Version == version && pair.Value.Value.PluginId == pluginId).Select(pair => pair.Value.Value))
                resources[resource.Code] = resource;
            return Array.AsReadOnly(resources.Values.OrderBy(resource => resource.Code, StringComparer.Ordinal).ToArray());
        }
    }

    private Lease RegisterCore<T>(string version, T[] values, Dictionary<(string Version, string Code), Entry<T>> table,
        Func<T, string> key, Func<T, T, bool> same) where T : class
    {
        var token = Guid.NewGuid(); var keys = values.Select(value => (Version: version, Code: key(value))).ToArray();
        lock (_gate)
        {
            // 先检查整个批次，不能写入一半后才发现另一个组件已登记不同声明。
            for (var i = 0; i < values.Length; i++)
                if (table.TryGetValue(keys[i], out var existing) && !same(existing.Value, values[i]))
                    throw new QianErrorException(new QianError(PlatformErrors.Conflict));
            for (var i = 0; i < values.Length; i++)
            {
                if (!table.TryGetValue(keys[i], out var entry)) table.Add(keys[i], entry = new Entry<T>(values[i]));
                entry.Owners.Add(token);
            }
        }
        return new Lease(() =>
        {
            lock (_gate)
                foreach (var item in keys)
                    if (table.TryGetValue(item, out var entry) && entry.Owners.Remove(token) && entry.Owners.Count == 0) table.Remove(item);
        });
    }
    private static bool SamePermission(PermissionDefinition left, PermissionDefinition right) => left.Code == right.Code && left.Risk == right.Risk &&
        left.RequiresStepUp == right.RequiresStepUp && left.Terminals.Order(StringComparer.Ordinal).SequenceEqual(right.Terminals.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    private static void Version(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 200 || version.Any(char.IsControl)) throw Invalid();
    }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(AuthzErrors.MetadataInvalid, "authz/registry#", new Dictionary<string, string> { ["reason"] = "权限注册参数无效。" })] });
    private sealed class Entry<T>(T value) where T : class
    {
        internal readonly T Value = value;
        internal readonly HashSet<Guid> Owners = [];
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

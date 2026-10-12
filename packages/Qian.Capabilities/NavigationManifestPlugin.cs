using System.Text;
using System.Text.Json;
using Qian.Authz.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities;

/// <summary>从已捕获的 UI 清单贡献菜单，随实际业务插件一起装载、遮蔽与撤销。</summary>
public sealed class NavigationManifestPlugin : IQianPlugin
{
    private readonly IQianPlugin _inner;
    private readonly InstalledComponentMetadata _component;
    private readonly IReadOnlyList<NavigationContribution> _items;
    /// <summary>只读取制品快照；菜单声明不代表前端远程模块已经部署或渲染。</summary>
    public NavigationManifestPlugin(IQianPlugin inner, InstalledComponentMetadata component, ISurfaceReader reader)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(component); ArgumentNullException.ThrowIfNull(reader);
        _inner = inner; _component = component;
        var path = "./ui/manifest.json";
        var declared = component.Manifest.TryGetProperty("surfaces", out var surfaces) && surfaces.TryGetProperty("ui", out _);
        if (declared) path = surfaces.GetProperty("ui").GetString() ?? throw Invalid();
        if (!path.StartsWith("./", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal) || path[2..].Split('/').Any(part => part is "" or "." or "..")) throw Invalid();
        var bytes = component.ReadFile(path[2..]);
        if (bytes is null) { if (declared) throw Invalid(); _items = []; return; }
        if (component.ArtifactDigest is null || bytes.Length > 1_048_576) throw Invalid();
        var parsed = reader.Parse(path, new UTF8Encoding(false, true).GetString(bytes));
        if (parsed.Syntax is null || parsed.Diagnostics.Any(item => item.Severity == "error") || parsed.Syntax.Data.ValueKind != JsonValueKind.Object) throw Invalid();
        if (!parsed.Syntax.Data.TryGetProperty("pages", out var pages)) { _items = []; return; }
        if (pages.ValueKind != JsonValueKind.Array || pages.GetArrayLength() > 256) throw Invalid();
        var items = new List<NavigationContribution>(); var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.EnumerateArray())
        {
            if (page.ValueKind != JsonValueKind.Object) throw Invalid();
            if (!page.TryGetProperty("nav", out var nav)) continue;
            if (nav.ValueKind != JsonValueKind.Object) throw Invalid();
            var route = Text(page, "route"); var permission = Text(page, "permission"); var title = Text(nav, "title");
            var expose = Text(page, "expose");
            if (route.Length is 0 or > 200 || route[0] != '/' || route.StartsWith("//", StringComparison.Ordinal) || route.Any(ch => char.IsControl(ch) || ch is '\\' or '?' or '#') ||
                route.Split('/').Any(part => part is "." or "..") || !routes.Add(route) || !PermissionCode.IsValid(permission) ||
                title.Length is 0 or > 80 || title.Any(char.IsControl) || expose.Length is 0 or > 128) throw Invalid();
            if (5 + 36 + 1 + component.PluginId.Length + 6 + Uri.EscapeDataString(route).Length > 512) throw Invalid();
            var icon = nav.TryGetProperty("icon", out var iconValue) && iconValue.ValueKind == JsonValueKind.String ? iconValue.GetString() : null;
            if (nav.TryGetProperty("icon", out _) && (icon is null || icon.Length is 0 or > 80 || icon.Any(char.IsControl))) throw Invalid();
            var order = 0;
            if (nav.TryGetProperty("order", out var orderValue) && (orderValue.ValueKind != JsonValueKind.Number || !orderValue.TryGetInt32(out order) || order is < -10000 or > 10000)) throw Invalid();
            items.Add(new NavigationContribution(component.PluginId, route, title, icon, order, permission));
        }
        _items = items.AsReadOnly();
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _inner.Inject;
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.Entry.Plugin != _component.PluginId || ctx.Entry.Version != _component.Version) throw Invalid();
        await _inner.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
        foreach (var item in _items)
        { ct.ThrowIfCancellationRequested(); ctx.Provide(CapabilitiesDefinition.NavigationItemsKey, item.Name, item); }
    }
    private static string Text(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString()! : throw Invalid();
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}
internal sealed record NavigationContribution(string PluginId, string Route, string Title, string? Icon, int Order, string Permission)
{
    internal string Name => PluginId + "/page" + Route;
    internal NavigationItem Describe(string tenant) => new("qian:" + tenant + ":" + PluginId + ":page:" + Uri.EscapeDataString(Route), PluginId, Route, Title, Icon, Order);
}

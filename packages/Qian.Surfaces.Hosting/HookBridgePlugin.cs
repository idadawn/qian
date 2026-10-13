using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces.Hosting;

/// <summary>将已校验 hook 声明连接到真实事件与 handler，不执行声明中的任意代码。</summary>
public sealed class HookBridgePlugin : IQianPlugin, ISurfaceRuntimeValidator
{
    private readonly IQianPlugin _inner;
    private readonly SurfaceValidationResult _snapshot;
    /// <summary>被包装代码在 Apply 中暴露实际 handler，表面注册由宿主或 HotReloadSurfacePlugin 完成。</summary>
    public HookBridgePlugin(IQianPlugin inner, SurfaceValidationResult snapshot)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsValid) throw Invalid(); _inner = inner; _snapshot = snapshot;
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _inner.Inject;
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        if (_snapshot.Component!.Id != ctx.Entry.Plugin || _snapshot.Component.Manifest.GetProperty("version").GetString() != ctx.Entry.Version) throw Invalid();
        await _inner.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
        ValidateSurfaces(ctx, _snapshot);
        var name = _snapshot.Component.Id + "/hooks";
        Listen(SurfaceHookEvents.TenantProvisioned, "qian.tenancy"); Listen(SurfaceHookEvents.TenantPurged, "qian.tenancy");
        Listen(SurfaceHookEvents.EntryMounted, "qian.registry"); Listen(SurfaceHookEvents.EntryDisabled, "qian.registry");
        void Listen(QianEvent<HookNotification, IReadOnlyList<JsonElement>> evt, string owner)
            => ctx.On(evt, DispatchMode.Serial, (Func<HookNotification, ValueTask<JsonElement>>)(notification => Invoke(ctx, name, evt.Name, owner, notification)));
    }
    /// <inheritdoc />
    public void ValidateSurfaces(IPluginContext context, SurfaceValidationResult candidate)
    {
        if (!candidate.IsValid || candidate.Component?.Id != context.Entry.Plugin) throw Invalid();
        foreach (var document in candidate.Component.Documents.Where(document => document.Kind == "hooks"))
            foreach (var definition in document.Data.GetProperty("hooks").EnumerateObject())
            {
                if (definition.Name is not ("tenant/provisioned" or "tenant/purged" or "registry/entry-mounted" or "registry/entry-disabled")) throw Invalid();
                ValidateTargets(context, definition.Value);
            }
    }
    private static void ValidateTargets(IPluginContext ctx, JsonElement groups)
    {
        foreach (var group in groups.EnumerateArray())
            foreach (var action in group.GetProperty("hooks").EnumerateArray())
            {
                if (action.GetProperty("type").GetString() != "plugin") throw Invalid();
                _ = ctx.Inject<IHookHandler>(HookDefinition.Handlers, action.GetProperty("plugin").GetString() + "/" + action.GetProperty("handler").GetString());
            }
    }
    private static async ValueTask<JsonElement> Invoke(IPluginContext owner, string name, string eventName, string producer, HookNotification notification)
    {
        if (notification.Source.Entry.Plugin != producer || notification.Source.Scope.Kind != "tenant") throw Invalid();
        if (eventName.StartsWith("tenant/", StringComparison.Ordinal) && notification.Subject != notification.Source.Scope.Id) throw Invalid();
        var visible = owner.Scoped(notification.Source.Scope);
        SurfaceDocument document;
        try { document = visible.Inject<SurfaceDocument>(SurfaceKeys.Hooks, name); }
        catch (QianErrorException error) when (error.Error.Code == PlatformErrors.NotFound) { return JsonSerializer.SerializeToElement<object?>(null); }
        if (!document.Data.GetProperty("hooks").TryGetProperty(eventName, out var groups)) return JsonSerializer.SerializeToElement<object?>(null);
        ValidateTargets(visible, groups);
        var result = JsonSerializer.SerializeToElement<object?>(null);
        foreach (var group in groups.EnumerateArray())
        {
            notification.CancellationToken.ThrowIfCancellationRequested();
            if (group.TryGetProperty("matcher", out var matcher) && !Regex.IsMatch(notification.Subject, matcher.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) continue;
            foreach (var action in group.GetProperty("hooks").EnumerateArray())
            {
                var handler = visible.Inject<IHookHandler>(HookDefinition.Handlers, action.GetProperty("plugin").GetString() + "/" + action.GetProperty("handler").GetString());
                result = await handler.InvokeAsync(notification, notification.CancellationToken).ConfigureAwait(false);
            }
        }
        return result;
    }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

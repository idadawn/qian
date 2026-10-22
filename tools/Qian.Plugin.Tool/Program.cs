using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Qian.Plugin.Sdk;
using Qian.Plugin.Tool;
using Qian.Runtime.Errors;

try
{
    if (!(args.Length == 3 && args[0] is "check" or "sync") && !(args.Length == 2 && args[0] == "inventory"))
        throw new ArgumentException("用法：check|sync <可信项目编译程序集> <组件目录>；inventory <程序集>");
    var path = Path.GetFullPath(args[1]); var context = new MetadataContext(path);
    try
    {
        var assembly = context.LoadFromAssemblyPath(path);
        if (args[0] == "inventory")
        {
            Console.WriteLine(JsonSerializer.Serialize(assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && typeof(Qian.Runtime.IQianPlugin).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal).Select(type => new { type = type.FullName, component = type.GetCustomAttribute<QianPluginAttribute>()?.Id }), JsonSerializerOptions.Web));
            return 0;
        }
        var declarations = assembly.GetCustomAttributes<QianCodeMetadataAttribute>().ToArray();
        if (declarations.Length == 0) throw new ManifestException(ManifestErrors.MetadataMissing, "程序集没有 [QianPlugin] 入口或未引用源生成器。", "assembly#");
        var metadata = declarations.Select(declaration =>
        {
            try
            {
                return (Activator.CreateInstance(declaration.FactoryType) as IComponentCodeMetadataFactory
                    ?? throw new ManifestException(ManifestErrors.MetadataInvalid, "派生工厂没有实现 SDK 元数据协议。", "assembly#")).Create();
            }
            catch (Exception error) when (error is ArgumentException or TargetInvocationException or TypeInitializationException or InvalidOperationException or JsonException)
            { throw new ManifestException(ManifestErrors.MetadataInvalid, error.InnerException?.Message ?? error.Message, "assembly#"); }
        }).ToArray();
        if (metadata.Select(item => item.ComponentId).Distinct(StringComparer.Ordinal).Count() != metadata.Length)
            throw new ManifestException(ManifestErrors.MetadataInvalid, "同一程序集出现重复组件 ID。", "assembly#");
        var id = ManifestPlan.ReadComponentId(args[2]);
        var code = metadata.SingleOrDefault(item => item.ComponentId == id)
            ?? throw new ManifestException(ManifestErrors.ComponentMismatch, "清单 ID " + id + " 在程序集内没有匹配的 [QianPlugin] 入口。", ManifestPlan.ManifestFile + "#/id");
        var plan = ManifestPlan.Create(args[2], code);
        if (args[0] == "sync") plan.Apply();
        Console.WriteLine(JsonSerializer.Serialize(new { changed = plan.Drift, applied = args[0] == "sync" }, JsonSerializerOptions.Web));
        return args[0] == "check" && plan.Drift.Count != 0 ? 1 : 0;
    }
    finally { context.Unload(); }
}
catch (Exception error) when (error is ManifestException or ArgumentException or InvalidDataException or IOException or TargetInvocationException or TypeLoadException or JsonException or InvalidOperationException or KeyNotFoundException or ReflectionTypeLoadException)
{
    var failure = error as ManifestException ?? new ManifestException(ManifestErrors.ManifestInvalid, error.InnerException?.Message ?? error.Message, ManifestPlan.ManifestFile + "#");
    var catalog = ErrorCatalog.CreateDefault(); using var code = catalog.Register(failure.Code); using var field = catalog.Register(ManifestErrors.Field);
    var reason = new Dictionary<string, string> { ["reason"] = failure.Message };
    var detail = new QianError(failure.Code, reason) { Errors = [new QianFieldError(ManifestErrors.Field, failure.Pointer, reason)] };
    Console.Error.WriteLine(ErrorDetailJson.Serialize(catalog.CreateDetail(detail, Guid.NewGuid().ToString("N"))));
    return 2;
}

// 仅对用户显式选择的本地可信构建运行生成工厂，不用于市场准入或安装未知程序集。
internal sealed class MetadataContext(string path) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(path);
    private readonly Dictionary<string, string> _packages = PackageAssets(path);
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 工具与工厂必须共用 SDK 及其 Runtime 类型；仅查已加载集合会把尚未加载的依赖复制到另一上下文。
        try { return Default.LoadFromAssemblyName(assemblyName); }
        catch (FileNotFoundException) { }
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolved is null && assemblyName.Name is { } name) _packages.TryGetValue(name, out resolved);
        return resolved is null ? null : LoadFromAssemblyPath(resolved);
    }

    // class library 构建默认不复制全部 NuGet 依赖；按该构建的 deps 清单读取本机已还原的固定资产，绝不联网还原。
    private static Dictionary<string, string> PackageAssets(string assembly)
    {
        var deps = Path.ChangeExtension(assembly, ".deps.json"); var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(deps)) return result;
        using var document = JsonDocument.Parse(File.ReadAllText(deps)); var root = document.RootElement;
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach (var target in root.GetProperty("targets").EnumerateObject())
            foreach (var library in target.Value.EnumerateObject())
            {
                if (!root.GetProperty("libraries").TryGetProperty(library.Name, out var information) || !information.TryGetProperty("path", out var libraryPath) ||
                    !library.Value.TryGetProperty("runtime", out var runtime)) continue;
                foreach (var asset in runtime.EnumerateObject())
                {
                    if (!asset.Name.EndsWith(".dll", StringComparison.Ordinal)) continue;
                    var candidate = Path.GetFullPath(Path.Combine(cache, libraryPath.GetString()!, asset.Name));
                    if (File.Exists(candidate)) result.TryAdd(Path.GetFileNameWithoutExtension(asset.Name), candidate);
                }
            }
        return result;
    }
}

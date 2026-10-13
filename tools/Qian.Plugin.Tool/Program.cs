using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Qian.Plugin.Sdk;
using Qian.Plugin.Tool;
using Qian.Runtime.Errors;

try
{
    if (args.Length != 3 || args[0] is not ("check" or "sync")) throw new ArgumentException("用法：check|sync <可信项目编译程序集> <组件目录>");
    var path = Path.GetFullPath(args[1]); var context = new MetadataContext(path);
    try
    {
        var assembly = context.LoadFromAssemblyPath(path);
        var declaration = assembly.GetCustomAttribute<QianCodeMetadataAttribute>() ?? throw new InvalidDataException("程序集没有代码派生元数据。");
        var factory = Activator.CreateInstance(declaration.FactoryType) as IComponentCodeMetadataFactory ?? throw new InvalidDataException("派生元数据工厂无效。");
        var plan = ManifestPlan.Create(args[2], factory.Create());
        if (args[0] == "sync") plan.Apply();
        Console.WriteLine(JsonSerializer.Serialize(new { changed = plan.Drift, applied = args[0] == "sync" }, JsonSerializerOptions.Web));
        return args[0] == "check" && plan.Drift.Count != 0 ? 1 : 0;
    }
    finally { context.Unload(); }
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or TargetInvocationException or TypeLoadException)
{
    Console.Error.WriteLine(ErrorDetailJson.Serialize(ErrorCatalog.CreateDefault().CreateDetail(new QianError(PlatformErrors.InputInvalid), Guid.NewGuid().ToString("N"))));
    return 2;
}

// 仅对用户显式选择的本地可信构建运行生成工厂，不用于市场准入或安装未知程序集。
internal sealed class MetadataContext(string path) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(path);
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 工具与工厂必须共用 SDK 及其 Runtime 类型；仅查已加载集合会把尚未加载的依赖复制到另一上下文。
        try { return Default.LoadFromAssemblyName(assemblyName); }
        catch (FileNotFoundException) { }
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolved is null ? null : LoadFromAssemblyPath(resolved);
    }
}

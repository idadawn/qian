using System.Reflection;
using System.Text.Json;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data.Dialect.Pg;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Data.AspNetCore.Tests;

public sealed class PgDialectComponentTests
{
    [Fact]
    public async Task 清单入口实际提供具名方言_卸载同时撤销运行时与数据目录()
    {
        var services = new ServiceCollection();
        services.AddKunEventBus();
        services.AddQianDataTypes();
        await using var provider = services.BuildServiceProvider();
        await using var loader = new PluginLoader(provider);
        var code = Metadata();
        Assert.Equal(["Data"], code.Inject);
        Assert.Equal(["Data.Dialects:pg"], code.Provides);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.data.dialect.pg/.qian-plugin/plugin.json")));
        var root = manifest.RootElement;
        Assert.Equal(code.ComponentId, root.GetProperty("id").GetString());
        Assert.Equal(code.Inject, root.GetProperty("inject").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(code.Provides, root.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        var runtime = root.GetProperty("runtime");
        Assert.Equal("module", runtime.GetProperty("form").GetString());
        Assert.Equal(code.RuntimeEntry, runtime.GetProperty("entry").GetString());
        var entry = (IQianPlugin)Activator.CreateInstance(Type.GetType(runtime.GetProperty("entry").GetString()!, throwOnError: true)!)!;
        await loader.MountAsync([
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("pg", code.ComponentId, entry, code.Provides.Select(name => new ServiceKey(name)).ToArray())], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, loader.GetState("pg", ScopeKey.Global));
        var context = loader.GetContext("data", ScopeKey.Global);
        var contribution = Assert.Single(context.List(DataDialectContract.Key));
        Assert.Equal("pg", contribution.Name);
        Assert.IsType<PgDialect>(contribution.Value);
        Assert.Same(contribution.Value, provider.GetRequiredService<IDataDialectRegistry>().Find("pg"));
        Assert.Equal("pg", Assert.Single(provider.GetRequiredService<IDataTypeCatalog>().Snapshot().Dialects).Code);

        await loader.UnmountAsync("pg", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Empty(context.List(DataDialectContract.Key));
        Assert.Null(provider.GetRequiredService<IDataDialectRegistry>().Find("pg"));
        Assert.Empty(provider.GetRequiredService<IDataTypeCatalog>().Snapshot().Dialects);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 独立宿主重复装配仍只有一个常驻PG方言_不依赖组件挂载(bool dialectFirst)
    {
        var services = new ServiceCollection();
        if (dialectFirst) services.AddQianPgDialect();
        services.AddQianDataTypes();
        services.AddQianPgDialect();
        services.AddQianPgDialect();
        using var provider = services.BuildServiceProvider();
        var dialect = Assert.IsType<PgDialect>(Assert.Single(provider.GetServices<IDataDialect>()));
        Assert.Same(dialect, provider.GetRequiredService<IDataDialectRegistry>().Find("pg"));
        var catalog = provider.GetRequiredService<IDataTypeCatalog>();
        Assert.Equal(10, catalog.Snapshot().Types.Count);
        Assert.Same(dialect.Descriptor, Assert.Single(catalog.Snapshot().Dialects));
        Assert.Equal("jsonb", catalog.MapField("pg", new DataFieldDefinition("json")).NativeType);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }

    [Fact]
    public async Task 方言已有宿主登记时挂载失败并撤销已经提供的运行时项()
    {
        var services = new ServiceCollection();
        services.AddKunEventBus();
        services.AddQianDataTypes();
        services.AddQianPgDialect();
        await using var provider = services.BuildServiceProvider();
        await using var loader = new PluginLoader(provider);
        await loader.MountAsync([
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("pg", "qian.data.dialect.pg", new PgDialectPlugin(), [new ServiceKey("Data.Dialects:pg")])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("pg", ScopeKey.Global));
        Assert.Empty(loader.GetContext("data", ScopeKey.Global).List(DataDialectContract.Key));
        Assert.NotNull(provider.GetRequiredService<IDataDialectRegistry>().Find("pg"));
    }

    private static ComponentCodeMetadata Metadata() => typeof(PgDialectPlugin).Assembly.GetCustomAttributes<QianCodeMetadataAttribute>()
        .Select(attribute => ((IComponentCodeMetadataFactory)Activator.CreateInstance(attribute.FactoryType)!).Create()).Single(code => code.ComponentId == "qian.data.dialect.pg");

    private static PluginDefinition Define(string id, string plugin, IQianPlugin implementation, IReadOnlyList<ServiceKey> provides) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), implementation, provides);
}

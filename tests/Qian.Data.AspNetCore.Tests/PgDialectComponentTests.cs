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
        var entry = (IQianPlugin)Activator.CreateInstance(Type.GetType(code.RuntimeEntry, throwOnError: true)!)!;
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

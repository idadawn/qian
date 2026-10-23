using System.Reflection;
using System.Text.Json;
using Qian.Config.Apollo;
using Qian.Plugin.Sdk;

namespace Qian.Config.Tests;

public sealed class ConfigMetadataTests
{
    [Fact]
    public void 标准与Apollo公开入口保持原类型名_共享事件同源且只由标准组件拥有_权限不跨组件复制()
    {
        var metadata = typeof(ConfigurationPlugin).Assembly.GetCustomAttributes<QianCodeMetadataAttribute>()
            .Select(attribute => Assert.IsAssignableFrom<IComponentCodeMetadataFactory>(Activator.CreateInstance(attribute.FactoryType)).Create())
            .ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        Assert.Equal(3, metadata.Count);
        Assert.Equal("Qian.Config.Apollo.ConfigurationPlugin, Qian.Config.Apollo", metadata["qian.config"].RuntimeEntry);
        Assert.Equal("Qian.Config.Apollo.ApolloConfigurationPlugin, Qian.Config.Apollo", metadata["qian.config.apollo"].RuntimeEntry);
        var expected = Assert.Single(metadata["qian.config"].Events.EnumerateArray());
        Assert.True(JsonElement.DeepEquals(expected, Assert.Single(metadata["qian.config.apollo"].Events.EnumerateArray())));
        Assert.Equal("qian.config", expected.GetProperty("definitionComponent").GetString());
        Assert.Equal(ConfigRuntimeEvents.ReloadFailed.Name, expected.GetProperty("name").GetString());
        Assert.Equal("emit", expected.GetProperty("mode").GetString());
        Assert.Equal(typeof(ConfigReloadFailed).FullName, expected.GetProperty("payload").GetString());
        Assert.Equal("Qian.Config.ConfigRuntimeEvents.ReloadFailed", expected.GetProperty("source").GetString());

        var root = RepositoryRoot();
        foreach (var (id, code) in metadata.Where(pair => pair.Key != "qian.config.flags"))
        {
            Assert.Empty(code.Inject); Assert.Equal(ConfigDefinition.Key.Name, Assert.Single(code.Provides));
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins", id, ".qian-plugin/plugin.json")));
            Assert.Equal(code.RuntimeEntry, manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString());
            using var events = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins", id, "metadata/events.json")));
            Assert.True(JsonElement.DeepEquals(code.Events, events.RootElement));
        }
        Assert.True(File.Exists(Path.Combine(root, "plugins/qian.config/metadata/permissions.json")));
        Assert.True(File.Exists(Path.Combine(root, "plugins/qian.config/metadata/roles.json")));
        Assert.False(File.Exists(Path.Combine(root, "plugins/qian.config.apollo/metadata/permissions.json")));
        Assert.False(File.Exists(Path.Combine(root, "plugins/qian.config.apollo/metadata/roles.json")));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

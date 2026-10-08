using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors;
using YamlDotNet.Serialization;

namespace Qian.Runtime.Tests;

public sealed class RuntimeScenarioTests
{
    public static IEnumerable<object[]> Scenarios => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "tck"), "*.scenario.yml")
        .Order(StringComparer.Ordinal).Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Run_language_neutral_scenario(string path)
    {
        var yaml = new DeserializerBuilder().Build().Deserialize<object>(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var json = new SerializerBuilder().JsonCompatible().Build().Serialize(yaml);
        using var document = JsonDocument.Parse(json);
        var scenario = document.RootElement;
        Assert.Equal("1", scenario.GetProperty("version").ToString());
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        ConcurrentQueue<string> trace = [];
        var entries = scenario.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e);
        PluginDefinition Definition(string id)
        {
            var entry = entries[id];
            return new PluginDefinition(new PluginEntry(id, id, "1.0.0", Scope(entry), 0, JsonSerializer.SerializeToElement(new { })),
                new ScenarioPlugin(entry, trace), Keys(entry, "provides"));
        }
        foreach (var step in scenario.GetProperty("steps").EnumerateArray())
        {
            var op = step.GetProperty("op").GetString();
            switch (op)
            {
                case "mount":
                    var definitions = step.GetProperty("entries").EnumerateArray().Select(e => Definition(e.GetString()!)).ToArray();
                    if (step.TryGetProperty("error", out var error))
                    {
                        var exception = await Assert.ThrowsAsync<QianErrorException>(async () => await loader.MountAsync(definitions, TestContext.Current.CancellationToken));
                        Assert.Equal(error.GetProperty("code").GetString(), exception.Error.Code.Code);
                        Assert.Equal("qian.runtime", exception.Error.Code.Owner);
                        foreach (var arg in error.GetProperty("args").EnumerateObject())
                            Assert.Equal(arg.Value.GetString(), exception.Error.Args[arg.Name]);
                        var detail = ErrorCatalog.CreateDefault().CreateDetail(exception.Error, new string('a', 32), CultureInfo.InvariantCulture);
                        Assert.Equal(exception.Error.Code.Code, detail.Code);
                    }
                    else await loader.MountAsync(definitions, TestContext.Current.CancellationToken);
                    break;
                case "state":
                    Assert.Equal(step.GetProperty("value").GetString(), loader.GetState(step.GetProperty("entry").GetString()!, Scope(step)).ToString());
                    break;
                case "unmount":
                    await loader.UnmountAsync(step.GetProperty("entry").GetString()!, Scope(step), TestContext.Current.CancellationToken);
                    break;
                case "teardown": await loader.DisposeAsync(); break;
                case "missing":
                    Assert.Null(loader.GetContext(step.GetProperty("entry").GetString()!, Scope(step)).TryInject<string>(new ServiceKey(step.GetProperty("key").GetString()!)));
                    break;
                case "inject":
                    var context = loader.GetContext(step.GetProperty("entry").GetString()!, Scope(step));
                    var key = new ServiceKey(step.GetProperty("key").GetString()!);
                    Assert.Equal(step.GetProperty("value").GetString(), context.Inject<string>(key));
                    Assert.Equal(step.GetProperty("value").GetString(), context.Services.GetRequiredKeyedService<string>(key));
                    break;
                case "trace":
                    Assert.Equal(step.GetProperty("value").EnumerateArray().Select(e => e.GetString()), trace);
                    break;
                default: throw new InvalidOperationException($"未知 TCK 操作：{op}");
            }
        }
    }

    private static ServiceKey[] Keys(JsonElement node, string name) => node.TryGetProperty(name, out var values)
        ? values.EnumerateArray().Select(v => new ServiceKey(v.GetString()!)).ToArray() : [];

    private static ScopeKey Scope(JsonElement node)
    {
        if (!node.TryGetProperty("scope", out var value) || value.GetString() == "global") return ScopeKey.Global;
        var parts = value.GetString()!.Split(':', 2);
        return new ScopeKey(parts[0], parts[1]);
    }

    private sealed class ScenarioPlugin(JsonElement entry, ConcurrentQueue<string> trace) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => Keys(entry, "inject");

        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            trace.Enqueue($"apply:{ctx.Entry.Id}");
            foreach (var action in entry.GetProperty("apply").EnumerateArray())
            {
                switch (action.GetProperty("op").GetString())
                {
                    case "provide": ctx.Provide(new ServiceKey(action.GetProperty("key").GetString()!), action.GetProperty("value").GetString()!); break;
                    case "effect":
                        ctx.Effect(async () =>
                        {
                            await Task.Yield();
                            trace.Enqueue($"dispose:{action.GetProperty("name").GetString()}");
                            if (action.TryGetProperty("fail", out var fail) && fail.GetBoolean()) throw new InvalidOperationException("撤销失败。");
                        });
                        break;
                    case "optional":
                        Assert.Null(ctx.TryInject<string>(new ServiceKey(action.GetProperty("key").GetString()!)));
                        break;
                    case "fail": throw new InvalidOperationException("场景中的 Apply 失败。");
                    default: throw new InvalidOperationException("未知 TCK Apply 操作。");
                }
            }
            return ValueTask.CompletedTask;
        }
    }
}

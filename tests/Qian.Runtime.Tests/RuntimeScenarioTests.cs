using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Runtime.Errors;
using Qian.Surfaces;
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
        var yaml = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build()
            .Deserialize<object>(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var json = new SerializerBuilder().JsonCompatible().Build().Serialize(yaml);
        using var document = JsonDocument.Parse(json);
        var scenario = document.RootElement;
        Assert.Equal("1", scenario.GetProperty("version").ToString());
        // 每个场景先加载交付的 minimal 文件和真实表面校验器，不用虚构的空 profile。
        var baseline = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions
        {
            WorkingDirectory = FindRoot(), Profile = "minimal", IncludeHomePatch = false
        });
        Assert.True(baseline.IsValid);
        Assert.Empty(baseline.Snapshot!.Entries);
        Assert.Equal("qian.bundle.minimal@1.0.0", Assert.Single(baseline.Snapshot.Layers, layer => layer.Kind == "bundle").Reference);
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new ScenarioLogger();
        await using var loader = new PluginLoader(services, logger);
        await loader.MountAsync([], TestContext.Current.CancellationToken);
        ConcurrentQueue<string> trace = [];
        var events = new ScenarioEvents(scenario, trace);
        var entries = scenario.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e);
        PluginDefinition Definition(string id)
        {
            var entry = entries[id];
            return new PluginDefinition(new PluginEntry(id, id, "1.0.0", Scope(entry), 0, JsonSerializer.SerializeToElement(new { })),
                new ScenarioPlugin(entry, trace, events), Keys(entry, "provides"));
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
                case "dump":
                    Assert.Equal(step.GetProperty("entries").EnumerateArray().Select(e => e.GetString()), loader.DumpConfig().Select(e => e.Id));
                    break;
                case "cleanupErrors":
                    Assert.Equal(step.GetProperty("value").GetInt32(), logger.CleanupErrors);
                    break;
                case "failure":
                    var failure = Assert.IsType<QianErrorException>(loader.GetFailure(step.GetProperty("entry").GetString()!, Scope(step)));
                    Assert.Equal(step.GetProperty("code").GetString(), failure.Error.Code.Code);
                    foreach (var arg in step.GetProperty("args").EnumerateObject()) Assert.Equal(arg.Value.GetString(), failure.Error.Args[arg.Name]);
                    break;
                case "dispatch":
                    await events.Dispatch(Context(loader, step), step);
                    break;
                case "traceContains":
                    foreach (var value in step.GetProperty("value").EnumerateArray()) Assert.Contains(value.GetString(), trace);
                    break;
                case "missing":
                    Assert.Null(Context(loader, step).TryInject<string>(new ServiceKey(step.GetProperty("key").GetString()!)));
                    Assert.Null(Context(loader, step).Services.GetKeyedService<string>(new ServiceKey(step.GetProperty("key").GetString()!)));
                    break;
                case "list":
                    Assert.Equal(step.GetProperty("value").EnumerateArray().Select(v => v.GetString()),
                        Context(loader, step).List(new ServiceKey(step.GetProperty("key").GetString()!)).Select(r => r.Name));
                    break;
                case "notFoundEquivalent":
                    var view = Context(loader, step);
                    var registry = new ServiceKey(step.GetProperty("key").GetString()!);
                    var filtered = Assert.Throws<QianErrorException>(() => view.Inject<string>(registry, step.GetProperty("name").GetString()!));
                    var absent = Assert.Throws<QianErrorException>(() => view.Inject<string>(registry, "does-not-exist"));
                    var catalog = ErrorCatalog.CreateDefault();
                    var traceId = new string('a', 32);
                    Assert.Equal(JsonSerializer.Serialize(catalog.CreateDetail(absent.Error, traceId), ErrorDetailJson.TypeInfo),
                        JsonSerializer.Serialize(catalog.CreateDetail(filtered.Error, traceId), ErrorDetailJson.TypeInfo));
                    Assert.Null(view.Services.GetKeyedService<string>(new ServiceKey($"{registry.Name}:{step.GetProperty("name").GetString()}")));
                    break;
                case "inject":
                    var context = Context(loader, step);
                    var keyName = step.GetProperty("key").GetString()!;
                    if (step.TryGetProperty("name", out var registrationName))
                    {
                        Assert.Equal(step.GetProperty("value").GetString(), context.Inject<string>(new(keyName), registrationName.GetString()!));
                        keyName += $":{registrationName.GetString()}";
                    }
                    var key = new ServiceKey(keyName);
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

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Qian.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("找不到仓库。");
    }

    private sealed class ScenarioLogger : ILogger
    {
        private int _cleanupErrors;
        internal int CleanupErrors => Volatile.Read(ref _cleanupErrors);
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name == "CleanupFailed") Interlocked.Increment(ref _cleanupErrors);
        }
    }

    private static ServiceKey[] Keys(JsonElement node, string name) => node.TryGetProperty(name, out var values)
        ? values.EnumerateArray().Select(v => new ServiceKey(v.GetString()!)).ToArray() : [];

    private static IPluginContext Context(PluginLoader loader, JsonElement step)
    {
        var context = loader.GetContext(step.GetProperty("entry").GetString()!, Scope(step));
        return step.TryGetProperty("view", out var view) ? context.Scoped(ParseScope(view.GetString()!)) : context;
    }

    private static ScopeKey Scope(JsonElement node)
    {
        if (!node.TryGetProperty("scope", out var value) || value.GetString() == "global") return ScopeKey.Global;
        return ParseScope(value.GetString()!);
    }

    private static ScopeKey ParseScope(string value)
    {
        if (value == "global") return ScopeKey.Global;
        var parts = value.Split(':', 2);
        return new ScopeKey(parts[0], parts[1]);
    }

    private sealed class ScenarioPlugin(JsonElement entry, ConcurrentQueue<string> trace, ScenarioEvents events) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => Keys(entry, "inject");

        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            trace.Enqueue($"apply:{ctx.Entry.Id}");
            foreach (var action in entry.GetProperty("apply").EnumerateArray())
            {
                var target = action.TryGetProperty("scope", out _) ? ctx.Scoped(Scope(action)) : ctx;
                switch (action.GetProperty("op").GetString())
                {
                    case "provide":
                        var key = new ServiceKey(action.GetProperty("key").GetString()!);
                        if (action.TryGetProperty("name", out var name)) target.Provide(key, name.GetString()!, action.GetProperty("value").GetString()!);
                        else target.Provide(key, action.GetProperty("value").GetString()!);
                        break;
                    case "restrict":
                        var keep = action.GetProperty("keep").EnumerateArray().Select(v => v.GetString()).ToArray();
                        ctx.Restrict(Scope(action), r => keep.Contains(r.Name, StringComparer.Ordinal));
                        break;
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
                    case "on": events.On(target, action); break;
                    case "fail": throw new InvalidOperationException("场景中的 Apply 失败。");
                    default: throw new InvalidOperationException("未知 TCK Apply 操作。");
                }
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScenarioEvents
    {
        private readonly Dictionary<string, QianEvent<string, string?>> _single = [];
        private readonly Dictionary<string, QianEvent<string, IReadOnlyList<string?>>> _many = [];
        private readonly ConcurrentQueue<string> _trace;

        internal ScenarioEvents(JsonElement scenario, ConcurrentQueue<string> trace)
        {
            _trace = trace;
            if (!scenario.TryGetProperty("events", out var events)) return;
            foreach (var evt in events.EnumerateArray())
            {
                var name = evt.GetProperty("name").GetString()!;
                var mode = Enum.Parse<DispatchMode>(evt.GetProperty("mode").GetString()!, true);
                if (mode is DispatchMode.Parallel or DispatchMode.Serial) _many[name] = QianEvent.Define<string, IReadOnlyList<string?>>(name, mode);
                else _single[name] = QianEvent.Define<string, string?>(name, mode);
            }
        }

        internal void On(IPluginContext context, JsonElement action)
        {
            var name = action.GetProperty("event").GetString()!;
            var label = action.GetProperty("name").GetString()!;
            var priority = action.TryGetProperty("priority", out var p) ? p.GetInt32() : 0;
            var behavior = action.TryGetProperty("behavior", out var b) ? b.GetString() : "return";
            var value = action.TryGetProperty("value", out var v) ? v.GetString() : null;
            async ValueTask<string?> Result(string payload)
            {
                _trace.Enqueue($"enter:{label}");
                await Task.Yield();
                if (behavior == "fail") throw new InvalidOperationException(label);
                _trace.Enqueue($"exit:{label}");
                return value;
            }
            if (_many.TryGetValue(name, out var many))
            {
                var declaredMode = action.TryGetProperty("mode", out var supplied) ? Enum.Parse<DispatchMode>(supplied.GetString()!, true) : many.Mode;
                context.On(many, declaredMode, (Func<string, ValueTask<string?>>)Result, priority);
                return;
            }
            var evt = _single[name];
            var mode = action.TryGetProperty("mode", out var declared) ? Enum.Parse<DispatchMode>(declared.GetString()!, true) : evt.Mode;
            switch (evt.Mode)
            {
                case DispatchMode.Emit:
                    context.On(evt, mode, (Func<string, ValueTask>)(async payload => { await Result(payload); }), priority);
                    break;
                case DispatchMode.Bail:
                    context.On(evt, mode, (Func<string, ValueTask<string?>>)Result, priority);
                    break;
                case DispatchMode.Waterfall:
                    context.On(evt, mode, (Func<string, Func<ValueTask<string?>>, ValueTask<string?>>)(async (_, next) =>
                    {
                        _trace.Enqueue($"enter:{label}");
                        if (behavior == "fail") throw new InvalidOperationException(label);
                        var result = behavior == "next" ? $"{label}({await next()})" : value;
                        _trace.Enqueue($"exit:{label}");
                        return result;
                    }), priority);
                    break;
                default: throw new InvalidOperationException("不支持的 TCK 事件。");
            }
        }

        internal async Task Dispatch(IPluginContext context, JsonElement step)
        {
            var name = step.GetProperty("event").GetString()!;
            if (_many.TryGetValue(name, out var many))
            {
                var run = async () => await context.DispatchAsync(many, "payload", TestContext.Current.CancellationToken);
                if (step.TryGetProperty("error", out var error))
                {
                    if (error.GetString() == "parallel")
                    {
                        var exception = await Assert.ThrowsAsync<ParallelDispatchException<string?>>(run);
                        Assert.Equal(step.GetProperty("partial").EnumerateArray().Select(v => v.GetString()), exception.Partial);
                    }
                    else await Assert.ThrowsAsync<InvalidOperationException>(run);
                }
                else Assert.Equal(step.GetProperty("value").EnumerateArray().Select(v => v.GetString()), await run());
                return;
            }
            var evt = _single[name];
            var result = evt.Mode == DispatchMode.Waterfall
                ? await context.DispatchAsync(evt, "payload", () => ValueTask.FromResult<string?>("terminal"), TestContext.Current.CancellationToken)
                : await context.DispatchAsync(evt, "payload", TestContext.Current.CancellationToken);
            Assert.Equal(step.TryGetProperty("value", out var expected) ? expected.GetString() : null, result);
        }
    }
}

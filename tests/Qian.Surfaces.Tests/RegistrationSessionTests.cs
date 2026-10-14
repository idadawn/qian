using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces.Tests;

public sealed class RegistrationSessionTests
{
    private const string Plugin = "test.session";
    private const string First = "当用户需要核对第一版租户资料时读取已授权内容并给出完整结果。";
    private const string Second = "当用户需要核对第二版租户资料时按更新后的规则读取内容并给出完整结果。";
    private static readonly string[] Scopes = ["global", "tenant"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 并发读取方在反复替换期间只看到旧文档或新文档_从不缺失()
    {
        var before = Snapshot(("keep", First), ("drop", First));
        var after = Snapshot(("keep", Second), ("add", Second));
        await using var host = await Host.Start();
        using var session = new SurfaceRegistrationSession(host.Context, before);
        var keep = Plugin + "/keep";
        var observed = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        long misses = 0, reads = 0;
        using var stop = new CancellationTokenSource();
        using var started = new CountdownEvent(4);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            started.Signal();
            while (!stop.IsCancellationRequested)
            {
                foreach (var key in new[] { SurfaceKeys.Skills, SurfaceKeys.Tools })
                {
                    // 新旧快照都有 keep：任何一次读取都必须拿到旧内容或新内容，不能出现 NOT_FOUND 或空集合。
                    try { observed.TryAdd(host.Context.Inject<SurfaceDocument>(key, keep).Data.GetProperty("description").GetString()!, 0); }
                    catch (QianErrorException error) when (error.Error.Code == PlatformErrors.NotFound) { Interlocked.Increment(ref misses); }
                    if (host.Context.TryInject<SurfaceDocument>(new ServiceKey(key.Name + ":" + keep)) is null) Interlocked.Increment(ref misses);
                    if (host.Context.List(key).Count(item => item.Name == keep) != 1) Interlocked.Increment(ref misses);
                    Interlocked.Increment(ref reads);
                }
            }
        }, Ct)).ToArray();
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), Ct));
        var deadline = Stopwatch.StartNew();
        var replacements = 0;
        // 至少替换 2000 次，并且读取方确实观察到两版内容后才停止，避免读取线程没有与替换重叠时空转通过。
        while (replacements < 2000 || (observed.Count < 2 && deadline.Elapsed < TimeSpan.FromSeconds(10)))
            Assert.NotEqual(0, session.Replace(++replacements % 2 == 1 ? after : before));
        await stop.CancelAsync();
        await Task.WhenAll(readers);
        Assert.True(Interlocked.Read(ref misses) == 0, $"{replacements} 次替换、{Interlocked.Read(ref reads)} 次读取中出现 {Interlocked.Read(ref misses)} 次缺失。");
        Assert.Equal([First, Second], observed.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task 新增项冲突时回滚_旧注册与旧内容保持不变()
    {
        var before = Snapshot(("keep", First), ("drop", First));
        var after = Snapshot(("keep", Second), ("add", Second));
        await using var host = await Host.Start();
        using var session = new SurfaceRegistrationSession(host.Context, before);
        var original = host.Context.Inject<SurfaceDocument>(SurfaceKeys.Skills, Plugin + "/keep");
        // 新快照要新增的名称已被同 scope 的其它注册占用；独占注册（R38）让本次替换整体失败。
        using var occupied = host.Context.Provide(SurfaceKeys.Tools, Plugin + "/add", new object());
        var error = Assert.Throws<QianErrorException>(() => session.Replace(after));
        Assert.Equal(PlatformErrors.RegistrationConflict, error.Error.Code);
        Assert.Same(original, host.Context.Inject<SurfaceDocument>(SurfaceKeys.Skills, Plugin + "/keep"));
        Assert.Equal(First, host.Context.Inject<SurfaceDocument>(SurfaceKeys.Tools, Plugin + "/keep").Data.GetProperty("description").GetString());
        Assert.NotNull(host.Context.Inject<SurfaceDocument>(SurfaceKeys.Skills, Plugin + "/drop"));
        Assert.Null(host.Context.TryInject<SurfaceDocument>(new ServiceKey(SurfaceKeys.Skills.Name + ":" + Plugin + "/add")));
        Assert.Equal([Plugin + "/drop", Plugin + "/keep"], host.Context.List(SurfaceKeys.Skills).Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        occupied.Dispose();
        Assert.NotEqual(0, session.Replace(after));
        Assert.Equal(Second, host.Context.Inject<SurfaceDocument>(SurfaceKeys.Skills, Plugin + "/keep").Data.GetProperty("description").GetString());
        Assert.Equal([Plugin + "/add", Plugin + "/keep"], host.Context.List(SurfaceKeys.Tools).Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task 每一步注册变更之后_新旧快照都有的文档仍可解析()
    {
        var before = Snapshot(("keep", First), ("drop", First));
        var after = Snapshot(("keep", Second), ("add", Second));
        await using var host = await Host.Start();
        var observed = new ObservedContext(host.Context);
        using var session = new SurfaceRegistrationSession(observed, before);
        List<string> gaps = [];
        var steps = 0;
        // 不依赖线程调度：会话每完成一次登记、替换或撤销，立即检查 keep 在两个目录入口是否仍可解析。
        observed.Changed = operation =>
        {
            steps++;
            foreach (var key in new[] { SurfaceKeys.Skills, SurfaceKeys.Tools })
                if (host.Context.TryInject<SurfaceDocument>(new ServiceKey(key.Name + ":" + Plugin + "/keep")) is null) gaps.Add(operation + " 之后 " + key.Name + " 缺失");
        };
        session.Replace(after); session.Replace(before);
        Assert.Empty(gaps);
        Assert.Equal(12, steps); // 两次替换各有两项登记、两项原位替换与两项撤销，回调确实覆盖了每一步。
        Assert.Equal(First, host.Context.Inject<SurfaceDocument>(SurfaceKeys.Tools, Plugin + "/keep").Data.GetProperty("description").GetString());
    }

    [Fact]
    public async Task 原位替换中途失败时恢复已替换项_会话仍可继续替换()
    {
        await using var host = await Host.Start();
        var observed = new ObservedContext(host.Context);
        using var session = new SurfaceRegistrationSession(observed, Snapshot(("alpha", First), ("beta", First)));
        var next = Snapshot(("alpha", Second), ("beta", Second));
        observed.FailReplace = 3; // alpha 的 Skills 与 Tools 已替换，beta 的第一项替换失败。
        Assert.Equal("注入的替换失败。", Assert.Throws<InvalidOperationException>(() => session.Replace(next)).Message);
        foreach (var name in new[] { "alpha", "beta" })
            foreach (var key in new[] { SurfaceKeys.Skills, SurfaceKeys.Tools })
                Assert.Equal(First, host.Context.Inject<SurfaceDocument>(key, Plugin + "/" + name).Data.GetProperty("description").GetString());
        observed.FailReplace = 0;
        Assert.Equal(4, session.Replace(next));
        Assert.Equal(Second, host.Context.Inject<SurfaceDocument>(SurfaceKeys.Tools, Plugin + "/beta").Data.GetProperty("description").GetString());
        Assert.Equal([Plugin + "/alpha", Plugin + "/beta"], host.Context.List(SurfaceKeys.Skills).Select(item => item.Name).ToArray());
    }

    private static SurfaceValidationResult Snapshot(params (string Name, string Description)[] skills)
    {
        var directory = Directory.CreateTempSubdirectory("qian-session-");
        try
        {
            Write(".qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = Plugin, version = "1.0.0", description = "声明式表面替换的并发读取验收组件。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = "表面替换", category = "testing" }, inject = Array.Empty<string>(), provides = Array.Empty<string>(),
                policy = new { tier = "private", sandbox = "none", authentication = "ON_INSTALL" }, scopes = Scopes
            }));
            foreach (var (name, description) in skills) Write("skills/" + name + "/SKILL.md", "---\nname: " + name + "\ndescription: " + description + "\n---\n只读取当前 scope 的已授权内容。\n");
            var result = new SurfaceValidator().ValidateComponent(directory.FullName, new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server" });
            Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics));
            return result;
        }
        finally { directory.Delete(true); }
        void Write(string path, string contents) { var file = Path.Combine(directory.FullName, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, contents); }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
        private PluginLoader Runtime { get; }
        internal IPluginContext Context => Runtime.GetContext("surfaces", ScopeKey.Global);
        private Host() => Runtime = new PluginLoader(_services);
        internal static async Task<Host> Start()
        {
            var host = new Host();
            await host.Runtime.MountAsync([new PluginDefinition(new PluginEntry("surfaces", Plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new EmptyPlugin(), [])], Ct);
            Assert.Equal(PluginState.Ready, host.Runtime.GetState("surfaces", ScopeKey.Global));
            return host;
        }
        public async ValueTask DisposeAsync() { await Runtime.DisposeAsync(); await _services.DisposeAsync(); }
    }

    private sealed class EmptyPlugin : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }

    // 转发到真实上下文；在每次登记、替换与撤销完成后回调，并可让第 n 次替换失败。
    private sealed class ObservedContext(IPluginContext inner) : IPluginContext
    {
        private int _replacements;
        internal Action<string> Changed { get; set; } = _ => { };
        internal int FailReplace { get; set; }
        public PluginEntry Entry => inner.Entry;
        public ScopeKey Scope => inner.Scope;
        public IServiceProvider Services => inner.Services;
        public ILogger Logger => inner.Logger;
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, TService instance) where TService : class => Observe(key.Name, inner.Provide(key, instance));
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, string name, TService instance) where TService : class => Observe(key.Name + ":" + name, inner.Provide(key, name, instance));
        public TService Inject<TService>(ServiceKey key) where TService : class => inner.Inject<TService>(key);
        public TService Inject<TService>(ServiceKey key, string name) where TService : class => inner.Inject<TService>(key, name);
        public TService? TryInject<TService>(ServiceKey key) where TService : class => inner.TryInject<TService>(key);
        public ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class => inner.WhenReadyAsync<TService>(key, ct);
        public void Effect(IDisposable disposer) => inner.Effect(disposer);
        public void Effect(Action dispose) => inner.Effect(dispose);
        public void Effect(Func<ValueTask> disposeAsync) => inner.Effect(disposeAsync);
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority) => inner.On(evt, mode, handler, priority);
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler, int priority) => inner.On(evt, mode, handler, priority);
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<IReadOnlyList<TResult>> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, Func<ValueTask<TResult>> terminal, CancellationToken ct) => inner.DispatchAsync(evt, payload, terminal, ct);
        public IPluginContext Scoped(ScopeKey scope) => inner.Scoped(scope);
        public IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep) => inner.Restrict(scope, keep);
        public IReadOnlyList<Registration> List(ServiceKey key) => inner.List(key);
        private Handle<TService> Observe<TService>(string key, IRegistrationHandle<TService> handle) where TService : class
        {
            Changed("登记 " + key);
            return new Handle<TService>(this, key, handle);
        }
        private sealed class Handle<TService>(ObservedContext owner, string key, IRegistrationHandle<TService> inner) : IRegistrationHandle<TService> where TService : class
        {
            public void Replace(TService instance)
            {
                if (++owner._replacements == owner.FailReplace) throw new InvalidOperationException("注入的替换失败。");
                inner.Replace(instance); owner.Changed("替换 " + key);
            }
            public void Dispose() { inner.Dispose(); owner.Changed("撤销 " + key); }
        }
    }
}

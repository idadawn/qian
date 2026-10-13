using System.Security.Cryptography;
using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

/// <summary>开发态表面监视器的状态；不包含文件内容、配置或密钥。</summary>
public sealed record SurfaceReloadStatus(long Revision, bool RestartRequired, IReadOnlyList<string> ChangedCodeFiles, IReadOnlyList<SurfaceDiagnostic> Diagnostics);

/// <summary>在同一次代码 Apply 旁登记和刷新声明式表面，代码变化只报告需要重启。</summary>
public sealed class HotReloadSurfacePlugin : IQianPlugin
{
    private readonly IQianPlugin? _code;
    private readonly string _directory;
    private readonly ISurfaceValidator _validator;
    private readonly SurfaceValidationOptions _options;
    private readonly SurfaceValidationResult _initial;
    private readonly Lock _gate = new();
    private SurfaceReloadStatus _status = new(0, false, [], []);
    /// <summary>目录先经过共享校验；code 为 null 时是无代码组件。</summary>
    public HotReloadSurfacePlugin(string directory, ISurfaceValidator validator, SurfaceValidationOptions options, IQianPlugin? code = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(validator); ArgumentNullException.ThrowIfNull(options);
        _directory = Path.GetFullPath(directory); _validator = validator; _options = options; _code = code;
        _initial = validator.ValidateComponent(_directory, options);
        if (!_initial.IsValid) throw Invalid();
        Inject = code?.Inject ?? _initial.Component!.Manifest.GetProperty("inject").EnumerateArray().Select(item => new ServiceKey(item.GetString()!)).ToArray();
        SurfaceProvides = SurfaceRegistrationSession.Plan(_initial).Keys.Select(item => new ServiceKey(item.Key.Name + ":" + item.Name)).ToArray();
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject { get; }
    /// <summary>初始表面提供的具名项；代码的 provides 由宿主另行声明。</summary>
    public IReadOnlyList<ServiceKey> SurfaceProvides { get; }
    /// <summary>当前状态的不可变快照。</summary>
    public SurfaceReloadStatus Status { get { lock (_gate) return _status; } }
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        var before = Snapshot(_directory);
        var current = _validator.ValidateComponent(_directory, _options);
        if (!current.IsValid || !JsonElement.DeepEquals(current.Component!.Manifest, _initial.Component!.Manifest)) throw Invalid();
        if (_code is not null) await _code.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
        var session = new SurfaceRegistrationSession(ctx, current);
        ctx.Effect(session.Dispose);
        var lifetime = new WatchLifetime(_directory, () => Refresh(session, current, before));
        ctx.Effect(lifetime.DisposeAsync);
        lifetime.Start();
    }
    private void Refresh(SurfaceRegistrationSession session, SurfaceValidationResult initial, Dictionary<string, string> previous)
    {
        var before = Snapshot(_directory);
        if (Same(previous, before)) return;
        var result = _validator.ValidateComponent(_directory, _options);
        var after = Snapshot(_directory);
        if (!Same(before, after)) return; // 原子保存或批量写入尚未稳定，下一次检查再读取。
        var changed = previous.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).Where(path => previous.GetValueOrDefault(path) != after.GetValueOrDefault(path)).ToArray();
        var code = changed.Where(path => path.StartsWith("src/", StringComparison.Ordinal) || path.EndsWith(".dll", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal)).ToList();
        if (result.Component is not null && !JsonElement.DeepEquals(result.Component.Manifest, initial.Component!.Manifest)) code.Add(".qian-plugin/plugin.json");
        lock (_gate)
        {
            var restart = _status.ChangedCodeFiles.Concat(code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            _status = new SurfaceReloadStatus(_status.Revision, restart.Length != 0, Array.AsReadOnly(restart), result.Diagnostics);
            if (!result.IsValid || code.Contains(".qian-plugin/plugin.json", StringComparer.Ordinal)) return;
        }
        var safe = result.Component!;
        if (safe.Manifest.TryGetProperty("x-qian-generated", out var generated) && generated.ValueKind == JsonValueKind.Object && generated.TryGetProperty("tools", out var generatedTools))
        {
            var files = generatedTools.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
            var originals = initial.Component!.Documents.Where(item => item.Kind == "tool" && files.Contains(item.File)).ToArray();
            if (safe.Documents.Any(item => item.Kind == "tool" && files.Contains(item.File) && !originals.Any(old => old.File == item.File && JsonElement.DeepEquals(old.Data, item.Data))) ||
                originals.Any(old => !safe.Documents.Any(item => item.File == old.File && JsonElement.DeepEquals(old.Data, item.Data))))
                lock (_gate) _status = _status with { RestartRequired = true, ChangedCodeFiles = Array.AsReadOnly(_status.ChangedCodeFiles.Append("tools (generated)").Distinct(StringComparer.Ordinal).ToArray()) };
            safe = safe with { Documents = safe.Documents.Where(item => item.Kind != "tool" || !files.Contains(item.File)).Concat(originals).ToArray() };
        }
        var count = session.Replace(result with { Component = safe });
        previous.Clear(); foreach (var entry in after) previous.Add(entry.Key, entry.Value);
        if (count != 0) lock (_gate) _status = _status with { Revision = _status.Revision + 1 };
    }
    private static bool Same(Dictionary<string, string> first, Dictionary<string, string> second) => first.Count == second.Count && first.All(pair => second.GetValueOrDefault(pair.Key) == pair.Value);
    private static Dictionary<string, string> Snapshot(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal); long total = 0;
        Walk(new DirectoryInfo(root)); return files;
        void Walk(DirectoryInfo directory)
        {
            if (directory.LinkTarget is not null) throw Invalid();
            foreach (var entry in directory.EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (entry.Name is ".git" or "bin" or "obj" or "node_modules" or ".cache") continue;
                if (entry.LinkTarget is not null) throw Invalid();
                if (entry is DirectoryInfo child) Walk(child);
                else if (entry is FileInfo file)
                {
                    total += file.Length;
                    if (files.Count >= 10000 || total > 64 * 1024 * 1024) throw Invalid();
                    using var input = file.OpenRead(); files.Add(Path.GetRelativePath(root, file.FullName).Replace('\\', '/'), Convert.ToHexStringLower(SHA256.HashData(input)));
                }
            }
        }
    }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
    private sealed class WatchLifetime(string directory, Action refresh) : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly FileSystemWatcher _watcher = new(directory) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName };
        private Task? _worker;
        private int _dirty = 1;
        internal void Start()
        {
            _watcher.Changed += Changed; _watcher.Created += Changed; _watcher.Deleted += Changed; _watcher.Renamed += Changed; _watcher.Error += (_, _) => Interlocked.Exchange(ref _dirty, 1);
            _watcher.EnableRaisingEvents = true;
            _worker = Run();
        }
        private void Changed(object sender, FileSystemEventArgs args) => Interlocked.Exchange(ref _dirty, 1);
        private async Task Run()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200)); var ticks = 0;
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
                {
                    if (Interlocked.Exchange(ref _dirty, 0) == 0 && ++ticks < 10) continue;
                    ticks = 0;
                    try { refresh(); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or QianErrorException or InvalidOperationException)
                    { Interlocked.Exchange(ref _dirty, 1); } // 正在写入或无效输入不替换旧注册，也不回显文件内容。
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel(); _watcher.Dispose();
            if (_worker is not null) await _worker.ConfigureAwait(false);
            _stop.Dispose();
        }
    }
}

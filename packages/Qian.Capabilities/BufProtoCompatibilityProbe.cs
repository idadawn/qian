using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>通过固定版本 Buf 进行离线编译与 FILE 级兼容检查，不执行任何 protoc 插件或制品配置。</summary>
public sealed partial class BufProtoCompatibilityProbe : IProtoCompatibilityProbe
{
    private const string Config = "{\"version\":\"v2\",\"modules\":[{\"path\":\".\"}],\"breaking\":{\"use\":[\"FILE\"]}}";
    private const int DiagnosticLines = 50, DiagnosticLineLength = 500;
    private readonly string _executable;
    private readonly ILogger? _logger;
    /// <summary>使用宿主预装且通过锁文件核验的绝对路径；logger 记录检查器输出中不是规则报告的行。</summary>
    public BufProtoCompatibilityProbe(string executablePath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath)) throw new ArgumentException("必须提供已安装检查器的绝对路径。", nameof(executablePath));
        _executable = executablePath; _logger = logger;
    }
    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> CompileAsync(ProtoContractSet source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); cancellationToken.ThrowIfCancellationRequested();
        var directory = Directory.CreateTempSubdirectory("qian-proto-compile-");
        try { await Version(directory.FullName, cancellationToken).ConfigureAwait(false); return await Compile(source, directory.FullName, cancellationToken).ConfigureAwait(false); }
        finally { directory.Delete(true); }
    }
    /// <inheritdoc />
    public async ValueTask<ContractCompatibility> CompareAsync(ProtoContractSet baseline, ProtoContractSet candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline); ArgumentNullException.ThrowIfNull(candidate); cancellationToken.ThrowIfCancellationRequested();
        var directory = Directory.CreateTempSubdirectory("qian-proto-compare-");
        try
        {
            await Version(directory.FullName, cancellationToken).ConfigureAwait(false);
            var before = await Compile(baseline, Directory.CreateDirectory(Path.Combine(directory.FullName, "base")).FullName, cancellationToken).ConfigureAwait(false);
            var after = await Compile(candidate, Directory.CreateDirectory(Path.Combine(directory.FullName, "candidate")).FullName, cancellationToken).ConfigureAwait(false);
            return await CompareDescriptorsAsync(before, after, cancellationToken).ConfigureAwait(false);
        }
        finally { directory.Delete(true); }
    }
    /// <inheritdoc />
    public ValueTask<ContractCompatibility> CompareDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default)
        => CompareDescriptors(baseline, candidate, false, cancellationToken);
    /// <inheritdoc />
    public ValueTask<ContractCompatibility> CompareRuntimeDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default)
        => CompareDescriptors(baseline, candidate, true, cancellationToken);
    private async ValueTask<ContractCompatibility> CompareDescriptors(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, bool runtime, CancellationToken cancellationToken)
    {
        if (baseline.Length is 0 or > 64 * 1024 * 1024 || candidate.Length is 0 or > 64 * 1024 * 1024) throw new ArgumentException("协议描述大小无效。");
        var directory = Directory.CreateTempSubdirectory("qian-proto-descriptors-");
        try
        {
            await Version(directory.FullName, cancellationToken).ConfigureAwait(false);
            var beforePath = Path.Combine(directory.FullName, "base.bin"); var afterPath = Path.Combine(directory.FullName, "candidate.bin");
            await File.WriteAllBytesAsync(beforePath, baseline.ToArray(), cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(afterPath, candidate.ToArray(), cancellationToken).ConfigureAwait(false);
            var config = Path.Combine(directory.FullName, "buf.yaml"); await File.WriteAllTextAsync(config, runtime ? Config.Replace("FILE", "PACKAGE", StringComparison.Ordinal) : Config, cancellationToken).ConfigureAwait(false);
            var result = await ContractToolProcess.RunAsync(_executable, directory.FullName,
                ["breaking", afterPath, "--against", beforePath, "--config", config, "--error-format=json", "--disable-symlinks"], cancellationToken).ConfigureAwait(false);
            var report = ParseBreaking(result.Output, result.Error);
            if (_logger is not null)
            {
                if (report.Noise.Count != 0) Log.ToolOutput(_logger, result.Exit, report.Noise.Count, string.Join("\n", report.Noise));
                if (report.Errors.Count != 0) Log.ToolError(_logger, result.Exit, report.Errors.Count, string.Join("\n", report.Errors));
            }
            if (result.Exit == 0) return new ContractCompatibility(true, []);
            if (result.Exit != 100) throw Failure("proto 兼容检查器执行失败。", report);
            // 退出码 100 表示存在破坏性变更；没有任何合法规则行时不能当作兼容，也不能凭空给出结论。
            if (report.Rules.Count == 0) throw Failure("proto 检查器缺少破坏性规则。", report);
            return new ContractCompatibility(false, report.Rules);
        }
        finally { directory.Delete(true); }
    }
    // 规则只从标准输出取：形如 {"type":"<规则 ID>",...} 的合法 JSON 行。标准输出的其余行（警告、进度、无效 JSON）与标准错误分开截断保留，
    // 标准错误不参与规则解析，也不并入标准输出的日志；都不抛异常。
    internal static BreakingReport ParseBreaking(string output, string error)
    {
        var rules = new SortedSet<string>(StringComparer.Ordinal); var noise = new List<string>(); var errors = new List<string>();
        foreach (var line in Lines(output))
        {
            if (Rule(line) is { } rule) rules.Add(rule);
            else Keep(noise, line);
        }
        foreach (var line in Lines(error)) Keep(errors, line);
        return new BreakingReport(Array.AsReadOnly(rules.ToArray()), noise.AsReadOnly(), errors.AsReadOnly());
        static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        static void Keep(List<string> lines, string line)
        {
            if (lines.Count < DiagnosticLines) lines.Add(line.Length > DiagnosticLineLength ? line[..DiagnosticLineLength] : line);
        }
        static string? Rule(string line)
        {
            if (!line.StartsWith('{')) return null;
            try
            {
                using var report = JsonDocument.Parse(line);
                return report.RootElement.ValueKind == JsonValueKind.Object && report.RootElement.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                    type.GetString() is { Length: > 0 and <= 200 } value && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') ? value : null;
            }
            catch (JsonException) { return null; }
        }
    }
    // 失败时两路输出分别附在异常上：标准输出里不是规则的行、标准错误。
    private static InvalidOperationException Failure(string message, BreakingReport report)
    {
        var error = new InvalidOperationException(message);
        error.Data["contractDiagnostics"] = string.Join("\n", report.Noise);
        error.Data["contractErrors"] = string.Join("\n", report.Errors);
        return error;
    }
    internal sealed record BreakingReport(IReadOnlyList<string> Rules, IReadOnlyList<string> Noise, IReadOnlyList<string> Errors);
    private async Task Version(string directory, CancellationToken ct)
    {
        var version = await ContractToolProcess.RunAsync(_executable, directory, ["--version"], ct).ConfigureAwait(false);
        if (version.Exit != 0 || version.Output.Trim() != "1.73.0") throw new InvalidOperationException("proto 检查器版本与锁定版本不一致。");
    }
    private async Task<byte[]> Compile(ProtoContractSet source, string directory, CancellationToken ct)
    {
        var module = Directory.CreateDirectory(Path.Combine(directory, "module"));
        foreach (var path in source.Paths)
        {
            ct.ThrowIfCancellationRequested(); var file = Path.Combine(module.FullName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, source.ReadFile(path), ct).ConfigureAwait(false);
        }
        var config = Path.Combine(module.FullName, "buf.yaml"); await File.WriteAllTextAsync(config, Config, ct).ConfigureAwait(false);
        var output = Path.Combine(directory, "descriptor.bin");
        var result = await ContractToolProcess.RunAsync(_executable, module.FullName,
            ["build", ".", "--config", config, "--as-file-descriptor-set", "--exclude-source-info", "--disable-symlinks", "--output", output], ct).ConfigureAwait(false);
        if (result.Exit != 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var info = new FileInfo(output);
        if (!info.Exists || info.Length is 0 or > 64 * 1024 * 1024) throw new InvalidOperationException("proto 编译结果大小无效。");
        return await File.ReadAllBytesAsync(output, ct).ConfigureAwait(false);
    }
    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "proto 检查器（退出码 {Exit}）的标准输出有 {Count} 行不是规则报告：\n{Lines}")]
        public static partial void ToolOutput(ILogger logger, int exit, int count, string lines);
        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "proto 检查器（退出码 {Exit}）的标准错误输出了 {Count} 行：\n{Lines}")]
        public static partial void ToolError(ILogger logger, int exit, int count, string lines);
    }
}

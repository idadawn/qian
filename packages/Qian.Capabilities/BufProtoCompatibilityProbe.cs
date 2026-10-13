using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>通过固定版本 Buf 进行离线编译与 FILE 级兼容检查，不执行任何 protoc 插件或制品配置。</summary>
public sealed class BufProtoCompatibilityProbe : IProtoCompatibilityProbe
{
    private const string Config = "{\"version\":\"v2\",\"modules\":[{\"path\":\".\"}],\"breaking\":{\"use\":[\"FILE\"]}}";
    private readonly string _executable;
    /// <summary>使用宿主预装且通过锁文件核验的绝对路径。</summary>
    public BufProtoCompatibilityProbe(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath)) throw new ArgumentException("必须提供已安装检查器的绝对路径。", nameof(executablePath));
        _executable = executablePath;
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
            if (result.Exit == 0) return new ContractCompatibility(true, []);
            if (result.Exit != 100) throw new InvalidOperationException("proto 兼容检查器执行失败。");
            var rules = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var line in (result.Output + "\n" + result.Error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                using var report = JsonDocument.Parse(line);
                var type = report.RootElement.GetProperty("type").GetString();
                if (string.IsNullOrWhiteSpace(type) || type.Length > 200 || type.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_')) throw new InvalidOperationException("proto 检查器返回无效诊断。");
                rules.Add(type);
            }
            if (rules.Count == 0) throw new InvalidOperationException("proto 检查器缺少破坏性规则。");
            return new ContractCompatibility(false, Array.AsReadOnly(rules.ToArray()));
        }
        finally { directory.Delete(true); }
    }
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
}

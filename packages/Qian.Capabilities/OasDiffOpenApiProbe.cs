using System.Text;
using System.Text.Json;
using Microsoft.OpenApi;

namespace Qian.Capabilities;

/// <summary>调用宿主预装的锁定版本检查器；不下载程序、不联网解析引用、不执行 shell。</summary>
public sealed class OasDiffOpenApiProbe : IOpenApiCompatibilityProbe
{
    private readonly string _executable;
    /// <summary>executablePath 必须是宿主配置的绝对路径，不能来自请求或组件输入。</summary>
    public OasDiffOpenApiProbe(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath)) throw new ArgumentException("契约检查器必须预装并使用绝对路径。", nameof(executablePath));
        _executable = executablePath;
    }
    /// <inheritdoc />
    public async ValueTask<ContractCompatibility> CompareAsync(JsonElement baseline, JsonElement candidate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = Validate(baseline); var after = Validate(candidate);
        var directory = Directory.CreateTempSubdirectory("qian-openapi-check-");
        try
        {
            var version = await Run(directory.FullName, ["--version"], cancellationToken).ConfigureAwait(false);
            if (version.Exit != 0 || version.Output.Trim() != "oasdiff version 1.33.0") throw new InvalidOperationException("契约检查器版本不符合锁定版本。");
            var basePath = Path.Combine(directory.FullName, "base.json"); var candidatePath = Path.Combine(directory.FullName, "candidate.json");
            var config = Path.Combine(directory.FullName, "empty.yaml");
            await File.WriteAllTextAsync(basePath, before, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(candidatePath, after, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(config, "{}", cancellationToken).ConfigureAwait(false);
            var checkedResult = await Run(directory.FullName, ["breaking", basePath, candidatePath, "--format", "json", "--fail-on", "WARN", "--allow-external-refs=false", "--config", config], cancellationToken).ConfigureAwait(false);
            if (checkedResult.Exit is not (0 or 1)) throw new InvalidOperationException("契约检查器执行失败。");
            using var report = JsonDocument.Parse(checkedResult.Output);
            if (report.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("契约检查器返回了无效结果。");
            var rules = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in report.RootElement.EnumerateArray())
            {
                var rule = item.GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(rule) || rule.Length > 200 || rule.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-'))
                    throw new InvalidOperationException("契约检查器返回了无效规则。");
                rules.Add(rule);
            }
            if ((checkedResult.Exit == 0) != (rules.Count == 0)) throw new InvalidOperationException("契约检查器结果与退出状态不一致。");
            return new ContractCompatibility(rules.Count == 0, Array.AsReadOnly(rules.ToArray()));
        }
        finally { directory.Delete(true); }
    }
    private static string Validate(JsonElement document)
    {
        var text = document.GetRawText();
        if (Encoding.UTF8.GetByteCount(text) > 1_048_576 || document.ValueKind != JsonValueKind.Object ||
            !document.TryGetProperty("openapi", out var version) || version.ValueKind != JsonValueKind.String || !version.GetString()!.StartsWith("3.1.", StringComparison.Ordinal))
            throw new ArgumentException("契约必须是大小受限的 OpenAPI 3.1 文档。");
        var parsed = OpenApiDocument.Parse(text, "json", new Microsoft.OpenApi.Reader.OpenApiReaderSettings { LoadExternalRefs = false });
        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0) throw new ArgumentException("OpenAPI 契约无效。");
        return text;
    }
    private async Task<(int Exit, string Output)> Run(string directory, string[] arguments, CancellationToken ct)
    {
        var result = await ContractToolProcess.RunAsync(_executable, directory, arguments, ct).ConfigureAwait(false);
        return (result.Exit, result.Output);
    }
}

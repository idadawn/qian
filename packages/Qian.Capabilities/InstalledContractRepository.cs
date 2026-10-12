using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Qian.Registry;
using Qian.Surfaces.Abstractions;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

// 只消费 Registry 实际预装集合的文件快照，不读工作目录，不接受 Submitted 候选作为运行资格。
internal sealed partial class InstalledContractRepository : IContractRepository, IDisposable
{
    private readonly Dictionary<(string Plugin, string Version), IReadOnlyList<ContractDocument>> _documents;
    private readonly IOpenApiCompatibilityProbe? _probe;
    private readonly ISurfaceReader _reader;
    private readonly IProtoCompatibilityProbe? _protoProbe;
    private readonly Dictionary<(string Plugin, string Version), ProtoContractSet> _protoSources = [];
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;
    internal InstalledContractRepository(IEnumerable<InstalledComponentMetadata> installed, IOpenApiCompatibilityProbe? probe, ISurfaceReader reader, IProtoCompatibilityProbe? protoProbe = null)
    {
        _probe = probe; _reader = reader; _protoProbe = protoProbe;
        var versions = new Dictionary<(string, string), IReadOnlyList<ContractDocument>>();
        foreach (var component in installed)
        {
            var documents = new List<ContractDocument>();
            var declarations = component.Metadata.Where(item => System.IO.Path.GetFileName(item.Key) == "contracts.json").ToArray();
            if (declarations.Length > 1) throw Invalid();
            foreach (var declaration in declarations)
            {
                if (component.ArtifactDigest is null || declaration.Value.ValueKind != JsonValueKind.Array) throw Invalid();
                foreach (var entry in declaration.Value.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 3 ||
                        !entry.TryGetProperty("id", out var idValue) || !entry.TryGetProperty("kind", out var kindValue) || !entry.TryGetProperty("path", out var pathValue) ||
                        idValue.ValueKind != JsonValueKind.String || kindValue.ValueKind != JsonValueKind.String || pathValue.ValueKind != JsonValueKind.String) throw Invalid();
                    var id = idValue.GetString()!; var kind = kindValue.GetString()!; var path = pathValue.GetString()!;
                    if (id.Length > 300 || !ContractId().IsMatch(id) || (!id.StartsWith(component.PluginId + "/", StringComparison.Ordinal) && !id.StartsWith(component.PluginId + ".", StringComparison.Ordinal)) ||
                        kind is not ("openapi" or "proto" or "json-schema") || !path.StartsWith("./", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal) ||
                        path[2..].Split('/').Any(part => part is "" or "." or "..") || documents.Any(document => document.Id == id)) throw Invalid();
                    var bytes = component.ReadFile(path[2..]);
                    if (bytes is null || bytes.Length is 0 or > 1_048_576) throw Invalid();
                    Validate(kind, path, bytes, reader);
                    documents.Add(new ContractDocument(component.PluginId, component.Version, id, kind, path[2..], component.ArtifactDigest, bytes));
                }
            }
            if (documents.Any(document => document.Kind == "proto"))
                _protoSources.Add((component.PluginId, component.Version), new ProtoContractSet(component.FilePaths.Where(path => path.EndsWith(".proto", StringComparison.Ordinal))
                    .ToDictionary(path => path, path => new ReadOnlyMemory<byte>(component.ReadFile(path)!), StringComparer.Ordinal)));
            if (!versions.TryAdd((component.PluginId, component.Version), documents.AsReadOnly())) throw Invalid();
        }
        _documents = versions;
    }
    public IReadOnlyList<ContractDocument> GetContracts(string pluginId, string version)
    {
        EnsureActive(); return _documents.TryGetValue((pluginId, version), out var documents) ? documents : [];
    }
    public ContractDocument? Find(string pluginId, string version, string contractId) => GetContracts(pluginId, version).SingleOrDefault(document => document.Id == contractId);
    public ProtoContractSet GetProtoSource(string pluginId, string version, string contractId)
    {
        EnsureActive();
        if (Find(pluginId, version, contractId) is not { Kind: "proto" } || !_protoSources.TryGetValue((pluginId, version), out var source))
            throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return source;
    }
    internal async ValueTask ValidateProtoAsync(CancellationToken ct)
    {
        EnsureActive();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        foreach (var source in _protoSources.Values)
            await RequireProto().CompileAsync(source, linked.Token).ConfigureAwait(false);
        EnsureActive(); linked.Token.ThrowIfCancellationRequested();
    }
    public async ValueTask<ContractCompatibility> CheckProtoAsync(string pluginId, string baselineVersion, string candidateVersion, string contractId, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var result = await RequireProto().CompareAsync(GetProtoSource(pluginId, baselineVersion, contractId), GetProtoSource(pluginId, candidateVersion, contractId), linked.Token).ConfigureAwait(false);
        EnsureActive(); linked.Token.ThrowIfCancellationRequested(); return result;
    }
    private IProtoCompatibilityProbe RequireProto() => _protoProbe ?? throw new InvalidOperationException("宿主尚未装配真实 proto 检查器。");
    public async ValueTask<ContractCompatibility> CheckOpenApiAsync(string pluginId, string baselineVersion, string candidateVersion, string contractId, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        linked.Token.ThrowIfCancellationRequested();
        var baseline = Find(pluginId, baselineVersion, contractId) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        var candidate = Find(pluginId, candidateVersion, contractId) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        if (baseline.Kind != "openapi" || candidate.Kind != "openapi") throw Invalid();
        if (_probe is null) throw new InvalidOperationException("宿主尚未装配真实 OpenAPI 契约检查器。");
        var before = Parse(baseline.Path, baseline.ReadContent(), _reader);
        var after = Parse(candidate.Path, candidate.ReadContent(), _reader);
        var result = await _probe.CompareAsync(before, after, linked.Token).ConfigureAwait(false);
        EnsureActive(); linked.Token.ThrowIfCancellationRequested(); return result;
    }
    private static void Validate(string kind, string path, byte[] bytes, ISurfaceReader reader)
    {
        var content = new UTF8Encoding(false, true).GetString(bytes);
        if (kind == "proto")
        {
            // 先读取原始源码；Provider 在公开仓库服务前还会执行真正的编译验证。
            if (!path.EndsWith(".proto", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(content)) throw Invalid();
            return;
        }
        var document = Parse(path, bytes, reader);
        if (kind == "openapi")
        {
            if (!document.TryGetProperty("openapi", out var version) || version.ValueKind != JsonValueKind.String || !version.GetString()!.StartsWith("3.1.", StringComparison.Ordinal)) throw Invalid();
            var parsed = OpenApiDocument.Parse(document.GetRawText(), "json", new Microsoft.OpenApi.Reader.OpenApiReaderSettings { LoadExternalRefs = false });
            if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0) throw Invalid();
        }
        else
        {
            var options = new BuildOptions { SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } };
            var meta = (JsonSchema)options.SchemaRegistry.Get(new Uri("https://json-schema.org/draft/2020-12/schema"))!;
            if (!meta.Evaluate(document).IsValid) throw Invalid();
        }
    }
    private static JsonElement Parse(string path, byte[] bytes, ISurfaceReader reader)
    {
        var result = reader.Parse(path, new UTF8Encoding(false, true).GetString(bytes));
        if (result.Syntax is null || result.Diagnostics.Any(item => item.Severity == "error")) throw Invalid();
        return result.Syntax.Data;
    }
    private void EnsureActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        { try { _lifetime.Source.Cancel(); } finally { _lifetime.Source.Dispose(); } }
    }
    private static (CancellationTokenSource, CancellationToken) CreateLifetime()
    { var source = new CancellationTokenSource(); return (source, source.Token); }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+/v[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ContractId();
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>平台装配器使用的普通插件包装：在业务 Apply 前执行真实契约兼容检查，不修改 Runtime。</summary>
public sealed partial class OpenApiContractPlugin : IQianPlugin
{
    private readonly IQianPlugin _inner;
    private readonly Func<CancellationToken, ValueTask<ContractCompatibility>> _check;
    private readonly string? _expectedPlugin;
    private readonly string? _expectedVersion;
    private readonly string _contractId;
    /// <summary>基线与候选契约由受信任装配器从版本化制品取得；不能以当前调用者过滤后的文档作为发布基线。</summary>
    public OpenApiContractPlugin(IQianPlugin inner, IOpenApiCompatibilityProbe probe, string contractId, JsonElement baseline, JsonElement candidate)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(probe);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!ContractIdPattern().IsMatch(contractId)) throw new ArgumentException("契约 ID 必须使用组件命名空间并带 /vN。", nameof(contractId));
        _inner = inner; _contractId = contractId;
        var before = baseline.Clone(); var after = candidate.Clone();
        _check = ct => probe.CompareAsync(before, after, ct);
    }
    private OpenApiContractPlugin(IQianPlugin inner, IContractRepository repository, string pluginId, string baselineVersion, string candidateVersion, string contractId)
    {
        _inner = inner; _contractId = contractId; _expectedPlugin = pluginId; _expectedVersion = candidateVersion;
        _check = ct => repository.CheckOpenApiAsync(pluginId, baselineVersion, candidateVersion, contractId, ct);
    }
    /// <summary>按完整制品的精确组件版本建立准入包装；缺失基线或候选时拒绝，不能使用用户过滤后的接口文档。</summary>
    public static OpenApiContractPlugin FromRepository(IQianPlugin inner, IContractRepository repository, string pluginId, string baselineVersion, string candidateVersion, string contractId)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId); ArgumentException.ThrowIfNullOrWhiteSpace(baselineVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateVersion); ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!ContractIdPattern().IsMatch(contractId) || repository.Find(pluginId, baselineVersion, contractId) is not { Kind: "openapi" } ||
            repository.Find(pluginId, candidateVersion, contractId) is not { Kind: "openapi" }) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return new OpenApiContractPlugin(inner, repository, pluginId, baselineVersion, candidateVersion, contractId);
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _inner.Inject;
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if ((_expectedPlugin is not null && ctx.Entry.Plugin != _expectedPlugin) || (_expectedVersion is not null && ctx.Entry.Version != _expectedVersion))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (!_contractId.StartsWith(ctx.Entry.Plugin + "/", StringComparison.Ordinal) && !_contractId.StartsWith(ctx.Entry.Plugin + ".", StringComparison.Ordinal))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var result = await _check(ct).ConfigureAwait(false);
        if (!result.IsCompatible) throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed)
            { Pointer = ErrorPointer.Input("contracts", _contractId) });
        ct.ThrowIfCancellationRequested();
        await _inner.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
    }
    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+/v[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ContractIdPattern();
}

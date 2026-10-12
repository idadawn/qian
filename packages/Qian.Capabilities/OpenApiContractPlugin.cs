using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>平台装配器使用的普通插件包装：在业务 Apply 前执行真实契约兼容检查，不修改 Runtime。</summary>
public sealed partial class OpenApiContractPlugin : IQianPlugin
{
    private readonly IQianPlugin _inner;
    private readonly IOpenApiCompatibilityProbe _probe;
    private readonly string _contractId;
    private readonly JsonElement _baseline;
    private readonly JsonElement _candidate;
    /// <summary>基线与候选契约由受信任装配器从版本化制品取得；不能以当前调用者过滤后的文档作为发布基线。</summary>
    public OpenApiContractPlugin(IQianPlugin inner, IOpenApiCompatibilityProbe probe, string contractId, JsonElement baseline, JsonElement candidate)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(probe);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!ContractIdPattern().IsMatch(contractId)) throw new ArgumentException("契约 ID 必须使用组件命名空间并带 /vN。", nameof(contractId));
        _inner = inner; _probe = probe; _contractId = contractId; _baseline = baseline.Clone(); _candidate = candidate.Clone();
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _inner.Inject;
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (!_contractId.StartsWith(ctx.Entry.Plugin + "/", StringComparison.Ordinal) && !_contractId.StartsWith(ctx.Entry.Plugin + ".", StringComparison.Ordinal))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var result = await _probe.CompareAsync(_baseline, _candidate, ct).ConfigureAwait(false);
        if (!result.IsCompatible) throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed)
            { Pointer = ErrorPointer.Input("contracts", _contractId) });
        ct.ThrowIfCancellationRequested();
        await _inner.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
    }
    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+/v[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ContractIdPattern();
}

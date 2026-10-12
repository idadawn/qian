using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>业务 Apply 前核对版本制品与实际 gRPC 反射端点，任何探测失败均不公开业务注册。</summary>
public sealed class GrpcContractPlugin : IQianPlugin
{
    private readonly IQianPlugin _inner;
    private readonly IContractRepository _repository;
    private readonly IGrpcContractProbe _peer;
    private readonly string _plugin;
    private readonly string _baseline;
    private readonly string _candidate;
    private readonly string _contract;
    /// <summary>精确版本取自已捕获制品，peer 的连接与凭据由宿主按出网和安装授权配置。</summary>
    public GrpcContractPlugin(IQianPlugin inner, IContractRepository repository, IGrpcContractProbe peer, string pluginId, string baselineVersion, string candidateVersion, string contractId)
    {
        ArgumentNullException.ThrowIfNull(inner); ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(peer);
        _ = repository.GetProtoSource(pluginId, baselineVersion, contractId); _ = repository.GetProtoSource(pluginId, candidateVersion, contractId);
        _inner = inner; _repository = repository; _peer = peer; _plugin = pluginId; _baseline = baselineVersion; _candidate = candidateVersion; _contract = contractId;
    }
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _inner.Inject;
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.Entry.Plugin != _plugin || ctx.Entry.Version != _candidate) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (!(await _repository.CheckProtoAsync(_plugin, _baseline, _candidate, _contract, ct).ConfigureAwait(false)).IsCompatible ||
            !(await _peer.CheckAsync(_repository.GetProtoSource(_plugin, _candidate, _contract), ct).ConfigureAwait(false)).IsCompatible)
            throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed) { Pointer = ErrorPointer.Input("contracts", _contract) });
        ct.ThrowIfCancellationRequested(); await _inner.ApplyAsync(ctx, config, ct).ConfigureAwait(false);
    }
}

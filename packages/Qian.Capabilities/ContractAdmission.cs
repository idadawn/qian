using System.Text.Json;
using Qian.Runtime;
using Qian.Runtime.Errors;
using SemanticVersion = SemanticVersioning.Version;

namespace Qian.Capabilities;

// 平台挂载路径的契约准入（R76）：按预装制品里的契约族选择基线，用 S1-RT-006 的同一组包装插件在业务 Apply 前比较。
internal sealed class ContractAdmission(InstalledContractRepository repository, IGrpcContractPeers? peers) : IContractAdmission
{
    public IQianPlugin Admit(IQianPlugin plugin, string pluginId, string version)
    {
        ArgumentNullException.ThrowIfNull(plugin); ArgumentException.ThrowIfNullOrWhiteSpace(pluginId); ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var admitted = plugin;
        foreach (var contract in repository.GetContracts(pluginId, version).OrderBy(document => document.Id, StringComparer.Ordinal))
        {
            // 只有 OpenAPI 与 proto 有真实兼容检查器；json-schema 契约随制品登记，不在挂载时比较。
            if (contract.Kind is not ("openapi" or "proto")) continue;
            if (Baseline(pluginId, version, contract) is not { } baseline) continue;
            admitted = contract.Kind == "openapi"
                ? OpenApiContractPlugin.FromRepository(admitted, repository, pluginId, baseline, version, contract.Id)
                : peers?.Find(pluginId, version) is { } peer
                    ? new GrpcContractPlugin(admitted, repository, peer, pluginId, baseline, version, contract.Id)
                    : new UnavailablePeerPlugin(admitted, contract.Id);
        }
        return admitted;
    }

    // 基线 = 同组件已预装版本中低于候选、且声明同一契约族与同一种类的最高版本；契约族的首个版本没有基线。
    private string? Baseline(string pluginId, string version, ContractDocument contract)
    {
        var candidate = new SemanticVersion(version);
        return repository.Versions(pluginId)
            .Select(text => (Text: text, Parsed: new SemanticVersion(text)))
            .Where(item => item.Parsed < candidate && repository.Find(pluginId, item.Text, contract.Id) is { } document && document.Kind == contract.Kind)
            .OrderByDescending(item => item.Parsed).Select(item => item.Text).FirstOrDefault();
    }

    // 声明了 proto 契约却没有宿主授权的反射对端：无法完成一致性探测，不能当作兼容。
    private sealed class UnavailablePeerPlugin(IQianPlugin inner, string contractId) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
            throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed) { Pointer = ErrorPointer.Input("contracts", contractId) });
    }
}

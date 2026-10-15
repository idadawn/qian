using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>
/// 平台挂载路径的契约准入（S1-RT-006 复核 R76）：组件版本在 metadata/contracts.json 声明契约族时，挂载前用 OpenApiContractPlugin /
/// GrpcContractPlugin 核对它对同一契约族前一个已安装版本的向后兼容性。Registry 的 scope Reconciler 与 Lifecycle 的挂载都经它包装，
/// 不能由宿主自行决定是否套用。
/// </summary>
public interface IContractAdmission
{
    /// <summary>
    /// 为宿主预装的组件版本建立准入包装：未声明契约的版本原样返回；契约族的首个版本没有基线，不做兼容比较；其余版本在业务 Apply 前比较，
    /// 不兼容、比较无法完成或 proto 契约缺少可用对端时 Apply 失败（条目为 Failed），不公开任何业务注册。
    /// </summary>
    IQianPlugin Admit(IQianPlugin plugin, string pluginId, string version);
}

/// <summary>宿主为已部署的组件版本提供 gRPC 反射对端（经出网与安装授权配置的连接）；没有可用对端时返回 null，声明 proto 契约的挂载因此失败。</summary>
public interface IGrpcContractPeers
{
    /// <summary>按组件 ID 与精确版本取得对端探针。</summary>
    IGrpcContractProbe? Find(string pluginId, string version);
}

using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>实际 HTTP 宿主的契约来源；必须按当前身份、端点策略和真实挂载视图裁剪。</summary>
public interface IHttpContractSource
{
    /// <summary>从实际宿主端点导出一个组件的完整契约，供受信任制品构建使用，不经过用户权限裁剪。</summary>
    ValueTask<JsonElement> CompileDocumentAsync(string pluginId, CancellationToken cancellationToken = default);
    /// <summary>读取当前请求可访问的组件 HTTP 文档；没有可信 HTTP 请求时不得推测端点权限。</summary>
    ValueTask<JsonElement> GetDocumentAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
}

/// <summary>平台运营端点的契约归属；只用于构建完整 OpenAPI，不开放租户业务挂载或授权旁路。</summary>
public sealed record PlatformHttpContract(string PluginId);

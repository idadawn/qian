using System.Net;
using System.Text.Json;
using Qian.Config;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Identity.Connector.Oidc;

/// <summary>
/// 通用 OIDC 连接器组件：按配置构造 <see cref="OidcIdentityConnector"/>，登记到当前 scope 的 <c>Identity.Connectors</c> 注册表（R38），
/// 注册名即配置的 <c>name</c>（缺省 <c>oidc</c>，对应服务键 <c>Identity.Connectors:oidc</c>）。挂在租户 scope 时只对该租户可见，并遮蔽全局的同名连接器；
/// 卸载即撤销注册并释放自有的 HTTP 连接池。无参构造经 Config 组件按本组件的声明与审批解析 secretRef；
/// 不装 Config 的独立宿主可传入自己的 <see cref="ISecretResolver"/>。
/// </summary>
public sealed class OidcConnectorPlugin : IQianPlugin
{
    private readonly ISecretResolver? _secrets;
    private readonly HttpMessageHandler? _handler;

    /// <summary>经 Config 组件解析客户端密钥（<c>ctx.ResolveSecretAsync</c>）。</summary>
    public OidcConnectorPlugin() { }

    /// <summary>由宿主提供密钥解析与（可选的）出站处理器；处理器归宿主所有，本组件不释放。</summary>
    public OidcConnectorPlugin(ISecretResolver secrets, HttpMessageHandler? handler = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _handler = handler;
    }

    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _secrets is null ? [IdentityDefinition.Key, ConfigDefinition.Key] : [IdentityDefinition.Key];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var options = OidcConnectorOptions.FromConfig(config);
        var handler = _handler;
        if (handler is null)
        {
            // 自有连接池：不跟随重定向、不带 Cookie；连接定期轮换以跟上上游 DNS 变化。
            var owned = new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(10), PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            ctx.Effect(owned);
            handler = owned;
        }
        var clock = ctx.Services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var connector = new OidcIdentityConnector(options, _secrets ?? new PluginSecrets(ctx), handler, clock);
        // 效果逆序撤销：先撤注册（不再有新请求进来），再释放连接器，最后释放连接池。
        ctx.Effect(connector);
        ctx.IdentityConnectors().Register(options.Name, connector);
        return ValueTask.CompletedTask;
    }

    // 每次换码现取：经 Config 组件按本组件的声明、审批与当前租户解析，组件卸载后随上下文失效。
    private sealed class PluginSecrets(IPluginContext ctx) : ISecretResolver
    {
        public ValueTask<SecretValue> ResolveAsync(string reference, CancellationToken cancellationToken = default) => ctx.ResolveSecretAsync(reference, cancellationToken);
    }
}

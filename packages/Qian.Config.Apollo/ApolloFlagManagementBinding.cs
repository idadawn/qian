using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Config.Apollo;

/// <summary>可信宿主预先登记的单一 Apollo 草稿目标；不能由组件配置或模型请求替换。</summary>
public sealed class ApolloFlagManagementBinding
{
    /// <summary>固定 Portal 根地址、应用、环境、集群、已存在的操作员及平台级 Token 引用。</summary>
    public ApolloFlagManagementBinding(Uri portal, string appId, string environment, string cluster, string operatorName, string tokenReference, string receiptBinding)
    {
        if (portal is null || !portal.IsAbsoluteUri || portal.Scheme is not ("http" or "https") || portal.UserInfo.Length != 0 ||
            portal.Query.Length != 0 || portal.Fragment.Length != 0 || portal.AbsolutePath != "/" ||
            !FeatureFlagRules.ValidName(receiptBinding) || !Safe(appId) || !Safe(cluster) || !Safe(operatorName) || environment is not ("UAT" or "PRO") ||
            !SecretReference.IsValid(tokenReference) || SecretReference.Parse(tokenReference).Scope != "platform")
            throw QianConfigurationExtensions.Invalid("flags.binding");
        Portal = portal; AppId = appId; Environment = environment; Cluster = cluster; OperatorName = operatorName; TokenReference = tokenReference; ReceiptBinding = receiptBinding;
    }
    /// <summary>可信 Portal 根地址。</summary>
    public Uri Portal { get; }
    /// <summary>唯一应用 ID。</summary>
    public string AppId { get; }
    /// <summary>UAT 或 PRO。</summary>
    public string Environment { get; }
    /// <summary>固定集群。</summary>
    public string Cluster { get; }
    /// <summary>Portal 中已存在的操作员；不是模型输入。</summary>
    public string OperatorName { get; }
    /// <summary>每次请求重新解析的 secretRef；只允许 platform scope。</summary>
    public string TokenReference { get; }
    /// <summary>已在 NativeData 登记的专用平台回执连接绑定。</summary>
    public string ReceiptBinding { get; }
    private static bool Safe(string value) => value is { Length: > 0 and <= 100 } && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or '@');
}

/// <summary>宿主引导适配；实际可调用能力只在 qian.config.flags 挂载期间存在。</summary>
public static class FeatureFlagManagementHosting
{
    /// <summary>复制受信任绑定表；不读取 Apollo 自身返回的地址或 Token 引用。</summary>
    public static IServiceCollection AddQianFeatureFlagManagement(this IServiceCollection services, IReadOnlyDictionary<string, ApolloFlagManagementBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count == 0 || bindings.Any(pair => !FeatureFlagRules.ValidName(pair.Key) || pair.Value is null) ||
            services.Any(service => service.ServiceType == typeof(FlagManagementBindings))) throw QianConfigurationExtensions.Invalid("flags.bindings");
        services.AddSingleton(new FlagManagementBindings(bindings.ToFrozenDictionary(StringComparer.Ordinal)));
        return services;
    }
}
internal sealed record FlagManagementBindings(FrozenDictionary<string, ApolloFlagManagementBinding> Items);

using Kun.Authorization;
using Qian.Runtime;

namespace Qian.Authz.Abstractions;

/// <summary>Authz seam 的 Definition；只声明可替换端口，不实现授权引擎。</summary>
public static class AuthzDefinition
{
    /// <summary>同一 scope 的唯一权限判定服务键。</summary>
    public static readonly ServiceKey Key = new("Authz");
    /// <summary>活动权限声明注册表；与判定服务分工，不授予主体权限。</summary>
    public static readonly ServiceKey PermissionsKey = new("Authz.Permissions");
    /// <summary>资源类型扩展端口的稳定服务键。</summary>
    public static readonly ServiceKey ResourceTypesKey = new("qian.authz.resource-type/v1");
    /// <summary>平台超管显式旁路端口（<see cref="ISuperadminBypass"/>）；缺失时调用方须 fail-closed。</summary>
    public static readonly ServiceKey SuperadminBypassKey = new("Authz.SuperadminBypass");
    /// <summary>解析当前 scope 的授权服务；没有 Provider 时沿用运行时的缺失服务错误。</summary>
    public static IAuthzService Authz(this IPluginContext context) => context.Inject<IAuthzService>(Key);
}

/// <summary>复用 Kun 的五个判定方法与数据/字段端口，避免 UI/API/MCP 产生三套鉴权契约。</summary>
public interface IAuthzService : IPermissionChecker
{
    /// <summary>本地数据权限端口；S1 不因此获得 S2 的谓词编译器。</summary>
    IDataPermissionFilter DataPermissions { get; }
    /// <summary>服务端字段投影与写入白名单端口。</summary>
    IFieldPolicy Fields { get; }
    /// <summary>
    /// 列出主体经角色授权持有的权限点及其风险、step-up、终端元数据与当前判定；未经授予的权限点不出现。
    /// 供 me/permissions 与界面过滤使用：需要 step-up 或终端证明的权限点照常列出，Allowed 为 false、Reason 给出缺少的条件。
    /// </summary>
    ValueTask<IReadOnlyList<EffectiveGrant>> ListEffectivePermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default);
}

/// <summary>主体实际持有的一个权限点：声明元数据与当前请求上下文下的判定（Allowed / Reason 与 Check 一致）。</summary>
public sealed record EffectiveGrant(PermissionDefinition Definition, bool Allowed, string Reason);

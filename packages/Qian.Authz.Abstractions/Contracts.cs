using Kun.Authorization;
using Qian.Runtime;

namespace Qian.Authz.Abstractions;

/// <summary>Authz seam 的 Definition；只声明可替换端口，不实现授权引擎。</summary>
public static class AuthzDefinition
{
    /// <summary>同一 scope 的唯一权限判定服务键。</summary>
    public static readonly ServiceKey Key = new("Authz");
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
}

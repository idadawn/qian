using Qian.Runtime.Errors;

namespace Qian.Audit.Native;

/// <summary>离线迁移和在线核验共用的专用角色声明；角色名不是凭据。</summary>
public sealed class NativeAuditRoles
{
    /// <summary>指定独立租户登录、NOLOGIN 函数所有者及可选的平台登录。</summary>
    public NativeAuditRoles(string tenantLogin, string functionOwner, string? platformLogin = null)
    {
        Validate(tenantLogin); Validate(functionOwner); if (platformLogin is not null) Validate(platformLogin);
        if (tenantLogin == functionOwner || platformLogin == tenantLogin || platformLogin == functionOwner) throw Invalid();
        TenantLogin = tenantLogin; FunctionOwner = functionOwner; PlatformLogin = platformLogin;
    }
    /// <summary>只读表与调用租户追加函数的受限登录。</summary>
    public string TenantLogin { get; }
    /// <summary>不能登录、不能继承且没有成员关系的函数所有者。</summary>
    public string FunctionOwner { get; }
    /// <summary>显式平台登录；null 表示关闭平台路径。</summary>
    public string? PlatformLogin { get; }
    internal static void Validate(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is < 'a' or > 'z' || role.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))) throw Invalid();
    }
    internal static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

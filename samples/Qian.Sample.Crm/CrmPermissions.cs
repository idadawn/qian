using System.Text.Json;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Surfaces;

namespace Qian.Sample.Crm;

public static class CrmPermissions
{
    public static PermissionCatalog Compile()
    {
        using var permissionsStream = typeof(CrmPermissions).Assembly.GetManifestResourceStream("crm.metadata.permissions.json")
            ?? throw new InvalidOperationException("缺少客户权限声明。");
        using var rolesStream = typeof(CrmPermissions).Assembly.GetManifestResourceStream("crm.metadata.roles.json")
            ?? throw new InvalidOperationException("缺少客户角色模板。");
        using var permissions = JsonDocument.Parse(permissionsStream); using var roles = JsonDocument.Parse(rolesStream);
        return new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.sample.crm", "1.0.0", permissions.RootElement, roles.RootElement);
    }
}

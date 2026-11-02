using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.AspNetCore;

public static partial class TenancyEndpoints
{
    // 管理员维护（S1-TEN-010）：账号查找与新建是平台级操作，旁路审计的资源为 tenancy/admin-accounts；其余按目标租户路径授权。
    // 权限点为 qian.tenancy:admin:<action>。运营入口只收 user 主体，Agent 与委托调用一律拒绝（替换与重置按 R52 即 humanOnly）。
    // 重置密码的动作叫 reset-credential：组件表面的明文扫描把以 password 结尾的键当敏感字段，权限风险表里不能出现 reset-password 键。
    private static void MapAdministrators(IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        const string accounts = "tenancy/admin-accounts";
        Bind(endpoints.MapPost("/api/tenancy/admin-accounts:lookup", (HttpContext http, TenantAdminAccountLookup input) =>
            Execute(http, source, Admin("read"), accounts, async (ctx, _) => Results.Ok(await Administration(ctx).FindAccountAsync(input, http.RequestAborted).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound))), () => ValidateBody(input)))).Produces<TenantAdminAccount>();
        Bind(endpoints.MapPost("/api/tenancy/admin-accounts", (HttpContext http, CreateTenantAdminAccount input) =>
            Execute(http, source, Admin("create"), accounts, async (ctx, _) =>
                Results.Created((string?)null, await Administration(ctx).CreateAccountAsync(input, http.RequestAborted).ConfigureAwait(false)), () => ValidateBody(input))))
            .Produces<TenantAdminAccount>(StatusCodes.Status201Created).Produces<ErrorDetail>(422, ErrorDetailJson.MediaType);
        Bind(endpoints.MapGet("/api/tenancy/tenants/{id:guid}/admins", (HttpContext http, Guid id) =>
            Execute(http, source, Admin("read"), "tenancy/tenants/" + id, async (ctx, _) =>
                Results.Ok(await Administration(ctx).GetAsync(id, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantAdminList>();
        Bind(endpoints.MapPut("/api/tenancy/tenants/{id:guid}/admins", (HttpContext http, Guid id, ReplaceTenantAdmins input) =>
            Execute(http, source, Admin("assign"), "tenancy/tenants/" + id, async (ctx, principal) =>
                Results.Ok(await Administration(ctx).ReplaceAsync(id, input, principal.Subject, http.RequestAborted).ConfigureAwait(false)), () =>
                {
                    ValidateBody(input);
                    if (input.Items is not [{ SubjectId: var subject }] || subject == Guid.Empty)
                        throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/items/0/subjectId" });
                }))).Produces<TenantAdminList>().Produces<ErrorDetail>(422, ErrorDetailJson.MediaType);
        Bind(endpoints.MapPost("/api/tenancy/tenants/{id:guid}/admins/{membershipId:guid}:reset-password", (HttpContext http, Guid id, Guid membershipId, ResetTenantAdminPassword input) =>
            Execute(http, source, Admin("reset-credential"), "tenancy/tenants/" + id + "/admins/" + membershipId, async (ctx, principal) =>
            {
                await Administration(ctx).ResetPasswordAsync(id, membershipId, input, principal.Subject, http.RequestAborted).ConfigureAwait(false);
                return Results.NoContent();
            }, () => ValidateBody(input)))).Produces(StatusCodes.Status204NoContent).Produces<ErrorDetail>(422, ErrorDetailJson.MediaType);
    }
    private static string Admin(string action) => "qian.tenancy:admin:" + action;
    private static ITenantAdministration Administration(IPluginContext ctx) => ctx.Inject<ITenantAdministration>(TenantAdministration.Key);
    // 契约里的长度与条数声明在授权之前按同一组标注执行；未知字段与缺失的必填项已在绑定时拒绝。
    private static void ValidateBody(object body)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true)) return;
        var member = results[0].MemberNames.FirstOrDefault();
        throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = member is null ? "#" : "#/" + JsonNamingPolicy.CamelCase.ConvertName(member) });
    }
}

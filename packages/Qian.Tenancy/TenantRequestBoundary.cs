using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Kun.MultiTenancy.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Qian.Tenancy;

/// <summary>租户业务入口的严格边界；宿主认证后调用，平台运营入口独立鉴权，不经此边界回退成租户。</summary>
public sealed class TenantRequestBoundary(ITenantContext tenant)
{
    private readonly DefaultTenantResolver _resolver = new(Options.Create(new KunMultiTenancyOptions { Production = true }));

    /// <summary>忽略请求头，只从已认证身份的唯一 tenant_id 建立范围；终止、异常和取消后均还原原上下文。</summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(next);
        var id = TenantContextPropagator.Parse(await _resolver.ResolveTenantIdAsync(context).ConfigureAwait(false));
        if (tenant.TenantId is { } existing && TenantContextPropagator.Parse(existing) != id) throw TenantContextPropagator.Denied();
        using var scope = tenant.BeginScope(id.ToString("D"));
        await next(context).ConfigureAwait(false);
    }
}

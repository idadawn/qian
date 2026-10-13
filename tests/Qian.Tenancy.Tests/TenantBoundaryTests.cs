using System.Security.Claims;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Tests;

public sealed class TenantBoundaryTests
{
    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    private static readonly Guid A = Guid.Parse("01900000-0000-7000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("01900000-0000-7000-8000-000000000002");

    [Fact]
    public async Task 无认证或仅有请求头_重复或缺失租户claim_均不进入业务()
    {
        var tenant = new AsyncLocalTenantContext(); var boundary = new TenantRequestBoundary(tenant);
        ClaimsIdentity[] identities = [new(), new([new Claim("tenant_id", A.ToString())]),
            new([new Claim("sub", "person")], "validated"),
            new([new Claim("tenant_id", A.ToString()), new Claim("tenant_id", A.ToString())], "validated"),
            new([new Claim("tenant_id", "default")], "validated")];
        foreach (var identity in identities)
        {
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }; http.Request.Headers["X-Tenant-Id"] = A.ToString();
            var called = false;
            var error = await Assert.ThrowsAsync<QianErrorException>(() => boundary.InvokeAsync(http, _ => { called = true; return Task.CompletedTask; }));
            Assert.Equal("UNAUTHENTICATED", error.Error.Code.Code); Assert.False(called); Assert.Null(tenant.TenantId);
        }
    }

    [Fact]
    public async Task 已认证租户跨await可读_忽略冲突请求头_异常后还原_并行请求不串租户()
    {
        var tenant = new AsyncLocalTenantContext(); var boundary = new TenantRequestBoundary(tenant);
        async Task Enter(Guid id)
        {
            var http = new DefaultHttpContext { User = Principal(id) }; http.Request.Headers["X-Tenant-Id"] = "ignored";
            await boundary.InvokeAsync(http, async _ => { await Task.Yield(); Assert.Equal(id.ToString(), tenant.TenantId); });
            Assert.Null(tenant.TenantId);
        }
        await Task.WhenAll(Enter(A), Enter(B));
        await Assert.ThrowsAsync<InvalidOperationException>(() => boundary.InvokeAsync(new DefaultHttpContext { User = Principal(A) }, _ => throw new InvalidOperationException("test")));
        Assert.Null(tenant.TenantId);
        using (tenant.BeginScope(B.ToString()))
        {
            await Assert.ThrowsAsync<QianErrorException>(() => boundary.InvokeAsync(new DefaultHttpContext { User = Principal(A) }, _ => Task.CompletedTask));
            Assert.Equal(B.ToString(), tenant.TenantId);
        }
    }

    [Fact]
    public async Task 事件和作业JSON跨序列化_元数据跨副本_消费端显式恢复相同租户()
    {
        var sender = new AsyncLocalTenantContext(); var receiver = new AsyncLocalTenantContext();
        var outbound = new TenantContextPropagator(sender); var inbound = new TenantContextPropagator(receiver);
        using var source = sender.BeginScope(A.ToString());
        foreach (var input in new[] { "{\"type\":\"crm.customer.created.v1\",\"payload\":{\"name\":\"客户\"}}", "{\"job\":\"export\",\"arguments\":{\"format\":\"csv\"}}" })
        {
            var wire = Json(outbound.Write(Json(input)).GetRawText());
            using (inbound.BeginScope(wire, A)) { await Task.Yield(); Assert.Equal(A.ToString(), receiver.TenantId); }
            Assert.Null(receiver.TenantId);
        }
        using (inbound.BeginScope(outbound.WriteHeaders().ToArray(), A)) Assert.Equal(A.ToString(), receiver.TenantId);
        Assert.Null(receiver.TenantId);
    }

    [Fact]
    public void 缺少重复空值跨租户传播均拒绝_不能从消息获得另一个租户权限()
    {
        var tenant = new AsyncLocalTenantContext(); var propagator = new TenantContextPropagator(tenant);
        Assert.Throws<QianErrorException>(() => propagator.Write(Json("{}")));
        Assert.Throws<QianErrorException>(() => propagator.WriteHeaders());
        foreach (var input in new[] { "{}", "{\"tenant_id\":null}", "{\"tenant_id\":\"default\"}",
            $$"""{"tenant_id":"{{B}}"}""", $$"""{"tenant_id":"{{A}}","tenant_id":"{{A}}"}""" })
            Assert.Throws<QianErrorException>(() => propagator.BeginScope(Json(input), A));
        Assert.Throws<QianErrorException>(() => propagator.BeginScope([new("tenant_id", A.ToString()), new("tenant_id", A.ToString())], A));
        Assert.Throws<QianErrorException>(() => propagator.BeginScope([new("tenant_id", A.ToString())], Guid.Empty));
        Assert.Null(tenant.TenantId);
        using (tenant.BeginScope(A.ToString()))
        {
            Assert.Throws<QianErrorException>(() => propagator.Write(Json($$"""{"tenant_id":"{{B}}"}""")));
            Assert.Throws<QianErrorException>(() => propagator.BeginScope(Json($$"""{"tenant_id":"{{B}}"}"""), B));
            Assert.Equal(A.ToString(), tenant.TenantId);
        }
    }

    [Fact]
    public void 创建编辑输入不接受tenantId_编辑不能改变租户号和隔离策略()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateTenant>("{\"code\":\"a\",\"name\":\"A\",\"tenantId\":\"other\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateTenant>("{\"name\":\"A\",\"expectedRevision\":1,\"isolation\":\"database\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateTenant>("{\"name\":\"A\",\"expectedRevision\":1,\"code\":\"b\"}", options));
    }
    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity([new Claim("tenant_id", id.ToString())], "validated"));
}

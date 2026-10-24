using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Authz.AspNetCore;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected const string Reader = "crm-command-reader";
    private protected const string CustomerRead = "qian.sample.crm:customer:read";
    private protected const string CustomerWrite = "qian.sample.crm:customer:write";
}

// S1-AZ-004：参考宿主按生产模式运行的三处 PEP。HTTP 端点以 RequireQianPermission 声明权限点，gRPC 方法以 [RequiresPermission] 声明，
// 两者经同一执行端口对同一权限点结论一致；未声明权限点的组件端点与方法缺省拒绝；每次拒绝都有决策日志；旧令牌按服务端 identity_revision 下限拒绝。
public sealed partial class IdentityAuthzTests
{
    [Fact]
    public async Task gRPC与HTTP对同一权限点判定一致_未声明权限点的组件端点与方法在生产模式缺省拒绝_每次判定都有决策日志()
    {
        await using var host = await Start(grpc: true, decisionLogSampling: 1, map: app =>
        {
            app.MapGrpcService<AcceptanceEcho>().RequireAuthorization("crm-command-reader");
            // 带组件端点绑定却没有 RequireQianPermission 的端点：生产模式缺省拒绝。
            app.MapGet("/api/acceptance/undeclared", () => Results.Ok(new { ok = true })).RequireAuthorization("crm-command-reader")
                .WithMetadata(new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, "qian.sample.crm/customer.list"));
        });
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using var channel = GrpcChannel.ForAddress(host.GrpcAddress, new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { UseProxy = false }, DisposeHttpClient = true });
        var client = channel.CreateCallInvoker();

        // HTTP：租户 A 的 writer 持 tenant.owner（含 customer:read）→ 200（HTTP PEP 与命令 PEP 各记一条放行）；reader 没有任何角色 → 403 AUTHZ_DENIED，响应不可缓存。
        using (var allowed = await host.Get(A, "/api/customers")) Assert.True(allowed.StatusCode == HttpStatusCode.OK, host.FailureEvidence);
        using (var denied = await host.Send(Get("/api/customers", host.Token(A, "crm.read", Reader))))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal("AUTHZ_DENIED", await Code(denied));
            Assert.True(denied.Headers.CacheControl is { Private: true, NoStore: true }, denied.Headers.CacheControl?.ToString());
        }
        // gRPC 一元与服务端流：同一权限点、同一主体，结论与 HTTP 相同；拒绝带 ErrorDetail trailer。
        Assert.Equal("read:ping", await client.AsyncUnaryCall(AcceptanceBindings.Read, null, Call(host.Token(A, "crm.read")), "ping"));
        using (var stream = client.AsyncServerStreamingCall(AcceptanceBindings.Stream, null, Call(host.Token(A, "crm.read")), "ping"))
        { Assert.True(await stream.ResponseStream.MoveNext(Ct)); Assert.Equal("stream:ping", stream.ResponseStream.Current); }
        var unary = await Assert.ThrowsAsync<RpcException>(() => client.AsyncUnaryCall(AcceptanceBindings.Read, null, Call(host.Token(A, "crm.read", Reader)), "ping").ResponseAsync);
        AssertDenied(unary, "AUTHZ_DENIED", 403);
        using (var stream = client.AsyncServerStreamingCall(AcceptanceBindings.Stream, null, Call(host.Token(A, "crm.read", Reader)), "ping"))
            AssertDenied(await Assert.ThrowsAsync<RpcException>(() => stream.ResponseStream.MoveNext(Ct)), "AUTHZ_DENIED", 403);
        // 未声明权限点：拥有全部权限的 writer 也被缺省拒绝（gRPC 方法与 HTTP 端点各一）。
        AssertDenied(await Assert.ThrowsAsync<RpcException>(() => client.AsyncUnaryCall(AcceptanceBindings.Open, null, Call(host.Token(A, "crm.read")), "ping").ResponseAsync), "AUTHZ_DENIED", 403);
        using (var undeclared = await host.Get(A, "/api/acceptance/undeclared")) { Assert.Equal(HttpStatusCode.Forbidden, undeclared.StatusCode); Assert.Equal("AUTHZ_DENIED", await Code(undeclared)); }
        // 跨租户令牌：B 的 reader 在 A 的资源上没有角色；租户 B 的链里只有 B 的判定。
        using (var foreign = await host.Send(Get("/api/customers", host.Token(B, "crm.read", Reader)))) Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        using (tenant.BeginScope(A))
        {
            Assert.Equal(
            [
                (Writer, "allowed", AuthzEnforcement.HttpEntryPoint, "GET /api/customers", "role_grant"),
                (Writer, "allowed", AuthzEnforcement.CommandEntryPoint, "qian.sample.crm/customer.list", "role_grant"),
                (Reader, "denied", AuthzEnforcement.HttpEntryPoint, "GET /api/customers", "no_matching_grant"),
                (Writer, "allowed", AuthzEnforcement.GrpcEntryPoint, "/crm.acceptance.Echo/Read", "role_grant"),
                (Writer, "allowed", AuthzEnforcement.GrpcEntryPoint, "/crm.acceptance.Echo/Stream", "role_grant"),
                (Reader, "denied", AuthzEnforcement.GrpcEntryPoint, "/crm.acceptance.Echo/Read", "no_matching_grant"),
                (Reader, "denied", AuthzEnforcement.GrpcEntryPoint, "/crm.acceptance.Echo/Stream", "no_matching_grant"),
                (Writer, "denied", AuthzEnforcement.GrpcEntryPoint, "/crm.acceptance.Echo/Open", "permission_not_declared"),
                (Writer, "denied", AuthzEnforcement.HttpEntryPoint, "GET /api/acceptance/undeclared", "permission_not_declared"),
            ], await Decisions(host, A));
            Assert.True((await Source(host).Audit().VerifyAsync(Ct)).Valid);
        }
        using (tenant.BeginScope(B)) Assert.Equal([(Reader, "denied", AuthzEnforcement.HttpEntryPoint, "GET /api/customers", "no_matching_grant")], await Decisions(host, B));
    }

    [Fact]
    public async Task 更高identity_revision出现后旧令牌被服务端拒绝_HTTP回401且gRPC回Unauthenticated_移除角色后授权修订递增旧快照三秒内失效_authz_grant_changed进Outbox()
    {
        await using var host = await Start(grpc: true, decisionLogSampling: 1, map: app => app.MapGrpcService<AcceptanceEcho>().RequireAuthorization("crm-command-reader"));
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        Task<HttpResponseMessage> List(long? revision) => host.Send(Get("/api/customers", host.Token(A, "crm.read", identityRevision: revision)));
        using (var first = await List(1)) Assert.True(first.StatusCode == HttpStatusCode.OK, host.FailureEvidence);
        using (var newer = await List(2)) Assert.Equal(HttpStatusCode.OK, newer.StatusCode);
        // 身份修订递增后（身份组件换发新令牌），仍持旧修订的令牌在服务端比对下限时被拒绝：401 UNAUTHENTICATED，客户端据此换令牌（S1-DEC-027）；
        // 没带修订的 S2S 令牌不比对。
        using (var stale = await List(1)) { Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode); Assert.Equal("UNAUTHENTICATED", await Code(stale)); }
        using (var bare = await List(null)) Assert.Equal(HttpStatusCode.OK, bare.StatusCode);
        using (tenant.BeginScope(A))
        {
            Assert.Equal((Writer, "denied", AuthzEnforcement.HttpEntryPoint, "GET /api/customers", "identity_revision_stale"), Assert.Single(await Decisions(host, A), item => item.Result == "denied"));
            var roles = new PostgresTenantRoleStore(data, tenant);
            // 先补一个管理员，再移除 writer 的 tenant.owner（否则触发最后一个管理员的自锁守卫）。
            await roles.AssignAsync("tenant.admin", "user", "admin-a", "acceptance", Ct);
            Assert.True(await roles.RevokeAsync("tenant.owner", "service", Writer, "acceptance", Ct));
        }
        // 租户授权修订递增 → 判定快照失效：拿着最新修订的令牌也在 3 秒内被拒绝（no_matching_grant）。
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            using var response = await List(2);
            if (response.StatusCode == HttpStatusCode.Forbidden) break;
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), "撤销角色 3 秒后旧快照仍放行。");
            await Task.Delay(50, Ct);
        }
        using (tenant.BeginScope(A))
        {
            Assert.Contains((Writer, "denied", AuthzEnforcement.HttpEntryPoint, "GET /api/customers", "no_matching_grant"), await Decisions(host, A));
            await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
            var revision = await session.Context.Revisions.AsNoTracking().Where(row => row.TenantId == A).Select(row => row.Revision).SingleAsync(Ct);
            var events = (await session.Context.OutboxMessages.AsNoTracking().Where(row => row.TenantId == A && row.EventTypeId == AuthzEventIds.GrantChanged).ToListAsync(Ct))
                .Select(row => QianEventEnvelope.Parse(row.Payload).Payload)
                .Select(payload => (payload.GetProperty("change").GetString(), payload.GetProperty("subjectType").GetString(), payload.GetProperty("subjectId").GetString(), payload.GetProperty("authzRevision").GetInt64()))
                .ToArray();
            Assert.Contains(("revoked", "service", Writer, revision), events);
            Assert.Contains(("assigned", "user", "admin-a", revision - 1), events);
        }
        // gRPC 与 HTTP 同一映射：旧令牌回 Unauthenticated，ErrorDetail 为 401 UNAUTHENTICATED（S1-DEC-027）。
        using var channel = GrpcChannel.ForAddress(host.GrpcAddress, new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { UseProxy = false }, DisposeHttpClient = true });
        AssertDenied(await Assert.ThrowsAsync<RpcException>(() => channel.CreateCallInvoker().AsyncUnaryCall(AcceptanceBindings.Read, null, Call(host.Token(A, "crm.read", identityRevision: 1)), "ping").ResponseAsync),
            "UNAUTHENTICATED", 401, StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task OpenAPI中每个组件端点带x_qian_permission_与端点声明一致_并记录401与403响应()
    {
        await using var host = await Start(capabilities: true);
        using var response = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence);
        var api = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var declared = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in api.GetProperty("paths").EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("x-qian-plugin", out var plugin) || plugin.GetString() != "qian.sample.crm") continue;
                Assert.True(operation.Value.TryGetProperty("x-qian-permission", out var permission), operation.Name + " " + path.Name + " 没有 x-qian-permission");
                declared[operation.Name.ToUpperInvariant() + " " + path.Name] = permission.GetString()!;
                Assert.NotEmpty(operation.Value.GetProperty("security").EnumerateArray());
                var responses = operation.Value.GetProperty("responses");
                Assert.True(responses.TryGetProperty("401", out _)); Assert.True(responses.TryGetProperty("403", out _));
            }
        Assert.Equal(new Dictionary<string, string> { ["GET /api/customers"] = CustomerRead, ["GET /api/customers/{id}"] = CustomerRead, ["POST /api/customers"] = CustomerWrite }, declared);
    }
}

public abstract partial class CustomerCommandTests
{
    private protected static HttpRequestMessage Get(string path, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }
    private protected static CallOptions Call(string token) => new(new Metadata { { "authorization", "Bearer " + token } }, cancellationToken: Ct);
    private protected static void AssertDenied(RpcException error, string code, int status, StatusCode grpc = StatusCode.PermissionDenied)
    {
        Assert.Equal(grpc, error.StatusCode); Assert.Equal(code, error.Status.Detail);
        var trailer = Assert.Single(error.Trailers, entry => entry.Key == AuthzGrpcServerInterceptor.ErrorDetailTrailer);
        var detail = ErrorDetailJson.Deserialize(Encoding.UTF8.GetString(trailer.ValueBytes)); Assert.NotNull(detail);
        Assert.Equal((code, status), (detail.Code, detail.Status));
    }
    private protected static CrmCommandRuntime Runtime(RunningApp host) => host.App.Services.GetRequiredService<CrmCommandRuntime>();
    private protected static Qian.Runtime.IPluginContext Source(RunningApp host) => Runtime(host).Source();
    // 当前租户链里的决策日志（按链序）。
    private protected static async Task<(string Actor, string Result, string EntryPoint, string Target, string Reason)[]> Decisions(RunningApp host, string tenantId)
    {
        var records = await Runtime(host).Loader.GetContext("authz", Qian.Runtime.ScopeKey.Global).Scoped(new Qian.Runtime.ScopeKey("tenant", tenantId)).Audit().ReadAsync(0, 100, cancellationToken: Ct);
        return [.. records.Select(entry => entry.Record).Where(record => record.CapabilityId == AuthzEnforcement.DecisionCapabilityId)
            .Select(record => (record.ActorId, record.Result, record.Payload.GetProperty("entryPoint").GetString()!, record.Payload.GetProperty("target").GetString()!, record.Payload.GetProperty("reason").GetString()!))];
    }

    // 手工绑定的验收 gRPC 服务：Read / Stream 声明 customer:read，Open 故意不声明。
    [BindServiceMethod(typeof(AcceptanceBindings), nameof(AcceptanceBindings.BindService))]
    public class AcceptanceEcho
    {
        [RequiresPermission(CustomerRead)]
        public virtual Task<string> Read(string request, ServerCallContext context) => Task.FromResult("read:" + request);
        [RequiresPermission(CustomerRead)]
        public virtual async Task Stream(string request, IServerStreamWriter<string> stream, ServerCallContext context) => await stream.WriteAsync("stream:" + request, context.CancellationToken);
        public virtual Task<string> Open(string request, ServerCallContext context) => Task.FromResult("open:" + request);
    }
    public static class AcceptanceBindings
    {
        private static readonly Marshaller<string> Marshaller = Marshallers.Create(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);
        public static readonly Method<string, string> Read = new(MethodType.Unary, "crm.acceptance.Echo", "Read", Marshaller, Marshaller);
        public static readonly Method<string, string> Stream = new(MethodType.ServerStreaming, "crm.acceptance.Echo", "Stream", Marshaller, Marshaller);
        public static readonly Method<string, string> Open = new(MethodType.Unary, "crm.acceptance.Echo", "Open", Marshaller, Marshaller);
        public static void BindService(ServiceBinderBase binder, AcceptanceEcho? service)
        {
            binder.AddMethod(Read, service is null ? null : service.Read); binder.AddMethod(Stream, service is null ? null : service.Stream);
            binder.AddMethod(Open, service is null ? null : service.Open);
        }
    }
}

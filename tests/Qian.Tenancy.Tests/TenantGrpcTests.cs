using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.Tests;

public sealed class TenantGrpcTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid A = Guid.Parse("01900000-0000-7000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("01900000-0000-7000-8000-000000000002");

    [Fact]
    public async Task 真实HTTP2四种调用传递租户_双租户并发不串范围_原始调用头不被修改()
    {
        await using var host = await GrpcHost.Start(Ct); var sender = new AsyncLocalTenantContext();
        var client = host.Channel.Intercept(new TenantGrpcClientInterceptor(sender));
        async Task Run(Guid id)
        {
            using var scope = sender.BeginScope(id.ToString()); var headers = host.Headers(id);
            foreach (var shape in Enum.GetValues<MethodType>())
            {
                var replies = await Call(client, shape, headers); Assert.All(replies, reply => Assert.Equal(id.ToString(), reply));
                Assert.DoesNotContain(headers, entry => entry.Key == "tenant_id");
            }
            Assert.Equal(id.ToString(), sender.TenantId);
        }
        await Task.WhenAll(Run(A), Run(B));
        Assert.Null(sender.TenantId); Assert.Equal(8, host.Calls.Count);
        Assert.Equal(4, host.Calls.Count(tenant => tenant == A.ToString())); Assert.Equal(4, host.Calls.Count(tenant => tenant == B.ToString()));
    }

    [Fact]
    public async Task 真实服务在读取业务流前拒绝缺失重复冲突元数据_无令牌_篡改签名_缺少claim()
    {
        await using var host = await GrpcHost.Start(Ct); var raw = host.Channel.CreateCallInvoker();
        var missing = host.Headers(A);
        var conflict = host.Headers(A); conflict.Add("tenant_id", B.ToString());
        var duplicate = host.Headers(A); duplicate.Add("tenant_id", A.ToString()); duplicate.Add("tenant_id", A.ToString());
        var anonymous = new Metadata { { "tenant_id", A.ToString() } };
        var noClaim = host.Headers(null); noClaim.Add("tenant_id", A.ToString());
        var badSignature = host.Headers(A, tamper: true); badSignature.Add("tenant_id", A.ToString());
        foreach (var metadata in new[] { missing, conflict, duplicate, anonymous, noClaim, badSignature })
        foreach (var shape in Enum.GetValues<MethodType>())
        {
            var denied = await Assert.ThrowsAsync<RpcException>(() => Call(raw, shape, metadata));
            Assert.Equal(StatusCode.Unauthenticated, denied.StatusCode);
            if (metadata == missing || metadata == conflict || metadata == duplicate || metadata == noClaim)
            {
                var trailer = Assert.Single(denied.Trailers, entry => entry.Key == TenantGrpcServerInterceptor.ErrorDetailTrailer);
                var detail = ErrorDetailJson.Deserialize(Encoding.UTF8.GetString(trailer.ValueBytes)); Assert.NotNull(detail);
                Assert.Equal("UNAUTHENTICATED", detail.Code); Assert.Equal(401, detail.Status);
                Assert.Matches("^[a-f0-9]{32}$", detail.TraceId); Assert.StartsWith("/TenantEcho/", detail.Instance);
            }
        }
        Assert.Empty(host.Calls);
    }

    [Fact]
    public async Task 发送端不使用默认租户_拒绝已有冲突或重复字段_不发出网络调用()
    {
        await using var host = await GrpcHost.Start(Ct); var sender = new AsyncLocalTenantContext();
        var client = host.Channel.Intercept(new TenantGrpcClientInterceptor(sender));
        await Assert.ThrowsAsync<QianErrorException>(() => Call(client, MethodType.Unary, host.Headers(A)));
        using var scope = sender.BeginScope(A.ToString());
        foreach (var values in new[] { new[] { B.ToString() }, new[] { A.ToString(), A.ToString() } })
        {
            var headers = host.Headers(A); foreach (var value in values) headers.Add("tenant_id", value);
            foreach (var shape in Enum.GetValues<MethodType>())
                await Assert.ThrowsAsync<QianErrorException>(() => Call(client, shape, headers));
            Assert.Equal(values, headers.Where(entry => entry.Key == "tenant_id").Select(entry => entry.Value));
        }
        Assert.Empty(host.Calls);
    }

    private static async Task<List<string>> Call(CallInvoker client, MethodType shape, Metadata headers)
    {
        var options = new CallOptions(headers, deadline: DateTime.UtcNow.AddSeconds(15), cancellationToken: Ct);
        switch (shape)
        {
            case MethodType.Unary:
                using (var call = client.AsyncUnaryCall(Bindings.Unary, null, options, "request")) return [await call.ResponseAsync];
            case MethodType.ServerStreaming:
                using (var call = client.AsyncServerStreamingCall(Bindings.Server, null, options, "request")) return await Read(call.ResponseStream);
            case MethodType.ClientStreaming:
                using (var call = client.AsyncClientStreamingCall(Bindings.Client, null, options))
                {
                    var earlyClose = await Write(call.RequestStream);
                    var response = await call.ResponseAsync; // 先核对最终状态/尾随元数据，不能把提前关闭写流当成服务端状态。
                    if (earlyClose is not null) throw earlyClose;
                    return [response];
                }
            case MethodType.DuplexStreaming:
                using (var call = client.AsyncDuplexStreamingCall(Bindings.Duplex, null, options))
                {
                    var earlyClose = await Write(call.RequestStream);
                    var response = await Read(call.ResponseStream);
                    if (earlyClose is not null) throw earlyClose;
                    return response;
                }
            default: throw new ArgumentOutOfRangeException(nameof(shape));
        }
        static async Task<RpcException?> Write(IClientStreamWriter<string> stream)
        {
            try { await stream.WriteAsync("one", Ct); await stream.CompleteAsync(); return null; }
            catch (RpcException error) when (error.StatusCode == StatusCode.Cancelled && !Ct.IsCancellationRequested)
            { return error; } // 仍须从响应读取权威结果；若响应成功，调用方继续抛出此写入错误。
        }
        static async Task<List<string>> Read(IAsyncStreamReader<string> stream)
        {
            var replies = new List<string>(); while (await stream.MoveNext(Ct)) replies.Add(stream.Current); return replies;
        }
    }

    private sealed class GrpcHost : IAsyncDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);
        private WebApplication _app = null!;
        public GrpcChannel Channel { get; private set; } = null!;
        public ConcurrentBag<string> Calls { get; } = [];
        private SigningCredentials Credentials => new(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        public static async Task<GrpcHost> Start(CancellationToken ct)
        {
            var host = new GrpcHost();
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
                builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); builder.Services.AddSingleton(host.Calls);
                builder.Services.AddGrpc(options => options.Interceptors.Add<TenantGrpcServerInterceptor>());
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true, ValidIssuer = "https://fixture.invalid/",
                        ValidateAudience = true, ValidAudience = "tenancy-grpc", ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                        ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(host._rsa), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                    };
                });
                builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
                builder.Services.AddQianErrors();
                builder.Services.AddAuthorization(); host._app = builder.Build();
                host._app.UseQianErrors(); host._app.UseAuthentication(); host._app.UseAuthorization(); host._app.MapGrpcService<Echo>().RequireAuthorization();
                await host._app.StartAsync(ct); host.Channel = GrpcChannel.ForAddress(host._app.Urls.Single(), new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { UseProxy = false }, DisposeHttpClient = true }); return host;
            }
            catch { await host.DisposeAsync(); throw; }
        }
        public Metadata Headers(Guid? tenant, bool tamper = false)
        {
            var claims = new List<Claim> { new("sub", "grpc-test-user") }; if (tenant is not null) claims.Add(new("tenant_id", tenant.Value.ToString()));
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("https://fixture.invalid/", "tenancy-grpc", claims,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), Credentials));
            if (tamper)
            {
                var parts = token.Split('.'); var signature = Base64UrlEncoder.DecodeBytes(parts[2]); signature[0] ^= 1;
                token = parts[0] + "." + parts[1] + "." + Base64UrlEncoder.Encode(signature);
            }
            return new Metadata { { "authorization", "Bearer " + token } };
        }
        public async ValueTask DisposeAsync()
        {
            Channel?.Dispose(); if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); } _rsa.Dispose();
        }
    }

    [BindServiceMethod(typeof(Bindings), nameof(Bindings.BindService))]
    public class Echo(ITenantContext tenant, ConcurrentBag<string> calls)
    {
        public virtual async Task<string> Unary(string request, ServerCallContext context)
        { var id = Enter(); await Task.Yield(); Assert.Equal(id, tenant.TenantId); return id; }
        public virtual async Task Server(string request, IServerStreamWriter<string> stream, ServerCallContext context)
        { var id = Enter(); await stream.WriteAsync(id, context.CancellationToken); await Task.Yield(); Assert.Equal(id, tenant.TenantId); await stream.WriteAsync(id, context.CancellationToken); }
        public virtual async Task<string> Client(IAsyncStreamReader<string> stream, ServerCallContext context)
        { var id = Enter(); await foreach (var _ in stream.ReadAllAsync(context.CancellationToken)) Assert.Equal(id, tenant.TenantId); return id; }
        public virtual async Task Duplex(IAsyncStreamReader<string> input, IServerStreamWriter<string> output, ServerCallContext context)
        { var id = Enter(); await foreach (var _ in input.ReadAllAsync(context.CancellationToken)) { Assert.Equal(id, tenant.TenantId); await output.WriteAsync(id, context.CancellationToken); } }
        private string Enter() { Assert.NotNull(tenant.TenantId); calls.Add(tenant.TenantId); return tenant.TenantId; }
    }
    public static class Bindings
    {
        private static readonly Marshaller<string> Marshaller = Marshallers.Create(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);
        public static readonly Method<string, string> Unary = new(MethodType.Unary, "TenantEcho", "Unary", Marshaller, Marshaller);
        public static readonly Method<string, string> Server = new(MethodType.ServerStreaming, "TenantEcho", "Server", Marshaller, Marshaller);
        public static readonly Method<string, string> Client = new(MethodType.ClientStreaming, "TenantEcho", "Client", Marshaller, Marshaller);
        public static readonly Method<string, string> Duplex = new(MethodType.DuplexStreaming, "TenantEcho", "Duplex", Marshaller, Marshaller);
        public static void BindService(ServiceBinderBase binder, Echo? service)
        {
            binder.AddMethod(Unary, service is null ? null : service.Unary); binder.AddMethod(Server, service is null ? null : service.Server);
            binder.AddMethod(Client, service is null ? null : service.Client); binder.AddMethod(Duplex, service is null ? null : service.Duplex);
        }
    }
}

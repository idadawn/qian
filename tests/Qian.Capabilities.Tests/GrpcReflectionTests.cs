using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Qian.Capabilities.Tests;

public sealed class GrpcReflectionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] GlobalScopes = ["global"];
    private const string Issuer = "qian-contract-test";
    private const string Audience = "qian-contract-peer";
    [Fact]
    public async System.Threading.Tasks.Task 真实认证HTTP2反射与实际RPC一致_声明类型漂移拒绝_匿名不能探测()
    {
        var compiler = new BufProtoCompatibilityProbe(Environment.GetEnvironmentVariable("QIAN_BUF_PATH") ?? throw new InvalidOperationException("缺少真实 proto 检查器。"));
        var descriptor = FileDescriptorSet.Parser.ParseFrom((await compiler.CompileAsync(Source(), Ct)).Span);
        var files = FileDescriptor.BuildFromByteStrings(descriptor.File.Select(file => file.ToByteString()));
        var service = files.SelectMany(file => file.Services).Single();
        using var rsa = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "contract-key" };
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc(); builder.Services.AddSingleton(new ReflectionServiceImpl(service));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = Audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorizationBuilder().AddPolicy("contracts", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "qian.registry:contract:read"));
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGrpcService<EchoService>().RequireAuthorization("contracts"); app.MapGrpcService<ReflectionServiceImpl>().RequireAuthorization("contracts");
        await app.StartAsync(Ct);
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { UseProxy = false }, DisposeHttpClient = true }); var invoker = channel.CreateCallInvoker();
            var token = new JwtSecurityToken(Issuer, Audience, [new Claim("sub", "registry-checker"), new Claim("scope", "qian.registry:contract:read")],
                DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            var headers = new Metadata { { "authorization", "Bearer " + new JwtSecurityTokenHandler().WriteToken(token) } };
            using (var call = invoker.AsyncUnaryCall(Binding.Method, null, new CallOptions(headers, cancellationToken: Ct), new StringValue { Value = "real protobuf" }))
                Assert.Equal("real protobuf", (await call.ResponseAsync).Value);
            var probe = new GrpcReflectionContractProbe(compiler, invoker, headers);
            Assert.True((await probe.CheckAsync(Source(), Ct)).IsCompatible);
            Assert.True((await probe.CheckAsync(Source(path: "contracts/echo.proto"), Ct)).IsCompatible);
            var baseline = Artifact("0.9.0"); var candidate = Artifact("1.0.0");
            using var repository = new InstalledContractRepository([baseline.Metadata, candidate.Metadata], null, new SurfaceValidator(), compiler);
            await repository.ValidateProtoAsync(Ct);
            await using var runtime = new PluginLoader(app.Services);
            var unauthenticated = new GrpcContractPlugin(candidate.Definition.Plugin, repository, new GrpcReflectionContractProbe(compiler, invoker), "test.rpc", "0.9.0", "1.0.0", "test.rpc.api/v1");
            await runtime.MountAsync([candidate.Definition with { Plugin = unauthenticated }], Ct);
            Assert.Equal(PluginState.Failed, runtime.GetState("remote", ScopeKey.Global));
            Assert.IsType<RpcException>(runtime.GetFailure("remote", ScopeKey.Global));
            await runtime.UnmountAsync("remote", ScopeKey.Global, Ct);
            await runtime.MountAsync([candidate.Definition with { Plugin = new GrpcContractPlugin(candidate.Definition.Plugin, repository, probe, "test.rpc", "0.9.0", "1.0.0", "test.rpc.api/v1") }], Ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("remote", ScopeKey.Global));
            Assert.Single(runtime.GetContext("remote", ScopeKey.Global).List(SurfaceKeys.Commands));
            // 平台挂载路径的契约准入（R76，S1-RT-005）：按预装基线套用同一个 GrpcContractPlugin；契约族首个版本不比较，
            // 没有宿主授权的反射对端时 fail-closed（PRECONDITION_FAILED，pointer 指向契约）。
            await runtime.UnmountAsync("remote", ScopeKey.Global, Ct);
            Assert.Same(baseline.Definition.Plugin, new ContractAdmission(repository, null).Admit(baseline.Definition.Plugin, "test.rpc", "0.9.0"));
            await runtime.MountAsync([candidate.Definition with { Plugin = new ContractAdmission(repository, null).Admit(candidate.Definition.Plugin, "test.rpc", "1.0.0") }], Ct);
            Assert.Equal(PluginState.Failed, runtime.GetState("remote", ScopeKey.Global));
            var unavailable = Assert.IsType<Qian.Runtime.Errors.QianErrorException>(runtime.GetFailure("remote", ScopeKey.Global));
            Assert.Equal(("PRECONDITION_FAILED", "#/contracts/test.rpc.api~1v1"), (unavailable.Error.Code.Code, unavailable.Error.Pointer));
            await runtime.UnmountAsync("remote", ScopeKey.Global, Ct);
            var admitted = new ContractAdmission(repository, new Peers(probe)).Admit(candidate.Definition.Plugin, "test.rpc", "1.0.0");
            Assert.IsType<GrpcContractPlugin>(admitted);
            await runtime.MountAsync([candidate.Definition with { Plugin = admitted }], Ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("remote", ScopeKey.Global));
            var mismatch = await probe.CheckAsync(Source("google.protobuf.Int32Value"), Ct);
            Assert.False(mismatch.IsCompatible); Assert.NotEmpty(mismatch.BreakingRules);
            var error = await Assert.ThrowsAsync<RpcException>(() => new GrpcReflectionContractProbe(compiler, invoker).CheckAsync(Source(), Ct).AsTask());
            Assert.Equal(StatusCode.Unauthenticated, error.StatusCode);
        }
        finally { await app.StopAsync(Ct); }
    }
    // 宿主按出网与安装授权配置的反射对端：只对确切的组件版本给出探针。
    private sealed class Peers(IGrpcContractProbe probe) : IGrpcContractPeers
    {
        public IGrpcContractProbe? Find(string pluginId, string version) => pluginId == "test.rpc" && version == "1.0.0" ? probe : null;
    }
    private static ProtoContractSet Source(string input = "google.protobuf.StringValue", string path = "echo.proto") => new(new Dictionary<string, ReadOnlyMemory<byte>>
    {
        [path] = Encoding.UTF8.GetBytes("syntax = \"proto3\"; package sample.v1; import \"google/protobuf/wrappers.proto\"; service EchoApi { rpc Echo(" + input + ") returns (google.protobuf.StringValue); }")
    });
    private static RegistryComponentBinding Artifact(string version)
    {
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            [".qian-plugin/plugin.json"] = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = "test.rpc", version, description = "外部协议准入的真实声明式组件测试。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = "协议", category = "testing" }, inject = Array.Empty<string>(), provides = Array.Empty<string>(),
                policy = new { tier = "private", sandbox = "none", authentication = "ON_INSTALL" }, scopes = GlobalScopes
            }),
            ["metadata/contracts.json"] = Encoding.UTF8.GetBytes("[{\"id\":\"test.rpc.api/v1\",\"kind\":\"proto\",\"path\":\"./echo.proto\"}]"),
            ["echo.proto"] = Source().ReadFile("echo.proto"),
            ["commands/inspect.md"] = Encoding.UTF8.GetBytes("---\ndescription: 只有实际协议探测成功后才公开的声明式命令。\n---\n检查远程服务。\n")
        };
        var directory = Directory.CreateTempSubdirectory("qian-grpc-contract-artifact-");
        try
        {
            foreach (var file in files)
            {
                var path = Path.Combine(directory.FullName, file.Key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, file.Value.ToArray());
            }
            var validator = new SurfaceValidator(); var options = new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true };
            var result = validator.ValidateComponent(directory.FullName, options); Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics));
            var inner = new DeclarativeSurfacePlugin(result);
            var definition = new PluginDefinition(new PluginEntry("remote", "test.rpc", version, ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), inner, inner.Provides);
            var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
            var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
            return RegistryComponentBinding.FromArtifact(definition, files, digest, validator, options);
        }
        finally { directory.Delete(true); }
    }
    [BindServiceMethod(typeof(Binding), nameof(Binding.BindService))]
    public class EchoService
    {
        public virtual System.Threading.Tasks.Task<StringValue> Echo(StringValue request, ServerCallContext context) => System.Threading.Tasks.Task.FromResult(new StringValue { Value = request.Value });
    }
    public static class Binding
    {
        private static readonly Marshaller<StringValue> Serializer = Marshallers.Create<StringValue>(value => value.ToByteArray(), value => StringValue.Parser.ParseFrom(value));
        public static readonly Method<StringValue, StringValue> Method = new(MethodType.Unary, "sample.v1.EchoApi", "Echo", Serializer, Serializer);
        public static void BindService(ServiceBinderBase binder, EchoService? service) => binder.AddMethod(Method, service is null ? null : service.Echo);
    }
}

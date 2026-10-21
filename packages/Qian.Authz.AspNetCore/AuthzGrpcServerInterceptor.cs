using System.Diagnostics;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Kun.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Authz.AspNetCore;

/// <summary>
/// gRPC 入口的 PEP：四种调用形态在进入 handler 或读取首帧前，对方法上 <see cref="RequiresPermissionAttribute"/> 声明的每个权限点执行一次判定；
/// 未声明的方法在生产模式缺省拒绝并记录（显式标记 <see cref="PermissionEnforcedInHandlerAttribute"/> 的方法除外）。拒绝以 <c>PermissionDenied</c> 返回，trailer <c>qian-error-bin</c> 携带 ErrorDetail JSON；
/// 判定与 HTTP、命令管道经同一执行端口，对同一权限点结论一致。
/// </summary>
public sealed partial class AuthzGrpcServerInterceptor(AuthzEnforcementSource source, ILogger<AuthzGrpcServerInterceptor> logger) : Interceptor
{
    /// <summary>携带冻结 ErrorDetail JSON 的二进制响应 trailer，与租户拦截器同名。</summary>
    public const string ErrorDetailTrailer = "qian-error-bin";

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    { await CheckAsync(context).ConfigureAwait(false); return await continuation(request, context).ConfigureAwait(false); }
    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    { await CheckAsync(context).ConfigureAwait(false); await continuation(request, responseStream, context).ConfigureAwait(false); }
    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    { await CheckAsync(context).ConfigureAwait(false); return await continuation(requestStream, context).ConfigureAwait(false); }
    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    { await CheckAsync(context).ConfigureAwait(false); await continuation(requestStream, responseStream, context).ConfigureAwait(false); }

    private async Task CheckAsync(ServerCallContext context)
    {
        var http = context.GetHttpContext(); var endpoint = http.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<PermissionEnforcedInHandlerAttribute>() is not null) return;
        var declared = endpoint?.Metadata.GetOrderedMetadata<RequiresPermissionAttribute>() ?? [];
        var permissions = declared.Select(item => item.Permission).Distinct(StringComparer.Ordinal).ToArray();
        if (permissions.Length == 0 && !source.IsProduction(http.RequestServices)) { LogUndeclared(logger, context.Method); return; }
        try
        {
            if (HttpEnforcement.ReadSubject(http, out var subject) is { } unauthenticated) throw new QianErrorException(unauthenticated);
            IEnumerable<string?> required = permissions.Length == 0 ? new string?[] { null } : permissions;
            foreach (var permission in required)
            {
                var decision = await HttpEnforcement.EnforceAsync(http, source, subject, permission, AuthzEnforcement.GrpcEntryPoint, context.Method).ConfigureAwait(false);
                if (!decision.Allow) throw new QianErrorException(new QianError(QianPermissionHandler.Denial(decision)));
            }
        }
        catch (QianErrorException error) { throw Rpc(http, context, error.Error); }
    }

    private static RpcException Rpc(HttpContext http, ServerCallContext context, QianError error)
    {
        var catalog = http.RequestServices.GetService<ErrorCatalog>() ?? ErrorCatalog.CreateDefault();
        var trace = Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        var detail = catalog.CreateDetail(error, trace, instance: context.Method);
        var status = detail.Status switch { 401 => StatusCode.Unauthenticated, 404 => StatusCode.NotFound, _ => StatusCode.PermissionDenied };
        return new RpcException(new Status(status, detail.Code), new Metadata { { ErrorDetailTrailer, Encoding.UTF8.GetBytes(ErrorDetailJson.Serialize(detail)) } });
    }
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "gRPC 方法 {Method} 没有声明权限点；开发模式放行，生产模式将缺省拒绝。")]
    private static partial void LogUndeclared(ILogger logger, string method);
}

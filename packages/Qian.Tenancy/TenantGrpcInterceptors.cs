using System.Diagnostics;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Kun.Core.MultiTenancy;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

/// <summary>租户 gRPC 服务入口；宿主先完成 JWT 认证，再核对传播元数据，覆盖全部四种调用形态。</summary>
public sealed class TenantGrpcServerInterceptor(ITenantContext tenant) : Interceptor
{
    /// <summary>携带冻结 ErrorDetail JSON 的二进制响应 trailer。</summary>
    public const string ErrorDetailTrailer = "qian-error-bin";
    private readonly ErrorCatalog _errors = ErrorCatalog.CreateDefault();
    private readonly TenantRequestBoundary _boundary = new(tenant);
    private readonly TenantContextPropagator _propagator = new(tenant);

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation) => Invoke(context, () => continuation(request, context));

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation) =>
        Invoke(context, async () => { await continuation(request, responseStream, context).ConfigureAwait(false); return true; });

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation) => Invoke(context, () => continuation(requestStream, context));

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation) =>
        Invoke(context, async () => { await continuation(requestStream, responseStream, context).ConfigureAwait(false); return true; });

    private async Task<T> Invoke<T>(ServerCallContext context, Func<Task<T>> continuation)
    {
        T result = default!;
        try
        {
            await _boundary.InvokeAsync(context.GetHttpContext(), async _ =>
            {
                using var scope = _propagator.BeginScope(context.RequestHeaders.Where(entry => !entry.IsBinary)
                    .Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value)), TenantContextPropagator.Parse(tenant.TenantId));
                result = await continuation().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (QianErrorException error) when (error.Error.Code.Code == "UNAUTHENTICATED")
        {
            var trace = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
            var detail = _errors.CreateDetail(error.Error, trace, instance: context.Method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, detail.Code),
                new Metadata { { ErrorDetailTrailer, Encoding.UTF8.GetBytes(ErrorDetailJson.Serialize(detail)) } });
        }
        return result;
    }
}

/// <summary>从当前可信租户上下文添加 gRPC tenant_id 元数据；认证令牌由宿主的身份客户端另行提供。</summary>
public sealed class TenantGrpcClientInterceptor(ITenantContext tenant) : Interceptor
{
    private readonly TenantContextPropagator _propagator = new(tenant);

    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation) => continuation(request, WithTenant(context));
    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation) => continuation(request, WithTenant(context));
    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) => continuation(request, WithTenant(context));
    /// <inheritdoc />
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) => continuation(WithTenant(context));
    /// <inheritdoc />
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) => continuation(WithTenant(context));

    private ClientInterceptorContext<TRequest, TResponse> WithTenant<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class where TResponse : class
    {
        var propagated = _propagator.WriteHeaders(); var metadata = new Metadata();
        foreach (var entry in context.Options.Headers ?? new Metadata())
            if (entry.IsBinary) metadata.Add(entry.Key, entry.ValueBytes.ToArray()); else metadata.Add(entry.Key, entry.Value);
        var existing = metadata.Where(entry => entry.Key == "tenant_id").ToArray();
        if (existing.Length == 0) metadata.Add("tenant_id", propagated["tenant_id"]);
        else
        {
            // 不覆盖调用方已有的冲突/重复值，也不原地修改可能复用的 CallOptions.Headers。
            using var verified = _propagator.BeginScope(existing.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value)),
                TenantContextPropagator.Parse(propagated["tenant_id"]));
        }
        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(metadata));
    }
}

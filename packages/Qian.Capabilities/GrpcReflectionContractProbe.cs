using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Reflection.V1Alpha;

namespace Qian.Capabilities;

/// <summary>使用真实 gRPC reflection 读取实际服务描述；传输、TLS 与认证凭据由宿主提供。</summary>
public sealed class GrpcReflectionContractProbe : IGrpcContractProbe
{
    private readonly IProtoCompatibilityProbe _compiler;
    private readonly CallInvoker _invoker;
    private readonly Metadata _headers;
    /// <summary>invoker 只能来自宿主已授权的服务连接，headers 会复制且不会写入诊断。</summary>
    public GrpcReflectionContractProbe(IProtoCompatibilityProbe compiler, CallInvoker invoker, Metadata? headers = null)
    {
        ArgumentNullException.ThrowIfNull(compiler); ArgumentNullException.ThrowIfNull(invoker);
        _compiler = compiler; _invoker = invoker; _headers = new Metadata();
        foreach (var header in headers ?? new Metadata())
            if (header.IsBinary) _headers.Add(header.Key, header.ValueBytes.ToArray()); else _headers.Add(header.Key, header.Value);
    }
    /// <inheritdoc />
    public async ValueTask<ContractCompatibility> CheckAsync(ProtoContractSet expected, CancellationToken cancellationToken = default)
    {
        var bytes = await _compiler.CompileAsync(expected, cancellationToken).ConfigureAwait(false);
        var contract = FileDescriptorSet.Parser.ParseFrom(bytes.Span);
        var services = contract.File.SelectMany(file => file.Service.Select(service => string.IsNullOrEmpty(file.Package) ? service.Name : file.Package + "." + service.Name)).Distinct(StringComparer.Ordinal).ToArray();
        if (services.Length == 0) throw new ArgumentException("gRPC 契约必须声明服务。", nameof(expected));
        var client = new ServerReflection.ServerReflectionClient(_invoker);
        using var call = client.ServerReflectionInfo(_headers, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: cancellationToken);
        var received = new Dictionary<string, FileDescriptorProto>(StringComparer.Ordinal); long total = 0;
        foreach (var service in services)
        {
            await SendAsync(call, new ServerReflectionRequest { FileContainingSymbol = service }, cancellationToken).ConfigureAwait(false);
            if (!await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("反射服务提前结束。");
            var response = call.ResponseStream.Current;
            if (response.MessageResponseCase == ServerReflectionResponse.MessageResponseOneofCase.ErrorResponse)
            {
                if (response.ErrorResponse.ErrorCode == (int)StatusCode.NotFound) return new ContractCompatibility(false, ["GRPC_SERVICE_MISSING"]);
                throw new InvalidOperationException("反射服务拒绝描述请求。");
            }
            if (response.MessageResponseCase != ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse || response.FileDescriptorResponse.FileDescriptorProto.Count == 0)
                throw new InvalidOperationException("反射服务没有返回协议描述。");
            foreach (var fileBytes in response.FileDescriptorResponse.FileDescriptorProto)
            {
                total += fileBytes.Length;
                if (total > 64 * 1024 * 1024 || received.Count >= 10000) throw new InvalidOperationException("反射描述超出限制。");
                var file = FileDescriptorProto.Parser.ParseFrom(fileBytes);
                if (received.TryGetValue(file.Name, out var prior) && !prior.Equals(file)) throw new InvalidOperationException("反射服务返回了冲突的文件描述。");
                received[file.Name] = file;
            }
        }
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);
        // 消费最终状态，不能把提前返回的描述当成整个 RPC 已成功。
        while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("反射服务返回了额外响应。");
        var actual = new FileDescriptorSet(); actual.File.AddRange(received.Values.OrderBy(file => file.Name, StringComparer.Ordinal));
        return await _compiler.CompareRuntimeDescriptorsAsync(bytes, actual.ToByteArray(), cancellationToken).ConfigureAwait(false);
    }
    // 服务端读请求前就结束调用（如认证拒绝后立即回 401 并重置流）时，写请求可能先于调用状态就绪而失败：Grpc.Net.Client 报 Cancelled，
    // 或直接抛传输层 IOException。调用方没有取消时改读响应流，它会等到调用结束并抛出最终状态（如 Unauthenticated）；读不到失败状态时仍报原异常。
    private static async Task SendAsync(AsyncDuplexStreamingCall<ServerReflectionRequest, ServerReflectionResponse> call, ServerReflectionRequest request, CancellationToken cancellationToken)
    {
        try { await call.RequestStream.WriteAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && error is RpcException { StatusCode: StatusCode.Cancelled } or IOException)
        {
            await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

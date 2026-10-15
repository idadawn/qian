using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Reflection.V1Alpha;

namespace Qian.Capabilities.Tests;

// S1-RT-028：服务端读请求前就结束调用（认证拒绝后立即回 401 并重置流）时，Grpc.Net.Client 写请求流可能先于调用状态就绪而报 Cancelled。
// 这里按脚本回放双向流，确定性地构造这一时序，不依赖真实 HTTP/2 的线程调度；真实认证宿主下的匿名探测见 GrpcReflectionTests。
public sealed class GrpcReflectionContractProbeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 写请求先于调用状态失败报Cancelled时以响应流给出的最终状态报错_认证拒绝得到Unauthenticated()
    {
        var call = new ScriptedCall { Write = _ => throw Rpc(StatusCode.Cancelled) };
        call.Reads.Enqueue(() => throw Rpc(StatusCode.Unauthenticated));
        var error = await Assert.ThrowsAsync<RpcException>(() => Probe(call).CheckAsync(Source(), Ct).AsTask());
        Assert.Equal(StatusCode.Unauthenticated, error.StatusCode);
        Assert.Equal(1, call.ReadCount);
    }

    [Fact]
    public async Task 写请求抛传输层IOException时同样以响应流给出的最终状态报错()
    {
        // 共用连接压测里出现过：服务端重置流后写请求直接抛 IOException（含 HttpProtocolException），没有换成 gRPC 状态。
        var call = new ScriptedCall { Write = _ => throw new IOException("The request was aborted.") };
        call.Reads.Enqueue(() => throw Rpc(StatusCode.Unauthenticated));
        Assert.Equal(StatusCode.Unauthenticated, (await Assert.ThrowsAsync<RpcException>(() => Probe(call).CheckAsync(Source(), Ct).AsTask())).StatusCode);

        var aborted = new IOException("The request was aborted.");
        var ended = new ScriptedCall { Write = _ => throw aborted };
        ended.Reads.Enqueue(() => null);
        Assert.Same(aborted, await Assert.ThrowsAsync<IOException>(() => Probe(ended).CheckAsync(Source(), Ct).AsTask()));
    }

    [Fact]
    public async Task 调用方取消或截止时间到期时不读响应流_原样报Cancelled与DeadlineExceeded()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var canceled = new ScriptedCall { Write = _ => { cancel.Cancel(); throw Rpc(StatusCode.Cancelled); } };
        canceled.Reads.Enqueue(() => throw Rpc(StatusCode.Unauthenticated));
        var error = await Assert.ThrowsAsync<RpcException>(() => Probe(canceled).CheckAsync(Source(), cancel.Token).AsTask());
        Assert.Equal(StatusCode.Cancelled, error.StatusCode); Assert.Equal(0, canceled.ReadCount);

        var expired = new ScriptedCall { Write = _ => throw Rpc(StatusCode.DeadlineExceeded) };
        expired.Reads.Enqueue(() => throw Rpc(StatusCode.Unauthenticated));
        error = await Assert.ThrowsAsync<RpcException>(() => Probe(expired).CheckAsync(Source(), Ct).AsTask());
        Assert.Equal(StatusCode.DeadlineExceeded, error.StatusCode); Assert.Equal(0, expired.ReadCount);
    }

    [Fact]
    public async Task 响应流没有给出失败状态时仍报写请求的原异常()
    {
        var original = Rpc(StatusCode.Cancelled);
        var ended = new ScriptedCall { Write = _ => throw original };
        ended.Reads.Enqueue(() => null);
        Assert.Same(original, await Assert.ThrowsAsync<RpcException>(() => Probe(ended).CheckAsync(Source(), Ct).AsTask()));

        var canceled = new ScriptedCall { Write = _ => throw Rpc(StatusCode.Cancelled) };
        canceled.Reads.Enqueue(() => throw Rpc(StatusCode.Cancelled));
        Assert.Equal(StatusCode.Cancelled, (await Assert.ThrowsAsync<RpcException>(() => Probe(canceled).CheckAsync(Source(), Ct).AsTask())).StatusCode);
    }

    [Fact]
    public async Task 写请求成功时照常读取反射描述并比较()
    {
        var call = new ScriptedCall();
        call.Reads.Enqueue(() => new ServerReflectionResponse { FileDescriptorResponse = new FileDescriptorResponse { FileDescriptorProto = { Echo().ToByteString() } } });
        call.Reads.Enqueue(() => null);
        var compiler = new FixedCompiler();
        Assert.True((await new GrpcReflectionContractProbe(compiler, call).CheckAsync(Source(), Ct)).IsCompatible);
        Assert.Equal(2, call.ReadCount); Assert.Equal(1, compiler.Compared);
    }

    private static GrpcReflectionContractProbe Probe(ScriptedCall call) => new(new FixedCompiler(), call);
    private static RpcException Rpc(StatusCode code) => new(new Status(code, "scripted"));
    private static ProtoContractSet Source() => new(new Dictionary<string, ReadOnlyMemory<byte>> { ["echo.proto"] = Encoding.UTF8.GetBytes("syntax = \"proto3\";") });
    private static FileDescriptorProto Echo() => new()
    {
        Name = "echo.proto", Package = "sample.v1", Syntax = "proto3", Dependency = { "google/protobuf/wrappers.proto" },
        Service = { new ServiceDescriptorProto { Name = "EchoApi", Method = { new MethodDescriptorProto { Name = "Echo", InputType = ".google.protobuf.StringValue", OutputType = ".google.protobuf.StringValue" } } } }
    };

    // 编译结果固定为一个 Echo 服务；运行时比较只计数并判兼容，其余比较探测器不会用到。
    private sealed class FixedCompiler : IProtoCompatibilityProbe
    {
        public int Compared { get; private set; }
        public ValueTask<ReadOnlyMemory<byte>> CompileAsync(ProtoContractSet source, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>>(new FileDescriptorSet { File = { Echo() } }.ToByteArray());
        public ValueTask<ContractCompatibility> CompareRuntimeDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default)
        {
            Compared++; return ValueTask.FromResult(new ContractCompatibility(true, []));
        }
        public ValueTask<ContractCompatibility> CompareAsync(ProtoContractSet baseline, ProtoContractSet candidate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ContractCompatibility> CompareDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // 按脚本回放双向流：Write 决定写请求的结果，Reads 依次给出每次读取的消息（null 表示响应流正常结束）或抛出的状态。
    private sealed class ScriptedCall : CallInvoker
    {
        public Func<CancellationToken, Task> Write { get; init; } = _ => Task.CompletedTask;
        public Queue<Func<object?>> Reads { get; } = new();
        public int ReadCount { get; private set; }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            new(new Writer<TRequest>(Write), new Reader<TResponse>(this), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();

        private sealed class Writer<T>(Func<CancellationToken, Task> write) : IClientStreamWriter<T>
        {
            public WriteOptions? WriteOptions { get; set; }
            public Task WriteAsync(T message) => WriteAsync(message, CancellationToken.None);
            public async Task WriteAsync(T message, CancellationToken cancellationToken) { await Task.Yield(); await write(cancellationToken); }
            public Task CompleteAsync() => Task.CompletedTask;
        }

        private sealed class Reader<T>(ScriptedCall call) : IAsyncStreamReader<T>
        {
            public T Current { get; private set; } = default!;
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                await Task.Yield(); call.ReadCount++;
                if (call.Reads.Dequeue()() is not { } message) return false;
                Current = (T)message; return true;
            }
        }
    }
}

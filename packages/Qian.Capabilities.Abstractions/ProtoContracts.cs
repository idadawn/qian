namespace Qian.Capabilities;

/// <summary>同一制品内的封闭 proto 源文件集合；不包含生成器、网络依赖或宿主路径。</summary>
public sealed class ProtoContractSet
{
    private readonly Dictionary<string, byte[]> _files;
    /// <summary>复制源文件，约束与制品相同的文件数、路径及总大小。</summary>
    public ProtoContractSet(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count is 0 or > 10000) throw new ArgumentException("proto 文件数量无效。", nameof(files));
        _files = new Dictionary<string, byte[]>(StringComparer.Ordinal); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var file in files)
        {
            var parts = file.Key.Split('/'); total += file.Value.Length;
            if (total > 64 * 1024 * 1024 || file.Value.Length is 0 or > 1_048_576 || !file.Key.EndsWith(".proto", StringComparison.Ordinal) ||
                file.Key.Contains('\\', StringComparison.Ordinal) || file.Key.Contains(':', StringComparison.Ordinal) || file.Key.Any(char.IsControl) ||
                parts.Length > 64 || parts.Any(part => part is "" or "." or ".." or ".git") || !names.Add(file.Key)) throw new ArgumentException("proto 文件路径或大小无效。", nameof(files));
            _files.Add(file.Key, file.Value.ToArray());
        }
        Paths = Array.AsReadOnly(_files.Keys.Order(StringComparer.Ordinal).ToArray());
    }
    /// <summary>制品内的文件名。</summary>
    public IReadOnlyList<string> Paths { get; }
    /// <summary>返回独立副本。</summary>
    public byte[] ReadFile(string path) => _files.TryGetValue(path, out var bytes) ? bytes.ToArray() : throw new KeyNotFoundException();
}

/// <summary>真实 proto 编译与兼容性探测；无法编译不能作为兼容结果。</summary>
public interface IProtoCompatibilityProbe
{
    /// <summary>编译为包含依赖的标准 FileDescriptorSet，供实际反射一致性探测使用。</summary>
    ValueTask<ReadOnlyMemory<byte>> CompileAsync(ProtoContractSet source, CancellationToken cancellationToken = default);
    /// <summary>以完整 descriptor 比较保持源代码、JSON 和 wire 契约的向后兼容性。</summary>
    ValueTask<ContractCompatibility> CompareAsync(ProtoContractSet baseline, ProtoContractSet candidate, CancellationToken cancellationToken = default);
    /// <summary>比较已编译或来自实际反射服务的标准 FileDescriptorSet。</summary>
    ValueTask<ContractCompatibility> CompareDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default);
    /// <summary>按包与符号比较运行时反射；允许同一包的文件布局不同，仍检查 RPC、消息和字段契约。</summary>
    ValueTask<ContractCompatibility> CompareRuntimeDescriptorsAsync(ReadOnlyMemory<byte> baseline, ReadOnlyMemory<byte> candidate, CancellationToken cancellationToken = default);
}

/// <summary>从宿主配置的真实 gRPC 端点核对已声明协议，不接收客户端 URL 或可信布尔值。</summary>
public interface IGrpcContractProbe
{
    /// <summary>读取服务反射描述并与完整编译契约比较；认证、传输或描述失败均不能当作兼容。</summary>
    ValueTask<ContractCompatibility> CheckAsync(ProtoContractSet expected, CancellationToken cancellationToken = default);
}

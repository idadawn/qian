using System.Text.Json;
using Qian.Runtime;

namespace Qian.Files;

/// <summary>文件 seam 的稳定服务键；消费者只依赖本 Definition，换 Provider 或新增存储后端都不改调用代码。</summary>
public static class FilesDefinition
{
    /// <summary>当前 scope 可见的文件服务（单例 seam，组件模型 §6.2）。</summary>
    public static readonly ServiceKey Key = new("Files");
    /// <summary>存储后端注册表（R38 具名 seam）：后端以 <c>Files.Backends:&lt;名称&gt;</c> 登记，组件用 <c>[Contributes]</c> 声明。</summary>
    public static readonly ServiceKey BackendsKey = new("Files.Backends");
    /// <summary>上传策略注册表：策略是数据而不是具名服务，冲突时以 <c>Files.Policies:&lt;用途&gt;</c> 报告。</summary>
    public static readonly ServiceKey PoliciesKey = new("Files.Policies");

    /// <summary>
    /// 解析已挂载的文件服务。带 <c>[QianPlugin]</c> 的组件在 ApplyAsync 里不要用它（上下文作参数会被 QIANSDK002 判为逃逸），
    /// 改写成 <c>ctx.Inject&lt;IFileService&gt;(FilesDefinition.Key)</c>。
    /// </summary>
    public static IFileService Files(this IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Inject<IFileService>(Key);
    }
}

/// <summary>
/// 文件能力门面：每个操作都作为 qian.files 的能力经 Mediator 命令管道执行，租户、授权与审计三个强制执行点与同事务 Outbox 一并生效；
/// 缺少任一基础策略时 fail-closed 拒绝。租户只取自已验证令牌，调用方不能指定租户或存储键。
/// </summary>
public interface IFileService
{
    /// <summary>上传策略注册表：消费组件在 Apply 里按用途登记，释放句柄交给 ctx.Effect（R38 注册表 seam）。</summary>
    IUploadPolicyRegistry Policies { get; }
    /// <summary>存储后端注册表：按名称登记后端工厂，组件配置 storage.kind 按名称选用（R38 注册表 seam）。</summary>
    IFileBackendRegistry Backends { get; }
    /// <summary>当前后端需要宿主代为执行直传与直下时的网关（local 后端）；后端自带签名（S3 / OSS）时为 null。</summary>
    IFileTransferGateway? Transfer { get; }
    /// <summary>能力 qian.files/presign-upload：校验策略与配额、预占配额并签发短时 PUT 地址（不超过 15 分钟）。</summary>
    ValueTask<PresignedUpload> PresignUploadAsync(PresignUploadRequest request, CancellationToken cancellationToken = default);
    /// <summary>能力 qian.files/complete-upload：服务端自算大小与 SHA-256 并嗅探类型；不符时文件转入隔离并以错误返回。</summary>
    ValueTask<FileDescriptor> CompleteAsync(CompleteUploadRequest request, CancellationToken cancellationToken = default);
    /// <summary>能力 qian.files/get：读取当前租户一个文件的元数据；其它租户或不存在一律 NOT_FOUND。</summary>
    ValueTask<FileDescriptor> GetAsync(Guid fileId, CancellationToken cancellationToken = default);
    /// <summary>能力 qian.files/download-url：签发不超过 5 分钟的下载地址，恒定 Content-Disposition: attachment，每次签发都记审计。</summary>
    ValueTask<DownloadTicket> GetDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default);
    /// <summary>能力 qian.files/delete：软删并立即释放配额；对象的物理删除由保留期作业负责（S1-FILE-003）。</summary>
    ValueTask<FileDescriptor> DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken = default);
    /// <summary>能力 qian.files/quota：当前租户的配额上限、已用字节与文件数。</summary>
    ValueTask<FileQuota> GetQuotaAsync(CancellationToken cancellationToken = default);
}

/// <summary>按用途登记的上传策略；同一用途独占，第二次登记失败。</summary>
public interface IUploadPolicyRegistry
{
    /// <summary>按 <see cref="UploadPolicy.Purpose"/> 登记策略，返回可重复释放的撤销句柄。</summary>
    IDisposable Register(UploadPolicy policy);
    /// <summary>读取用途的当前策略；未登记返回 null。</summary>
    UploadPolicy? Find(string purpose);
    /// <summary>按用途排序的已登记策略快照。</summary>
    IReadOnlyList<UploadPolicy> List();
}

/// <summary>按名称登记的存储后端工厂；同名独占。</summary>
public interface IFileBackendRegistry
{
    /// <summary>登记名称 <paramref name="name"/> 的后端工厂，返回可重复释放的撤销句柄。</summary>
    IDisposable Register(string name, IFileBackendFactory factory);
    /// <summary>读取名称对应的工厂；未登记返回 null。</summary>
    IFileBackendFactory? Find(string name);
    /// <summary>按名称排序的已登记后端名。</summary>
    IReadOnlyList<string> List();
}

/// <summary>存储后端工厂：由组件配置的 storage 节创建后端实例。</summary>
public interface IFileBackendFactory
{
    /// <summary>创建后端；配置不合法时抛出带 INPUT_INVALID 的 QianErrorException。</summary>
    IFileBackend Create(FileBackendContext context);
}

/// <summary>创建后端所需的输入：组件配置 storage 节、文件组件的时钟与宿主服务（日志等）。</summary>
public sealed record FileBackendContext(JsonElement Storage, TimeProvider Clock, IServiceProvider Services);

/// <summary>最小存储后端端口：对象键由文件服务生成，后端不感知租户与配额。</summary>
public interface IFileBackend
{
    /// <summary>后端名称，写入 file_objects.provider。</summary>
    string Name { get; }
    /// <summary>需要宿主代为执行直传与直下时返回网关，否则 null。</summary>
    IFileTransferGateway? Transfer { get; }
    /// <summary>签发 PUT 上传地址：请求体超过 <paramref name="maxBytes"/> 必须被拒绝。</summary>
    ValueTask<PresignedTransfer> CreateUploadAsync(string objectKey, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken = default);
    /// <summary>签发 GET 下载地址：恒定 Content-Disposition: attachment，文件名为 <paramref name="fileName"/>。</summary>
    ValueTask<PresignedTransfer> CreateDownloadAsync(string objectKey, string fileName, TimeSpan lifetime, CancellationToken cancellationToken = default);
    /// <summary>服务端读取对象内容；对象不存在时抛出 FileNotFoundException。</summary>
    ValueTask<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);
    /// <summary>服务端写入对象；超过 <paramref name="maxBytes"/> 时不留对象并抛出 InvalidOperationException。</summary>
    ValueTask WriteAsync(string objectKey, Stream content, string contentType, long maxBytes, CancellationToken cancellationToken = default);
    /// <summary>删除对象；不存在时返回 false。</summary>
    ValueTask<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>后端签发的直传 / 直下地址；Headers 对上传是必须携带的请求头，对下载是响应将带的头。</summary>
public sealed record PresignedTransfer(Uri Url, string Method, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

/// <summary>
/// 由宿主 HTTP 层代为执行签名直传与直下的网关（local 后端）。签名即授权：网关校验签名与过期，不读租户上下文；
/// 签名不符与已过期一律按不存在处理，不泄露对象是否存在。
/// </summary>
public interface IFileTransferGateway
{
    /// <summary>校验上传签名后写入请求体；已存在的对象不覆盖。</summary>
    ValueTask<FileTransferResult> ReceiveAsync(FileTransferRequest request, Stream content, CancellationToken cancellationToken = default);
    /// <summary>校验下载签名后打开对象；签名不符、过期或对象不存在返回 null。</summary>
    ValueTask<FileTransferDownload?> OpenAsync(FileTransferRequest request, CancellationToken cancellationToken = default);
}

/// <summary>一次直传 / 直下请求：对象键取自路径，查询参数与请求头原样传入，由网关校验签名。</summary>
public sealed record FileTransferRequest(string ObjectKey, IReadOnlyDictionary<string, string> Query, IReadOnlyDictionary<string, string> Headers);

/// <summary>直传结果，宿主映射为 HTTP 状态码。</summary>
public enum FileTransferResult
{
    /// <summary>已写入（201）。</summary>
    Stored,
    /// <summary>签名不符、已过期或缺少必需参数（404，不区分原因）。</summary>
    Rejected,
    /// <summary>对象已存在，不覆盖（409）。</summary>
    Conflict,
    /// <summary>请求体超过签名里的上限（413）。</summary>
    TooLarge
}

/// <summary>直下内容：响应头取自签名参数与存储时的嗅探类型。</summary>
public sealed record FileTransferDownload(Stream Content, long Length, string ContentType, string ContentDisposition);

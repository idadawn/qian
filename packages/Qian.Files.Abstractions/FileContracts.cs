using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Qian.Files;

/// <summary>file_objects.status 的取值。</summary>
public static class FileStatus
{
    /// <summary>已签发上传地址、尚未完成校验；按声明大小预占配额，过了完成时限不再计入。</summary>
    public const string Uploading = "uploading";
    /// <summary>已校验大小、SHA-256 与类型，可以签发下载地址。</summary>
    public const string Available = "available";
    /// <summary>完成时校验不符而隔离；不能下载，计入配额直至软删。</summary>
    public const string Quarantined = "quarantined";
    /// <summary>已软删，配额立即释放；对象的物理删除由保留期作业负责。</summary>
    public const string Deleted = "deleted";
}

/// <summary>隔离原因，写入 file_objects.quarantine_reason。</summary>
public static class FileQuarantineReason
{
    /// <summary>实际大小与声明不符。</summary>
    public const string SizeMismatch = "size_mismatch";
    /// <summary>服务端自算的 SHA-256 与声明不符。</summary>
    public const string ChecksumMismatch = "checksum_mismatch";
    /// <summary>服务端嗅探出的类型不在上传策略允许范围内。</summary>
    public const string TypeNotAllowed = "type_not_allowed";
}

/// <summary>qian.files 的权限点（R9 全名）；与 plugins/qian.files/metadata/permissions.json 一致。</summary>
public static class FilesPermissions
{
    /// <summary>读取文件元数据、配额与签发下载地址。</summary>
    public const string Read = "qian.files:file:read";
    /// <summary>申请上传与完成上传。</summary>
    public const string Write = "qian.files:file:write";
    /// <summary>软删文件。</summary>
    public const string Delete = "qian.files:file:delete";
}

/// <summary>qian.files 的能力 ID（R23：&lt;plugin&gt;/&lt;name&gt;）。</summary>
public static class FilesCapabilities
{
    /// <summary>申请上传。</summary>
    public const string PresignUpload = "qian.files/presign-upload";
    /// <summary>完成上传。</summary>
    public const string CompleteUpload = "qian.files/complete-upload";
    /// <summary>读取文件元数据。</summary>
    public const string Get = "qian.files/get";
    /// <summary>签发下载地址。</summary>
    public const string DownloadUrl = "qian.files/download-url";
    /// <summary>软删文件。</summary>
    public const string Delete = "qian.files/delete";
    /// <summary>读取配额。</summary>
    public const string Quota = "qian.files/quota";
}

/// <summary>
/// 上传策略：用途 Purpose 形如 &lt;组件 ID&gt;/&lt;名称&gt;，组件 ID 即存储键里的 plugin_id 段；AllowedTypes 为 MIME 类型或 type/* 通配；
/// MaxSize 为单个文件上限（字节）；MaxCount 非空时为每个租户该用途的在用文件数上限。
/// </summary>
public sealed record UploadPolicy(string Purpose, IReadOnlyList<string> AllowedTypes, long MaxSize, int? MaxCount = null);

/// <summary>申请上传：Size 与 ContentType 为客户端声明，服务端只拿来预占配额与绑定上传地址，完成时自行校验。</summary>
public sealed record PresignUploadRequest(Guid IdempotencyKey, string Purpose, string Name, long Size, string ContentType, string Sha256);

/// <summary>完成上传。</summary>
public sealed record CompleteUploadRequest(Guid IdempotencyKey, Guid FileId);

/// <summary>软删文件。</summary>
public sealed record DeleteFileRequest(Guid IdempotencyKey, Guid FileId);

/// <summary>签发的上传地址：按 Method 带 RequiredHeaders 把内容发到 Url，过期即失效。</summary>
public sealed record PresignedUpload(Guid FileId, Uri Url, string Method, IReadOnlyDictionary<string, string> RequiredHeaders, DateTimeOffset ExpiresAt);

/// <summary>签发的下载地址；响应恒定 Content-Disposition: attachment。</summary>
public sealed record DownloadTicket(Uri Url, DateTimeOffset ExpiresAt);

/// <summary>租户文件配额：上限来自租户配置覆盖或组件配置默认；用量为未软删文件的声明大小之和。</summary>
public sealed record FileQuota(long LimitBytes, long UsedBytes, long FileCount);

/// <summary>文件元数据；存储键由服务端生成，恒以 tenants/{tenant_id}/ 开头。</summary>
public sealed record FileDescriptor(
    Guid Id, string TenantId, string PluginId, string Purpose, string Name, long Size, string ContentType, string Sha256,
    string StorageKey, string Provider, string Status, string ScanStatus, DateTimeOffset CreatedAt)
{
    /// <summary>隔离原因（<see cref="FileQuarantineReason"/>）；未隔离为 null。</summary>
    public string? QuarantineReason { get; init; }
    /// <summary>软删时刻；未删为 null。</summary>
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>文件契约的源生成序列化入口（camelCase、拒绝未声明成员、必填构造参数），HTTP 表面与宿主共用。</summary>
public static class FilesContractJson
{
    private static FilesContractJsonContext Context { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    });
    /// <summary>申请上传的输入。</summary>
    public static JsonTypeInfo<PresignUploadRequest> PresignUploadRequestTypeInfo => Context.PresignUploadRequest;
    /// <summary>签发的上传地址。</summary>
    public static JsonTypeInfo<PresignedUpload> PresignedUploadTypeInfo => Context.PresignedUpload;
    /// <summary>签发的下载地址。</summary>
    public static JsonTypeInfo<DownloadTicket> DownloadTicketTypeInfo => Context.DownloadTicket;
    /// <summary>文件元数据。</summary>
    public static JsonTypeInfo<FileDescriptor> FileDescriptorTypeInfo => Context.FileDescriptor;
    /// <summary>租户文件配额。</summary>
    public static JsonTypeInfo<FileQuota> FileQuotaTypeInfo => Context.FileQuota;
    /// <summary>上传策略（组件配置 policies[] 的项）。</summary>
    public static JsonTypeInfo<UploadPolicy> UploadPolicyTypeInfo => Context.UploadPolicy;
}

[JsonSerializable(typeof(PresignUploadRequest))]
[JsonSerializable(typeof(PresignedUpload))]
[JsonSerializable(typeof(DownloadTicket))]
[JsonSerializable(typeof(FileDescriptor))]
[JsonSerializable(typeof(FileQuota))]
[JsonSerializable(typeof(UploadPolicy))]
internal sealed partial class FilesContractJsonContext : JsonSerializerContext;

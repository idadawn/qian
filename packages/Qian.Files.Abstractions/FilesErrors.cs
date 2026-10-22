using Qian.Runtime.Errors;

namespace Qian.Files;

/// <summary>文件领域的错误词汇，归属 qian.files；跨租户与不存在一律复用 NOT_FOUND，不另立错误码。</summary>
public static class FilesErrors
{
    /// <summary>租户字节配额不足（定义与 contracts/v0 §3.6 的示例一致）。</summary>
    public static readonly ErrorCode QuotaExceeded = Business("QUOTA_EXCEEDED", 422,
        "超出配额", "文件大小超出租户配额（剩余 {remaining} 字节）。",
        "Quota exceeded", "The file exceeds the tenant quota ({remaining} bytes left).");
    /// <summary>声明或嗅探出的类型不在上传策略允许范围内。</summary>
    public static readonly ErrorCode TypeNotAllowed = Business("FILE_TYPE_NOT_ALLOWED", 422,
        "文件类型不允许", "文件类型 {contentType} 不在该用途的上传策略允许范围内。",
        "File type not allowed", "The file type {contentType} is not allowed by the upload policy of this purpose.");
    /// <summary>声明大小超过上传策略的单文件上限。</summary>
    public static readonly ErrorCode TooLarge = Business("FILE_TOO_LARGE", 422,
        "文件过大", "文件大小超过该用途的上限 {maxSize} 字节。",
        "File too large", "The file exceeds the limit of {maxSize} bytes for this purpose.");
    /// <summary>该用途的在用文件数已达上传策略上限。</summary>
    public static readonly ErrorCode CountExceeded = Business("FILE_COUNT_EXCEEDED", 422,
        "文件数量超限", "该用途的文件数量已达到上限 {maxCount}。",
        "Too many files", "The number of files for this purpose has reached the limit of {maxCount}.");
    /// <summary>完成上传时实际大小或 SHA-256 与声明不符，文件已隔离。</summary>
    public static readonly ErrorCode ChecksumMismatch = Business("FILE_CHECKSUM_MISMATCH", 422,
        "文件校验不符", "上传内容的大小或 SHA-256 与声明不一致，文件已隔离。",
        "File checksum mismatch", "The uploaded content does not match the declared size or SHA-256; the file has been quarantined.");
    /// <summary>上传的完成时限已过，需要重新申请上传。</summary>
    public static readonly ErrorCode UploadExpired = Business("FILE_UPLOAD_EXPIRED", 409,
        "上传已过期", "上传已超过完成时限，请重新申请上传。",
        "Upload expired", "The upload is past its completion deadline; request a new upload.");
    /// <summary>完成上传时还没有收到文件内容。</summary>
    public static readonly ErrorCode UploadIncomplete = Business("FILE_UPLOAD_INCOMPLETE", 409,
        "上传未完成", "尚未收到文件内容，请先按上传地址发送内容再完成上传。",
        "Upload incomplete", "No content has been received yet; send the content to the upload URL before completing the upload.");
    /// <summary>文件当前状态不能签发下载地址（上传中或已隔离）。</summary>
    public static readonly ErrorCode NotAvailable = Business("FILE_NOT_AVAILABLE", 409,
        "文件不可用", "文件当前状态为 {status}，不能签发下载地址。",
        "File not available", "The file is {status} and cannot be downloaded.");

    /// <summary>可登记和导出的完整错误词汇。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = Array.AsReadOnly(new[]
        { QuotaExceeded, TypeNotAllowed, TooLarge, CountExceeded, ChecksumMismatch, UploadExpired, UploadIncomplete, NotAvailable });

    /// <summary>成组登记本组件错误码；失败撤销已登记项，返回可交给 ctx.Effect 的幂等释放句柄。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var code in All) registrations.Add(catalog.Register(code));
            return new Registration(registrations);
        }
        catch
        {
            foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose();
            throw;
        }
    }

    private static ErrorCode Business(string code, int status, string titleZh, string messageZh, string titleEn, string messageEn) =>
        ErrorCode.Business(code, status, "qian.files",
            new Dictionary<string, string> { ["zh-CN"] = titleZh, ["en"] = titleEn },
            new Dictionary<string, string> { ["zh-CN"] = messageZh, ["en"] = messageEn });

    private sealed class Registration(List<IDisposable> entries) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var entry in entries.AsEnumerable().Reverse()) entry.Dispose();
        }
    }
}

using System.Data.Common;
using Kun.Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Files;

// 组件运行时状态：时钟、选用的后端、两个注册表、配额默认值与租户配置读取入口。由 FilesPlugin 在 Apply 中建立，handler 经 FileService 取用。
internal sealed class FilesRuntime(TimeProvider clock, FileBackendRegistry backends, UploadPolicyRegistry policies, long defaultQuota, Func<ITenantSettings?> settings)
{
    internal static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);
    // 上传地址到期后仍给完成上传留的时限：到期前一刻才传完的内容也能完成；过了时限的预占不再计入配额。
    internal static readonly TimeSpan CompleteGrace = TimeSpan.FromMinutes(10);
    private IFileBackend? _backend;

    internal TimeProvider Clock => clock;
    internal FileBackendRegistry Backends => backends;
    internal UploadPolicyRegistry Policies => policies;
    internal IFileBackend Backend => _backend ?? throw new InvalidOperationException("文件后端尚未选用。");

    internal void Use(IFileBackend backend) => _backend = backend;

    // 上限：租户配置覆盖（ITenantSettings，经 TEN-004 写入 Registry 合成配置）优先，其次组件配置默认。
    // 宿主没有 Registry（参考宿主、独立模式）或 Registry 里没有 qian.files 条目时，租户覆盖不可用，回退组件配置。
    internal async ValueTask<long> QuotaLimitAsync(CancellationToken cancellationToken)
    {
        if (settings() is { } tenant)
        {
            try
            {
                if (await tenant.Get<long?>("qian.files", "quota.perTenantBytes", cancellationToken).ConfigureAwait(false) is { } value && value >= 0) return value;
            }
            catch (ServiceNotReadyException) { }
            catch (KeyNotFoundException) { }
            catch (QianErrorException error) when (error.Error.Code == PlatformErrors.NotFound) { }
        }
        return defaultQuota;
    }

    // 在用：未软删的 available / quarantined，以及完成时限内的上传预占（按声明大小）。
    internal static IQueryable<FileObject> Counted(IQueryable<FileObject> files, DateTime now)
    {
        var cutoff = now - CompleteGrace;
        return files.Where(file => file.Status == FileStatus.Available || file.Status == FileStatus.Quarantined ||
            (file.Status == FileStatus.Uploading && file.UploadExpiresAt > cutoff));
    }
}

// 存储键由服务端生成：tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}；上传暂存键与其同目录，以 .upload 结尾。
internal static class FileKeys
{
    internal static string Directory(string tenant, string plugin, DateTime now) =>
        "tenants/" + tenant + "/" + plugin + "/" + now.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture) + "/" +
        now.ToString("MM", System.Globalization.CultureInfo.InvariantCulture) + "/";
    internal static string Storage(string directory, Guid id, string contentType) => directory + id.ToString("D") + ContentSniffer.Extension(contentType);
    internal static string Upload(string storageKey, Guid id) => storageKey[..(storageKey.LastIndexOf('/') + 1)] + id.ToString("D") + LocalFileBackend.UploadSuffix;
}

// IFileService 的实现：每个操作作为 qian.files 的能力经 Kun Mediator 发送，进入 SDK 命令管道（租户、授权、审计与同事务 Outbox）。
internal sealed class FileService(ISender sender, FilesRuntime runtime) : IFileService
{
    internal FilesRuntime Runtime => runtime;
    public IUploadPolicyRegistry Policies => runtime.Policies;
    public IFileBackendRegistry Backends => runtime.Backends;
    public IFileTransferGateway? Transfer => runtime.Backend.Transfer;

    public ValueTask<PresignedUpload> PresignUploadAsync(PresignUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Retry(() => sender.Send(new PresignUploadCommand(request.IdempotencyKey, request.Purpose, request.Name, request.Size, request.ContentType, request.Sha256), cancellationToken), cancellationToken);
    }

    public async ValueTask<FileDescriptor> CompleteAsync(CompleteUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = await Retry(() => sender.Send(new CompleteUploadCommand(request.IdempotencyKey, request.FileId), cancellationToken), cancellationToken).ConfigureAwait(false);
        if (file.Status == FileStatus.Quarantined) throw QuarantineError(file);
        // 能力事务已提交：暂存对象只是上传通道，删除失败不影响结果，残留由保留期作业清理（S1-FILE-003）。
        try { await runtime.Backend.DeleteAsync(FileKeys.Upload(file.StorageKey, file.Id), CancellationToken.None).ConfigureAwait(false); }
        catch (IOException) { }
        return file;
    }

    public ValueTask<FileDescriptor> GetAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        sender.Send(new GetFileQuery(fileId), cancellationToken);

    public ValueTask<DownloadTicket> GetDownloadUrlAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        sender.Send(new DownloadUrlQuery(fileId), cancellationToken);

    public ValueTask<FileDescriptor> DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Retry(() => sender.Send(new DeleteFileCommand(request.IdempotencyKey, request.FileId), cancellationToken), cancellationToken);
    }

    public ValueTask<FileQuota> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        sender.Send(new QuotaQuery(), cancellationToken);

    // 完成时隔离的文件：状态已随能力事务提交，再以对应业务错误告知调用方。
    internal static QianErrorException QuarantineError(FileDescriptor file) => new(file.QuarantineReason == FileQuarantineReason.TypeNotAllowed
        ? new QianError(FilesErrors.TypeNotAllowed, new Dictionary<string, string> { ["contentType"] = file.ContentType })
        : new QianError(FilesErrors.ChecksumMismatch));

    // SERIALIZABLE 冲突（配额与幂等键的并发判定）整笔回滚，按同一幂等键重发；用尽后返回 409。
    private static async ValueTask<T> Retry<T>(Func<ValueTask<T>> send, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await send().ConfigureAwait(false); }
            catch (Exception error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(10 * (attempt + 1), cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (Retryable(error)) { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        }
    }

    // Npgsql 的执行策略会把瞬态异常再包一层 InvalidOperationException；只拆已知框架包装，按明确 SQLSTATE 决定是否整笔重发。
    internal static bool Retryable(Exception error)
    {
        var current = error;
        while (current is DbUpdateException or InvalidOperationException && current.InnerException is { } inner) current = inner;
        return current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } ||
            current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: FilesDbContext.IdempotencyIndex } ||
            current is DbException { SqlState: "40001" or "40P01" };
    }
}

// R38 注册表 seam 的进程内实现：同名独占，释放句柄可重复调用。
internal sealed class UploadPolicyRegistry : IUploadPolicyRegistry
{
    private readonly Dictionary<string, UploadPolicy> _items = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public IDisposable Register(string purpose, UploadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var invalid = policy.Purpose != purpose ? "purpose" : FilesConfiguration.ValidatePolicy(policy);
        if (invalid is not null) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/" + invalid });
        var copy = policy with { AllowedTypes = Array.AsReadOnly(policy.AllowedTypes.ToArray()) };
        lock (_gate)
            if (!_items.TryAdd(purpose, copy)) throw new QianErrorException(new QianError(PlatformErrors.RegistrationConflict, Conflict("Files.Policies", purpose)));
        return new Release(() => { lock (_gate) if (_items.TryGetValue(purpose, out var current) && ReferenceEquals(current, copy)) _items.Remove(purpose); });
    }
    public UploadPolicy? Find(string purpose) { lock (_gate) return _items.GetValueOrDefault(purpose); }
    public IReadOnlyList<UploadPolicy> List() { lock (_gate) return Array.AsReadOnly(_items.Values.OrderBy(item => item.Purpose, StringComparer.Ordinal).ToArray()); }

    internal static Dictionary<string, string> Conflict(string key, string name) => new() { ["key"] = key + ":" + name, ["owner"] = "qian.files" };
}

internal sealed class FileBackendRegistry : IFileBackendRegistry
{
    private readonly Dictionary<string, IFileBackendFactory> _items = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public IDisposable Register(string name, IFileBackendFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrEmpty(name) || name.Length > 32 || !name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') || !char.IsAsciiLetterLower(name[0]))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/name" });
        lock (_gate)
            if (!_items.TryAdd(name, factory)) throw new QianErrorException(new QianError(PlatformErrors.RegistrationConflict, UploadPolicyRegistry.Conflict("Files.Backends", name)));
        return new Release(() => { lock (_gate) if (_items.TryGetValue(name, out var current) && ReferenceEquals(current, factory)) _items.Remove(name); });
    }
    public IFileBackendFactory? Find(string name) { lock (_gate) return _items.GetValueOrDefault(name); }
    public IReadOnlyList<string> List() { lock (_gate) return Array.AsReadOnly(_items.Keys.Order(StringComparer.Ordinal).ToArray()); }
}

internal sealed class Release(Action release) : IDisposable
{
    private int _disposed;
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) release(); }
}

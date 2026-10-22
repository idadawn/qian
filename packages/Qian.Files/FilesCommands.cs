using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Files;

[Capability("presign-upload", typeof(PresignedUpload), typeof(FilesCommandJson),
    Description = "按上传策略与租户配额申请一个文件的直传地址（不超过 15 分钟）；存储键由服务端生成，重复提交同一幂等键返回同一文件。",
    SideEffect = "write", Idempotent = true, RequiredPermission = FilesPermissions.Write, AgentExpose = true)]
internal sealed partial record PresignUploadCommand(Guid IdempotencyKey, string Purpose, string Name, long Size, string ContentType, string Sha256);

[Capability("complete-upload", typeof(FileDescriptor), typeof(FilesCommandJson),
    Description = "完成上传：服务端自算大小与 SHA-256 并嗅探类型，相符则文件可用，不符则隔离；重复完成返回同一结果。",
    SideEffect = "write", Idempotent = true, RequiredPermission = FilesPermissions.Write, AgentExpose = true)]
internal sealed partial record CompleteUploadCommand(Guid IdempotencyKey, Guid FileId);

[Capability("get", typeof(FileDescriptor), typeof(FilesCommandJson),
    Description = "查看当前租户一个文件的元数据与状态。", RequiredPermission = FilesPermissions.Read, AgentExpose = true)]
internal sealed partial record GetFileQuery(Guid FileId);

[Capability("download-url", typeof(DownloadTicket), typeof(FilesCommandJson),
    Description = "为当前租户一个可用文件签发下载地址（不超过 5 分钟，恒定以附件下载），每次签发都记审计。",
    Audit = true, RequiredPermission = FilesPermissions.Read, AgentExpose = true)]
internal sealed partial record DownloadUrlQuery(Guid FileId);

[Capability("delete", typeof(FileDescriptor), typeof(FilesCommandJson),
    Description = "软删当前租户的一个文件并立即释放配额；对象按保留期再物理删除。",
    SideEffect = "destructive", Idempotent = true, RequiresConfirmation = true, RequiredPermission = FilesPermissions.Delete, AgentExpose = true)]
internal sealed partial record DeleteFileCommand(Guid IdempotencyKey, Guid FileId);

[Capability("quota", typeof(FileQuota), typeof(FilesCommandJson),
    Description = "查看当前租户的文件配额上限、已用字节与文件数。", RequiredPermission = FilesPermissions.Read, AgentExpose = true)]
internal sealed partial record QuotaQuery;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(PresignUploadCommand))]
[JsonSerializable(typeof(CompleteUploadCommand))]
[JsonSerializable(typeof(GetFileQuery))]
[JsonSerializable(typeof(DownloadUrlQuery))]
[JsonSerializable(typeof(DeleteFileCommand))]
[JsonSerializable(typeof(QuotaQuery))]
[JsonSerializable(typeof(PresignedUpload))]
[JsonSerializable(typeof(FileDescriptor))]
[JsonSerializable(typeof(DownloadTicket))]
[JsonSerializable(typeof(FileQuota))]
internal sealed partial class FilesCommandJson : JsonSerializerContext;

// handler 取组件运行时的入口：宿主登记能力时给出的同一个插件上下文来源；解析不到 qian.files 的 Provider 时与不存在同为 NOT_FOUND。
internal sealed class FilesCapabilitySource(Func<IServiceProvider, IPluginContext> source)
{
    internal FilesRuntime Resolve(IServiceProvider services)
    {
        try
        {
            return source(services).TryInject<IFileService>(FilesDefinition.Key) is FileService files ? files.Runtime
                : throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException or KeyNotFoundException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }
}

internal sealed class PresignUploadHandler(CommandDataSession<FilesDbContext> work, FilesCapabilitySource source, IServiceProvider services, ITenantContext tenant)
    : IRequestHandler<PresignUploadCommand, PresignedUpload>
{
    public async ValueTask<PresignedUpload> Handle(PresignUploadCommand request, CancellationToken cancellationToken)
    {
        var runtime = source.Resolve(services); var db = work.Context;
        var tenantId = tenant.TenantId ?? throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var policy = Validate(request, runtime, out var name, out var contentType, out var sha256);
        var now = runtime.Clock.GetUtcNow().UtcDateTime;
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', request.Purpose, name, request.Size, contentType, sha256))));
        var existing = await db.Files.SingleOrDefaultAsync(file => file.IdempotencyKey == request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.RequestHash != fingerprint || existing.Status != FileStatus.Uploading) throw new QianErrorException(new QianError(PlatformErrors.Conflict));
            if (existing.UploadExpiresAt <= now) throw new QianErrorException(new QianError(FilesErrors.UploadExpired));
            return await Sign(runtime, existing, existing.UploadExpiresAt - now, cancellationToken).ConfigureAwait(false);
        }
        if (policy.MaxCount is { } maxCount &&
            await FilesRuntime.Counted(db.Files, now).CountAsync(file => file.Purpose == policy.Purpose, cancellationToken).ConfigureAwait(false) >= maxCount)
            throw new QianErrorException(new QianError(FilesErrors.CountExceeded, new Dictionary<string, string> { ["maxCount"] = Text(maxCount) }) { Pointer = ErrorPointer.Input("purpose") });
        var limit = await runtime.QuotaLimitAsync(cancellationToken).ConfigureAwait(false);
        var used = await FilesRuntime.Counted(db.Files, now).SumAsync(file => file.Size, cancellationToken).ConfigureAwait(false);
        if (used + request.Size > limit)
            throw new QianErrorException(new QianError(FilesErrors.QuotaExceeded, new Dictionary<string, string> { ["remaining"] = Text(Math.Max(0, limit - used)) })
            { Pointer = ErrorPointer.Input("size") });
        var id = Guid.CreateVersion7(); var storageKey = FileKeys.Storage(FileKeys.Directory(tenantId, FilesConfiguration.OwnerOf(policy.Purpose), now), id, contentType);
        var file = new FileObject(id)
        {
            PluginId = FilesConfiguration.OwnerOf(policy.Purpose), Purpose = policy.Purpose, Name = name, Size = request.Size, ContentType = contentType, Sha256 = sha256,
            StorageKey = storageKey, UploadKey = FileKeys.Upload(storageKey, id), Provider = runtime.Backend.Name, Status = FileStatus.Uploading,
            UploadExpiresAt = now + FilesRuntime.UploadLifetime, IdempotencyKey = request.IdempotencyKey, RequestHash = fingerprint
        };
        db.Files.Add(file);
        return await Sign(runtime, file, FilesRuntime.UploadLifetime, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<PresignedUpload> Sign(FilesRuntime runtime, FileObject file, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var transfer = await runtime.Backend.CreateUploadAsync(file.UploadKey, file.ContentType, file.Size, lifetime, cancellationToken).ConfigureAwait(false);
        return new PresignedUpload(file.Id, transfer.Url, transfer.Method, transfer.Headers, transfer.ExpiresAt);
    }

    // 输入 Schema 表达不了的规则：用途须已登记，文件名为单段名称，类型须为合法 MIME 且在策略内，SHA-256 为 64 位十六进制。
    private static UploadPolicy Validate(PresignUploadCommand request, FilesRuntime runtime, out string name, out string contentType, out string sha256)
    {
        if (request.IdempotencyKey == Guid.Empty) throw Invalid("idempotencyKey");
        var policy = runtime.Policies.Find(request.Purpose ?? "");
        if (policy is null)
        {
            var known = runtime.Policies.List().Take(AllowedValues.MaxCount).Select(item => item.Purpose).ToArray();
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
            {
                Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("purpose")) { Allowed = known.Length == 0 ? null : AllowedValues.Of(known) }]
            });
        }
        name = (request.Name ?? "").Trim();
        if (name.Length is 0 or > 255 || name.Any(c => char.IsControl(c) || c is '/' or '\\') || name is "." or "..") throw Invalid("name");
        if (request.Size < 1) throw Invalid("size");
        if (request.Size > policy.MaxSize)
            throw new QianErrorException(new QianError(FilesErrors.TooLarge, new Dictionary<string, string> { ["maxSize"] = Text(policy.MaxSize) }) { Pointer = ErrorPointer.Input("size") });
        contentType = ContentSniffer.Normalize(request.ContentType) ?? throw Invalid("contentType");
        if (!ContentSniffer.Allows(policy, contentType))
            throw new QianErrorException(new QianError(FilesErrors.TypeNotAllowed, new Dictionary<string, string> { ["contentType"] = contentType })
            {
                Pointer = ErrorPointer.Input("contentType"), Allowed = AllowedValues.Of(policy.AllowedTypes.Take(AllowedValues.MaxCount).ToArray())
            });
        sha256 = (request.Sha256 ?? "").ToLowerInvariant();
        if (sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigitLower)) throw Invalid("sha256");
        return policy;
    }

    internal static QianErrorException Invalid(string member) => new(new QianError(PlatformErrors.InputInvalid)
    {
        Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input(member))]
    });

    internal static string Text(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class CompleteUploadHandler(CommandDataSession<FilesDbContext> work, FilesCapabilitySource source, IServiceProvider services)
    : IRequestHandler<CompleteUploadCommand, FileDescriptor>
{
    public async ValueTask<FileDescriptor> Handle(CompleteUploadCommand request, CancellationToken cancellationToken)
    {
        if (request.IdempotencyKey == Guid.Empty) throw PresignUploadHandler.Invalid("idempotencyKey");
        var runtime = source.Resolve(services);
        var file = await work.Context.Files.SingleOrDefaultAsync(row => row.Id == request.FileId, cancellationToken).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        if (file.Status != FileStatus.Uploading) return file.Describe();
        if (runtime.Clock.GetUtcNow().UtcDateTime > file.UploadExpiresAt + FilesRuntime.CompleteGrace) throw new QianErrorException(new QianError(FilesErrors.UploadExpired));
        await UploadVerifier.VerifyAsync(runtime, file, cancellationToken).ConfigureAwait(false);
        if (file.Status == FileStatus.Available)
            work.Outbox.Enqueue(new FileObjectCreatedEvent(file.Id, file.PluginId, file.Purpose, file.Size, file.ContentType, file.Sha256));
        return file.Describe();
    }
}

internal sealed class GetFileHandler(CommandDataSession<FilesDbContext> work) : IRequestHandler<GetFileQuery, FileDescriptor>
{
    public async ValueTask<FileDescriptor> Handle(GetFileQuery request, CancellationToken cancellationToken) =>
        (await work.Context.Files.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.FileId, cancellationToken).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound))).Describe();
}

internal sealed class DownloadUrlHandler(CommandDataSession<FilesDbContext> work, FilesCapabilitySource source, IServiceProvider services)
    : IRequestHandler<DownloadUrlQuery, DownloadTicket>
{
    public async ValueTask<DownloadTicket> Handle(DownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var runtime = source.Resolve(services);
        var file = await work.Context.Files.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.FileId, cancellationToken).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        if (file.Status != FileStatus.Available)
            throw new QianErrorException(new QianError(FilesErrors.NotAvailable, new Dictionary<string, string> { ["status"] = file.Status }));
        var transfer = await runtime.Backend.CreateDownloadAsync(file.StorageKey, file.Name, FilesRuntime.DownloadLifetime, cancellationToken).ConfigureAwait(false);
        return new DownloadTicket(transfer.Url, transfer.ExpiresAt);
    }
}

internal sealed class DeleteFileHandler(CommandDataSession<FilesDbContext> work) : IRequestHandler<DeleteFileCommand, FileDescriptor>
{
    public async ValueTask<FileDescriptor> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        if (request.IdempotencyKey == Guid.Empty) throw PresignUploadHandler.Invalid("idempotencyKey");
        // 软删的行被全局过滤器隐藏：重复删除返回 404，不会再次产生副作用（QIAN0004 不允许绕过过滤器取回原行）。
        var file = await work.Context.Files.SingleOrDefaultAsync(row => row.Id == request.FileId, cancellationToken).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        var published = file.Status == FileStatus.Available;
        file.SoftDelete();
        if (published) work.Outbox.Enqueue(new FileObjectDeletedEvent(file.Id, file.PluginId, file.Purpose));
        return file.Describe();
    }
}

internal sealed class QuotaHandler(CommandDataSession<FilesDbContext> work, FilesCapabilitySource source, IServiceProvider services) : IRequestHandler<QuotaQuery, FileQuota>
{
    public async ValueTask<FileQuota> Handle(QuotaQuery request, CancellationToken cancellationToken)
    {
        var runtime = source.Resolve(services);
        var counted = FilesRuntime.Counted(work.Context.Files.AsNoTracking(), runtime.Clock.GetUtcNow().UtcDateTime);
        var used = await counted.SumAsync(file => file.Size, cancellationToken).ConfigureAwait(false);
        var count = await counted.LongCountAsync(cancellationToken).ConfigureAwait(false);
        return new FileQuota(await runtime.QuotaLimitAsync(cancellationToken).ConfigureAwait(false), used, count);
    }
}

// 完成上传的单遍校验：先读头部嗅探类型，再把暂存对象边算 SHA-256 边写到正式存储键。写进正式键的字节就是被校验的字节，
// 之后对暂存键的任何写入都影响不到已完成的文件。不符时删除正式对象并转入隔离（暂存对象留作证据，由保留期作业清理）。
internal static class UploadVerifier
{
    internal static async ValueTask VerifyAsync(FilesRuntime runtime, FileObject file, CancellationToken cancellationToken)
    {
        Stream staging;
        try { staging = await runtime.Backend.OpenReadAsync(file.UploadKey, cancellationToken).ConfigureAwait(false); }
        catch (FileNotFoundException) { throw new QianErrorException(new QianError(FilesErrors.UploadIncomplete)); }
        await using (staging.ConfigureAwait(false))
        {
            var head = new byte[ContentSniffer.HeadLength]; var length = 0;
            while (length < head.Length)
            {
                var read = await staging.ReadAsync(head.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                length += read;
            }
            var detected = ContentSniffer.Detect(head.AsSpan(0, length), file.ContentType);
            var policy = runtime.Policies.Find(file.Purpose);
            if (policy is null || !ContentSniffer.Allows(policy, detected))
            {
                Quarantine(file, FileQuarantineReason.TypeNotAllowed, detected);
                return;
            }
            var storageKey = FileKeys.Storage(file.StorageKey[..(file.StorageKey.LastIndexOf('/') + 1)], file.Id, detected);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var content = new HashingStream(head.AsMemory(0, length), staging, hash);
            try
            {
                await runtime.Backend.WriteAsync(storageKey, content, detected, file.Size, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (content.Length > file.Size)
            {
                Quarantine(file, FileQuarantineReason.SizeMismatch, detected);
                return;
            }
            var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            var reason = content.Length != file.Size ? FileQuarantineReason.SizeMismatch : sha256 != file.Sha256 ? FileQuarantineReason.ChecksumMismatch : null;
            if (reason is not null)
            {
                await runtime.Backend.DeleteAsync(storageKey, cancellationToken).ConfigureAwait(false);
                Quarantine(file, reason, detected);
                return;
            }
            file.StorageKey = storageKey; file.ContentType = detected; file.Status = FileStatus.Available;
        }
    }

    private static void Quarantine(FileObject file, string reason, string detected)
    {
        file.Status = FileStatus.Quarantined; file.QuarantineReason = reason; file.ContentType = detected;
    }
}

// 先吐出已读的头部，再读其余内容；读到的每个字节都进增量哈希。Length 为已读字节数。
internal sealed class HashingStream(ReadOnlyMemory<byte> head, Stream rest, IncrementalHash hash) : Stream
{
    private ReadOnlyMemory<byte> _head = head;
    private long _length;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _length; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read;
        if (!_head.IsEmpty)
        {
            read = Math.Min(buffer.Length, _head.Length);
            _head[..read].CopyTo(buffer); _head = _head[read..];
        }
        else read = await rest.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        hash.AppendData(buffer.Span[..read]); _length += read;
        return read;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Storage;
using Kun.Storage.Local;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors;

namespace Qian.Files;

/// <summary>
/// local 后端工厂（Kun.Storage.Local，仅开发与单节点）。storage.root 为对象根目录（绝对路径），storage.endpoint 为宿主映射直传 / 直下端点的公开基址。
/// Kun 本地预签名地址不带签名，本后端在其查询参数上追加 HMAC-SHA256 签名并由 <see cref="IFileTransferGateway"/> 校验。
/// </summary>
internal sealed class LocalFileBackendFactory(ReadOnlyMemory<byte> signingKey) : IFileBackendFactory
{
    internal const string Name = "local";

    public IFileBackend Create(FileBackendContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Storage.ValueKind != JsonValueKind.Object) throw Invalid("storage");
        string? root = null; Uri? endpoint = null;
        foreach (var property in context.Storage.EnumerateObject())
        {
            switch (property.Name)
            {
                case "kind": break;
                case "root" when property.Value.ValueKind == JsonValueKind.String && Path.IsPathFullyQualified(property.Value.GetString()!):
                    root = Path.GetFullPath(property.Value.GetString()!); break;
                case "endpoint" when property.Value.ValueKind == JsonValueKind.String &&
                    Uri.TryCreate(property.Value.GetString(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
                    uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0:
                    endpoint = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/"); break;
                default: throw Invalid(property.Name);
            }
        }
        if (root is null) throw Invalid("root");
        if (endpoint is null) throw Invalid("endpoint");
        var logger = context.Services.GetService<ILoggerFactory>()?.CreateLogger<LocalFileSystemStorage>() ?? NullLogger<LocalFileSystemStorage>.Instance;
        var storage = new LocalFileSystemStorage(Options.Create(new LocalFileStorageOptions
        {
            RootPath = root, PublicBaseUrl = endpoint, DefaultPresignedUrlLifetime = FilesRuntime.UploadLifetime
        }), logger, context.Clock);
        return new LocalFileBackend(storage, signingKey.Length == 0 ? RandomNumberGenerator.GetBytes(32) : signingKey.ToArray(), context.Clock);
    }

    private static QianErrorException Invalid(string member) =>
        new(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/storage/" + member });
}

internal sealed class LocalFileBackend(LocalFileSystemStorage storage, byte[] key, TimeProvider clock) : IFileBackend, IFileTransferGateway
{
    internal const string UploadSuffix = ".upload";
    private const string SignatureParameter = "signature";
    private const string MaxBytesHeader = "X-Kun-Max-Bytes";

    public string Name => LocalFileBackendFactory.Name;
    public IFileTransferGateway? Transfer => this;

    public async ValueTask<PresignedTransfer> CreateUploadAsync(string objectKey, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        if (!objectKey.EndsWith(UploadSuffix, StringComparison.Ordinal)) throw new ArgumentException("直传只允许写上传暂存键。", nameof(objectKey));
        var presigned = await storage.GetPresignedUploadAsync(objectKey, contentType, new UploadConstraints(maxBytes), lifetime, cancellationToken).ConfigureAwait(false);
        var query = Query(presigned.Url);
        var signature = Sign("PUT", objectKey, query["expires"], query["contentType"], presigned.RequiredHeaders[MaxBytesHeader]);
        var headers = new Dictionary<string, string>(presigned.RequiredHeaders, StringComparer.OrdinalIgnoreCase);
        return new PresignedTransfer(Append(presigned.Url, signature), "PUT", headers, presigned.ExpiresAt);
    }

    // 恒定 ForceDownload：不论存储类型，下载地址都带 attachment，且没有任何开关能改。
    public async ValueTask<PresignedTransfer> CreateDownloadAsync(string objectKey, string fileName, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        if (objectKey.EndsWith(UploadSuffix, StringComparison.Ordinal)) throw new ArgumentException("暂存键不能签发下载。", nameof(objectKey));
        var presigned = await storage.GetPresignedDownloadAsync(objectKey, new DownloadOptions(ForceDownload: true, DownloadFileName: fileName), lifetime, cancellationToken).ConfigureAwait(false);
        var query = Query(presigned.Url);
        if (!query.TryGetValue("disposition", out var disposition) || !disposition.StartsWith("attachment;", StringComparison.Ordinal))
            throw new InvalidOperationException("Kun 本地存储未返回 attachment 下载参数。");
        var signature = Sign("GET", objectKey, query["expires"], disposition);
        return new PresignedTransfer(Append(presigned.Url, signature), "GET",
            new Dictionary<string, string>(presigned.ResponseHeaders, StringComparer.OrdinalIgnoreCase), presigned.ExpiresAt);
    }

    public async ValueTask<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
        await storage.OpenReadAsync(objectKey, cancellationToken).ConfigureAwait(false);

    public async ValueTask WriteAsync(string objectKey, Stream content, string contentType, long maxBytes, CancellationToken cancellationToken = default) =>
        await storage.UploadAsync(objectKey, content, contentType, new UploadConstraints(maxBytes), cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        await storage.DeleteAsync(objectKey, cancellationToken).ConfigureAwait(false);

    public async ValueTask<FileTransferResult> ReceiveAsync(FileTransferRequest request, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(content);
        if (!request.ObjectKey.EndsWith(UploadSuffix, StringComparison.Ordinal) || !ValidKey(request.ObjectKey) ||
            !request.Query.TryGetValue("expires", out var expires) || !request.Query.TryGetValue("contentType", out var contentType) ||
            !request.Headers.TryGetValue(MaxBytesHeader, out var maxBytesText) ||
            !Verify(request, Sign("PUT", request.ObjectKey, expires, contentType, maxBytesText)) || Expired(expires) ||
            !long.TryParse(maxBytesText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxBytes) ||
            !request.Headers.TryGetValue("Content-Type", out var actualType) ||
            !string.Equals(actualType.Split(';', 2)[0].Trim(), contentType, StringComparison.OrdinalIgnoreCase))
            return FileTransferResult.Rejected;
        // 一个上传地址只写一次：已存在的暂存对象不覆盖，完成上传读到的就是第一次写入的内容。
        if (await storage.ExistsAsync(request.ObjectKey, cancellationToken).ConfigureAwait(false)) return FileTransferResult.Conflict;
        try
        {
            await storage.UploadAsync(request.ObjectKey, content, contentType, new UploadConstraints(maxBytes), cancellationToken).ConfigureAwait(false);
            return FileTransferResult.Stored;
        }
        catch (InvalidOperationException)
        {
            // Kun 在超过 MaxBytes 时已删除半截文件。
            return FileTransferResult.TooLarge;
        }
        catch
        {
            // 中断的请求体会留下没有类型旁注的半截文件；删除后客户端可以用同一地址重试。
            try { await storage.DeleteAsync(request.ObjectKey, CancellationToken.None).ConfigureAwait(false); }
            catch (IOException) { }
            throw;
        }
    }

    public async ValueTask<FileTransferDownload?> OpenAsync(FileTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ObjectKey.EndsWith(UploadSuffix, StringComparison.Ordinal) || !ValidKey(request.ObjectKey) ||
            !request.Query.TryGetValue("expires", out var expires) || !request.Query.TryGetValue("disposition", out var disposition) ||
            !disposition.StartsWith("attachment;", StringComparison.Ordinal) ||
            !Verify(request, Sign("GET", request.ObjectKey, expires, disposition)) || Expired(expires))
            return null;
        var metadata = await storage.GetMetadataAsync(request.ObjectKey, cancellationToken).ConfigureAwait(false);
        if (metadata is null) return null;
        try
        {
            var stream = await storage.OpenReadAsync(request.ObjectKey, cancellationToken).ConfigureAwait(false);
            return new FileTransferDownload(stream, metadata.Size, metadata.ContentType ?? ContentSniffer.OctetStream, disposition);
        }
        catch (FileNotFoundException) { return null; }
    }

    private static bool ValidKey(string objectKey) =>
        objectKey.StartsWith("tenants/", StringComparison.Ordinal) && !objectKey.Contains("..", StringComparison.Ordinal) && !objectKey.Contains('\\', StringComparison.Ordinal);

    private bool Expired(string expires) =>
        !long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || clock.GetUtcNow().ToUnixTimeSeconds() > seconds;

    private static bool Verify(FileTransferRequest request, string expected) =>
        request.Query.TryGetValue(SignatureParameter, out var actual) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected));

    // 签名覆盖方法、对象键与 Kun 生成的全部查询字段（上传另含 MaxBytes 请求头），字段以换行分隔，任一改动都使签名失效。
    private string Sign(params string[] parts)
    {
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(string.Join('\n', parts)));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Dictionary<string, string> Query(Uri url)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            values.Add(Uri.UnescapeDataString(parts[0]), parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "");
        }
        return values;
    }

    private static Uri Append(Uri url, string signature) =>
        new(url.AbsoluteUri + (url.Query.Length == 0 ? "?" : "&") + SignatureParameter + "=" + Uri.EscapeDataString(signature), UriKind.Absolute);
}

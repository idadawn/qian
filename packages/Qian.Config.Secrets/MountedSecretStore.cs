using System.Security.Cryptography;
using Qian.Runtime.Errors;

namespace Qian.Config.Secrets;

/// <summary>读取受信任根目录中的 K8s 投射文件，支持根内的 ..data 原子轮换。</summary>
public sealed class MountedSecretStore
{
    private readonly string _root;
    /// <summary>根目录应只读挂载；文件布局为 namespace/Secret/key。</summary>
    public MountedSecretStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = new DirectoryInfo(Path.GetFullPath(rootDirectory));
        if (!root.Exists || root.LinkTarget is not null) throw new QianErrorException(new QianError(SecretErrors.Unavailable));
        _root = Path.TrimEndingDirectorySeparator(Canonical(root.FullName, 0));
    }
    /// <summary>可信宿主的低层读取入口；组件应使用带权限检查的 ISecretResolver。</summary>
    public async ValueTask<SecretValue> ReadAsync(KubernetesSecretLocation location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        byte[]? bytes = null;
        try
        {
            var path = Resolve(location);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is 0 or > 1024 * 1024) throw new QianErrorException(new QianError(SecretErrors.Unavailable));
            bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return new SecretValue(bytes);
        }
        catch (FileNotFoundException) { throw new QianErrorException(new QianError(SecretErrors.NotFound)); }
        catch (DirectoryNotFoundException) { throw new QianErrorException(new QianError(SecretErrors.NotFound)); }
        catch (UnauthorizedAccessException) { throw new QianErrorException(new QianError(SecretErrors.Unavailable)); }
        catch (IOException) { throw new QianErrorException(new QianError(SecretErrors.Unavailable)); }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    private string Resolve(KubernetesSecretLocation location)
    {
        var current = _root;
        foreach (var part in new[] { location.Namespace, location.Name, location.Key })
        {
            current = Path.Combine(current, part);
            var info = Directory.Exists(current) ? (FileSystemInfo)new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null)
                current = info.ResolveLinkTarget(returnFinalTarget: false)?.FullName
                    ?? throw new QianErrorException(new QianError(SecretErrors.NotFound));
            current = Canonical(current, 0);
            if (!current.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new QianErrorException(new QianError(SecretErrors.AccessDenied));
        }
        return current;
    }

    private static string Canonical(string path, int links)
    {
        if (links > 40) throw new QianErrorException(new QianError(SecretErrors.AccessDenied));
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var parts = full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var index = 0; index < parts.Length; index++)
        {
            current = Path.Combine(current, parts[index]);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is null) continue;
            var target = info.ResolveLinkTarget(returnFinalTarget: false)?.FullName
                ?? throw new QianErrorException(new QianError(SecretErrors.NotFound));
            return Canonical(Path.Combine([target, .. parts[(index + 1)..]]), links + 1);
        }
        return current;
    }
}

using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

internal sealed record RegistryArtifactFile(string Path, long Bytes, string Sha256);

// 只表示完整内容经过摘要和共用静态校验；不携带伪造的 Verified/Published 或签名结果。
internal sealed class RegistryArtifactContent
{
    private static readonly SearchValues<char> Hex = SearchValues.Create("0123456789abcdef");
    internal string Digest { get; }
    internal ParsedComponent Component { get; }
    internal IReadOnlyList<RegistryArtifactFile> Files { get; }
    internal IReadOnlyDictionary<string, JsonElement> Metadata { get; }
    private RegistryArtifactContent(string digest, ParsedComponent component, IReadOnlyList<RegistryArtifactFile> files,
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        Digest = digest; Component = component with { Documents = Array.AsReadOnly(component.Documents.ToArray()) }; Files = files;
        Metadata = new ReadOnlyDictionary<string, JsonElement>(metadata.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal));
    }

    internal static RegistryArtifactContent Read(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> source, string expectedDigest,
        ISurfaceValidator validator, SurfaceValidationOptions options)
    {
        if (!ValidDigest(expectedDigest)) throw Invalid("artifact.json#/digest", "制品必须声明 SHA-256 内容摘要。");
        if (source.Count is 0 or > 10000) throw Invalid("artifact.json#/files", "制品文件数量无效。");
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long size = 0;
        foreach (var item in source)
        {
            ValidatePath(item.Key);
            size += item.Value.Length;
            if (size > 64 * 1024 * 1024) throw new QianErrorException(new QianError(PlatformErrors.PayloadTooLarge));
            if (!names.Add(item.Key)) throw Invalid("artifact.json#/files", "文件名存在大小写冲突。");
            files.Add(item.Key, item.Value.ToArray());
        }
        var records = files.Select(item => new RegistryArtifactFile(item.Key, item.Value.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(item.Value)))).ToArray();
        // 与 CLI directory digest 同一 D4 格式：UTF-8、LF、序数路径排序。
        var inventory = string.Concat(records.Select(file => file.Path + "\t" + file.Bytes.ToString(CultureInfo.InvariantCulture) + "\t" + file.Sha256 + "\n"));
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
        if (digest != expectedDigest) throw Invalid("artifact.json#/digest", "制品内容与声明摘要不一致。");
        var temporary = Path.Combine(Path.GetTempPath(), "qian-registry-artifact-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(temporary);
            else Directory.CreateDirectory(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            foreach (var item in files)
            {
                var destination = Path.Combine(temporary, item.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(item.Value);
            }
            // 校验的是已复制的私有快照，不会在哈希与解析之间重新读取调用方目录。
            var result = validator.ValidateComponent(temporary, options);
            if (!result.IsValid) throw Diagnostics(result.Diagnostics);
            var metadata = result.Component!.Documents.Where(document => document.Kind == "metadata")
                .ToDictionary(document => document.File, document => document.Data.Clone(), StringComparer.Ordinal);
            return new RegistryArtifactContent(digest, result.Component, Array.AsReadOnly(records), metadata);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
        { throw Invalid("artifact.json#/files", "制品快照无法安全读取。"); }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    internal static bool ValidDigest(string digest) => digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal) &&
        digest.AsSpan(7).IndexOfAnyExcept(Hex) < 0;

    private static void ValidatePath(string path)
    {
        var parts = path.Split('/');
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal) || path.Contains(':', StringComparison.Ordinal) || path.Any(char.IsControl) ||
            parts.Length > 64 || parts.Any(part => part is "" or "." or ".." or ".git"))
            throw Invalid("artifact.json#/files", "制品路径无效或包含受限目录。");
    }
    private static QianErrorException Diagnostics(IReadOnlyList<SurfaceDiagnostic> diagnostics) => new(new QianError(PlatformErrors.InputInvalid)
    {
        Errors = diagnostics.Where(d => d.Severity == "error").Take(100).Select(d => new QianFieldError(RegistryErrors.Invalid,
            d.Error.Pointer ?? "artifact.json#", new Dictionary<string, string> { ["reason"] = "制品未通过共用静态校验。" })).ToArray()
    });
    internal static QianErrorException Invalid(string location, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(RegistryErrors.Invalid, location, new Dictionary<string, string> { ["reason"] = reason })] });
}

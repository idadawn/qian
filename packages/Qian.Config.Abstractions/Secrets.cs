using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Runtime;
using Kun.Core.MultiTenancy;

namespace Qian.Config;

/// <summary>已绑定调用方权限的按需密钥解析端口。</summary>
public interface ISecretResolver
{
    /// <summary>在调用时读取引用，不返回空值，不在错误中携带原始内容。</summary>
    ValueTask<SecretValue> ResolveAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>宿主根据已审批策略创建解析器；平台调用方身份取自真实插件上下文。</summary>
public interface ISecretResolverFactory
{
    /// <summary>绑定插件身份和生命周期；声明及审批权限从宿主策略中取，不由请求传入。</summary>
    ISecretResolver ForPlugin(IPluginContext context);
    /// <summary>独立宿主的显式入口，policyId 必须已在可信引导中登记。</summary>
    ISecretResolver ForHost(string policyId, ITenantContext? tenantContext = null);
}

/// <summary>密钥的受控内存副本；默认打印和 JSON 序列化不暴露内容。</summary>
[DebuggerDisplay("SecretValue(redacted)")]
[JsonConverter(typeof(SecretValueJsonConverter))]
public sealed class SecretValue : IDisposable
{
    private byte[]? _value;
    /// <summary>复制字节；调用方仍负责清理自己持有的输入。</summary>
    public SecretValue(ReadOnlySpan<byte> value) => _value = value.ToArray();
    /// <summary>显式取得 UTF-8 字符串，调用方负责其使用和生命周期，不能写入日志或配置导出。</summary>
    public string RevealUtf8()
    {
        var value = _value ?? throw new ObjectDisposedException(nameof(SecretValue));
        try { return new UTF8Encoding(false, true).GetString(value); }
        catch (DecoderFallbackException) { throw new InvalidOperationException("密钥不是 UTF-8 文本，请使用字节接口。"); }
    }
    /// <summary>显式取得只读字节，释放后本对象持有的缓冲归零。</summary>
    public ReadOnlyMemory<byte> Bytes => _value ?? throw new ObjectDisposedException(nameof(SecretValue));
    /// <inheritdoc />
    public override string ToString() => "SecretValue(redacted)";
    /// <summary>清零并释放本对象的副本；无法清零调用方已创建的字符串。</summary>
    public void Dispose()
    {
        var value = Interlocked.Exchange(ref _value, null);
        if (value is not null) CryptographicOperations.ZeroMemory(value);
    }
}

/// <summary>禁止把已解析密钥当作普通 JSON 数据。</summary>
public sealed class SecretValueJsonConverter : JsonConverter<SecretValue>
{
    /// <inheritdoc />
    public override SecretValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("密钥值不允许从 JSON 反序列化。");
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SecretValue value, JsonSerializerOptions options) =>
        throw new NotSupportedException("已解析密钥不允许 JSON 序列化。");
}

/// <summary>一次解析审计，只含引用名、调用方、租户和结果，不含值。</summary>
public sealed record SecretAccessEvent(string Caller, string Reference, string? TenantId, bool Allowed, string Outcome);

/// <summary>可替换的审计接收端；写入失败不得返回密钥。</summary>
public interface ISecretAccessObserver
{
    /// <summary>记录一次访问结果。</summary>
    ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default);
}

using System.Text.Json.Serialization;
using Kun.EventBus;

namespace Qian.Files;

/// <summary>文件的稳定跨进程事件 ID；首个发布版本之后，破坏性载荷改动必须升版本。</summary>
public static class FilesEventIds
{
    /// <summary>文件完成上传并通过校验，可以下载。</summary>
    public const string ObjectCreated = "files.object.created.v1";
    /// <summary>可下载的文件被软删。</summary>
    public const string ObjectDeleted = "files.object.deleted.v1";
    /// <summary>全部 S1 文件事件。</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { ObjectCreated, ObjectDeleted });
}

/// <summary>文件完成上传并转为 available；租户在信封上，载荷不重复。</summary>
[IntegrationEventId(FilesEventIds.ObjectCreated)]
public sealed record FileObjectCreatedEvent(Guid FileId, string PluginId, string Purpose, long Size, string ContentType, string Sha256);

/// <summary>available 文件被软删；上传中或已隔离的文件没有发过创建事件，删除时也不发本事件。</summary>
[IntegrationEventId(FilesEventIds.ObjectDeleted)]
public sealed record FileObjectDeletedEvent(Guid FileId, string PluginId, string Purpose);

/// <summary>框架事件目录注册；宿主与发布契约共用这一处。</summary>
public static class FilesEventRegistration
{
    /// <summary>显式传入源生成 JsonTypeInfo，避免反射序列化回退。</summary>
    public static IEventTypeRegistrar RegisterFilesEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(FilesEventIds.ObjectCreated, FilesEventJson.Default.FileObjectCreatedEvent);
        return registrar.Register(FilesEventIds.ObjectDeleted, FilesEventJson.Default.FileObjectDeletedEvent);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(FileObjectCreatedEvent))]
[JsonSerializable(typeof(FileObjectDeletedEvent))]
internal sealed partial class FilesEventJson : JsonSerializerContext;

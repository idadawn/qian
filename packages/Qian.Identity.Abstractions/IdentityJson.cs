using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Qian.Identity.Abstractions;

/// <summary>身份契约序列化入口；只处理 DTO，不验证令牌签名或授予权限。</summary>
public static class IdentityJson
{
    internal static IdentityJsonContext Context { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    });
    /// <summary>S2S DTO 元数据；调用方继续按冻结 Schema 和 JWT 规则验证。</summary>
    public static JsonTypeInfo<Principal> PrincipalTypeInfo => Context.Principal;
    /// <summary>客户端 DTO 元数据。</summary>
    public static JsonTypeInfo<Client> ClientTypeInfo => Context.Client;
    /// <summary>外部身份断言 DTO 元数据。</summary>
    public static JsonTypeInfo<ExternalAssertion> ExternalAssertionTypeInfo => Context.ExternalAssertion;
    /// <summary>事件载荷元数据；未知或未登记类型返回 null。</summary>
    public static JsonTypeInfo? EventTypeInfo(string id) => id switch
    {
        IdentityEventIds.Login => Context.IdentityLoginEvent,
        IdentityEventIds.Logout => Context.IdentityLogoutEvent,
        IdentityEventIds.PasswordChanged => Context.IdentityPasswordChangedEvent,
        IdentityEventIds.Locked => Context.IdentityLockedEvent,
        IdentityEventIds.SessionRevoked => Context.IdentitySessionRevokedEvent,
        DirectoryEventIds.OrganizationCreated => Context.DirectoryOrganizationCreatedEvent,
        DirectoryEventIds.OrganizationUpdated => Context.DirectoryOrganizationUpdatedEvent,
        DirectoryEventIds.OrganizationMoved => Context.DirectoryOrganizationMovedEvent,
        DirectoryEventIds.OrganizationDeleted => Context.DirectoryOrganizationDeletedEvent,
        DirectoryEventIds.PositionCreated => Context.DirectoryPositionCreatedEvent,
        DirectoryEventIds.PositionUpdated => Context.DirectoryPositionUpdatedEvent,
        DirectoryEventIds.PositionMoved => Context.DirectoryPositionMovedEvent,
        DirectoryEventIds.PositionDeleted => Context.DirectoryPositionDeletedEvent,
        DirectoryEventIds.MemberCreated => Context.DirectoryMemberCreatedEvent,
        DirectoryEventIds.MemberRelationsChanged => Context.DirectoryMemberRelationsChangedEvent,
        DirectoryEventIds.MemberStatusChanged => Context.DirectoryMemberStatusChangedEvent,
        DirectoryEventIds.MemberProfileUpdated => Context.DirectoryMemberProfileUpdatedEvent,
        _ => null
    };
}
/// <summary>随 Definition 分发的身份与目录事件 payload Schema。</summary>
public static class IdentitySchemas
{
    /// <summary>取回稳定事件对应的 JSON Schema 2020-12（身份事件与目录事件）。</summary>
    public static string Event(string eventId)
    {
        if (!IdentityEventIds.All.Contains(eventId, StringComparer.Ordinal) && !DirectoryEventIds.All.Contains(eventId, StringComparer.Ordinal))
            throw new ArgumentException("未登记的身份或目录事件。", nameof(eventId));
        using var stream = typeof(IdentitySchemas).Assembly.GetManifestResourceStream("identity/" + eventId + ".schema.json")
            ?? throw new InvalidOperationException("事件 Schema 未打包。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Principal))]
[JsonSerializable(typeof(Client))]
[JsonSerializable(typeof(ExternalAssertion))]
[JsonSerializable(typeof(IdentityLoginEvent))]
[JsonSerializable(typeof(IdentityLogoutEvent))]
[JsonSerializable(typeof(IdentityPasswordChangedEvent))]
[JsonSerializable(typeof(IdentityLockedEvent))]
[JsonSerializable(typeof(IdentitySessionRevokedEvent))]
[JsonSerializable(typeof(DirectoryOrganizationCreatedEvent))]
[JsonSerializable(typeof(DirectoryOrganizationUpdatedEvent))]
[JsonSerializable(typeof(DirectoryOrganizationMovedEvent))]
[JsonSerializable(typeof(DirectoryOrganizationDeletedEvent))]
[JsonSerializable(typeof(DirectoryPositionCreatedEvent))]
[JsonSerializable(typeof(DirectoryPositionUpdatedEvent))]
[JsonSerializable(typeof(DirectoryPositionMovedEvent))]
[JsonSerializable(typeof(DirectoryPositionDeletedEvent))]
[JsonSerializable(typeof(DirectoryMemberCreatedEvent))]
[JsonSerializable(typeof(DirectoryMemberRelationsChangedEvent))]
[JsonSerializable(typeof(DirectoryMemberStatusChangedEvent))]
[JsonSerializable(typeof(DirectoryMemberProfileUpdatedEvent))]
internal sealed partial class IdentityJsonContext : JsonSerializerContext;

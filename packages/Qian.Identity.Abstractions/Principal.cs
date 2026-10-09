using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Identity.Abstractions;

/// <summary>统一主体模型；Agent 在 S1 只预留，不因此开放签发。</summary>
[JsonConverter(typeof(PrincipalKindConverter))]
public enum PrincipalKind
{
    /// <summary>自然人。</summary>
    User,
    /// <summary>机器服务。</summary>
    Service,
    /// <summary>Agent 客户端，S4 启用。</summary>
    Agent
}
/// <summary>客户端登记类型，独立于 OIDC grant；Agent 为预留值。</summary>
[JsonConverter(typeof(ClientTypeConverter))]
public enum ClientType
{
    /// <summary>可保管机密的客户端。</summary>
    Confidential,
    /// <summary>公开客户端。</summary>
    Public,
    /// <summary>机器身份客户端。</summary>
    Machine,
    /// <summary>Agent 客户端。</summary>
    Agent
}

/// <summary>冻结 S2S payload 的 DTO，不承担 JWT 验签、发行或权限判定。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class Principal
{
    /// <summary>签发方。</summary>
    [JsonPropertyName(IdentityClaimNames.Issuer)] public required string Issuer { get; init; }
    /// <summary>服务受众。</summary>
    [JsonPropertyName(IdentityClaimNames.Audience)] public required IReadOnlyList<string> Audience { get; init; }
    /// <summary>person/client 标识。</summary>
    [JsonPropertyName(IdentityClaimNames.Subject)] public required string Subject { get; init; }
    /// <summary>主体类型。</summary>
    [JsonPropertyName(IdentityClaimNames.PrincipalKind)] public required PrincipalKind Kind { get; init; }
    /// <summary>租户 UUID 字符串；平台主体必须显式输出 null。</summary>
    [JsonPropertyName(IdentityClaimNames.TenantId), JsonIgnore(Condition = JsonIgnoreCondition.Never)] public required string? TenantId { get; init; }
    /// <summary>用户租户成员；机器主体不得携带。</summary>
    [JsonPropertyName(IdentityClaimNames.MembershipId)] public string? MembershipId { get; init; }
    /// <summary>用户身份修订。</summary>
    [JsonPropertyName(IdentityClaimNames.IdentityRevision)] public long? IdentityRevision { get; init; }
    /// <summary>可选项目。</summary>
    [JsonPropertyName(IdentityClaimNames.ProjectId)] public string? ProjectId { get; init; }
    /// <summary>可选插件主体。</summary>
    [JsonPropertyName(IdentityClaimNames.PluginId)] public string? PluginId { get; init; }
    /// <summary>粗粒度角色。</summary>
    [JsonPropertyName(IdentityClaimNames.Roles)] public required IReadOnlyList<string> Roles { get; init; }
    /// <summary>空格分隔 scope。</summary>
    [JsonPropertyName(IdentityClaimNames.Scope)] public required string Scope { get; init; }
    /// <summary>会话 UUID；机器主体使用 jti。</summary>
    [JsonPropertyName(IdentityClaimNames.SessionId)] public required string SessionId { get; init; }
    /// <summary>预留委托链。</summary>
    [JsonPropertyName(IdentityClaimNames.Actor)] public DelegationActor? Actor { get; init; }
    /// <summary>NumericDate。</summary>
    [JsonPropertyName(IdentityClaimNames.IssuedAt)] public required long IssuedAt { get; init; }
    /// <summary>NumericDate。</summary>
    [JsonPropertyName(IdentityClaimNames.NotBefore)] public required long NotBefore { get; init; }
    /// <summary>NumericDate，签发方须保证有效期不超过 300 秒。</summary>
    [JsonPropertyName(IdentityClaimNames.ExpiresAt)] public required long ExpiresAt { get; init; }
    /// <summary>令牌 UUID。</summary>
    [JsonPropertyName(IdentityClaimNames.TokenId)] public required string TokenId { get; init; }
    /// <summary>避免默认字符串化把 session_id、jti 或委托链写入日志。</summary>
    public override string ToString() => "Principal(redacted)";
}
/// <summary>RFC 8693 委托链，保持与 v0 Actor 定义同形。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DelegationActor
{
    /// <summary>行事主体。</summary>
    [JsonPropertyName(IdentityClaimNames.Subject)] public required string Subject { get; init; }
    /// <summary>主体类型。</summary>
    [JsonPropertyName(IdentityClaimNames.PrincipalKind)] public required PrincipalKind Kind { get; init; }
    /// <summary>更早的行事方。</summary>
    [JsonPropertyName(IdentityClaimNames.Actor)] public DelegationActor? Actor { get; init; }
    /// <inheritdoc />
    public override string ToString() => "DelegationActor(redacted)";
}

internal sealed class PrincipalKindConverter : JsonConverter<PrincipalKind>
{
    public override PrincipalKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() switch { "user" => PrincipalKind.User, "service" => PrincipalKind.Service, "agent" => PrincipalKind.Agent, _ => throw new JsonException("未知主体类型。") }
        : throw new JsonException("主体类型必须为字符串。");
    public override void Write(Utf8JsonWriter writer, PrincipalKind value, JsonSerializerOptions options) => writer.WriteStringValue(value switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", PrincipalKind.Agent => "agent", _ => throw new JsonException("未知主体类型。") });
}
internal sealed class ClientTypeConverter : JsonConverter<ClientType>
{
    public override ClientType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() switch { "confidential" => ClientType.Confidential, "public" => ClientType.Public, "machine" => ClientType.Machine, "agent" => ClientType.Agent, _ => throw new JsonException("未知客户端类型。") }
        : throw new JsonException("客户端类型必须为字符串。");
    public override void Write(Utf8JsonWriter writer, ClientType value, JsonSerializerOptions options) => writer.WriteStringValue(value switch { ClientType.Confidential => "confidential", ClientType.Public => "public", ClientType.Machine => "machine", ClientType.Agent => "agent", _ => throw new JsonException("未知客户端类型。") });
}

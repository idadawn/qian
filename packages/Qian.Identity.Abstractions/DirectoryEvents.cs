using Kun.EventBus;

namespace Qian.Identity.Abstractions;

/// <summary>
/// 组织目录的稳定跨进程事件 ID（CloudEvents 信封见 contracts/v0/event-envelope.schema.json）。
/// 首个发布版本之后，破坏性载荷改动必须升版本，不能原地改名。
/// </summary>
public static class DirectoryEventIds
{
    /// <summary>组织已创建。</summary>
    public const string OrganizationCreated = "directory.org.created.v1";
    /// <summary>组织的名称、类型、主管或责任岗位已修改（不含移动）。</summary>
    public const string OrganizationUpdated = "directory.org.updated.v1";
    /// <summary>组织已移动到新的上级；后代的 path 按前缀同步改写，不逐个发事件。</summary>
    public const string OrganizationMoved = "directory.org.moved.v1";
    /// <summary>组织已删除；删除子树时每个组织各一条。</summary>
    public const string OrganizationDeleted = "directory.org.deleted.v1";
    /// <summary>岗位已创建。</summary>
    public const string PositionCreated = "directory.position.created.v1";
    /// <summary>岗位的名称、所属组织、默认 / 责任标记或责任人已修改（不含移动）。</summary>
    public const string PositionUpdated = "directory.position.updated.v1";
    /// <summary>岗位已移动到新的父岗位；后代的 path 按前缀同步改写，不逐个发事件。</summary>
    public const string PositionMoved = "directory.position.moved.v1";
    /// <summary>岗位已删除；删除子树时每个岗位各一条。</summary>
    public const string PositionDeleted = "directory.position.deleted.v1";
    /// <summary>租户成员已建立。</summary>
    public const string MemberCreated = "directory.member.created.v1";
    /// <summary>成员的组织 / 岗位 / 用户组归属已整体替换。</summary>
    public const string MemberRelationsChanged = "directory.member.relations_changed.v1";
    /// <summary>成员状态或目录删除标记已变化。</summary>
    public const string MemberStatusChanged = "directory.member.status_changed.v1";
    /// <summary>成员档案已更新；载荷不含档案字段。</summary>
    public const string MemberProfileUpdated = "directory.member.profile_updated.v1";
    /// <summary>全部 S1 目录事件。</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        OrganizationCreated, OrganizationUpdated, OrganizationMoved, OrganizationDeleted,
        PositionCreated, PositionUpdated, PositionMoved, PositionDeleted,
        MemberCreated, MemberRelationsChanged, MemberStatusChanged, MemberProfileUpdated
    });
}

/// <summary>
/// 目录事件共用字段。DirectoryRevision 是租户目录修订号：每次产生事件的目录写入加一，同一次写入的事件共用一个值，消费端据此排序与去重。
/// 载荷只含 id 与结构字段，不含组织 / 岗位名称、成员姓名、拼音与联系方式；event_id、occurred_at 与 tenant_id 只在信封上（S1-DEC-020）。
/// </summary>
public abstract record DirectoryIntegrationEvent(long DirectoryRevision)
{
    /// <inheritdoc />
    public sealed override string ToString() => "DirectoryIntegrationEvent(redacted)";
}
/// <summary>组织已创建。</summary>
[IntegrationEventId(DirectoryEventIds.OrganizationCreated)]
public sealed record DirectoryOrganizationCreatedEvent(long DirectoryRevision, Guid OrganizationId, Guid? ParentId, string TypeCode, string Path, int Depth,
    Guid? ManagerMembershipId = null, Guid? ResponsiblePositionId = null) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>组织属性已修改；字段是修改后的当前值。</summary>
[IntegrationEventId(DirectoryEventIds.OrganizationUpdated)]
public sealed record DirectoryOrganizationUpdatedEvent(long DirectoryRevision, Guid OrganizationId, Guid? ParentId, string TypeCode, string Path, int Depth,
    Guid? ManagerMembershipId = null, Guid? ResponsiblePositionId = null) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>组织已移动；后代 path 以 PreviousPath 为前缀的部分替换为 Path。</summary>
[IntegrationEventId(DirectoryEventIds.OrganizationMoved)]
public sealed record DirectoryOrganizationMovedEvent(long DirectoryRevision, Guid OrganizationId, Guid? ParentId, Guid? PreviousParentId, string Path, string PreviousPath, int Depth)
    : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>组织已删除。</summary>
[IntegrationEventId(DirectoryEventIds.OrganizationDeleted)]
public sealed record DirectoryOrganizationDeletedEvent(long DirectoryRevision, Guid OrganizationId, Guid? ParentId, string Path) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>岗位已创建。</summary>
[IntegrationEventId(DirectoryEventIds.PositionCreated)]
public sealed record DirectoryPositionCreatedEvent(long DirectoryRevision, Guid PositionId, Guid OrganizationId, Guid? ParentId, string Path, int Depth,
    bool IsDefault, bool IsResponsible, Guid? ResponsibleMembershipId = null) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>岗位属性已修改；字段是修改后的当前值。</summary>
[IntegrationEventId(DirectoryEventIds.PositionUpdated)]
public sealed record DirectoryPositionUpdatedEvent(long DirectoryRevision, Guid PositionId, Guid OrganizationId, Guid? ParentId, string Path, int Depth,
    bool IsDefault, bool IsResponsible, Guid? ResponsibleMembershipId = null) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>岗位已移动；后代 path 以 PreviousPath 为前缀的部分替换为 Path。</summary>
[IntegrationEventId(DirectoryEventIds.PositionMoved)]
public sealed record DirectoryPositionMovedEvent(long DirectoryRevision, Guid PositionId, Guid OrganizationId, Guid? ParentId, Guid? PreviousParentId, string Path, string PreviousPath, int Depth)
    : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>岗位已删除。</summary>
[IntegrationEventId(DirectoryEventIds.PositionDeleted)]
public sealed record DirectoryPositionDeletedEvent(long DirectoryRevision, Guid PositionId, Guid OrganizationId, Guid? ParentId, string Path) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>成员已建立。</summary>
[IntegrationEventId(DirectoryEventIds.MemberCreated)]
public sealed record DirectoryMemberCreatedEvent(long DirectoryRevision, Guid MembershipId, Guid PersonId, string Status, long IdentityRevision) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>成员归属已整体替换；同时给出替换前后的完整集合。</summary>
[IntegrationEventId(DirectoryEventIds.MemberRelationsChanged)]
public sealed record DirectoryMemberRelationsChangedEvent(long DirectoryRevision, Guid MembershipId, Guid PersonId, long IdentityRevision,
    IReadOnlyList<MemberRelation> Relations, IReadOnlyList<MemberRelation> PreviousRelations) : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>成员状态或目录删除标记已变化。</summary>
[IntegrationEventId(DirectoryEventIds.MemberStatusChanged)]
public sealed record DirectoryMemberStatusChangedEvent(long DirectoryRevision, Guid MembershipId, Guid PersonId, string Status, string PreviousStatus, bool Deleted, long IdentityRevision)
    : DirectoryIntegrationEvent(DirectoryRevision);
/// <summary>成员档案已更新；变更内容经档案接口读取，载荷里没有。</summary>
[IntegrationEventId(DirectoryEventIds.MemberProfileUpdated)]
public sealed record DirectoryMemberProfileUpdatedEvent(long DirectoryRevision, Guid MembershipId, Guid PersonId) : DirectoryIntegrationEvent(DirectoryRevision);

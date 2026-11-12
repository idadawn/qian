using Kun.Core.Domain;

namespace Qian.Identity.Server;

// 每张目录表都是租户表；关联外键包含 tenant_id，不能用已知 UUID 跨租户建立归属。
internal sealed class OrganizationTypeRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
internal sealed class OrganizationRow : IMultiTenant
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public Guid? ParentId { get; set; }
    public string Path { get; set; } = "";
    public int Depth { get; set; }
    public Guid? ManagerMembershipId { get; set; }
    public Guid? ResponsiblePositionId { get; set; }
}
internal sealed class PositionRow : IMultiTenant
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Path { get; set; } = "";
    public int Depth { get; set; }
    public bool IsDefault { get; set; }
    public bool IsResponsible { get; set; }
    public Guid? ResponsibleMembershipId { get; set; }
}
internal sealed class DirectoryGroupRow : IMultiTenant
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
}
internal sealed class MemberRelationRow : IMultiTenant
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public Guid MembershipId { get; set; }
    public string TargetType { get; set; } = "";
    public Guid? OrganizationId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? GroupId { get; set; }
    public bool IsPrimary { get; set; }
}
internal sealed class UserProfileRow : IMultiTenant
{
    public Guid MembershipId { get; set; }
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Pinyin { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Extensions { get; set; } = "{}";
}

// 租户目录修订：每次产生目录事件的写入加一（原子 upsert，行锁随事务释放），同时充当目录事件的聚合根——
// 目录写入里会先删除再保存的行（删子树、替换归属）无法携带领域事件，事件统一挂在这一行上，随最后一次会话保存进入 Outbox。
internal sealed class DirectoryRevisionRow : IMultiTenant, IAggregateRoot
{
    public string TenantId { get; set; } = "";
    public long Revision { get; set; }
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;
    public void ClearDomainEvents() => _events.Clear();
    internal void Raise(IDomainEvent change) => _events.Add(change);
}

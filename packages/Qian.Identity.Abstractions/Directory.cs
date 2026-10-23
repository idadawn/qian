using System.Text.Json;

namespace Qian.Identity.Abstractions;

/// <summary>组织树的稳定约束；根节点深度为 1。</summary>
public static class DirectoryRules
{
    /// <summary>S1 默认组织树深度上限。</summary>
    public const int MaximumDepth = 9;
    /// <summary>验证移动后的整棵子树深度；超限抛出结构化领域错误。</summary>
    public static void ValidateDepth(int parentDepth, int subtreeHeight)
    {
        if (parentDepth < 0 || subtreeHeight < 1 || parentDepth + subtreeHeight > MaximumDepth)
            throw new Qian.Runtime.Errors.QianErrorException(new Qian.Runtime.Errors.QianError(IdentityErrors.DirectoryDepthExceeded));
    }
    /// <summary>启用与禁用可互转，只有禁用可进入锁定；锁定须先由可信账号管理流程解除。</summary>
    public static bool CanTransition(string from, string to) => from == to && from is AccountStatuses.Active or AccountStatuses.Disabled or AccountStatuses.Locked ||
        from == AccountStatuses.Active && to == AccountStatuses.Disabled || from == AccountStatuses.Disabled && to is AccountStatuses.Active or AccountStatuses.Locked;
}
/// <summary>租户可配置组织类型；编码不可改，名称可改。</summary>
public sealed record OrganizationType(string Code, string Name);
/// <summary>创建或更新组织；父节点为空代表单棵组织树的根。</summary>
public sealed record OrganizationInput(string Name, string TypeCode, Guid? ParentId = null, Guid? ManagerMembershipId = null, Guid? ResponsiblePositionId = null);
/// <summary>组织投影；Path 以斜线包围 UUID，每次移动同步更新全部后代。</summary>
public sealed record Organization(Guid Id, string Name, string TypeCode, Guid? ParentId, string Path, int Depth, Guid? ManagerMembershipId, Guid? ResponsiblePositionId);
/// <summary>创建或更新岗位；父岗位可位于同租户的不同组织。</summary>
public sealed record PositionInput(string Name, Guid OrganizationId, Guid? ParentId = null, bool IsDefault = false, bool IsResponsible = false, Guid? ResponsibleMembershipId = null);
/// <summary>岗位及责任人投影；每个组织至多一个默认岗位。</summary>
public sealed record DirectoryPosition(Guid Id, string Name, Guid OrganizationId, Guid? ParentId, string Path, int Depth, bool IsDefault, bool IsResponsible, Guid? ResponsibleMembershipId);
/// <summary>成员归属；TargetType 为 organization / position / group，每种归属至多一个主归属。</summary>
public sealed record MemberRelation(string TargetType, Guid TargetId, bool IsPrimary = false);
/// <summary>用户组是当前租户中的纯成员集合。</summary>
public sealed record DirectoryGroup(Guid Id, string Name);
/// <summary>档案写入；联系方式与登录标识分离，不会改变凭据或验证状态。</summary>
public sealed record UserProfileInput(string Name, string? Pinyin = null, string? Phone = null, string? Email = null, JsonElement? Extensions = null)
{
    /// <inheritdoc />
    public override string ToString() => "UserProfileInput(redacted)";
}
/// <summary>一对一成员档案；对外联系方式仅给掩码，扩展仅由租户管理员读取。</summary>
public sealed record UserProfile(Guid MembershipId, string Name, string? Pinyin, string? Phone, string? Email, JsonElement Extensions)
{
    /// <inheritdoc />
    public override string ToString() => "UserProfile(redacted)";
}
/// <summary>目录成员投影，只来源于真实租户 Membership。</summary>
public sealed record DirectoryMember(Membership Membership, bool Deleted);
/// <summary>批量操作单项回执；失败仅给稳定错误码，不携带异常或个人信息。</summary>
public sealed record DirectoryBatchItem(Guid MembershipId, bool Succeeded, string? ErrorCode);
/// <summary>批量回执：SUCCESS / PARTIAL_SUCCESS / FAILED；每个输入成员恰好对应一项。</summary>
public sealed record DirectoryBatchResult(string Status, IReadOnlyList<DirectoryBatchItem> Items);
/// <summary>目录操作的机器可读安全元数据；Executable 为 false 时只是预留契约，不存在执行入口。</summary>
public sealed record DirectoryOperation(string Name, string Description, string SideEffect, string PolicyHint, bool AgentExpose, bool RequiresConfirmation, bool Executable, string Example);
/// <summary>目录未实现操作的安全边界，不是可执行能力目录。</summary>
public static class DirectoryOperations
{
    /// <summary>尚未实现的高风险操作边界；真实能力使用 SDK 生成契约，不在此重复声明。</summary>
    public static IReadOnlyList<DirectoryOperation> All { get; } = Array.AsReadOnly(new[]
    {
        new DirectoryOperation("directory.member.purge", "物理清除需人工治理；S1 不提供执行入口。", "destructive", "deny", false, false, false, "无执行入口")
    });
}
/// <summary>Identity seam 的组织目录子端口；所有租户数据取当前已验证租户，不接受输入 tenant_id。</summary>
public interface IIdentityDirectory
{
    /// <summary>列出租户组织类型字典。</summary>
    ValueTask<IReadOnlyList<OrganizationType>> ListOrganizationTypesAsync(CancellationToken cancellationToken = default);
    /// <summary>创建或修改租户组织类型名称。</summary>
    ValueTask<OrganizationType> SetOrganizationTypeAsync(OrganizationType request, CancellationToken cancellationToken = default);
    /// <summary>删除未被组织引用的类型。</summary>
    ValueTask DeleteOrganizationTypeAsync(string code, CancellationToken cancellationToken = default);
    /// <summary>读取按 Path 排序的完整组织树（以 ParentId 关联）。</summary>
    ValueTask<IReadOnlyList<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default);
    /// <summary>创建组织。</summary>
    ValueTask<Organization> CreateOrganizationAsync(OrganizationInput request, CancellationToken cancellationToken = default);
    /// <summary>更新或移动组织；循环与整棵子树深度超限时拒绝。</summary>
    ValueTask<Organization> UpdateOrganizationAsync(Guid id, OrganizationInput request, CancellationToken cancellationToken = default);
    /// <summary>删除空组织子树；任何后代组织或岗位仍有成员时拒绝。</summary>
    ValueTask DeleteOrganizationAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>列出全部岗位。</summary>
    ValueTask<IReadOnlyList<DirectoryPosition>> ListPositionsAsync(CancellationToken cancellationToken = default);
    /// <summary>创建岗位。</summary>
    ValueTask<DirectoryPosition> CreatePositionAsync(PositionInput request, CancellationToken cancellationToken = default);
    /// <summary>更新岗位归属或移动岗位父链，维护全部后代 Path。</summary>
    ValueTask<DirectoryPosition> UpdatePositionAsync(Guid id, PositionInput request, CancellationToken cancellationToken = default);
    /// <summary>删除空岗位子树；有成员时拒绝。</summary>
    ValueTask DeletePositionAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>列出用户组。</summary>
    ValueTask<IReadOnlyList<DirectoryGroup>> ListGroupsAsync(CancellationToken cancellationToken = default);
    /// <summary>创建用户组。</summary>
    ValueTask<DirectoryGroup> CreateGroupAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>删除空用户组。</summary>
    ValueTask DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>读取成员多归属。</summary>
    ValueTask<IReadOnlyList<MemberRelation>> GetRelationsAsync(Guid membershipId, CancellationToken cancellationToken = default);
    /// <summary>原子替换多归属；校验所有目标在当前租户、每种类型唯一主归属。</summary>
    ValueTask<IReadOnlyList<MemberRelation>> SetRelationsAsync(Guid membershipId, IReadOnlyList<MemberRelation> relations, CancellationToken cancellationToken = default);
    /// <summary>读取档案；不存在返回 null。</summary>
    ValueTask<UserProfile?> GetProfileAsync(Guid membershipId, CancellationToken cancellationToken = default);
    /// <summary>更新档案，不改动 Person 凭据。</summary>
    ValueTask<UserProfile> SetProfileAsync(Guid membershipId, UserProfileInput request, CancellationToken cancellationToken = default);
    /// <summary>分页读取真实租户成员，不合成平台管理员成员。</summary>
    ValueTask<IReadOnlyList<DirectoryMember>> ListMembersAsync(int offset = 0, int limit = 50, CancellationToken cancellationToken = default);
    /// <summary>读取指定组织（可选子树）下的成员。</summary>
    ValueTask<IReadOnlyList<DirectoryMember>> GetOrganizationMembersAsync(Guid organizationId, bool descendants = false, int offset = 0, int limit = 50, CancellationToken cancellationToken = default);
    /// <summary>读取岗位直接任职成员。</summary>
    ValueTask<IReadOnlyList<DirectoryMember>> GetPositionHoldersAsync(Guid positionId, int offset = 0, int limit = 50, CancellationToken cancellationToken = default);
    /// <summary>按目录状态机变更成员；禁用/锁定即时吊销会话。</summary>
    ValueTask<Membership> SetDirectoryMemberStatusAsync(Guid membershipId, string status, string reason, CancellationToken cancellationToken = default);
    /// <summary>批量禁用（最多 100）；租户管理员逐项拒绝，其余各自独立事务。</summary>
    ValueTask<DirectoryBatchResult> DisableMembersAsync(IReadOnlyList<Guid> membershipIds, string reason, CancellationToken cancellationToken = default);
    /// <summary>删除是禁用并标记，不物理删除；管理员保护与批量禁用相同。</summary>
    ValueTask DeleteMemberAsync(Guid membershipId, string reason, CancellationToken cancellationToken = default);
}

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
/// <summary>
/// 租户管理员在本租户直接新增成员（S1-IDP-017，R145）。<see cref="Identifier"/> 是手机号或用户名，按语法判定类型，不收邮箱：
/// 已对应自然人时把他绑定为本租户成员，不改动他的凭据；否则新建自然人，以 <see cref="InitialPassword"/> 为初始密码，首次登录强制修改。
/// <see cref="DisplayName"/> 是成员在本租户的显示名。租户只取自当前已验证的租户上下文，不接受请求字段。
/// </summary>
public sealed record DirectoryMemberInput(string Identifier, string DisplayName)
{
    /// <summary>新建自然人时必填的初始密码；绑定已有自然人时不使用、不保存。使用后由调用方释放。</summary>
    public AuthenticationProof? InitialPassword { get; init; }
    /// <summary>租户内账号，租户内唯一、创建后不可改；缺省取用户名（未被占用时），否则随机生成不透明账号。</summary>
    public string? Account { get; init; }
    /// <inheritdoc />
    public override string ToString() => "DirectoryMemberInput(redacted)";
}
/// <summary>新增成员的结果：成员投影、是否新建了自然人，以及本次授予的初始角色（同一运行时没有授权组件时为空）。</summary>
public sealed record DirectoryMemberAdded(Membership Membership, bool PersonCreated, IReadOnlyList<string> GrantedRoles);
/// <summary>批量操作单项回执；失败仅给稳定错误码，不携带异常或个人信息。</summary>
public sealed record DirectoryBatchItem(Guid MembershipId, bool Succeeded, string? ErrorCode);
/// <summary>批量回执：SUCCESS / PARTIAL_SUCCESS / FAILED；每个输入成员恰好对应一项。</summary>
public sealed record DirectoryBatchResult(string Status, IReadOnlyList<DirectoryBatchItem> Items);
/// <summary>
/// 成员查询条件。Keyword、MembershipIds、PersonIds 至多给一个：关键字按显示名、账号与拼音做忽略大小写的包含匹配，不匹配手机号与邮箱；
/// 按 id 批量时只返回当前租户的可见成员，查不到的 id 直接省略。结果按账号、成员 id 排序；按 id 批量不分页，这批 id 里的可见成员全部返回
/// （Offset、Limit 不生效，HTTP 入口同时带 offset / limit 时 400），其余查询按 Offset、Limit 分页。
/// </summary>
public sealed record DirectoryMemberQuery
{
    /// <summary>每批 id 的上限。</summary>
    public const int MaximumIds = 100;
    /// <summary>关键字长度上限（去掉首尾空白后）。</summary>
    public const int MaximumKeywordLength = 64;
    /// <summary>只取该组织下的成员（含经该组织岗位归属的成员）；为空时取全租户。</summary>
    public Guid? OrganizationId { get; init; }
    /// <summary>组织范围是否含整棵子树。</summary>
    public bool Descendants { get; init; }
    /// <summary>关键字，去掉首尾空白后 1–64 字。</summary>
    public string? Keyword { get; init; }
    /// <summary>按目录成员 id 批量查询，1–100 个且不重复。</summary>
    public IReadOnlyList<Guid>? MembershipIds { get; init; }
    /// <summary>按自然人 id 批量查询，1–100 个且不重复。</summary>
    public IReadOnlyList<Guid>? PersonIds { get; init; }
    /// <summary>跳过的条数；按 id 批量时不生效。</summary>
    public int Offset { get; init; }
    /// <summary>本页条数，1–200；按 id 批量时不生效。</summary>
    public int Limit { get; init; } = 50;
}
/// <summary>上级链与部门负责人里「责任岗位空缺」的原因。</summary>
public static class DirectoryVacancies
{
    /// <summary>责任岗位没有设责任人。</summary>
    public const string Unassigned = "unassigned";
    /// <summary>责任人不在任：成员被停用、锁定或已从目录删除，或其平台账号被停用、锁定；不返回其信息。</summary>
    public const string Inactive = "inactive";
}
/// <summary>上级链的一级：一个责任岗位及其现任责任人；空缺时 Responsible 为空、Vacancy 给原因，级次照样保留。</summary>
public sealed record DirectorySuperior(int Level, Guid PositionId, Guid OrganizationId, Membership? Responsible, string? Vacancy);
/// <summary>
/// 按岗位链推导的上级责任人序列。起点是成员的主岗位（或调用方指定的本人岗位），没有主岗位时从主组织起算，两者都没有则为空序列。
/// </summary>
public sealed record DirectorySuperiors(Guid MembershipId, Guid? StartPositionId, Guid? StartOrganizationId, IReadOnlyList<DirectorySuperior> Items);
/// <summary>直属主管：主组织的主管字段，本人即主管、未设或主管不在任（成员或其平台账号被停用、锁定，或已从目录删除）时逐级取上级组织的主管；SourceOrganizationId 是主管所在组织。</summary>
public sealed record DirectoryManager(Guid MembershipId, Membership? Manager, Guid? SourceOrganizationId);
/// <summary>部门负责人：组织责任岗位的现任责任人（空缺时给原因）与组织的主管字段。</summary>
public sealed record OrganizationHead(Guid OrganizationId, Guid? ResponsiblePositionId, Membership? Responsible, string? Vacancy, Membership? Manager);
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
    /// <summary>按组织范围、关键字或 id 批量查询真实租户成员；条件互斥规则见 <see cref="DirectoryMemberQuery"/>。</summary>
    ValueTask<IReadOnlyList<DirectoryMember>> QueryMembersAsync(DirectoryMemberQuery query, CancellationToken cancellationToken = default);
    /// <summary>按岗位链返回上级责任人序列；positionId 须是该成员担任的岗位，缺省取主岗位。</summary>
    ValueTask<DirectorySuperiors> GetSuperiorsAsync(Guid membershipId, Guid? positionId = null, CancellationToken cancellationToken = default);
    /// <summary>直属主管：主组织（无主组织取主岗位所属组织）起逐级上溯的主管字段。</summary>
    ValueTask<DirectoryManager> GetManagerAsync(Guid membershipId, CancellationToken cancellationToken = default);
    /// <summary>部门负责人：责任岗位的现任责任人与主管。</summary>
    ValueTask<OrganizationHead> GetOrganizationHeadAsync(Guid organizationId, CancellationToken cancellationToken = default);
    /// <summary>
    /// 上级链的反查：depth 为 1 时是以该成员为第 1 级上级责任人的成员（@下属），为 n 时是前 n 级，为空时是任一级（@子孙）；不含本人，按账号、成员 id 分页。
    /// </summary>
    ValueTask<IReadOnlyList<DirectoryMember>> GetSubordinatesAsync(Guid membershipId, int? depth = null, int offset = 0, int limit = 50, CancellationToken cancellationToken = default);
    /// <summary>
    /// 租户管理员直接新增成员（S1-IDP-017）：标识已对应自然人时绑定为本租户成员，否则新建自然人并设初始密码、首次登录强制修改。
    /// 经租户配额判定，满额返回 CARDINALITY_USER_LIMIT 且什么都不写；该自然人已是本租户成员（含已停用、已从目录删除）返回 DIRECTORY_MEMBER_EXISTS；
    /// 平台身份、已停用或已锁定的账号返回 DIRECTORY_MEMBER_INELIGIBLE。新成员与 <c>directory.member.created.v1</c> 同事务写入；
    /// 同一运行时装了授权组件时在提交前授予初始角色 <c>tenant.user</c>（R158），授予失败则整体不落库。
    /// </summary>
    ValueTask<DirectoryMemberAdded> AddMemberAsync(DirectoryMemberInput request, CancellationToken cancellationToken = default);
    /// <summary>按目录状态机变更成员；禁用/锁定即时吊销会话。</summary>
    ValueTask<Membership> SetDirectoryMemberStatusAsync(Guid membershipId, string status, string reason, CancellationToken cancellationToken = default);
    /// <summary>批量禁用（最多 100）；租户管理员逐项拒绝，其余各自独立事务。</summary>
    ValueTask<DirectoryBatchResult> DisableMembersAsync(IReadOnlyList<Guid> membershipIds, string reason, CancellationToken cancellationToken = default);
    /// <summary>删除是禁用并标记，不物理删除；管理员保护与批量禁用相同。</summary>
    ValueTask DeleteMemberAsync(Guid membershipId, string reason, CancellationToken cancellationToken = default);
}

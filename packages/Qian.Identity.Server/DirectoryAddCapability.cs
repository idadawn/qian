using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

// 新增成员的标准能力（S1-IDP-017）：与 HTTP 目录面共用 AddMemberAsync 的真实规则与 DirectoryMemberAddition 的审计。标准能力目录、输入校验与
// 24h 回执由 Capabilities 提供。输入里有初始密码（敏感字段），所以按 PRD 7.4.1「重置他人密码这类危险操作 humanOnly」不对 Agent 暴露，仍须人确认。
// 新建账号并占用配额：中风险（禁用为低风险）。
[Permission("member", "create", Risk = PermissionRisk.Medium)]
internal sealed class DirectoryAddCapability(IPluginContext owner, IdentityAccountService accounts) : IExecutableCapability, IDisposable
{
    internal const string Permission = "qian.identity.server:member:create";
    internal static CapabilityContract Definition { get; } = new("qian.identity.server",
        """{"name":"directory.member.add","description":"在当前租户新增成员：手机号或用户名已对应自然人时绑定为本租户成员，否则新建自然人并设初始密码、首次登录强制修改；受用户配额约束，新成员获得初始角色 tenant.user。","sideEffect":"write","policyHint":"confirm","idempotent":true,"async":"none","requiredScopes":[],"requiredSecrets":[],"dataClassification":"pii","costTier":"low","contractVersion":"directory-v1","audit":true,"agent":{"expose":false,"requiresConfirmation":true},"binding":{"kind":"command","id":"qian.identity.server/directory.member.add"},"requiredPermission":"qian.identity.server:member:create"}""",
        DirectoryCapabilityJson.Default.AddDirectoryMember, DirectoryCapabilityJson.Default.DirectoryMemberAddResult, ["initialPassword"]);
    // 能力输入是 camelCase：身份组件按 HTTP 请求（snake_case）给出的错误位置换成能力输入的字段。
    private static readonly Dictionary<string, string> Pointers = new(StringComparer.Ordinal)
    { ["#/identifier"] = "#/identifier", ["#/display_name"] = "#/displayName", ["#/initial_password"] = "#/initialPassword", ["#/account"] = "#/account" };
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;
    public CapabilityContract Contract => Definition;

    public async ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (Volatile.Read(ref _disposed) != 0) throw DirectoryCapabilityGuard.Missing();
        try
        {
            _ = owner.Services;
            if (!ReferenceEquals(caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, Definition.Id), this)) throw DirectoryCapabilityGuard.Missing();
        }
        catch (ObjectDisposedException) { throw DirectoryCapabilityGuard.Missing(); }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var (tenantId, personId) = await DirectoryCapabilityGuard.AuthorizeAsync(caller, accounts, Permission, linked.Token).ConfigureAwait(false);
        AddDirectoryMember request;
        try { request = input.Deserialize(DirectoryCapabilityJson.Default.AddDirectoryMember) ?? throw new JsonException(); }
        catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)); }
        if (request.IdempotencyKey == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.identity", EventActor.User(personId.ToString("D"))));
        var fingerprint = new AuditInputProjection(JsonSerializer.SerializeToElement(Definition.InputSchema), isCommand: true).InputHash(input)!;
        using var password = request.InitialPassword is null ? null : new AuthenticationProof(Encoding.UTF8.GetBytes(request.InitialPassword));
        DirectoryMemberAdded added;
        try
        {
            added = await DirectoryMemberAddition.RunAsync(accounts, caller.Audit(), tenantId, personId, "api", fingerprint, request.IdempotencyKey,
                new DirectoryMemberInput(request.Identifier, request.DisplayName) { InitialPassword = password, Account = request.Account }, linked.Token).ConfigureAwait(false);
        }
        catch (QianErrorException error) { throw DirectoryMemberAddition.Remap(error, Pointers); }
        var membership = added.Membership;
        return JsonSerializer.SerializeToElement(new DirectoryMemberAddResult(membership.Id, membership.PersonId, membership.Account, membership.Status, added.PersonCreated, added.GrantedRoles),
            DirectoryCapabilityJson.Default.DirectoryMemberAddResult);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) { _lifetime.Source.Cancel(); _lifetime.Source.Dispose(); }
    }
    private static (CancellationTokenSource Source, CancellationToken Token) CreateLifetime() { var source = new CancellationTokenSource(); return (source, source.Token); }
}

// 新增成员的执行与审计，HTTP 入口与标准能力共用（S1-IDP-017 验收 6）。记录进目标租户的审计链：操作人是 actor，租户是 tenant_id，
// 成员在 payload 里（成员 id、自然人 id、是否新建、本次授予的角色），不含标识原值、姓名与密码；输入只留脱敏投影的哈希。
// 开始记录写不进去就不执行；业务失败记 failure（只有稳定错误码），成功记 success。收尾写入有 5 秒上限，不受调用方取消影响。
internal static class DirectoryMemberAddition
{
    internal const string CapabilityId = "qian.identity.server/directory.member.add";
    internal const string ContractVersion = "directory-v1";

    internal static async Task<DirectoryMemberAdded> RunAsync(IdentityAccountService accounts, IAuditService? audit, Guid tenantId, Guid actor, string channel, string inputHash,
        Guid? idempotencyKey, DirectoryMemberInput input, CancellationToken ct)
    {
        var attempt = Guid.CreateVersion7(); var trace = ActivityTraceId.CreateRandom().ToHexString();
        AuditRecord Record(string result, JsonElement payload) => new(Guid.CreateVersion7(), tenantId.ToString("D"), "user", actor.ToString("D"), null,
            CapabilityId, ContractVersion, inputHash, channel, null, result, trace, DateTimeOffset.UtcNow, payload);
        if (audit is not null) await audit.AppendAsync(Record("started", JsonSerializer.SerializeToElement(new { attempt, idempotencyKey })), ct).ConfigureAwait(false);
        DirectoryMemberAdded added;
        try { added = await accounts.AddMemberAsync(input, ct).ConfigureAwait(false); }
        catch (Exception error) when (audit is not null)
        {
            using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var code = (error as QianErrorException)?.Error.Code.Code;
            await audit.AppendAsync(Record("failure", JsonSerializer.SerializeToElement(new { attempt, code })), finish.Token).ConfigureAwait(false);
            throw;
        }
        if (audit is not null)
        {
            using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await audit.AppendAsync(Record("success", JsonSerializer.SerializeToElement(new
            {
                attempt, membershipId = added.Membership.Id, personId = added.Membership.PersonId, personCreated = added.PersonCreated, grantedRoles = added.GrantedRoles
            })), finish.Token).ConfigureAwait(false);
        }
        return added;
    }

    // 映射表之外的位置换成整个请求体，不把身份组件内部的字段名带进别的契约。
    internal static QianErrorException Remap(QianErrorException error, IReadOnlyDictionary<string, string> pointers)
    {
        string Map(string pointer) => pointers.TryGetValue(pointer, out var mapped) ? mapped : "#";
        var source = error.Error;
        if (source.Pointer is null && source.Errors.Count == 0) return error;
        return new QianErrorException(new QianError(source.Code, source.Args)
        {
            Pointer = source.Pointer is null ? null : Map(source.Pointer), Allowed = source.Allowed,
            Errors = [.. source.Errors.Select(item => new QianFieldError(item.Code, Map(item.Pointer), item.Args) { Allowed = item.Allowed })]
        }, error);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AddDirectoryMember(Guid IdempotencyKey, string Identifier, string DisplayName, string? InitialPassword = null, string? Account = null)
{
    public override string ToString() => "AddDirectoryMember(redacted)";
}
internal sealed record DirectoryMemberAddResult(Guid MembershipId, Guid PersonId, string Account, string Status, bool PersonCreated, IReadOnlyList<string> GrantedRoles);

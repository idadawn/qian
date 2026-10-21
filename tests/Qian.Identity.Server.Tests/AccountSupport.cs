using System.Text;
using Qian.Identity.Abstractions;
using Qian.Tenancy;

namespace Qian.Identity.Server.Tests;

// 账号相关集成测试共用的造数与断言辅助。
internal static class AccountSupport
{
    internal const string Password = "Correct-Horse-9";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _sequence;

    internal static AuthenticationProof Proof(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <summary>每次返回不同的手机号，避免用例之间（共用一个库）互相占用。</summary>
    internal static string NextPhone() => "139" + (10_000_000 + Interlocked.Increment(ref _sequence) * 7 % 90_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static LoginInput Input(string identifier, string password, string client = "web", Guid? tenant = null) =>
        new(client, tenant, null, null, Proof(password)) { Identifier = identifier };

    /// <summary>创建一个带手机号（已验证）与初始密码的 Person；默认不强制改密，便于直接登录。</summary>
    internal static async Task<(Person Person, string Phone)> CreatePerson(AccountsHost.Running host, string name = "张三", string? phone = null, string password = Password,
        bool requireChange = false, bool platform = false, IReadOnlyList<IdentifierInput>? extra = null)
    {
        phone ??= NextPhone();
        using var proof = Proof(password);
        var identifiers = new List<IdentifierInput> { new(IdentifierKind.Phone, phone, true) };
        if (extra is not null) identifiers.AddRange(extra);
        var person = await host.Accounts.CreatePersonAsync(new CreatePerson(name, identifiers) { InitialPassword = proof, RequirePasswordChange = requireChange, PlatformAccess = platform }, Ct);
        return (person, phone);
    }

    /// <summary>在目标租户的作用域内为 Person 建立成员身份。</summary>
    internal static async Task<Membership> Join(AccountsHost.Running host, TenantDescriptor tenant, Guid person, string name = "成员", bool admin = false, string? account = null, Guid? id = null)
    {
        using var scope = host.Tenant.BeginScope(tenant.Id.ToString("D"));
        return await host.Accounts.CreateMembershipAsync(new CreateMembership(person, name) { IsTenantAdministrator = admin, Account = account, MembershipId = id }, Ct);
    }

    internal static async Task<T> Throws<T>(Func<Task> action) where T : Exception => await Assert.ThrowsAsync<T>(action);

    /// <summary>断言一次调用以指定的稳定错误码被拒绝，返回出错内容以便继续断言参数与位置。</summary>
    internal static async Task<Qian.Runtime.Errors.QianError> Rejected(string code, Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<Qian.Runtime.Errors.QianErrorException>(action);
        Assert.Equal(code, failure.Error.Code.Code);
        return failure.Error;
    }
}

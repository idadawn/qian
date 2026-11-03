using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Runtime;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

// 本租户摘要（S1-TEN-021）：外壳首页的试用提示、开通引导与企业信息读自这里。只给同一租户的人类用户；平台读租户仍走运营接口（超管旁路与审计）。
public sealed partial class TenantAdminTests
{
    [Fact]
    public async Task 本租户摘要只给同租户用户_平台主体其它租户Agent与无凭据被拒_不写旁路审计_字段与运营列表同口径_计数端口缺席时用量为null()
    {
        await using var ops = await StartOps();
        var (business, client) = (ops.Business, ops.Client);
        var root = business.Token(null, "", "root", "user");
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        // 两个租户：甲以新账号开户、20 人上限、试用 30 天；乙以已有账号开户、不限额、未激活。开通编排第 05–07 步建立管理员成员与指针。
        var account = await Read<TenantAdminAccount>(await Send(client, HttpMethod.Post, Accounts, root,
            new CreateTenantAdminAccount("管理员甲", TenantAdminIdentifierKind.Phone, NewPhone(), TemporaryPassword)), HttpStatusCode.Created);
        var first = await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, "/api/tenancy/tenants", root,
            new CreateTenant("summarya", "摘要甲") { Administrator = new(Guid.CreateVersion7(), account.SubjectId.ToString("D")), Quotas = new TenantQuotas(20) }), HttpStatusCode.Created);
        var until = DateTimeOffset.UtcNow.AddDays(30);
        Assert.Equal(TenantStatus.Trial, (await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, $"/api/tenancy/tenants/{first.Id}:activate", root,
            new ActivateTenant(Trial: true, ValidUntil: until)), HttpStatusCode.OK)).Status);
        var (person, _) = await NewPerson(business, "管理员乙");
        var second = await Read<TenantDescriptor>(await Send(client, HttpMethod.Post, "/api/tenancy/tenants", root,
            new CreateTenant("summaryb", "摘要乙") { Administrator = new(Guid.CreateVersion7(), person.Id.ToString("D")) }), HttpStatusCode.Created);
        var before = await PlatformEntries(source);

        // 同租户的用户只读到本租户：字段与运营列表同口径（开户管理员计 1 人），不带隔离策略与并发版本。
        using (var response = await Send(client, HttpMethod.Get, Summary(first.Id), business.Token(first.Id.ToString(), "", Guid.CreateVersion7().ToString(), "user")))
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
            var summary = JsonSerializer.Deserialize<JsonElement>(body);
            Assert.Equal("administratorName,code,id,name,status,usersLimit,usersUsed,validUntil",
                string.Join(",", summary.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
            Assert.Equal((first.Id.ToString(), "summarya", "摘要甲", "trial", 20, 1, "管理员甲"), (summary.GetProperty("id").GetString(), summary.GetProperty("code").GetString(),
                summary.GetProperty("name").GetString(), summary.GetProperty("status").GetString(), summary.GetProperty("usersLimit").GetInt32(), summary.GetProperty("usersUsed").GetInt32(),
                summary.GetProperty("administratorName").GetString()));
            Assert.True(Math.Abs((summary.GetProperty("validUntil").GetDateTimeOffset() - until).TotalMilliseconds) < 1);
        }
        var other = await Read<TenantSummary>(await Send(client, HttpMethod.Get, Summary(second.Id), business.Token(second.Id.ToString(), "", Guid.CreateVersion7().ToString(), "user")), HttpStatusCode.OK);
        Assert.Equal(new TenantSummary(second.Id, "summaryb", "摘要乙", TenantStatus.Pending) { UsersUsed = 1, AdministratorName = "管理员乙" }, other);

        // 无凭据 401；平台主体（超管与否）、其它租户的成员与本租户的 Agent 一律 403，不区分目标租户是否存在；令牌与路径一致但没有主档时 404。
        Assert.Null(await Rejected(await Send(client, HttpMethod.Get, Summary(first.Id), null), HttpStatusCode.Unauthorized, "UNAUTHENTICATED"));
        foreach (var token in new[] { root, business.Token(null, "", "intruder", "user"), business.Token(second.Id.ToString(), "", Guid.CreateVersion7().ToString(), "user"),
            business.Token(first.Id.ToString(), "", Guid.CreateVersion7().ToString(), "agent"), business.Token(first.Id.ToString(), "", "svc", "service") })
        {
            await Rejected(await Send(client, HttpMethod.Get, Summary(first.Id), token), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
            await Rejected(await Send(client, HttpMethod.Get, Summary(Guid.NewGuid()), token), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        }
        var missing = Guid.NewGuid();
        await Rejected(await Send(client, HttpMethod.Get, Summary(missing), business.Token(missing.ToString(), "", Guid.CreateVersion7().ToString(), "user")), HttpStatusCode.NotFound, "NOT_FOUND");

        // 本租户 scope 里看不到身份侧的用户计数端口（restrict 隐去）：用量为 null（页面显示「—」），不是 0，其它字段照旧；端口恢复后又是 1。
        var identity = business.Identity.Loader!.GetContext("identity", ScopeKey.Global);
        using (identity.Restrict(new ScopeKey("tenant", first.Id.ToString()), registration => registration.Key != TenantSettings.UserCounterKey))
        {
            var absent = await Read<JsonElement>(await Send(client, HttpMethod.Get, Summary(first.Id), business.Token(first.Id.ToString(), "", Guid.CreateVersion7().ToString(), "user")), HttpStatusCode.OK);
            Assert.False(absent.TryGetProperty("usersUsed", out var used) && used.ValueKind != JsonValueKind.Null, absent.GetRawText());
            Assert.Equal(("summarya", 20, "管理员甲"), (absent.GetProperty("code").GetString(), absent.GetProperty("usersLimit").GetInt32(), absent.GetProperty("administratorName").GetString()));
        }
        Assert.Equal(1, (await Read<TenantSummary>(await Send(client, HttpMethod.Get, Summary(first.Id), business.Token(first.Id.ToString(), "", Guid.CreateVersion7().ToString(), "user")), HttpStatusCode.OK)).UsersUsed);
        // 摘要不经超管旁路：平台链上没有新增记录。
        Assert.Equal(before, await PlatformEntries(source));
    }

    private static string Summary(Guid tenant) => $"/api/tenancy/tenants/{tenant}/summary";

    // 平台链的条数：分页读到末尾（每页 200），链上记录再多也不会少算。
    private static async Task<int> PlatformEntries(IPluginContext source)
    {
        var count = 0; long after = 0;
        while (true)
        {
            var page = await source.Audit().ReadPlatformAsync(source, after, 200, cancellationToken: Ct);
            count += page.Count;
            if (page.Count < 200) return count;
            after = page[^1].Sequence;
        }
    }
}

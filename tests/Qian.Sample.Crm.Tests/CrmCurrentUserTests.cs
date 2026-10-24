using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Identity.Abstractions;

namespace Qian.Sample.Crm.Tests;

// 参考应用身份适配器 CrmCurrentUser 的逐层单测（S1-YUE-016，R132 ②）：平台表面交快照（AcceptPlatformSurface）、本宿主验签（Capture）与 IsAuthenticated 的逐项比对各自单独钉住。
// 外壳整条链路（ShellInvokeTests）只能整体测到它们：平台表面验签时已核对 sub 与快照一致、Qian.Capabilities 对带 act 的主体先回 404、本宿主验签从不存 user 快照，
// 删掉适配器里的任何一项比对，那条链路照过。
public sealed class CrmCurrentUserTests
{
    private const string Tenant = "01900000-0000-7000-8000-000000000001";
    private const string Person = "0190a000-0000-7000-8000-0000000000a1";
    private const string Machine = "crm-shell-machine";
    private const string Agent = "crm-agent-preview";

    // 对照：平台表面交来、与已认证主体逐项一致的快照，三种主体都认（现有行为，S1-YUE-015 裁决 A1）；下面每条反例只改一项。
    [Theory, InlineData(PrincipalKind.User), InlineData(PrincipalKind.Service), InlineData(PrincipalKind.Agent)]
    public void 平台表面交来与已认证主体一致的快照_三种主体都认(PrincipalKind kind)
    {
        var http = Authenticated(Subject(kind), Name(kind)); var snapshot = Snapshot(Subject(kind), kind);
        CrmCurrentUser.AcceptPlatformSurface(http, snapshot);
        var user = Adapter(http);
        Assert.True(user.IsAuthenticated); Assert.Same(snapshot, user.Principal); Assert.Equal(Subject(kind), user.UserId);
    }

    [Theory, InlineData(PrincipalKind.User), InlineData(PrincipalKind.Service), InlineData(PrincipalKind.Agent)]
    public void 平台表面交来的快照主体与已认证主体不同_不认(PrincipalKind kind)
    {
        var http = Authenticated(Subject(kind), Name(kind));
        CrmCurrentUser.AcceptPlatformSurface(http, Snapshot(kind == PrincipalKind.User ? "0190a000-0000-7000-8000-0000000000b2" : Subject(kind) + "-other", kind));
        Assert.False(Adapter(http).IsAuthenticated);
    }

    // 主体相同、只有种类不同；任一方向都不认（user 冒充机器主体会绕过「user 只认平台表面」，机器主体冒充 user 会被按本人记审计）。
    [Theory, InlineData("user", PrincipalKind.Service), InlineData("service", PrincipalKind.User), InlineData("service", PrincipalKind.Agent), InlineData("agent", PrincipalKind.Service)]
    public void 平台表面交来的快照主体种类与已认证主体不同_不认(string authenticated, PrincipalKind kind)
    {
        var http = Authenticated(Person, authenticated);
        CrmCurrentUser.AcceptPlatformSurface(http, Snapshot(Person, kind));
        Assert.False(Adapter(http).IsAuthenticated);
    }

    // 带 act 的委托主体（S1 不开放委托）：快照位里什么也不留，Principal 也不外露。
    [Theory, InlineData(PrincipalKind.User), InlineData(PrincipalKind.Service), InlineData(PrincipalKind.Agent)]
    public void 平台表面交来带act的快照_不收也不认(PrincipalKind kind)
    {
        var http = Authenticated(Subject(kind), Name(kind));
        CrmCurrentUser.AcceptPlatformSurface(http, Snapshot(Subject(kind), kind, new DelegationActor { Subject = Agent, Kind = PrincipalKind.Agent }));
        var user = Adapter(http);
        Assert.False(user.IsAuthenticated); Assert.Null(user.Principal);
    }

    // 快照位只经 AcceptPlatformSurface 与 Capture 写入：两处都不存带 act 的快照，Capture 也从不存 user 快照，所以 IsAuthenticated 里对 act 与来源的比对在可达状态下是纵深防御。
    // 这里绕过两道入口直接放进快照位，单独钉住 IsAuthenticated 这一层；第一条是对照，证明直接放进去的快照本身会被认。
    [Fact]
    public void 绕过入口放进快照位_带act或本宿主来源的user快照IsAuthenticated仍不认_机器主体快照照认()
    {
        var control = Authenticated(Person, "user"); Plant(control, Snapshot(Person, PrincipalKind.User), platformSurface: true);
        Assert.True(Adapter(control).IsAuthenticated);
        foreach (var kind in new[] { PrincipalKind.User, PrincipalKind.Service })
        {
            var delegated = Authenticated(Subject(kind), Name(kind));
            Plant(delegated, Snapshot(Subject(kind), kind, new DelegationActor { Subject = Agent, Kind = PrincipalKind.Agent }), platformSurface: true);
            Assert.False(Adapter(delegated).IsAuthenticated);
        }
        var local = Authenticated(Person, "user"); Plant(local, Snapshot(Person, PrincipalKind.User), platformSurface: false);
        Assert.False(Adapter(local).IsAuthenticated);
        var machine = Authenticated(Machine, "service"); Plant(machine, Snapshot(Machine, PrincipalKind.Service), platformSurface: false);
        Assert.True(Adapter(machine).IsAuthenticated);
    }

    // 本宿主（受众 qian.sample.crm）在认证事件里只收服务与 Agent：已验证的 user 令牌、带 act 的机器令牌都判失败且不留快照。
    [Fact]
    public void 本宿主验签_已验证的user令牌与带act的机器令牌判失败不留快照_服务与Agent令牌照收()
    {
        var actor = new { sub = Agent, principal_kind = "agent" };
        foreach (var rejected in new[] { Validated(Person, "user"), Validated(Machine, "service", actor), Validated(Agent, "agent", actor) })
        {
            CrmCurrentUser.Capture(rejected);
            Assert.True(rejected.Result is { Succeeded: false, Failure: not null });
            Assert.Null(Adapter(rejected.HttpContext).Principal);
        }
        foreach (var (subject, kind) in new[] { (Machine, "service"), (Agent, "agent") })
        {
            var accepted = Validated(subject, kind);
            CrmCurrentUser.Capture(accepted);
            Assert.Null(accepted.Result);
            // 认证成功后 HttpContext.User 才是这份主身份。
            accepted.HttpContext.User = accepted.Principal!;
            var user = Adapter(accepted.HttpContext);
            Assert.True(user.IsAuthenticated); Assert.Equal((subject, kind), (user.Principal!.Subject, Name(user.Principal.Kind)));
        }
    }

    private static string Name(PrincipalKind kind) => kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => "agent" };
    private static string Subject(PrincipalKind kind) => kind switch { PrincipalKind.User => Person, PrincipalKind.Service => Machine, _ => Agent };
    private static CrmCurrentUser Adapter(HttpContext http) => new(new Accessor(http));

    // 已认证的请求：认证方案验签通过后 HttpContext.User 上的主身份（sub / principal_kind / tenant_id）。
    private static DefaultHttpContext Authenticated(string subject, string kind) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject), new("principal_kind", kind), new("tenant_id", Tenant)], JwtBearerDefaults.AuthenticationScheme))
    };

    // 验签后的主体快照（冻结的 S2S claim 集）；user 带成员与身份修订，机器主体不带。
    private static Principal Snapshot(string subject, PrincipalKind kind, DelegationActor? actor = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        return new Principal
        {
            Issuer = "https://gateway.qian.test/s2s", Audience = [PlatformSurfaceHost.Audience], Subject = subject, Kind = kind, TenantId = Tenant,
            MembershipId = kind == PrincipalKind.User ? Guid.CreateVersion7().ToString() : null, IdentityRevision = kind == PrincipalKind.User ? 1 : null,
            Roles = [], Scope = "crm.read crm.write", SessionId = id, Actor = actor, IssuedAt = now, NotBefore = now, ExpiresAt = now + 60, TokenId = id
        };
    }

    // 本宿主认证事件的上下文：令牌已验签（Capture 只在验签通过后调用，这里不再签名），主身份即载荷里的 claim（MapInboundClaims = false）。
    private static TokenValidatedContext Validated(string subject, string kind, object? actor = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "crm-command-acceptance", ["aud"] = new[] { "qian.sample.crm" }, ["sub"] = subject, ["principal_kind"] = kind, ["tenant_id"] = Tenant,
            ["roles"] = Array.Empty<string>(), ["scope"] = "crm.read crm.write", ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        if (kind == "user") { payload["membership_id"] = Guid.CreateVersion7().ToString(); payload["identity_revision"] = 1; }
        if (actor is not null) payload["act"] = actor;
        var token = new JsonWebToken(new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload)));
        return new TokenValidatedContext(new DefaultHttpContext(), new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        { Principal = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, JwtBearerDefaults.AuthenticationScheme)), SecurityToken = token };
    }

    // 快照位与快照类型都是适配器私有的（只许两道入口写入），测试经反射放置；改名时这里取不到成员直接报错，不会静默放过。
    private static void Plant(HttpContext http, Principal principal, bool platformSurface)
    {
        var key = typeof(CrmCurrentUser).GetField("Key", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? throw new MissingFieldException(nameof(CrmCurrentUser), "Key");
        var type = typeof(CrmCurrentUser).GetNestedType("Snapshot", BindingFlags.NonPublic) ?? throw new TypeLoadException("CrmCurrentUser.Snapshot");
        http.Items[key] = Activator.CreateInstance(type, principal, platformSurface);
    }

    // 不经 HttpContextAccessor 的静态 AsyncLocal：每条用例只看自己的请求。
    private sealed class Accessor(HttpContext http) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get => http; set => throw new NotSupportedException(); }
    }
}

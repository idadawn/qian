using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Qian.Audit;
using Qian.Gateway;
using Qian.Identity.Abstractions;
using Qian.Platform.Host;
using Qian.Runtime;
using Qian.Tenancy;
using Qian.Tenancy.Signup;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

// staging 首次部署的同进程形状（S1-DEP-011，R143；装配方式 R150 ①）：生产平台宿主 hosts/Qian.Platform.Host 并入身份分支（Identity:InProcess=true），网关是独立进程，
// 用网关宿主 appsettings.json 的生产路由表。数据库从空库开始，角色照部署侧的建角色脚本建好（应用登录沿用基线的 qian_app，库的 CONNECT 收回 PUBLIC 后逐个授予），
// 之后的一切都走生产入口：迁移 Job（--migrate-only，管理员连接做离线 DDL，迁移属主执行包内迁移）→ 首个平台运营账号的引导 Job（--bootstrap-operator）→ 在线副本。
// 没有开发路由、开发种子与开发期发件箱；只有开通申请审核那一段为了拿到验证码，在宿主起来之后另挂一个测试发件端口（生产在 D4 下没有发件端口，R150 ④）。
public sealed class PlatformIdentityHostTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string GatewayClientId = "qian-gateway";
    private const string Resource = "https://api.qian.test";
    private const string S2sIssuer = "https://gateway.qian.test/s2s";
    private const string Migrator = "qian_migrator", AuthzRole = "qian_platform_authz", AuditRole = "qian_platform_audit", AuditWriter = "qian_audit_writer";
    private const string GatewayTenantRole = "qian_gateway_audit_tenant", GatewayPlatformRole = "qian_gateway_audit_platform", GatewayWriter = "qian_gateway_audit_writer";
    private const string OperatorName = "ops", OperatorInitial = "Bootstrap-Horse-2026", OperatorPassword = "Operator-Horse-2026";
    private const string AdminInitial = "Initial-Horse-2026", AdminPassword = "Admin-Horse-2026";
    // 同进程形状下宿主预装的 system 级组件：platform profile 启用的条目加身份扩展换装与新增的两个。
    private static readonly string[] PreinstalledSystem = ["qian.audit", "qian.authz", "qian.capabilities", "qian.config", "qian.data", "qian.events", "qian.identity.server",
        "qian.lifecycle", "qian.registry", "qian.tenancy", "qian.tenancy.signup"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 同进程形状_迁移Job可重复_引导Job只建一次首个平台运营_运营经外壳登录开户激活_租户管理员登录选租户进工作台_另一租户看不到_没有发件端口发码503_开通申请审核通过_授权变更后旧修订401换令牌后恢复()
    {
        using var tls = GatewayTestHost.Certificate(); using var gatewayPort = LoopbackPort.Reserve(); using var platformPort = LoopbackPort.Reserve();
        var shell = new Uri($"https://localhost:{gatewayPort.Port}/"); var issuer = new Uri($"http://127.0.0.1:{platformPort.Port}/");
        var directory = Directory.CreateTempSubdirectory("qian-staging-shape-");
        try
        {
            await using var redis = new RedisFixture(); await redis.InitializeAsync();
            var (settings, clientSecret) = await Prepare(directory.FullName, issuer, shell);
            string[] args = ["--environment=Development", "--patch", Path.Combine(directory.FullName, "environment.yml")];
            // 网关先起（身份发行方的元数据按需取），平台宿主再取网关公开的 JWKS 验 S2S。
            var (routes, clusters) = ProductionRoutes(issuer);
            using var signing = RSA.Create(2048);
            await using var gateway = await GatewayTestHost.Start(gatewayPort, tls, issuer.AbsoluteUri, GatewayClientId, Resource, clientSecret, redis.ConnectionString, S2sIssuer,
                routes, clusters, signing, map: app => app.MapQianShellBootstrap(),
                rateLimits: new GatewayRateLimits { KeyPrefix = "qian:staging-shape:" + Guid.NewGuid().ToString("N"), IpPermitLimit = 10_000, TenantPermitLimit = 10_000, UserPermitLimit = 10_000 });
            settings["S2S:JwksUri"] = new Uri(gateway.HttpAddress, ".well-known/jwks.json").AbsoluteUri;

            // 一、迁移 Job（--migrate-only）跑两遍：第二遍不改动也不报错。之后每个业务 schema 都在、租户表强制 RLS、包内迁移有历史、两个平台登录与网关审计角色的授权就位；
            //     运行期的应用登录只有增删改查，不能建表，迁移历史只读。
            for (var round = 0; round < 2; round++)
            {
                await using var job = Build(["--migrate-only", .. args], settings);
                await PlatformMigrations.RunAsync(job, Ct);
            }
            Assert.Equal(["plugin_qian_audit", "plugin_qian_authz", "plugin_qian_capabilities", "plugin_qian_identity", "plugin_qian_lifecycle", "plugin_qian_registry",
                "plugin_qian_tenancy", "plugin_qian_tenancy_signup"],
                await Strings("SELECT nspname FROM pg_namespace WHERE nspname LIKE 'plugin_qian_%' AND nspname <> 'plugin_qian_data' ORDER BY 1"));
            // 租户表（tenant_id 非空，与 Qian 迁移生成器补 RLS 的判据相同）一律 FORCE RLS；outbox 等 tenant_id 可空的表按设计不加。离线建表与包内迁移两条路径都在内。
            Assert.Empty(await Strings("""
                SELECT n.nspname || '.' || c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind = 'r' AND n.nspname LIKE 'plugin\_qian\_%' AND c.relname NOT LIKE '\_\_%'
                  AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND a.attnotnull AND NOT a.attisdropped)
                  AND NOT (c.relrowsecurity AND c.relforcerowsecurity)
                """));
            Assert.NotEmpty(await Strings("""
                SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'plugin_qian_tenancy' AND c.relrowsecurity AND c.relforcerowsecurity
                """));
            Assert.NotEmpty(await Strings("SELECT \"MigrationId\" FROM plugin_qian_identity.\"__EFMigrationsHistory\""));
            Assert.NotEmpty(await Strings("SELECT \"MigrationId\" FROM plugin_qian_tenancy_signup.\"__EFMigrationsHistory\""));
            Assert.Equal(["qian_audit_native_guard"], await Strings("SELECT DISTINCT polname FROM pg_policy WHERE polname = 'qian_audit_native_guard'"));
            Assert.Equal(["true|false|false"], await Strings($"""
                SELECT has_table_privilege('{PostgresFixture.AppRoleName}', 'plugin_qian_tenancy.tenants', 'INSERT')::text || '|' ||
                       has_schema_privilege('{PostgresFixture.AppRoleName}', 'plugin_qian_tenancy', 'CREATE')::text || '|' ||
                       has_table_privilege('{PostgresFixture.AppRoleName}', 'plugin_qian_identity."__EFMigrationsHistory"', 'INSERT')::text
                """));

            // 二、系统组件修订的安装 Job（--install-system-revisions，R151 ①）：签名清单照 scripts/sign-system-revisions.py 的口径由测试的 Ed25519 钥生成（公钥在 Secret 挂载里，
            //     信任根经 secretRef 引用）。清单缺一项即失败（退出码 1）、什么都不装；完整清单跑两遍，每个预装的 system 级组件恰好一个已激活修订，第二遍不再安装或激活。
            var incomplete = Path.Combine(directory.FullName, "system-revisions-incomplete.json");
            var signatures = JsonNode.Parse(await File.ReadAllTextAsync(settings[PlatformSystemRevisions.SignaturesFileKey]!, Ct))!;
            var partial = signatures.DeepClone(); partial["artifacts"]!.AsArray().RemoveAt(0);
            await File.WriteAllTextAsync(incomplete, partial.ToJsonString(), Ct);
            Assert.Equal(1, await PlatformHost.RunAsync([PlatformSystemRevisions.Argument, .. args], PlatformIdentity.Extend,
                builder => Configure(builder, new Dictionary<string, string?>(settings) { [PlatformSystemRevisions.SignaturesFileKey] = incomplete }, port: null)));
            Assert.Equal(["0"], await Strings("SELECT count(*)::text FROM plugin_qian_lifecycle.plugin_revisions"));
            for (var round = 0; round < 2; round++)
            {
                await using var job = Build([PlatformSystemRevisions.Argument, .. args], settings);
                await PlatformSystemRevisions.RunAsync(job, Ct);
            }
            // 签名脚本签仓库（镜像）里全部 system 级 module 组件，Job 只装宿主预装的那些。
            Assert.Subset(signatures["artifacts"]!.AsArray().Select(item => (string)item!["plugin"]!).ToHashSet(StringComparer.Ordinal), PreinstalledSystem.ToHashSet(StringComparer.Ordinal));
            Assert.Equal(PreinstalledSystem, await Strings("SELECT plugin_id FROM plugin_qian_lifecycle.plugin_revisions WHERE state = 'Active' ORDER BY 1"));
            Assert.Equal([$"11|{signatures["keyId"]}|{signatures["keyId"]}"], await Strings("""
                SELECT count(*)::text || '|' || min(signed_by) || '|' || max(signed_by) FROM plugin_qian_lifecycle.plugin_revisions
                """));

            // 三、引导 Job（--bootstrap-operator）：首次建平台身份 ops 并授平台超管，平台审计链记一条；再跑一次即拒绝（退出码 1），库里仍只有一个平台超管。
            await using (var job = Build([PlatformOperatorBootstrap.Argument, .. args], settings)) await PlatformOperatorBootstrap.RunAsync(job, Ct);
            Assert.Equal(1, await PlatformHost.RunAsync([PlatformOperatorBootstrap.Argument, .. args], PlatformIdentity.Extend, builder => Configure(builder, settings, port: null)));
            Assert.Equal(["1"], await Strings("""
                SELECT count(*)::text FROM plugin_qian_authz.role_assignments a JOIN plugin_qian_authz.roles r ON r.id = a.role_id
                WHERE r.code = 'platform.superadmin' AND a.tenant_id IS NULL
                """));

            // 四、在线副本：生产入口，同一进程里是平台插件树加身份分支。
            platformPort.Dispose();
            await using var platform = Build(args, settings, platformPort.Port);
            await platform.StartAsync(Ct);
            var runtime = platform.Services.GetRequiredService<PlatformRuntime>();
            Assert.True(runtime.IsReady);
            Assert.Equal(["audit", "authz", "capabilities", "config", "data", "events", "identity", "lifecycle", "registry", "tenancy", "tenancy.signup"],
                runtime.Profile.Entries.Select(entry => entry.Id).Order(StringComparer.Ordinal));
            Assert.Equal("qian.identity.server@1.0.0", runtime.Profile.Entries.Single(entry => entry.Id == "identity").Plugin);
            foreach (var entry in runtime.Profile.Entries) Assert.False(entry.Manifest.TryGetProperty("x-skeleton", out _), entry.Id);
            var trail = await PlatformChain(runtime);
            var bootstrapRecord = Assert.Single(trail, record => record.CapabilityId == "qian.authz:platform-role:bootstrap");
            Assert.Equal(("service", "platform-bootstrap", "job", "allowed"), (bootstrapRecord.ActorType, bootstrapRecord.ActorId, bootstrapRecord.Channel, bootstrapRecord.Result));
            using (var anonymous = new HttpClient())
            {
                Assert.Equal("ready", await anonymous.GetStringAsync(new Uri(issuer, "readyz"), Ct));
                using var discovery = JsonDocument.Parse(await anonymous.GetStringAsync(new Uri(issuer, ".well-known/openid-configuration"), Ct));
                Assert.Equal(issuer.AbsoluteUri, discovery.RootElement.GetProperty("issuer").GetString());
                // 没有开发路由与开发期发件箱。
                foreach (var path in new[] { "__dev/signup/outbox?phone=13900000000", "__dev/identity/bump" })
                    using (var dev = await anonymous.GetAsync(new Uri(issuer, path), Ct)) Assert.NotEqual(HttpStatusCode.OK, dev.StatusCode);
            }

            // 五、平台运营经外壳登录：首登改掉引导密码，进平台控制台——不代管，控制台目录给出租户运营与开通申请两个平台组件的远程与菜单。
            var (operatorBrowser, operatorCookies) = GatewayTestHost.Browser(tls); using var _1 = operatorBrowser;
            Assert.Equal("password_change_required", await SignIn(operatorBrowser, issuer, OperatorName, OperatorInitial, null, OperatorPassword));
            await gateway.CompleteLogin(operatorBrowser);
            var console = await Bootstrap(operatorBrowser, shell);
            Assert.False((bool)console["managed"]!);
            Assert.Equal(["global|qian.tenancy", "global|qian.tenancy.signup"], console["mounted"]!.AsArray().Select(item => (string)item!).Order(StringComparer.Ordinal));
            Assert.Equal(["/tenant-applications", "/tenants"], console["navigation"]!.AsArray().Select(item => (string)item!["route"]!).Order(StringComparer.Ordinal));

            // 六、运营开户（经网关的运营簇）：新建管理员账号 → 开户（开通编排第 05 步经同进程的身份端口建管理员成员，第 07 步授 tenant.owner）→ 激活。甲乙两个租户。
            var operatorCsrf = await Csrf(operatorBrowser, shell);
            var adminA = await NewAdmin(operatorBrowser, shell, operatorCsrf, "甲方管理员", "acmeadmin");
            var adminB = await NewAdmin(operatorBrowser, shell, operatorCsrf, "乙方管理员", "betaadmin");
            var tenantA = await OpenTenant(operatorBrowser, shell, operatorCsrf, runtime, "acme", "甲方科技", adminA);
            var tenantB = await OpenTenant(operatorBrowser, shell, operatorCsrf, runtime, "beta", "乙方贸易", adminB);
            Assert.Contains(tenantA.ToString("D"), Ids(await Text(operatorBrowser, shell, "api/tenancy/tenants")));

            // 七、租户管理员登录：首登改密并选甲方，回到外壳进工作台——bootstrap 的上下文是甲方、不代管；本租户摘要能读。
            var (adminBrowser, _) = GatewayTestHost.Browser(tls); using var _2 = adminBrowser;
            Assert.Equal("password_change_required", await SignIn(adminBrowser, issuer, "acmeadmin", AdminInitial, tenantA, AdminPassword));
            await gateway.CompleteLogin(adminBrowser);
            var workbench = await Bootstrap(adminBrowser, shell);
            Assert.False((bool)workbench["managed"]!);
            Assert.Equal(tenantA.ToString("D"), (string?)workbench["context"]!["tenant"]!["id"]);
            var summary = JsonNode.Parse(await Text(adminBrowser, shell, $"api/tenancy/tenants/{tenantA}/summary"))!;
            Assert.Equal(("acme", "甲方管理员"), ((string?)summary["code"], (string?)summary["administratorName"]));

            // 八、另一个租户的管理员看不到它的数据：乙方管理员的工作台是乙方；读甲方的本租户摘要 403，读运营接口的租户列表 403。
            var (otherBrowser, _) = GatewayTestHost.Browser(tls); using var _3 = otherBrowser;
            await SignIn(otherBrowser, issuer, "betaadmin", AdminInitial, tenantB, AdminPassword);
            await gateway.CompleteLogin(otherBrowser);
            Assert.Equal(tenantB.ToString("D"), (string?)(await Bootstrap(otherBrowser, shell))["context"]!["tenant"]!["id"]);
            await Status(otherBrowser, shell, $"api/tenancy/tenants/{tenantA}/summary", HttpStatusCode.Forbidden, "AUTHZ_DENIED");
            using (var list = await otherBrowser.GetAsync(new Uri(shell, "api/tenancy/tenants"), Ct)) Assert.True(list.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, list.StatusCode.ToString());

            // 九、自助申请（D4，R150 ④）：没有发件端口时发验证码 503 SIGNUP_UNAVAILABLE，运营开户照常（上面两户、下面审核开出的第三户）。
            //     挂上测试发件端口后走完：核验手机号 → 提交申请（新账号）→ 运营在开通申请里审核通过（试用套餐）→ 租户开通并进入试用，申请人以管理员登录新租户。
            using (var applicant = new SignupBrowser(issuer))
            {
                using var unavailable = await applicant.Send(HttpMethod.Post, "/api/signup/verifications", new { phone = "13912345678" });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
                Assert.Equal("SIGNUP_UNAVAILABLE", JsonDocument.Parse(await unavailable.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("code").GetString());
            }
            var outbox = new TestOutbox();
            await runtime.Loader.MountAsync([new PluginDefinition(new PluginEntry("tenancy.signup.messenger", "test.signup-messenger", "1.0.0", ScopeKey.Global, 0,
                JsonSerializer.SerializeToElement(new { })), new MessengerPlugin(outbox), [TenancySignupDefinition.MessengerKey])], Ct);
            Guid applicationId;
            using (var applicant = new SignupBrowser(issuer))
            {
                var challenge = (await Json(await applicant.Send(HttpMethod.Post, "/api/signup/verifications", new { phone = "13912345678" }), HttpStatusCode.Accepted)).GetProperty("id").GetGuid();
                await Json(await applicant.Send(HttpMethod.Post, "/api/signup/session", new { verificationId = challenge, code = outbox.Code(challenge) }), HttpStatusCode.OK);
                var submitted = await Json(await applicant.Send(HttpMethod.Post, "/api/signup/me/applications", new Dictionary<string, object?>
                {
                    ["companyName"] = "丙方物流", ["code"] = "gamma", ["size"] = "1-20", ["acceptTerms"] = true, ["displayName"] = "丙方申请人", ["password"] = AdminPassword
                }), HttpStatusCode.Created);
                applicationId = submitted.GetProperty("id").GetGuid();
            }
            var applications = JsonNode.Parse(await Text(operatorBrowser, shell, "api/signup/applications?status=submitted"))!;
            Assert.Contains(applicationId.ToString("D"), applications["items"]!.AsArray().Select(item => (string?)item!["id"]));
            var approved = await Send(operatorBrowser, shell, operatorCsrf, HttpMethod.Post, $"api/signup/applications/{applicationId}:approve", new { plan = "trial" }, HttpStatusCode.OK);
            Assert.Equal("approved", (string?)approved["status"]);
            var gamma = JsonNode.Parse(await Text(operatorBrowser, shell, "api/tenancy/tenants?keyword=gamma"))!["items"]!.AsArray().Single(item => (string?)item!["code"] == "gamma")!;
            // 按试用套餐通过：租户开通并进入试用（可登录可用，期满按试用规则处理）。
            Assert.Equal("trial", (string?)gamma["status"]);
            var (applicantBrowser, _) = GatewayTestHost.Browser(tls); using var _4 = applicantBrowser;
            await SignIn(applicantBrowser, issuer, "13912345678", AdminPassword, Guid.Parse((string)gamma["id"]!), null);
            await gateway.CompleteLogin(applicantBrowser);
            Assert.Equal((string?)gamma["id"], (string?)(await Bootstrap(applicantBrowser, shell))["context"]!["tenant"]!["id"]);

            // 十、授权变更后旧修订 401（S1-DEC-027）：运营经本宿主把甲方管理员换成另一个账号。tenant.owner 的撤销在同一进程里递增原管理员的身份修订并抬高 PEP 下限，
            //     原管理员手里的会话（旧修订）再经网关调平台表面一律 401；换令牌（刷新会话）拿到新修订后恢复 200。
            //     身份修订按成员计（令牌修订 = 自然人修订 + 本租户成员修订）：比对甲方租户里原管理员成员的修订与 Authz 记下的 PEP 下限。
            var membershipRevision = $"SELECT identity_revision::text FROM plugin_qian_identity.memberships WHERE tenant_id = '{tenantA}' AND person_id = '{adminA}'";
            var floor = $"""
                SELECT identity_revision::text FROM plugin_qian_authz.subject_identity_revisions WHERE tenant_id = '{tenantA}' AND subject_type = 'user' AND subject_id = '{adminA}'
                """;
            var before = long.Parse(Assert.Single(await Strings(membershipRevision)), System.Globalization.CultureInfo.InvariantCulture);
            var successor = await NewAdmin(operatorBrowser, shell, operatorCsrf, "甲方新管理员", "acmenext");
            await Send(operatorBrowser, shell, operatorCsrf, HttpMethod.Put, $"api/tenancy/tenants/{tenantA}/admins", new { items = new[] { new { subjectId = successor } } }, HttpStatusCode.OK);
            var after = long.Parse(Assert.Single(await Strings(membershipRevision)), System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(after > before, $"成员修订没有递增：{before} → {after}");
            // PEP 下限高于原管理员手里令牌的修订（自然人修订 + 变更前的成员修订），不高于身份侧的当前修订（撤管理员标志时身份侧可能再递增一次，下限只随 Authz 的那次传播抬高）。
            var person = (await runtime.Global("identity").IdentityAccounts().FindPersonAsync("acmeadmin", Ct))!;
            var raised = long.Parse(Assert.Single(await Strings(floor)), System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(raised, person.IdentityRevision + before + 1, person.IdentityRevision + after);
            await Status(adminBrowser, shell, "api/authz/me/permissions", HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
            using (var stale = await adminBrowser.GetAsync(new Uri(shell, "api/shell/bootstrap"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            await RefreshSession(adminBrowser, shell);
            Assert.Equal(tenantA.ToString("D"), (string?)(await Bootstrap(adminBrowser, shell))["context"]!["tenant"]!["id"]);
            await Text(adminBrowser, shell, "api/authz/me/permissions");

            // 十一、身份分支只采信可信代理的转发头（Identity:TrustedProxyNetworks / ForwardLimit）：经代理链转来的登录恢复原始 HTTPS 与客户端地址——账号 Cookie 带 Secure，
            //      会话记下回溯两跳得到的客户端；客户端自己塞进 X-Forwarded-For 的最左一项在上限之外，不被采信。不带转发头的直连明文请求照旧：不带 Secure，记连接地址。
            foreach (var forwarded in new[] { true, false })
            {
                using var probe = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true, AllowAutoRedirect = false });
                var agent = "qian-proxy-probe/" + (forwarded ? "forwarded" : "direct");
                using var csrf = await probe.GetAsync(new Uri(issuer, "api/identity/csrf"), Ct); csrf.EnsureSuccessStatusCode();
                var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
                using var login = new HttpRequestMessage(HttpMethod.Post, new Uri(issuer, "api/identity/login"))
                {
                    Content = JsonContent.Create(new { client_id = GatewayClientId, identifier = OperatorName, password = OperatorPassword })
                };
                login.Headers.Add(token.GetProperty("header").GetString()!, token.GetProperty("token").GetString());
                login.Headers.UserAgent.ParseAdd(agent);
                if (forwarded) { login.Headers.Add("X-Forwarded-For", "198.51.100.7, 203.0.113.9, 192.0.2.10"); login.Headers.Add("X-Forwarded-Proto", "https"); }
                using var response = await probe.SendAsync(login, Ct);
                Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
                var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.qian.identity.signin=", StringComparison.Ordinal));
                Assert.Equal(forwarded, cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
                Assert.Equal([forwarded ? "203.0.113.9" : "127.0.0.1"], await Strings($"SELECT remote_address FROM plugin_qian_identity.sessions WHERE user_agent = '{agent}'"));
            }
        }
        finally { NpgsqlConnection.ClearAllPools(); directory.Delete(true); }
    }

    // 部署侧建角色（S1-DEP-001 基线之后、迁移 Job 之前）：迁移属主可建 schema，两个平台登录与网关审计登录是独立的 NOINHERIT 受限登录，审计函数属主是没有成员关系的 NOLOGIN 角色；
    // 库的 CONNECT 照基线收回 PUBLIC，逐个授予。再写 Secret 文件（连接串、身份证书与私钥、网关客户端密钥、引导密码）、环境 overlay 与宿主配置。
    private async Task<(Dictionary<string, string?> Settings, string ClientSecret)> Prepare(string directory, Uri issuer, Uri shell)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        const string Restricted = "NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION";
        await Admin($"""
            CREATE ROLE {Migrator} LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE {AuthzRole} LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE {AuditRole} LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE {AuditWriter} NOLOGIN {Restricted};
            CREATE ROLE {GatewayTenantRole} LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE {GatewayPlatformRole} LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE {GatewayWriter} NOLOGIN {Restricted};
            REVOKE ALL ON DATABASE {PostgresFixture.DatabaseName} FROM PUBLIC;
            GRANT CONNECT ON DATABASE {PostgresFixture.DatabaseName} TO {PostgresFixture.AppRoleName}, {AuthzRole}, {AuditRole}, {GatewayTenantRole}, {GatewayPlatformRole};
            GRANT CONNECT, CREATE ON DATABASE {PostgresFixture.DatabaseName} TO {Migrator};
            """);
        var clientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ConnectionFiles:App"] = postgres.AppConnectionString, ["ConnectionFiles:PlatformAuthz"] = Login(AuthzRole), ["ConnectionFiles:PlatformAudit"] = Login(AuditRole),
            ["ConnectionFiles:Migrations"] = Login(Migrator), ["ConnectionFiles:MigrationAdmin"] = postgres.ConnectionString,
            ["Identity:Clients:0:SecretFile"] = clientSecret, ["Bootstrap:PasswordFile"] = OperatorInitial
        };
        foreach (var purpose in new[] { "Signing", "Encryption" })
        {
            using var key = RSA.Create(2048);
            using var certificate = new CertificateRequest("CN=Qian staging identity " + purpose, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            files["Identity:" + purpose + "Certificate"] = certificate.ExportCertificatePem();
            files["Identity:" + purpose + "Key"] = key.ExportPkcs8PrivateKeyPem();
        }
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Root"] = RepositoryRoot(), ["S2S:Issuer"] = S2sIssuer, ["S2S:AllowHttpJwks"] = "true", ["Secrets:MountRoot"] = directory,
            ["Identity:InProcess"] = "true", ["Identity:AllowLoopbackHttp"] = "true", ["Identity:PagesRoot"] = Pages(directory, "identity-pages"),
            // 可信代理链（staging 是 hub → frp → Traefik）：测试客户端在回环上充当最后一跳代理，192.0.2.0/24 充当上一跳；回溯两跳。
            ["Identity:TrustedProxyNetworks:0"] = "127.0.0.0/8", ["Identity:TrustedProxyNetworks:1"] = "192.0.2.0/24", ["Identity:ForwardLimit"] = "2",
            ["Identity:Clients:0:ClientId"] = GatewayClientId, ["Identity:Clients:0:RedirectUri"] = new Uri(shell, "auth/callback").AbsoluteUri, ["Identity:Clients:0:Resource"] = Resource,
            ["Migrations:AppRole"] = PostgresFixture.AppRoleName, ["Migrations:AuditWriterRole"] = AuditWriter,
            ["Migrations:GatewayAudit:TenantRole"] = GatewayTenantRole, ["Migrations:GatewayAudit:PlatformRole"] = GatewayPlatformRole, ["Migrations:GatewayAudit:FunctionOwner"] = GatewayWriter,
            ["Bootstrap:Username"] = OperatorName, ["Bootstrap:DisplayName"] = "平台运营",
            // Lifecycle 信任根：公钥在 Secret 挂载 <MountRoot>/qian/trust-platform/public-key（平台宿主的固定绑定），签名清单由 ConfigMap 挂载。
            ["Trust:PlatformKeys:0:PublicKey"] = "secretRef://platform/trust/platform",
            [PlatformSystemRevisions.SignaturesFileKey] = await SystemRevisionSignatures(directory)
        };
        foreach (var (name, contents) in files)
        {
            var file = Path.Combine(directory, name.Replace(':', '-').ToLowerInvariant());
            await GatewayTestHost.WritePrivate(file, contents);
            settings[name] = file;
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "environment.yml"), JsonSerializer.Serialize(new object[]
        {
            new { id = "authz", config = new { advancedMode = false, platformRole = AuthzRole } },
            new { id = "audit", config = new { chain = new { store = "pg", platformRole = AuditRole } } },
            new { id = "events", config = new { } },
            new { id = "identity", config = new { issuer = issuer.AbsoluteUri } }
        }), Ct);
        return (settings, clientSecret);

        string Login(string role) => new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
    }

    // 认证页族的构建产物由镜像带上；集成测试只需要一个带 index.html 的目录（页面本身由前端测试与联调环境的 E2E 覆盖）。
    private static string Pages(string directory, string name)
    {
        var pages = Directory.CreateDirectory(Path.Combine(directory, name)).FullName;
        File.WriteAllText(Path.Combine(pages, "index.html"), "<!doctype html><title>Qian</title>");
        return pages;
    }

    // 照 scripts/sign-system-revisions.py 生成签名清单：仓库 plugins/ 下 system 级、module 形态、非骨架的组件文件夹，D4 目录摘要（与平台宿主预装绑定同一算法），
    // Ed25519 签摘要的 32 字节原始值。私钥只在本方法里；公钥写进 Secret 挂载，供 Lifecycle 信任根经 secretRef 读取。
    private static async Task<string> SystemRevisionSignatures(string directory)
    {
        var key = new Ed25519PrivateKeyParameters(new SecureRandom());
        var spki = Convert.FromHexString("302a300506032b6570032100").Concat(key.GeneratePublicKey().GetEncoded()).ToArray();
        var trust = Directory.CreateDirectory(Path.Combine(directory, "qian", "trust-platform")).FullName;
        await File.WriteAllTextAsync(Path.Combine(trust, "public-key"), "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(spki) + "\n-----END PUBLIC KEY-----\n", Ct);
        var artifacts = new List<object>();
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(RepositoryRoot(), "plugins")).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(folder, ".qian-plugin", "plugin.json");
            if (!File.Exists(manifestPath)) continue;
            using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath, Ct));
            var root = manifest.RootElement;
            if (Field(root, "policy", "tier") != "system" || Field(root, "runtime", "form") != "module" ||
                root.TryGetProperty("x-skeleton", out var skeleton) && skeleton.ValueKind == JsonValueKind.True) continue;
            var digest = FolderDigest(folder);
            var message = Convert.FromHexString(digest["sha256:".Length..]);
            var signer = new Ed25519Signer(); signer.Init(true, key); signer.BlockUpdate(message, 0, message.Length);
            artifacts.Add(new { plugin = root.GetProperty("id").GetString(), version = root.GetProperty("version").GetString(), digest, signature = Convert.ToBase64String(signer.GenerateSignature()) });
        }
        var file = Path.Combine(directory, "system-revisions.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
        {
            format = PlatformSystemRevisions.Format, keyId = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(spki)), source = "dir:plugins", artifacts
        }), Ct);
        return file;

        static string? Field(JsonElement element, string parent, string name) =>
            element.TryGetProperty(parent, out var section) && section.ValueKind == JsonValueKind.Object && section.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // D4 目录摘要：按序数路径排序的「路径\t字节数\tSHA-256」清单再取 SHA-256；bin、obj、node_modules、dist 不属于制品。
    private static string FolderDigest(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(folder, file).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules" or "dist"))
            .ToDictionary(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes, StringComparer.Ordinal);
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(inventory)));
    }

    private static WebApplication Build(string[] args, IReadOnlyDictionary<string, string?> settings, int? port = null) =>
        PlatformHost.Build(args, PlatformIdentity.Extend, builder => Configure(builder, settings, port));

    private static void Configure(WebApplicationBuilder builder, IReadOnlyDictionary<string, string?> settings, int? port)
    {
        builder.WebHost.UseUrls(port is null ? "http://127.0.0.1:0" : $"http://127.0.0.1:{port}"); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings);
        PlatformIdentity.Configure(builder);
    }

    // 网关宿主交付的路由表与簇（hosts/Qian.Gateway.Host/appsettings.json），簇地址换成本宿主的回环地址；开通申请的两条运营路由进运营簇。
    private static (GatewayRoute[] Routes, GatewayCluster[] Clusters) ProductionRoutes(Uri platform)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(RepositoryRoot(), "hosts", "Qian.Gateway.Host", "appsettings.json"), optional: false, reloadOnChange: false).Build();
        var routes = configuration.GetSection("Gateway:Routes").GetChildren().Select(item => new GatewayRoute(item["Id"] ?? "", item["Path"] ?? "", item["Cluster"] ?? "")
            { RateLimiterPolicy = item["RateLimiterPolicy"] ?? "" }).ToArray();
        foreach (var path in new[] { "/api/tenancy/{**rest}", "/api/signup/applications/{**rest}", "/api/signup/meta/{**rest}" })
            Assert.Equal(PlatformHostSettings.OperationsAudience, routes.Single(route => route.Path == path).Cluster);
        return (routes, [.. configuration.GetSection("Gateway:Clusters").GetChildren().Select(cluster => new GatewayCluster(cluster["Id"]!, platform.AbsoluteUri))]);
    }

    // 在发行方的账号接口完成密码登录：先取防伪令牌；要求首登改密时换成 newPassword，要求选择时选 tenant 的成员身份。返回第一步的状态。
    private static async Task<string> SignIn(HttpClient browser, Uri issuer, string identifier, string password, Guid? tenant, string? newPassword)
    {
        using var csrf = await browser.GetAsync(new Uri(issuer, "api/identity/csrf"), Ct); csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var result = await Post(new Uri(issuer, "api/identity/login"), new { client_id = GatewayClientId, identifier, password, tenant_id = tenant });
        var first = (string)result["status"]!;
        for (var step = 0; step < 3 && (string)result["status"]! != "authenticated"; step++)
            result = (string)result["status"]! switch
            {
                "password_change_required" => await Post(new Uri(issuer, "api/identity/login/password"),
                    new { ticket = (string)result["ticket"]!, new_password = newPassword ?? throw new InvalidOperationException("要求改密但没有给新密码。") }),
                "selection_required" => await Post(new Uri(issuer, "api/identity/login/select"), new
                {
                    ticket = (string)result["ticket"]!,
                    membership_id = (Guid?)Guid.Parse((string)result["choices"]!.AsArray().Single(choice => (string?)choice!["tenant_id"] == tenant?.ToString("D"))!["membership_id"]!)
                }),
                var other => throw new InvalidOperationException("登录状态不认识：" + other)
            };
        Assert.Equal("authenticated", (string?)result["status"]);
        return first;

        async Task<JsonNode> Post(Uri uri, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body, body.GetType()) };
            request.Headers.Add(token.GetProperty("header").GetString()!, token.GetProperty("token").GetString());
            using var response = await browser.SendAsync(request, Ct);
            var text = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, uri.AbsolutePath + " " + text);
            return JsonNode.Parse(text)!;
        }
    }

    private static async Task<Guid> NewAdmin(HttpClient browser, Uri shell, (string Header, string Token) csrf, string displayName, string username) =>
        Guid.Parse((string)(await Send(browser, shell, csrf, HttpMethod.Post, "api/tenancy/admin-accounts",
            new { displayName, identifierKind = "username", identifier = username, initialPassword = AdminInitial }, HttpStatusCode.Created))["subjectId"]!);

    // 开户并激活；开通编排的步骤里没有失败，成员（05）与 tenant.owner（07）两步都成功——这两步要同进程的身份端口与 Authz。
    private static async Task<Guid> OpenTenant(HttpClient browser, Uri shell, (string Header, string Token) csrf, PlatformRuntime runtime, string code, string name, Guid administrator)
    {
        var created = await Send(browser, shell, csrf, HttpMethod.Post, "api/tenancy/tenants",
            new { code, name, administrator = new { membershipId = Guid.CreateVersion7(), subjectId = administrator.ToString("D") } }, HttpStatusCode.Created);
        var id = Guid.Parse((string)created["id"]!);
        var steps = await runtime.Global("tenancy").Inject<ITenantProvisioning>(TenantProvisioning.Key).GetStepsAsync(id, Ct);
        Assert.DoesNotContain(steps, step => step.Status == "failed");
        // 第 03 步把每个预装的 system 级组件开通进租户（Lifecycle：已激活修订 + system 级恒视为已授权，R151）。
        Assert.Equal(PreinstalledSystem.Select(plugin => "03:component:" + plugin + "|succeeded").Order(StringComparer.Ordinal),
            steps.Where(step => step.Step.StartsWith("03:component:", StringComparison.Ordinal)).Select(step => step.Step + "|" + step.Status).Order(StringComparer.Ordinal));
        Assert.Equal(["succeeded", "succeeded"], steps.Where(step => step.Step is "05:membership" or "07:owner").Select(step => step.Status));
        var active = await Send(browser, shell, csrf, HttpMethod.Post, $"api/tenancy/tenants/{id}:activate", new { }, HttpStatusCode.OK);
        Assert.Equal("active", (string?)active["status"]);
        return id;
    }

    private static async Task<(string Header, string Token)> Csrf(HttpClient browser, Uri shell)
    {
        var csrf = JsonNode.Parse(await Text(browser, shell, "auth/csrf"))!;
        return ((string)csrf["headerName"]!, (string)csrf["token"]!);
    }

    private static async Task<JsonNode> Send(HttpClient browser, Uri shell, (string Header, string Token) csrf, HttpMethod method, string path, object body, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, new Uri(shell, path)) { Content = JsonContent.Create(body, body.GetType()) };
        request.Headers.Add(csrf.Header, csrf.Token);
        using var response = await browser.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, method + " " + path + " " + (int)response.StatusCode + " " + text);
        return JsonNode.Parse(text)!;
    }

    private static async Task<string> Text(HttpClient browser, Uri shell, string path)
    {
        using var response = await browser.GetAsync(new Uri(shell, path), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, path + " " + (int)response.StatusCode + " " + text);
        return text;
    }

    private static async Task Status(HttpClient browser, Uri shell, string path, HttpStatusCode expected, string code)
    {
        using var response = await browser.GetAsync(new Uri(shell, path), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, path + " " + (int)response.StatusCode + " " + text);
        Assert.Equal(code, JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonNode> Bootstrap(HttpClient browser, Uri shell) => JsonNode.Parse(await Text(browser, shell, "api/shell/bootstrap"))!;

    // POST /auth/session/refresh：换到带当前身份修订的令牌（外壳在 401 后就是这样做的）。
    private static async Task RefreshSession(HttpClient browser, Uri shell)
    {
        var csrf = await Csrf(browser, shell);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(shell, "auth/session/refresh"));
        request.Headers.Add(csrf.Header, csrf.Token);
        using var response = await browser.SendAsync(request, Ct);
        Assert.True(response.IsSuccessStatusCode, (int)response.StatusCode + " " + await response.Content.ReadAsStringAsync(Ct));
    }

    private static string[] Ids(string list) => [.. JsonNode.Parse(list)!["items"]!.AsArray().Select(item => (string)item!["id"]!)];

    private static async Task<IReadOnlyList<AuditRecord>> PlatformChain(PlatformRuntime runtime)
    {
        var producer = runtime.Global("audit");
        return [.. (await producer.Audit().ReadPlatformAsync(producer, 0, 200, cancellationToken: Ct)).Select(entry => entry.Record)];
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == expected, (int)response.StatusCode + " " + text);
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<string[]> Strings(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(Ct);
        var values = new List<string>();
        while (await reader.ReadAsync(Ct)) values.Add(reader.GetString(0));
        return [.. values];
    }

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }

    // 申请人的浏览器：发行方同源，带 Cookie 容器与同源 Origin（申请人写接口要求同源）。
    private sealed class SignupBrowser(Uri issuer) : IDisposable
    {
        private readonly HttpClient _client = new(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true, AllowAutoRedirect = false }) { BaseAddress = issuer };
        internal async Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, body.GetType()) };
            request.Headers.Add("Origin", issuer.GetLeftPart(UriPartial.Authority));
            return await _client.SendAsync(request, Ct);
        }
        public void Dispose() => _client.Dispose();
    }

    // 测试发件端口：只在审核那一段挂上，记下验证码；生产在 D4 下没有发件端口（R150 ④）。
    private sealed class TestOutbox : ISignupMessenger
    {
        private readonly ConcurrentDictionary<Guid, string> _codes = new();
        internal string Code(Guid challenge) => _codes[challenge];
        public ValueTask SendVerificationCodeAsync(SignupVerificationMessage message, CancellationToken cancellationToken = default)
        { _codes[message.Id] = message.Code; return ValueTask.CompletedTask; }
        public ValueTask SendResultAsync(SignupResultMessage message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class MessengerPlugin(TestOutbox outbox) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<ISignupMessenger>(TenancySignupDefinition.MessengerKey, outbox);
            return ValueTask.CompletedTask;
        }
    }
}

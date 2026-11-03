using System.Text.Json;
using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup.Tests;

// 不需要数据库的规则：配置 Schema 与缺省值、输入规范化与掩码、秘密材料、错误码、组件清单与状态机描述。
public sealed class SignupUnitTests
{
    private static readonly string[] Ops = ["ops"];
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root!.FullName;
    }

    [Fact]
    public void 配置Schema的缺省值与代码缺省一致_组件目录里的Schema由代码派生()
    {
        using var schema = JsonDocument.Parse(ComponentConfiguration.Schema);
        var properties = schema.RootElement.GetProperty("properties");
        int Default(params string[] path)
        {
            var node = properties;
            for (var i = 0; i < path.Length - 1; i++) node = node.GetProperty(path[i]).GetProperty("properties");
            return node.GetProperty(path[^1]).GetProperty("default").GetInt32();
        }
        var options = new TenancySignupOptions();
        Assert.Equal((30, 20, 3, 32), (Default("trialDays"), Default("defaultUsers"), Default("code", "minLength"), Default("code", "maxLength")));
        Assert.Equal((options.TrialDays, options.DefaultUsers, options.CodeMinLength, options.CodeMaxLength), (30, 20, 3, 32));
        Assert.Equal((6, 300, 5, 60, 5, 20), (Default("verification", "length"), Default("verification", "lifetimeSeconds"), Default("verification", "maxAttempts"),
            Default("verification", "resendSeconds"), Default("verification", "perPhonePerHour"), Default("verification", "perSourcePerHour")));
        Assert.Equal((6, 300, 5, 60, 5, 20), (options.CodeLength, (int)options.CodeLifetime.TotalSeconds, options.MaxAttempts, (int)options.ResendInterval.TotalSeconds,
            options.PerPhonePerHour, options.PerSourcePerHour));
        Assert.Equal((30, 30), (Default("session", "lifetimeMinutes"), (int)options.SessionLifetime.TotalMinutes));
        Assert.Equal((30, 30), (Default("codeAvailability", "perSourcePerMinute"), options.CodeChecksPerSourcePerMinute));
        Assert.Equal(("86", "86"), (properties.GetProperty("defaultCountryCode").GetProperty("default").GetString(), options.DefaultCountryCode));
        Assert.Equal(TenancySignupOptions.DefaultReserved, properties.GetProperty("code").GetProperty("properties").GetProperty("reserved").GetProperty("default").EnumerateArray().Select(item => item.GetString()!));
        // 组件目录里的 Schema 与代码真相源语义一致（qian-plugin sync 派生，清单门禁比对摘要）。
        using var published = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "plugins/qian.tenancy.signup/schemas/config.schema.json")));
        Assert.True(JsonElement.DeepEquals(schema.RootElement, published.RootElement));
    }

    [Fact]
    public void 配置严格读取_未知键与越界拒绝_覆盖值生效()
    {
        Assert.Equal(new TenancySignupOptions().TrialDays, TenancySignupOptions.Read(JsonSerializer.SerializeToElement(new { })).TrialDays);
        var read = TenancySignupOptions.Read(JsonSerializer.SerializeToElement(new
        {
            trialDays = 14, defaultUsers = 5, code = new { minLength = 4, maxLength = 20, reserved = Ops },
            verification = new { length = 4, lifetimeSeconds = 120, maxAttempts = 3, resendSeconds = 30, perPhonePerHour = 2, perSourcePerHour = 9 },
            session = new { lifetimeMinutes = 10 }, codeAvailability = new { perSourcePerMinute = 7 }, defaultCountryCode = "1"
        }));
        Assert.Equal((14, 5, 4, 20, 4, 3, 2, 9, 10d, 7, "1"), (read.TrialDays, read.DefaultUsers, read.CodeMinLength, read.CodeMaxLength, read.CodeLength, read.MaxAttempts,
            read.PerPhonePerHour, read.PerSourcePerHour, read.SessionLifetime.TotalMinutes, read.CodeChecksPerSourcePerMinute, read.DefaultCountryCode));
        Assert.Equal(["ops"], read.Reserved);
        foreach (var invalid in new object[]
                 {
                     new { unknown = 1 }, new { trialDays = 0 }, new { trialDays = "30" }, new { defaultUsers = 100001 }, new { code = new { minLength = 10, maxLength = 5 } },
                     new { code = new { reserved = new[] { "ops", "ops" } } }, new { code = new { reserved = new[] { "Ops" } } }, new { verification = new { length = 3 } },
                     new { verification = new { extra = true } }, new { session = new { lifetimeMinutes = 2 } }, new { defaultCountryCode = "086" }, new { tenantId = "x" }
                 })
            Assert.Equal(PlatformErrors.InputInvalid, Assert.Throws<QianErrorException>(() => TenancySignupOptions.Read(JsonSerializer.SerializeToElement(invalid))).Error.Code);
    }

    [Theory]
    [InlineData("13912345678", "+8613912345678")]
    [InlineData("139 1234 5678", "+8613912345678")]
    [InlineData("+86 139-1234-5678", "+8613912345678")]
    [InlineData("008613912345678", "+8613912345678")]
    [InlineData("8613912345678", "+8613912345678")]
    [InlineData("(0139) 1234.5678", "+8613912345678")]
    [InlineData("+14155550123", "+14155550123")]
    [InlineData("abc", null)]
    [InlineData("12", null)]
    [InlineData("+0123456789", null)]
    [InlineData("139#1234", null)]
    [InlineData("", null)]
    public void 手机号与身份服务同一规则规范化为E164(string input, string? expected) => Assert.Equal(expected, SignupRules.NormalizePhone(input, "86"));

    [Fact]
    public void 掩码_租户号规则_秘密材料_申请编号()
    {
        Assert.Equal("139****5678", SignupRules.MaskPhone("+8613912345678"));
        Assert.Equal("141****0123", SignupRules.MaskPhone("+14155550123"));
        Assert.Equal("44***58", SignupRules.MaskPhone("+44795858"));
        var options = new TenancySignupOptions();
        Assert.Equal(SignupCodeReasons.Invalid, SignupRules.CodeProblem("ab", options));
        Assert.Equal(SignupCodeReasons.Invalid, SignupRules.CodeProblem("Acme", options));
        Assert.Equal(SignupCodeReasons.Invalid, SignupRules.CodeProblem("acme-co", options));
        Assert.Equal(SignupCodeReasons.Invalid, SignupRules.CodeProblem(new string('a', 33), options));
        Assert.Equal(SignupCodeReasons.Reserved, SignupRules.CodeProblem("admin", options));
        Assert.Null(SignupRules.CodeProblem("acme2026", options));
        // 验证码：定长数字；只存加盐哈希，同一验证码换盐得到不同哈希；比对恒时。
        var code = SignupRules.NewCode(6);
        Assert.Matches("^[0-9]{6}$", code);
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = SignupRules.HashCode(salt, code);
        Assert.True(SignupRules.CodeMatches(salt, hash, code));
        Assert.False(SignupRules.CodeMatches(salt, hash, code == "000000" ? "000001" : "000000"));
        Assert.NotEqual(hash, SignupRules.HashCode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16), code));
        // 会话令牌：43 位 base64url，只存 SHA-256；格式不对的令牌直接当作无效会话。
        var token = SignupRules.NewToken();
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.Equal(32, SignupRules.HashToken(token)!.Length);
        Assert.Null(SignupRules.HashToken("not-a-token")); Assert.Null(SignupRules.HashToken(null)); Assert.Null(SignupRules.HashToken(token + "x"));
        Assert.Equal("AP-20261103-0007", SignupRules.Number(new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.FromHours(-2)).AddHours(-3), 7));
        Assert.Equal("AP-20261104-0001", SignupRules.Number(new DateTimeOffset(2026, 11, 4, 1, 0, 0, TimeSpan.Zero), 1));
        Assert.Equal("a@example.com", SignupRules.NormalizeEmail(" A@Example.COM "));
        Assert.Null(SignupRules.NormalizeEmail("not-mail"));
        Assert.True(SignupRules.IsText("第一行\n第二行", 2000)); Assert.False(SignupRules.IsText("响铃\a", 2000));
        Assert.False(SignupRules.IsName("  ", 200)); Assert.False(SignupRules.IsName("甲" + (char)0 + "乙", 200));
    }

    [Fact]
    public void 错误码归属本组件_状态码与种类符合规格_文案不含敏感字段()
    {
        var expected = new Dictionary<string, (int Status, ErrorKind Kind)>(StringComparer.Ordinal)
        {
            ["SIGNUP_UNAVAILABLE"] = (503, ErrorKind.Protocol), ["SIGNUP_CODE_INVALID"] = (400, ErrorKind.Protocol), ["SIGNUP_CODE_EXPIRED"] = (400, ErrorKind.Protocol),
            ["SIGNUP_ACCOUNT_UNAVAILABLE"] = (403, ErrorKind.Protocol), ["SIGNUP_APPLICATION_OPEN"] = (409, ErrorKind.Business),
            ["SIGNUP_CODE_TAKEN"] = (409, ErrorKind.Business), ["SIGNUP_STATE_CONFLICT"] = (409, ErrorKind.Business)
        };
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), TenancySignupErrors.All.Select(code => code.Code).Order(StringComparer.Ordinal));
        var catalog = ErrorCatalog.CreateDefault();
        foreach (var code in TenancySignupErrors.All)
        {
            Assert.Equal(expected[code.Code], (code.Status, code.Kind));
            Assert.Equal(TenancySignupDefinition.ComponentId, code.Owner);
            using (catalog.Register(code)) { }
        }
        // 验证码错误的文案带剩余次数（ErrorDetail 冻结、没有参数字段，次数只在文案里）。
        Assert.Equal(["remaining"], TenancySignupErrors.CodeInvalid.Parameters);
    }

    [Fact]
    public void 清单的optionalInject与入口代码一致_权限目录与UI清单符合规格_状态机给通过与驳回标humanOnly与确认()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "plugins/qian.tenancy.signup/.qian-plugin/plugin.json")));
        var optional = manifest.RootElement.GetProperty("optionalInject").EnumerateArray().Select(key => key.GetString()!).ToArray();
        Assert.Equal([AuthzDefinition.SuperadminBypassKey.Name, TenancySignupDefinition.MessengerKey.Name], optional);
        Assert.Equal(optional, TenancySignupComponent.OptionalInject.Select(key => key.Name));
        Assert.Equal(optional, TenancySignupPlugin.OptionalInject.Select(key => key.Name));
        var inject = manifest.RootElement.GetProperty("inject").EnumerateArray().Select(key => key.GetString()!).ToArray();
        Assert.Equal(new TenancySignupComponent().Inject.Select(key => key.Name).Order(StringComparer.Ordinal), inject);
        Assert.Equal(new TenancySignupPlugin().Inject.Select(key => key.Name), new TenancySignupComponent().Inject.Select(key => key.Name));
        Assert.Equal(["TenancySignup"], manifest.RootElement.GetProperty("provides").EnumerateArray().Select(key => key.GetString()));
        Assert.Empty(optional.Intersect(inject)); Assert.DoesNotContain(TenancySignupDefinition.Key.Name, optional);

        using var permissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "plugins/qian.tenancy.signup/metadata/permissions.json")));
        var application = Assert.Single(permissions.RootElement.EnumerateArray());
        Assert.Equal("application", application.GetProperty("resource").GetString());
        Assert.Equal(["read", "approve", "reject"], application.GetProperty("actions").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(("critical", "high"), (application.GetProperty("risk").GetProperty("approve").GetString(), application.GetProperty("risk").GetProperty("reject").GetString()));

        using var ui = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "plugins/qian.tenancy.signup/ui/manifest.json")));
        Assert.Equal("/remotes/qian.tenancy.signup/mf-manifest.json", ui.RootElement.GetProperty("federation").GetProperty("manifest").GetString());
        Assert.Equal(["ApplicationsPage"], ui.RootElement.GetProperty("federation").GetProperty("exposes").EnumerateArray().Select(item => item.GetString()));
        var page = Assert.Single(ui.RootElement.GetProperty("pages").EnumerateArray());
        Assert.Equal(("/tenant-applications", "ApplicationsPage", "qian.tenancy.signup:application:read", "开通申请", 20),
            (page.GetProperty("route").GetString(), page.GetProperty("expose").GetString(), page.GetProperty("permission").GetString(),
                page.GetProperty("nav").GetProperty("title").GetString(), page.GetProperty("nav").GetProperty("order").GetInt32()));

        var machine = JsonSerializer.SerializeToElement(SignupStateMachine.Current, JsonSerializerOptions.Web);
        foreach (var name in new[] { "approve", "reject" })
        {
            var action = machine.GetProperty("actions").EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);
            Assert.Equal((false, true), (action.GetProperty("agent").GetProperty("expose").GetBoolean(), action.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean()));
        }
        Assert.Equal(["approved", "rejected", "withdrawn"], machine.GetProperty("terminal").EnumerateArray().Select(item => item.GetString()));
        Assert.All(machine.GetProperty("actions").EnumerateArray(), action => Assert.False(action.GetProperty("agent").GetProperty("expose").GetBoolean()));
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server.Tests;

// 账号模型的纯规则：哈希格式、标识规范化与掩码、锁定算法、密码规范化、选项校验。不需要数据库。
public sealed partial class AccountRulesTests
{
    [GeneratedRegex(@"\A\$argon2id\$v=19\$m=19456,t=2,p=1\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Phc();

    [Fact]
    public void 密码以Argon2id的PHC格式存储_同一密码两次盐不同_校验对错分明且拒绝篡改与越界参数()
    {
        var hasher = new Argon2idPasswordHasher(new Argon2idOptions());
        var password = "Correct-Horse-9"u8.ToArray();
        var first = hasher.Hash(password); var second = hasher.Hash(password);
        Assert.Matches(Phc(), first);
        Assert.NotEqual(first, second);
        Assert.True(Argon2idPasswordHasher.Verify(first, password));
        Assert.True(Argon2idPasswordHasher.Verify(second, password));
        Assert.False(Argon2idPasswordHasher.Verify(first, "correct-horse-9"u8));
        Assert.False(Argon2idPasswordHasher.Verify(first, ""u8));
        // 摘要被改一位、格式残缺、成本参数越界（拖垮进程的内存）一律视为不匹配，而不是抛出或真的去算。
        var parts = first.Split('$'); parts[5] = parts[5][..5] + (parts[5][5] == 'A' ? 'B' : 'A') + parts[5][6..];
        Assert.False(Argon2idPasswordHasher.Verify(string.Join('$', parts), password));
        Assert.False(Argon2idPasswordHasher.Verify("$argon2id$v=19$m=19456,t=2,p=1$", password));
        Assert.False(Argon2idPasswordHasher.Verify("not a hash", password));
        Assert.False(Argon2idPasswordHasher.Verify(first.Replace("m=19456", "m=999999999", StringComparison.Ordinal), password));
        Assert.False(Argon2idPasswordHasher.Verify(first.Replace("argon2id", "argon2i", StringComparison.Ordinal), password));
        Assert.DoesNotContain("Correct-Horse", first, StringComparison.Ordinal);
    }

    [Fact]
    public void 成本参数调高后旧哈希仍可验证并被标记需要升级()
    {
        var weak = new Argon2idPasswordHasher(new Argon2idOptions { MemoryKiB = 8192, Iterations = 1 });
        var strong = new Argon2idPasswordHasher(new Argon2idOptions());
        var stored = weak.Hash("Correct-Horse-9"u8);
        Assert.True(Argon2idPasswordHasher.Verify(stored, "Correct-Horse-9"u8));
        Assert.True(strong.NeedsRehash(stored));
        Assert.False(strong.NeedsRehash(strong.Hash("Correct-Horse-9"u8)));
    }

    [Theory]
    [InlineData("13812345678", "Phone", "+8613812345678")]
    [InlineData("138 1234 5678", "Phone", "+8613812345678")]
    [InlineData("+86 138-1234-5678", "Phone", "+8613812345678")]
    [InlineData("008613812345678", "Phone", "+8613812345678")]
    [InlineData("8613812345678", "Phone", "+8613812345678")]
    [InlineData("＋８６１３８１２３４５６７８", "Phone", "+8613812345678")]
    [InlineData("+14155552671", "Phone", "+14155552671")]
    [InlineData("Alice@Example.COM", "Email", "alice@example.com")]
    [InlineData("  a.b+tag@mail.example.org ", "Email", "a.b+tag@mail.example.org")]
    [InlineData("Alice_01", "Username", "alice_01")]
    [InlineData("ａｌｉｃｅ", "Username", "alice")]
    public void 输入串按语法判定标识类型并规范化_无需用户选择类型(string input, string kind, string expected)
    {
        var (actualKind, value) = IdentifierRules.Classify(input, "86")!.Value;
        Assert.Equal(Enum.Parse<IdentifierKind>(kind), actualKind);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12")]
    [InlineData("+")]
    [InlineData("1-2-3")]
    [InlineData("a@b")]
    [InlineData("@example.com")]
    [InlineData("white space")]
    [InlineData("9lives")]
    [InlineData("ab")]
    [InlineData("user\u0000name")]
    public void 非法或无法判定的输入不产生标识(string input) => Assert.Null(IdentifierRules.Classify(input, "86"));

    [Fact]
    public void 超长输入不产生标识() => Assert.Null(IdentifierRules.Classify(new string('a', 400), "86"));

    [Theory]
    [InlineData(IdentifierKind.Phone, "+8613812345678", "138****5678")]
    [InlineData(IdentifierKind.Email, "alice@example.com", "a***@example.com")]
    [InlineData(IdentifierKind.Username, "alice", "al***")]
    [InlineData(IdentifierKind.Username, "bob", "b**")]
    public void 对外与审计只给掩码_原值不出身份组件(IdentifierKind kind, string value, string masked)
    {
        Assert.Equal(masked, IdentifierRules.Mask(kind, value));
        var (auditKind, auditMask) = IdentifierRules.MaskInput(value, "86");
        Assert.Equal(IdentifierRules.KindName(kind), auditKind);
        Assert.Equal(masked, auditMask);
        Assert.DoesNotContain(value, auditMask, StringComparison.Ordinal);
    }

    [Fact]
    public void 无法判定类型的输入在审计里只留长度()
    {
        var (kind, masked) = IdentifierRules.MaskInput("white space", "86");
        Assert.Equal("invalid", kind); Assert.Equal("len=11", masked);
        var empty = IdentifierRules.MaskInput(null, "86");
        Assert.Null(empty.Kind); Assert.Null(empty.Masked);
    }

    [Fact]
    public void 锁定模式达到阈值才锁定_时长固定()
    {
        var policy = new LoginPolicy { MaxFailedAttempts = 5, Mode = LockoutMode.Lock, LockoutDuration = TimeSpan.FromMinutes(15) };
        for (var failed = 1; failed < 5; failed++) Assert.Null(policy.LockoutFor(failed));
        Assert.Equal(TimeSpan.FromMinutes(15), policy.LockoutFor(5));
        Assert.Equal(TimeSpan.FromMinutes(15), policy.LockoutFor(40));
    }

    private static readonly double[] DelaySeconds = [5d, 10d, 20d, 40d, 60d, 60d];

    [Fact]
    public void 延时模式每多错一次翻倍并封顶()
    {
        var policy = new LoginPolicy { MaxFailedAttempts = 3, Mode = LockoutMode.Delay, DelayBase = TimeSpan.FromSeconds(5), MaxDelay = TimeSpan.FromSeconds(60) };
        Assert.Null(policy.LockoutFor(2));
        Assert.Equal(DelaySeconds, Enumerable.Range(3, 6).Select(failed => policy.LockoutFor(failed)!.Value.TotalSeconds));
        Assert.Equal(TimeSpan.FromSeconds(60), policy.LockoutFor(10_000));
    }

    [Fact]
    public void 密码经NFKC规范化_全角与半角同一密码_释放后清零()
    {
        using var half = PasswordSecret.From(new AuthenticationProof("Pass-word-1"u8));
        using var full = PasswordSecret.From(new AuthenticationProof(Encoding.UTF8.GetBytes("Ｐａｓｓ－ｗｏｒｄ－１")));
        Assert.Equal(half.Bytes, full.Bytes);
        Assert.Equal(11, half.Length);
        var bytes = half.Bytes; half.Dispose();
        Assert.All(bytes, b => Assert.Equal(0, b));
    }

    [Theory]
    [InlineData(" leading-space1", true, false)]
    [InlineData("trailing-space1 ", true, false)]
    [InlineData("ok-password-1", false, false)]
    [InlineData("tab\tinside-1", false, true)]
    [InlineData("new\nline-1", false, true)]
    public void 密码的首尾空白与控制字符被识别(string password, bool edge, bool control)
    {
        using var secret = PasswordSecret.From(new AuthenticationProof(Encoding.UTF8.GetBytes(password)));
        Assert.Equal(edge, secret.HasEdgeWhitespace);
        Assert.Equal(control, secret.HasControl);
    }

    [Fact]
    public void 选项校验拒绝放宽安全底线()
    {
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { Login = new LoginPolicy { SelectionLifetime = TimeSpan.FromMinutes(6) } }.Validate());
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { Login = new LoginPolicy { MaxFailedAttempts = 2 } }.Validate());
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { Password = new PasswordPolicy { MinLength = 6 } }.Validate());
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { Password = new PasswordPolicy { HistorySize = 0 } }.Validate());
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { Hashing = new Argon2idOptions { SaltBytes = 8 } }.Validate());
        Assert.Throws<ArgumentException>(() => new IdentityAccountOptions { DefaultPhoneCountryCode = "+86" }.Validate());
        new IdentityAccountOptions().Validate();
        new IdentityAccountOptions { Login = new LoginPolicy { SelectionLifetime = TimeSpan.FromMinutes(5), MaxFailedAttempts = 3 } }.Validate();
    }

    [Fact]
    public void 敏感类型的字符串化不泄露内容()
    {
        using var proof = new AuthenticationProof("secret-value"u8);
        var texts = new object[]
        {
            new LoginInput("web", null, null, null, proof) { Identifier = "13812345678" }, new TenantSelection("ticket-value", Guid.NewGuid()),
            new InitialPasswordChange("ticket-value", proof), new CreatePerson("张三", []) { InitialPassword = proof }, new PasswordChange(proof, proof), new PasswordReset(proof, "reason"),
            new CaptchaAnswer("id", "answer"), new LoginTicket(LoginTicketPurpose.Selection, "ticket-value", DateTimeOffset.UtcNow), new IdentityAccountOptions(), SignInResult.Failure(new Qian.Runtime.Errors.QianError(IdentityErrors.InvalidCredentials))
        };
        foreach (var value in texts)
        {
            var text = value.ToString()!;
            Assert.DoesNotContain("secret-value", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ticket-value", text, StringComparison.Ordinal);
            Assert.DoesNotContain("13812345678", text, StringComparison.Ordinal);
        }
    }
}

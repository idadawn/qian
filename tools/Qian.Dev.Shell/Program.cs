using Qian.Dev.Shell;
using Qian.Dev.TenantOps;

// 外壳认证联调环境（S1-YUE-002）：真实 qian.identity（与 CRM 参考运行时同宿主）+ 真实 Identity.Client / Qian.Gateway BFF（静态托管外壳）。
// 一般经 scripts/dev-shell.sh 调用，用法见 README.md。
var options = Options(args.Skip(1));
var state = options.GetValueOrDefault("state") ?? Path.Combine(DevHost.RepositoryRoot(), ".qian-dev", "shell");
switch (args.FirstOrDefault())
{
    case "serve":
        // 管理员连接含口令，只经环境变量传入，不放命令行参数。
        var admin = Environment.GetEnvironmentVariable("QIAN_DEV_PG_ADMIN");
        if (string.IsNullOrWhiteSpace(admin)) return Fail("缺少环境变量 QIAN_DEV_PG_ADMIN（开发 PostgreSQL 的管理员连接串）；请用 scripts/dev-shell.sh up 启动。");
        var dex = options.GetValueOrDefault("dex"); var dexSecret = options.GetValueOrDefault("dex-secret");
        if ((dex is null) != (dexSecret is null)) return Fail("--dex 与 --dex-secret 必须同时给出（上游 Dex 的 issuer 与客户端密钥文件）。");
        var settings = new DevShellSettings(state, admin, options.GetValueOrDefault("urls") ?? "http://127.0.0.1:5090", options.GetValueOrDefault("gateway") ?? "https://localhost:5443", dex, dexSecret);
        return await DevShellHost.RunAsync(settings, CancellationToken.None);
    default:
        Console.Error.WriteLine("""
            用法：
              serve [--urls http://127.0.0.1:5090] [--gateway https://localhost:5443] [--state 目录] [--dex http://127.0.0.1:55556/dex --dex-secret 文件]
                  重建开发库，启动身份 / 业务宿主（--urls，即 OIDC 发行方）与网关 BFF（--gateway，HTTPS 自签名，静态托管外壳）；需 QIAN_DEV_PG_ADMIN。
                  给出 --dex 时把通用 OIDC 连接器 dex 指向该上游（客户端 qian-identity，密钥读自 --dex-secret 文件）
            """);
        return 2;
}

static Dictionary<string, string?> Options(IEnumerable<string> arguments)
{
    var result = new Dictionary<string, string?>(StringComparer.Ordinal); string? name = null;
    foreach (var argument in arguments)
    {
        if (argument.StartsWith("--", StringComparison.Ordinal)) { name = argument[2..]; result[name] = null; }
        else if (name is not null) { result[name] = argument; name = null; }
        else throw new ArgumentException("无法识别的参数：" + argument);
    }
    return result;
}

static int Fail(string message) { Console.Error.WriteLine(message); return 2; }

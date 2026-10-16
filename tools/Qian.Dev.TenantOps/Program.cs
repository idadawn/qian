using Qian.Dev.TenantOps;

// 本机租户运营手测环境（S1-QA-012）的宿主与令牌小工具；一般经 scripts/dev-tenant-ops.sh 调用，用法见 README.md。
var options = Options(args.Skip(1));
var state = options.GetValueOrDefault("state") ?? Path.Combine(DevHost.RepositoryRoot(), ".qian-dev", "tenant-ops");
switch (args.FirstOrDefault())
{
    case "serve":
        // 管理员连接含口令，只经环境变量传入，不放命令行参数。
        var admin = Environment.GetEnvironmentVariable("QIAN_DEV_PG_ADMIN");
        if (string.IsNullOrWhiteSpace(admin)) return Fail("缺少环境变量 QIAN_DEV_PG_ADMIN（开发 PostgreSQL 的管理员连接串）；请用 scripts/dev-tenant-ops.sh up 启动。");
        return await DevHost.RunAsync(state, admin, options.GetValueOrDefault("urls") ?? "http://127.0.0.1:5080", CancellationToken.None);
    case "token":
        var kind = options.GetValueOrDefault("kind") ?? "user";
        if (kind is not ("user" or "service" or "agent")) return Fail("--kind 只接受 user、service、agent。");
        var tenant = options.GetValueOrDefault("tenant");
        if (tenant is not null && !Guid.TryParseExact(tenant, "D", out _)) return Fail("--tenant 必须是租户 UUID。");
        if (!int.TryParse(options.GetValueOrDefault("ttl") ?? "480", out var minutes) || minutes is < 1 or > 1440) return Fail("--ttl 是 1–1440 分钟。");
        Console.WriteLine(DevKeys.Token(DevKeys.LoadOrCreate(state), options.GetValueOrDefault("sub") ?? DevKeys.Superadmin, tenant, kind, TimeSpan.FromMinutes(minutes)));
        return 0;
    default:
        Console.Error.WriteLine("""
            用法：
              serve [--urls http://127.0.0.1:5080] [--state 目录]      重建开发库并启动运营宿主（需 QIAN_DEV_PG_ADMIN）
              token [--sub root] [--tenant 租户UUID] [--kind user] [--ttl 480] [--state 目录]
                                                                       签发本机开发令牌；默认是平台超管 root
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

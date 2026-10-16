using System.Text.Encodings.Web;
using System.Text.Json;
using Qian.Image.SelfCheck;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

// 镜像流水线的 JIT 自检程序（S1-DEP-003 / R106）：只在 CI 内构建运行，不是任何 profile 的产品镜像。
// dump-config 按交付文件合成 profile，供配置漂移门禁比对源码树与镜像内的结果；home 层关闭，层路径相对根目录。
// self-report 输出运行期证据，见 SelfReport。
try
{
    return args switch
    {
        ["self-report"] => SelfReport.Write(Console.OpenStandardOutput()),
        ["dump-config", .. var options] => DumpConfig(options),
        _ => throw new ArgumentException("未知命令。"),
    };
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message + " 用法：dump-config [--root 目录] --profile 名称 [--patch 文件]… | self-report");
    return 2;
}

static int DumpConfig(string[] arguments)
{
    var root = Environment.GetEnvironmentVariable("QIAN_ROOT");
    string? profile = null;
    var patches = new List<string>();
    for (var i = 0; i < arguments.Length; i += 2)
    {
        if (i + 1 >= arguments.Length) throw new ArgumentException(arguments[i] + " 缺少取值。");
        switch (arguments[i])
        {
            case "--root": root = arguments[i + 1]; break;
            case "--profile": profile = arguments[i + 1]; break;
            case "--patch": patches.Add(arguments[i + 1]); break;
            default: throw new ArgumentException("未知参数 " + arguments[i] + "。");
        }
    }
    if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(profile)) throw new ArgumentException("必须给出 --root（或 QIAN_ROOT）与 --profile。");

    var result = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions
    {
        WorkingDirectory = root, Profile = profile, Patches = patches, IncludeHomePatch = false,
    });
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    if (!result.IsValid)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { diagnostics = result.Diagnostics }, json));
        return 1;
    }
    // 与 qian dump-config --format json 同形：快照字段在顶层，另附诊断。
    var dump = JsonSerializer.SerializeToNode(result.Snapshot, json)!.AsObject();
    dump["diagnostics"] = JsonSerializer.SerializeToNode(result.Diagnostics, json);
    Console.WriteLine(dump.ToJsonString(json));
    return 0;
}

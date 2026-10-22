using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Profiles.Abstractions;

/// <summary>文件装配输入；宿主可附加已读取的 scope patch，装配器不访问数据库。</summary>
public sealed class ProfileLoadOptions
{
    /// <summary>项目根目录。</summary>
    public required string WorkingDirectory { get; init; }
    /// <summary>profiles 下的具名 profile。</summary>
    public string Profile { get; init; } = "platform";
    /// <summary>按出现顺序应用的 CLI overlay。</summary>
    public IReadOnlyList<string> Patches { get; init; } = [];
    /// <summary>QIAN_HOME；null 时取环境变量或用户 .qian 目录。</summary>
    public string? HomeDirectory { get; init; }
    /// <summary>测试和可复现构建可关闭 home 层。</summary>
    public bool IncludeHomePatch { get; init; } = true;
    /// <summary>额外本地组件根目录，支持包里的 qian 内容目录。</summary>
    public IReadOnlyList<string> ComponentDirectories { get; init; } = [];
    /// <summary>在文件层之后合入的数据库 scope 层；调用方负责租户归属、权限与权益。</summary>
    public IReadOnlyList<ScopePatchLayer> ScopePatches { get; init; } = [];
}
/// <summary>一份同 scope 的 patch/v2 数组；不保存权限结果，也不从租户输入推断归属。</summary>
public sealed class ScopePatchLayer
{
    /// <summary>仅接受 tenant / app / agent-session，不能覆盖 global。</summary>
    public required ScopeKey Scope { get; init; }
    /// <summary>用于诊断的相对来源名，不读取对应文件。</summary>
    public required string Source { get; init; }
    /// <summary>与文件 patch 相同的数组；缺省 scope 使用本层声明的 scope。</summary>
    public required JsonElement Patches { get; init; }
}
/// <summary>已禁用条目的定位信息，供宿主隐藏同名全局注册；配置内容不进入该描述。</summary>
public sealed record DisabledProfileEntry(string Id, string Scope, string Plugin, int Layer, string SourceLocation);
/// <summary>来源层，后层覆盖前层。</summary>
public sealed record ProfileLayer(int Index, string Kind, string Path, [property: JsonPropertyName("ref")] string? Reference, bool Present);
/// <summary>整棵插件树的一条可复制 patch 记录；诊断辅助成员不输出。</summary>
public sealed record ResolvedProfileEntry(string Id, string Scope, string Plugin, JsonElement Config, int Layer,
    IReadOnlyList<string> Inject, IReadOnlyList<string> OptionalInject, IReadOnlyList<string> Provides, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note)
{
    /// <summary>组件内容目录，供宿主解析静态表面。</summary>
    [JsonIgnore] public string ComponentDirectory { get; init; } = "";
    /// <summary>已验证的清单，不混入 dump-config 条目。</summary>
    [JsonIgnore] public JsonElement Manifest { get; init; }
}
/// <summary>解析与验证后的静态装配，不意味着业务 Provider 已启动。</summary>
public sealed record ProfileSnapshot(
    [property: JsonPropertyName("profile")] string Name, IReadOnlyList<ProfileLayer> Layers,
    IReadOnlyList<ResolvedProfileEntry> Entries, IReadOnlyList<string> MountOrder)
{
    /// <summary>装载器须为 scope 禁用项安装 restriction，避免回退到同名全局注册。</summary>
    [JsonIgnore] public IReadOnlyList<DisabledProfileEntry> DisabledEntries { get; init; } = [];
}
/// <summary>完整校验结果；错误时不返回可启动快照。</summary>
public sealed record ProfileLoadResult(ProfileSnapshot? Snapshot, IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    /// <summary>没有错误，可进一步交给宿主解析真实 Provider。</summary>
    public bool IsValid => Snapshot is not null && Diagnostics.All(d => d.Severity != "error");
}
/// <summary>profile 装配端口。</summary>
public interface IProfileLoader
{
    /// <summary>叠加所有来源层并校验配置与依赖。</summary>
    ProfileLoadResult Load(ProfileLoadOptions options);
}
/// <summary>装配器自己的字段错误码。</summary>
public static class ProfileErrors
{
    /// <summary>装配规则失败，说明与 pointer 指向来源文件。</summary>
    public static readonly ErrorCode Invalid = ErrorCode.Field("PROFILE_INVALID", "qian.profiles",
        new Dictionary<string, string> { ["zh-CN"] = "profile 装配失败", ["en"] = "Profile composition failed" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
}

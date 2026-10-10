using Qian.Profiles.Abstractions;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Docs.Abstractions;

/// <summary>文档生成输入；只读取 profile 与源码，不运行组件。</summary>
public sealed class DocumentOptions
{
    /// <summary>完整的静态装配选项；建议关闭 home patch 以得到可重现的仓库文档。</summary>
    public required ProfileLoadOptions Profile { get; init; }
    /// <summary>相对工作区的 C# 源码目录；缺省扫描 packages、plugins 与 src，排除构建目录与 tests。</summary>
    public IReadOnlyList<string> SourceDirectories { get; init; } = ["packages", "plugins", "src"];
}
/// <summary>代码中的声明或调用位置。</summary>
public sealed record EventSite(string File, int Line, string Symbol);
/// <summary>进程内事件的定义与生产、消费调用；不会将集成事件混入。</summary>
public sealed record EventMap(string Name, string Mode, EventSite Declaration, IReadOnlyList<EventSite> Producers, IReadOnlyList<EventSite> Consumers);
/// <summary>生成结果；诊断失败时仍返回事件映射供 CI 定位，不能发布文档。</summary>
public sealed record DocumentResult(IReadOnlyDictionary<string, string> Documents, IReadOnlyList<EventMap> Events, IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    /// <summary>不存在阻断性诊断。</summary>
    public bool IsValid => Diagnostics.All(d => d.Severity != "error");
}
/// <summary>构建期派生文档端口。</summary>
public interface IDocumentGenerator
{
    /// <summary>从已校验的装配与源码生成四份 Markdown 文档。</summary>
    DocumentResult Generate(DocumentOptions options);
}
/// <summary>文档 Definition 的字段错误码。</summary>
public static class DocumentErrors
{
    /// <summary>事件声明与注册的模式不同。</summary>
    public static readonly ErrorCode ModeMismatch = Define("DOC_EVENT_MODE_MISMATCH");
    /// <summary>事件声明重复或不能静态解析。</summary>
    public static readonly ErrorCode Declaration = Define("DOC_EVENT_DECLARATION_INVALID");
    /// <summary>动态调用无法静态建立映射。</summary>
    public static readonly ErrorCode Dynamic = Define("DOC_EVENT_DYNAMIC_REFERENCE");
    /// <summary>源码、Schema 或输出路径无效。</summary>
    public static readonly ErrorCode Source = Define("DOC_SOURCE_INVALID");
    /// <summary>全部文档诊断定义。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = [ModeMismatch, Declaration, Dynamic, Source];
    private static ErrorCode Define(string code) => ErrorCode.Field(code, "qian.docs",
        new Dictionary<string, string> { ["zh-CN"] = "文档派生校验", ["en"] = "Documentation validation" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
}

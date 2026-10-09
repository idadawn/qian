using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Surfaces.Abstractions;

/// <summary>静态表面文档；正文保持原样，本库不执行提示、命令、hook 或 MCP。</summary>
public sealed record SurfaceDocument(string Kind, string Name, string File, JsonElement Data, string Body);
/// <summary>诊断元信息位于 ErrorDetail 外层，保持冻结错误协议不变。</summary>
public sealed record SurfaceDiagnostic(string File, int Line, string Severity, ErrorDetail Error);
/// <summary>可独立交给装载器的组件静态快照。</summary>
public sealed record ParsedComponent(string Id, JsonElement Manifest, IReadOnlyList<SurfaceDocument> Documents)
{
    /// <summary>本快照已校验的 profile，供装载器过滤 profile 限定的声明。</summary>
    public string Profile { get; init; } = "minimal";
}
/// <summary>组件校验结果；仅有 warning 时仍有效。</summary>
public sealed record SurfaceValidationResult(ParsedComponent? Component, IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    /// <summary>全部文件均已通过结构与语义检查。</summary>
    public bool IsValid => Component is not null && Diagnostics.All(d => d.Severity != "error");
}
/// <summary>保留 JSON Pointer 到原文件行号的语法树投影。</summary>
public sealed record SurfaceSyntax(JsonElement Data, string Body, IReadOnlyDictionary<string, int> Lines);
/// <summary>纯解析结果。</summary>
public sealed record SurfaceParseResult(SurfaceSyntax? Syntax, IReadOnlyList<SurfaceDiagnostic> Diagnostics);
/// <summary>由宿主提供目录和执行环境；不从组件自己声明中授予执行权限。</summary>
public sealed class SurfaceValidationOptions
{
    /// <summary>当前 profile。</summary>
    public string Profile { get; init; } = "minimal";
    /// <summary>human-cli 或 server；内联 shell 仅 human-cli 可声明。</summary>
    public string ExecutionPlane { get; init; } = "human-cli";
    /// <summary>宿主是否显式允许受限 argv hook。</summary>
    public bool AllowCommandHooks { get; init; }
    /// <summary>工具全局名；本组件 tools 会自动加入。</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];
    /// <summary>外部技能全局名。</summary>
    public IReadOnlyList<string> Skills { get; init; } = [];
    /// <summary>除固定矩阵外的 Qian 事件名。</summary>
    public IReadOnlyList<string> Events { get; init; } = [];
    /// <summary>可调用能力 ID、plugin/handler 或 service.method 绑定。</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    /// <summary>完整目录中不存在的跨组件引用为 error；离线不完整目录中为 warning。</summary>
    public bool CatalogComplete { get; init; }
    /// <summary>调用方 trace-id；省略时为本次校验生成。</summary>
    public string? TraceId { get; init; }
}
/// <summary>共享语法与 Schema 端口，供 profile/patch 装配器复用。</summary>
public interface ISurfaceReader
{
    /// <summary>解析 JSON/YAML，或带 front matter 的 Markdown；禁止重复键与 YAML alias。</summary>
    SurfaceParseResult Parse(string file, string content, bool markdown = false, string? traceId = null);
    /// <summary>按内置 Schema 校验数据，错误包含文件指针。</summary>
    IReadOnlyList<SurfaceDiagnostic> ValidateSchema(string schema, string file, JsonElement data, string? traceId = null);
}
/// <summary>组件解析与语义校验端口；不依赖注册表或生命周期 Provider。</summary>
public interface ISurfaceValidator : ISurfaceReader
{
    /// <summary>读取组件目录并校验全部受支持的静态表面。</summary>
    SurfaceValidationResult ValidateComponent(string directory, SurfaceValidationOptions? options = null);
}

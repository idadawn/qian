using Qian.Runtime.Errors;

namespace Qian.Surfaces.Abstractions;

/// <summary>表面 Definition 拥有的字段错误码；由使用它的错误目录显式登记。</summary>
public static class SurfaceErrors
{
    /// <summary>文本语法无效或重复键。</summary>
    public static readonly ErrorCode Syntax = Define("SURFACE_SYNTAX_INVALID");
    /// <summary>不符合对应 JSON Schema。</summary>
    public static readonly ErrorCode Schema = Define("SURFACE_SCHEMA_INVALID");
    /// <summary>文件与声明名称不一致。</summary>
    public static readonly ErrorCode Name = Define("SURFACE_NAME_MISMATCH");
    /// <summary>引用不存在或尚未解析。</summary>
    public static readonly ErrorCode Reference = Define("SURFACE_REFERENCE_UNKNOWN");
    /// <summary>路径越界、符号链接或指定文件不存在。</summary>
    public static readonly ErrorCode Path = Define("SURFACE_PATH_INVALID");
    /// <summary>执行或信任策略拒绝。</summary>
    public static readonly ErrorCode Policy = Define("SURFACE_POLICY_DENIED");
    /// <summary>敏感字段包含明文。</summary>
    public static readonly ErrorCode Secret = Define("SURFACE_SECRET_LITERAL");
    /// <summary>工具描述哈希不一致。</summary>
    public static readonly ErrorCode Hash = Define("SURFACE_HASH_MISMATCH");
    /// <summary>本组件全部错误码。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = [Syntax, Schema, Name, Reference, Path, Policy, Secret, Hash];
    private static ErrorCode Define(string code) => ErrorCode.Field(code, "qian.surfaces",
        new Dictionary<string, string> { ["zh-CN"] = "声明式表面校验", ["en"] = "Surface validation" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
}

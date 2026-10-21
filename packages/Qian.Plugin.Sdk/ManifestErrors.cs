using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk;

/// <summary>可信组件清单构建的错误目录；不改变市场准入或运行权限。</summary>
public static class ManifestErrors
{
    /// <summary>程序集缺少组件注解或派生工厂。</summary>
    public static readonly ErrorCode MetadataMissing = Code("SDK_METADATA_MISSING", "程序集没有 QianPlugin 入口与派生元数据");
    /// <summary>清单 ID 没有匹配的组件入口。</summary>
    public static readonly ErrorCode ComponentMismatch = Code("SDK_COMPONENT_MISMATCH", "清单 ID 与代码入口不匹配");
    /// <summary>出现第二份组件清单。</summary>
    public static readonly ErrorCode ManifestDuplicate = Code("SDK_MANIFEST_DUPLICATE", "组件目录只能有唯一清单");
    /// <summary>派生文件与手写文件冲突。</summary>
    public static readonly ErrorCode HandwrittenConflict = Code("SDK_HANDWRITTEN_CONFLICT", "派生内容不能覆盖手写文件");
    /// <summary>路径越界或经过符号链接。</summary>
    public static readonly ErrorCode PathEscape = Code("SDK_PATH_ESCAPE", "派生路径必须留在组件目录内");
    /// <summary>生成元数据重复、跨组件或不完整。</summary>
    public static readonly ErrorCode MetadataInvalid = Code("SDK_METADATA_INVALID", "代码派生元数据无效");
    /// <summary>组件文件或 Schema 无效。</summary>
    public static readonly ErrorCode ManifestInvalid = Code("SDK_MANIFEST_INVALID", "组件文件或 Schema 无效");
    /// <summary>输入错误的具体位置与原因。</summary>
    public static readonly ErrorCode Field = ErrorCode.Field("SDK_MANIFEST_FIELD", "qian.plugin.sdk", new Dictionary<string, string> { ["zh-CN"] = "组件构建输入无效" }, new Dictionary<string, string> { ["zh-CN"] = "{reason}" });

    private static ErrorCode Code(string code, string title) => ErrorCode.Protocol(code, 400, "qian.plugin.sdk",
        new Dictionary<string, string> { ["zh-CN"] = title }, new Dictionary<string, string> { ["zh-CN"] = "{reason}" });
}

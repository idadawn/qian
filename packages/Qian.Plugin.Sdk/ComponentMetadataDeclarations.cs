using System.Text.Json.Nodes;
using Qian.Runtime;

namespace Qian.Plugin.Sdk;

/// <summary>生成代码使用的声明校验；只读取静态声明，不执行插件。</summary>
public static class ComponentMetadataDeclarations
{
    /// <summary>核验事件属性与实际强类型事件的模式一致，并产生可追溯的元数据项。</summary>
    public static JsonObject Event<TPayload, TResult>(QianEvent<TPayload, TResult> value, DispatchMode mode, string source)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(mode) || string.IsNullOrWhiteSpace(value.Name) || value.Mode != mode) throw new ArgumentException("事件属性与实际分发模式不一致：" + source);
        return new JsonObject { ["name"] = value.Name, ["mode"] = mode.ToString().ToLowerInvariant(), ["payload"] = typeof(TPayload).FullName,
            ["result"] = typeof(TResult).FullName, ["source"] = source };
    }

    /// <summary>在同源事件声明上保留唯一 Definition 归属；多个 Provider 可引用同一事件。</summary>
    public static JsonObject Event<TPayload, TResult>(QianEvent<TPayload, TResult> value, DispatchMode mode, string source, string definitionComponent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionComponent);
        var declaration = Event(value, mode, source);
        declaration["definitionComponent"] = definitionComponent;
        return declaration;
    }
}

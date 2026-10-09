namespace Qian.Runtime;

/// <summary>运行时拥有的声明式目录键；条目值由表面 Definition 提供，不执行其中的内容。</summary>
public static class SurfaceKeys
{
    /// <summary>技能目录。</summary>
    public static readonly ServiceKey Skills = new("Skills");
    /// <summary>Agent 定义目录。</summary>
    public static readonly ServiceKey Agents = new("Agents");
    /// <summary>人类命令目录。</summary>
    public static readonly ServiceKey Commands = new("Commands");
    /// <summary>hook 声明目录。</summary>
    public static readonly ServiceKey Hooks = new("Hooks");
    /// <summary>MCP 服务器声明目录。</summary>
    public static readonly ServiceKey McpServers = new("McpServers");
    /// <summary>模型工具目录。</summary>
    public static readonly ServiceKey Tools = new("Tools");
}

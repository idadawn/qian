namespace Qian.Testing.Containers;

/// <summary>
/// 没有 Docker 时集成测试怎么办：本机跳过；CI 上直接失败，因为 PR 级门禁要求集成测试必须真跑（PRD §14.4 C10）。
/// </summary>
public static class DockerRequirement
{
    /// <summary>设为 <c>1</c> 或 <c>true</c> 时，即使不在 CI 上也要求 Docker（缺失即失败而不是跳过）。</summary>
    public const string RequireVariable = "QIAN_REQUIRE_DOCKER";

    /// <summary>当前环境是否要求 Docker：<c>CI=true</c>（GitHub Actions 自动设置）或 <see cref="RequireVariable"/> 为真。</summary>
    /// <param name="getEnvironmentVariable">读取环境变量的函数，默认 <see cref="Environment.GetEnvironmentVariable(string)"/>；单测可替换。</param>
    public static bool IsRequired(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return IsTrue(getEnvironmentVariable("CI")) || IsTrue(getEnvironmentVariable(RequireVariable));
    }

    private static bool IsTrue(string? value) =>
        value is not null && (value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
}

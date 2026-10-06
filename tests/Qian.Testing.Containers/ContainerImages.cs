namespace Qian.Testing.Containers;

/// <summary>
/// 集成测试使用的镜像。版本与部署基线一致：PostgreSQL 16、Redis 7（ADR-017 D17.2）；
/// RabbitMQ 取当前 4.x 稳定线（PRD §14.6）。每个镜像都可用环境变量覆盖，便于与 staging 对齐或换内网镜像源。
/// </summary>
public static class ContainerImages
{
    /// <summary>PostgreSQL 镜像，覆盖变量 <c>QIAN_TEST_IMAGE_POSTGRES</c>。</summary>
    public static string Postgres => FromEnvironment("QIAN_TEST_IMAGE_POSTGRES", "postgres:16-alpine");

    /// <summary>Redis 镜像，覆盖变量 <c>QIAN_TEST_IMAGE_REDIS</c>。</summary>
    public static string Redis => FromEnvironment("QIAN_TEST_IMAGE_REDIS", "redis:7-alpine");

    /// <summary>RabbitMQ 镜像，覆盖变量 <c>QIAN_TEST_IMAGE_RABBITMQ</c>。</summary>
    public static string RabbitMq => FromEnvironment("QIAN_TEST_IMAGE_RABBITMQ", "rabbitmq:4.3-alpine");

    /// <summary>运行 Apollo 协议替身的 Python 镜像，覆盖变量 <c>QIAN_TEST_IMAGE_APOLLO_MOCK</c>。</summary>
    public static string ApolloMock => FromEnvironment("QIAN_TEST_IMAGE_APOLLO_MOCK", "python:3.13-alpine");

    private static string FromEnvironment(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

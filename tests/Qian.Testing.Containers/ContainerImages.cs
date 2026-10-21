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

    /// <summary>K3s 单节点集群镜像，覆盖变量 <c>QIAN_TEST_IMAGE_K3S</c>。</summary>
    public static string K3s => FromEnvironment("QIAN_TEST_IMAGE_K3S", "rancher/k3s:v1.31.5-k3s1");

    /// <summary>
    /// Authentik（S1-IDP-007 OIDC 连接器的真实上游），覆盖变量 <c>QIAN_TEST_IMAGE_AUTHENTIK</c>。测试容器的版本与运维侧现网的 Authentik（2024.10）解耦，
    /// 只验证标准 OIDC 授权码流程；server 与 worker 用同一个镜像。
    /// </summary>
    public static string Authentik => FromEnvironment("QIAN_TEST_IMAGE_AUTHENTIK", "ghcr.io/goauthentik/server:2026.8.5");

    /// <summary>运行 Apollo 协议替身的 Python 镜像，覆盖变量 <c>QIAN_TEST_IMAGE_APOLLO_MOCK</c>。</summary>
    public static string ApolloMock => FromEnvironment("QIAN_TEST_IMAGE_APOLLO_MOCK", "python:3.13-alpine");

    /// <summary>外部 OIDC 发行方 Dex 的镜像，覆盖变量 <c>QIAN_TEST_IMAGE_DEX</c>。</summary>
    public static string Dex => FromEnvironment("QIAN_TEST_IMAGE_DEX", "ghcr.io/dexidp/dex:v2.41.1");

    private static string FromEnvironment(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

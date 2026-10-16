using System.Text.Json;

namespace Qian.Tenancy;

/// <summary>S1 状态机与操作风险的机器可读描述；purge 只保留 S3 描述，不发布执行入口。</summary>
public static class TenantStateMetadata
{
    private const string Json = """{"states":["pending","trial","active","grace","suspended"],"transitions":[{"from":"pending","action":"activate","to":["trial","active"]},{"from":["trial","active"],"action":"expire","to":"grace"},{"from":"any-s1","action":"suspend","to":"suspended"},{"from":"suspended","action":"resume","to":"previous"}],"actions":[{"name":"activate","sideEffect":"write","agent":{"expose":false,"requiresConfirmation":false}},{"name":"suspend","sideEffect":"destructive","agent":{"expose":false,"requiresConfirmation":true}},{"name":"resume","sideEffect":"write","agent":{"expose":false,"requiresConfirmation":false}},{"name":"purge","sideEffect":"destructive","agent":{"expose":false,"requiresConfirmation":true},"available":false,"stage":"S3"},{"name":"scan-expired","sideEffect":"write","idempotent":true,"agent":{"expose":false,"requiresConfirmation":false}}]}""";
    /// <summary>每次返回独立文档，humanOnly 按 R52 读为 agent.expose=false。</summary>
    public static JsonElement Document => JsonSerializer.Deserialize<JsonElement>(Json);
    /// <summary>描述现有 JSON 形状的响应契约；与文档一起接受真实宿主验证。</summary>
    public static JsonElement Schema
    {
        get
        {
            using var stream = typeof(TenantStateMetadata).Assembly.GetManifestResourceStream("Qian.Tenancy.StateMachineSchema")
                ?? throw new InvalidOperationException("缺少租户状态机 Schema。");
            return JsonSerializer.Deserialize<JsonElement>(stream);
        }
    }
}

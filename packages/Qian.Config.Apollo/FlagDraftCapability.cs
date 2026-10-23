using System.Diagnostics;
using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

// 平台能力不走强制 tenant scope 的普通命令绑定；本绑定与 HTTP 共用完全相同的执行与授权。
internal sealed partial class FlagDraftCapability : IExecutableCapability, IFeatureFlagDrafts, IDisposable
{
    private readonly IPluginContext _owner;
    private readonly ApolloFlagManagementBinding _binding;
    private readonly HttpClient _client = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopped;
    private int _disposed;

    internal FlagDraftCapability(IPluginContext owner, ApolloFlagManagementBinding binding)
    { _owner = owner; _binding = binding; _stopped = _stop.Token; }

    public CapabilityContract Contract => Definition;
    internal static CapabilityContract Definition { get; } = new("qian.config.flags", """
        {"name":"flags.set","description":"确认后保存平台灰度规则草稿；不会发布，须由 Portal 操作员人工发布后才生效。","sideEffect":"write","policyHint":"confirm","idempotent":true,"async":"none","requiredScopes":[],"requiredSecrets":[],"dataClassification":"internal","costTier":"low","contractVersion":"v1","audit":true,"agent":{"expose":false,"requiresConfirmation":true},"binding":{"kind":"command","id":"qian.config.flags/flags.set"},"requiredPermission":"qian.config.flags:flag:set"}
        """, FlagManagementJson.Default.FeatureFlagDraft, FlagManagementJson.Default.FeatureFlagDraftResult);

    public async ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
    {
        if (caller.Scope != ScopeKey.Global || caller.Entry.Scope != ScopeKey.Global) throw Error(PlatformErrors.AuthzDenied);
        Check();
        try
        {
            if (!ReferenceEquals(caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, Contract.Id), this)) throw Error(PlatformErrors.NotFound);
        }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { throw Error(PlatformErrors.NotFound); }
        FeatureFlagDraft request;
        try { request = input.Deserialize(FlagManagementJson.Default.FeatureFlagDraft) ?? throw new JsonException(); }
        catch (JsonException) { throw Error(PlatformErrors.InputInvalid); }
        return JsonSerializer.SerializeToElement(await SetAsync(request, cancellationToken).ConfigureAwait(false), FlagManagementJson.Default.FeatureFlagDraftResult);
    }

    public async ValueTask<FeatureFlagDraftResult> SetAsync(FeatureFlagDraft request, CancellationToken cancellationToken = default)
    {
        Check();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopped);
        // scoped ICurrentUser 读取宿主已验证的请求身份；不能从模型输入、operator 名称或组件配置伪造主体。
        await using var scope = _owner.Services.CreateAsyncScope();
        var user = scope.ServiceProvider.GetService<ICurrentUser>();
        var principal = user?.Principal;
        if (user is not { IsAuthenticated: true } || principal is null || user.UserId != principal.Subject) throw Error(PlatformErrors.Unauthenticated);
        if (principal.TenantId is not null || principal.Kind != PrincipalKind.User || principal.Actor is not null) throw Error(PlatformErrors.AuthzDenied);
        if (request is null || request.IdempotencyKey == Guid.Empty || !request.Confirmed || !FeatureFlagRules.ValidName(request.Name) || request.Rule is null ||
            request.Rule.Tenants is null || request.Rule.Tenants.Count > FeatureFlagRules.MaximumTenants) throw Error(PlatformErrors.InputInvalid);
        var canonical = new FeatureFlagPolicy { Default = request.Rule.Default, Percentage = request.Rule.Percentage, Tenants = request.Rule.Tenants.Order().ToArray() };
        var rule = JsonSerializer.Serialize(canonical, FlagManagementJson.Default.FeatureFlagPolicy);
        var candidate = new Dictionary<string, string?> { [FeatureFlagRules.Prefix + request.Name] = rule };
        if (!FeatureFlagRules.Validate(candidate) || SecretSafety.ScanConfiguration(candidate).Count != 0) throw Error(PlatformErrors.InputInvalid);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rule)));
        var resource = "flags/" + request.Name;
        var authorization = await _owner.Enforcement().EnforceAsync(new(new(principal.Subject, null, "user", principal.IdentityRevision),
            FeatureFlagManagement.SetPermission, resource, "api", AuthzEnforcement.CommandEntryPoint, Contract.Id), linked.Token).ConfigureAwait(false);
        if (!authorization.Allow) throw Error(authorization.Reason switch
        {
            AuthzEnforcement.AuditUnavailable => PlatformErrors.NotFound,
            AuthzEnforcement.StaleIdentityRevision => PlatformErrors.Unauthenticated,
            _ => PlatformErrors.AuthzDenied
        });
        Check();
        var audit = _owner.Audit();
        var logger = _owner.Services.GetService<ILoggerFactory>()?.CreateLogger("Qian.Config.Flags") ?? NullLogger.Instance;
        var receipts = new FlagDraftReceipts(_owner, _binding);
        var attempt = Guid.CreateVersion7(); var trace = ActivityTraceId.CreateRandom().ToHexString();
        await audit.AppendPlatformAsync(_owner, Record("started"), linked.Token).ConfigureAwait(false);
        bool fresh;
        try { fresh = await receipts.ClaimAsync(request, principal.Subject, hash, linked.Token).ConfigureAwait(false); }
        catch (Exception error)
        {
            await Complete("rejected").ConfigureAwait(false);
            if (error is DbException) throw Error(PlatformErrors.Unavailable);
            throw;
        }
        if (!fresh)
        {
            await Complete("replayed").ConfigureAwait(false);
            return new FeatureFlagDraftResult { Name = request.Name, RuleHash = hash };
        }
        try
        {
            await SaveAsync(request.Name, rule, linked.Token).ConfigureAwait(false);
            using var durable = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await receipts.CompleteAsync(request.IdempotencyKey, "pending-publish", durable.Token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            using var uncertain = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await receipts.CompleteAsync(request.IdempotencyKey, "uncertain", uncertain.Token).ConfigureAwait(false); } catch { }
            await Complete("uncertain").ConfigureAwait(false);
            if (error is DbException) throw Error(PlatformErrors.Unavailable);
            throw;
        }
        await Complete("pending-publish").ConfigureAwait(false);
        return new FeatureFlagDraftResult { Name = request.Name, RuleHash = hash };

        AuditRecord Record(string result) => new(Guid.CreateVersion7(), null, "user", principal.Subject, null, Contract.Id, "v1", FlagDraftReceipts.RequestHash(request.Name, hash),
            "api", resource, result, trace, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new FlagDraftAudit(attempt, request.IdempotencyKey, receipts.TargetHash, result), FlagManagementJson.Default.FlagDraftAudit));
        async ValueTask Complete(string result)
        {
            // Portal 是外部提交点；已成功的草稿不能因补充审计失败伪装成可重试的业务失败。
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await audit.AppendPlatformAsync(_owner, Record(result), completion.Token).ConfigureAwait(false); }
            catch
            {
                LogCompletionFailed(logger, attempt, result);
            }
        }
    }

    private async Task SaveAsync(string name, string rule, CancellationToken ct)
    {
        var resolver = _owner.Config().Secrets?.ForPlugin(_owner) ?? throw Error(SecretErrors.Unavailable);
        using var secret = await resolver.ResolveAsync(_binding.TokenReference, ct).ConfigureAwait(false);
        var token = secret.RevealUtf8();
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl)) throw Error(SecretErrors.Unavailable);
        var path = "openapi/v1/envs/" + _binding.Environment + "/apps/" + Uri.EscapeDataString(_binding.AppId) +
            "/clusters/" + Uri.EscapeDataString(_binding.Cluster) + "/namespaces/qian.common.flags/items/" +
            FeatureFlagRules.Prefix + name + "?createIfNotExists=true";
        using var message = new HttpRequestMessage(HttpMethod.Put, new Uri(_binding.Portal, path));
        message.Headers.TryAddWithoutValidation("Authorization", token);
        message.Content = new StringContent(JsonSerializer.Serialize(new PortalDraft(FeatureFlagRules.Prefix + name, rule,
            "Qian flags.set draft", _binding.OperatorName, _binding.OperatorName), FlagManagementJson.Default.PortalDraft), Encoding.UTF8, "application/json");
        try
        {
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            // 不读取或记录 Portal 错误体，它可能回显 Token、规则或内部地址；重定向不转发凭据。
            if (!response.IsSuccessStatusCode) throw Error(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PlatformErrors.AuthzDenied,
                HttpStatusCode.Conflict => PlatformErrors.Conflict,
                _ => PlatformErrors.BadGateway
            });
        }
        catch (HttpRequestException) { throw Error(PlatformErrors.BadGateway); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Error(PlatformErrors.GatewayTimeout); }
    }

    private void Check()
    {
        if (Volatile.Read(ref _disposed) != 0) throw Error(PlatformErrors.NotFound);
        try
        {
            if (!ReferenceEquals(_owner.Inject<IFeatureFlagDrafts>(FeatureFlagManagement.Key), this)) throw Error(PlatformErrors.NotFound);
        }
        catch (Exception error) when (error is ObjectDisposedException or ServiceNotReadyException) { throw Error(PlatformErrors.NotFound); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel(); _client.Dispose(); _stop.Dispose();
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    [LoggerMessage(EventId = 2122, Level = LogLevel.Warning, Message = "特性开关草稿操作 {Attempt} 的 {Outcome} 收尾审计未写入；Portal 的已提交状态不回退。")]
    private static partial void LogCompletionFailed(ILogger logger, Guid attempt, string outcome);
}

internal sealed record PortalDraft(string Key, string Value, string Comment, string DataChangeCreatedBy, string DataChangeLastModifiedBy);
internal sealed record FlagDraftAudit(Guid Attempt, Guid IdempotencyKey, string TargetHash, string Outcome);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(FeatureFlagDraft))][JsonSerializable(typeof(FeatureFlagPolicy))][JsonSerializable(typeof(FeatureFlagDraftResult))]
[JsonSerializable(typeof(PortalDraft))][JsonSerializable(typeof(FlagDraftAudit))]
internal sealed partial class FlagManagementJson : JsonSerializerContext;

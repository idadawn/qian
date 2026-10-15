using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities.AspNetCore;

internal sealed class AspNetCoreHttpContractSource(IApiDescriptionGroupCollectionProvider descriptions, IHttpContextAccessor accessor, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions, IServiceScopeFactory scopeFactory) : IHttpContractSource
{
    public async ValueTask<JsonElement> GetDocumentAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var http = accessor.HttpContext ?? throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (!user.IsAuthenticated || http.User.Identity?.IsAuthenticated != true || user.Principal?.TenantId != caller.Scope.Id || !SameIdentity(http.User, user)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var services = http.RequestServices;
        var policies = services.GetRequiredService<IAuthorizationPolicyProvider>(); var authorize = services.GetRequiredService<IAuthorizationService>();
        var selected = new List<(ApiDescription Api, bool Anonymous)>();
        var scopes = new Dictionary<ServiceKey, IReadOnlyList<RegistryScopeEntry>>();
        foreach (var api in descriptions.ApiDescriptionGroups.Items.SelectMany(group => group.Items))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = api.ActionDescriptor.EndpointMetadata;
            var binding = metadata.OfType<PluginEndpointBinding>().SingleOrDefault();
            if (binding is null || api.HttpMethod is null || api.RelativePath is null) continue;
            if (!scopes.TryGetValue(binding.Key, out var view))
            { view = await caller.Registry().GetScopeEntriesAsync(caller.Scope, binding.Key, cancellationToken).ConfigureAwait(false); scopes.Add(binding.Key, view); }
            if (!view.Any(entry => entry.EntryId == binding.EntryId && entry.PluginId == binding.PluginId && entry.Name == binding.Name && (binding.Version is null || entry.Version == binding.Version))) continue;
            var policy = await AuthorizationPolicy.CombineAsync(policies, metadata.OfType<IAuthorizeData>(), metadata.OfType<AuthorizationPolicy>()).ConfigureAwait(false);
            var anonymous = metadata.OfType<IAllowAnonymous>().Any();
            if (!anonymous && policy is null) policy = await policies.GetFallbackPolicyAsync().ConfigureAwait(false);
            anonymous = anonymous || policy is null;
            var principal = http.User;
            if (!anonymous && policy!.AuthenticationSchemes.Count != 0)
            {
                var identities = new List<System.Security.Claims.ClaimsIdentity>();
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    var authenticated = await http.AuthenticateAsync(scheme).ConfigureAwait(false);
                    if (authenticated.Succeeded && authenticated.Principal is not null) identities.AddRange(authenticated.Principal.Identities);
                }
                principal = new System.Security.Claims.ClaimsPrincipal(identities);
                if (!SameIdentity(principal, user)) continue;
            }
            var resource = new DefaultHttpContext { RequestServices = services, User = principal };
            resource.Request.Method = api.HttpMethod; resource.Request.Path = "/" + api.RelativePath.Split('?')[0];
            resource.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), api.ActionDescriptor.DisplayName));
            if (!anonymous && !(await authorize.AuthorizeAsync(principal, resource, policy!).ConfigureAwait(false)).Succeeded) continue;
            selected.Add((api, anonymous));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!SameIdentity(http.User, user)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return Render(selected, jsonOptions.Value.SerializerOptions);
    }
    public async ValueTask<JsonElement> CompileDocumentAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        await using var scope = scopeFactory.CreateAsyncScope();
        var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var selected = new List<(ApiDescription Api, bool Anonymous)>();
        foreach (var api in descriptions.ApiDescriptionGroups.Items.SelectMany(group => group.Items))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = api.ActionDescriptor.EndpointMetadata;
            if ((metadata.OfType<PluginEndpointBinding>().SingleOrDefault()?.PluginId ?? metadata.OfType<PlatformHttpContract>().SingleOrDefault()?.PluginId) != pluginId || api.HttpMethod is null || api.RelativePath is null) continue;
            var policy = await AuthorizationPolicy.CombineAsync(policies, metadata.OfType<IAuthorizeData>(), metadata.OfType<AuthorizationPolicy>()).ConfigureAwait(false)
                ?? await policies.GetFallbackPolicyAsync().ConfigureAwait(false);
            selected.Add((api, metadata.OfType<IAllowAnonymous>().Any() || policy is null));
        }
        return Render(selected, jsonOptions.Value.SerializerOptions);
    }
    private static JsonElement Render(IEnumerable<(ApiDescription Api, bool Anonymous)> selected, JsonSerializerOptions options)
    {
        var paths = new JsonObject(); var schemas = new JsonObject();
        foreach (var (api, anonymous) in selected)
        {
            var component = api.ActionDescriptor.EndpointMetadata.OfType<PluginEndpointBinding>().SingleOrDefault()?.PluginId
                ?? api.ActionDescriptor.EndpointMetadata.OfType<PlatformHttpContract>().Single().PluginId;
            var pattern = RoutePatternFactory.Parse(api.RelativePath!);
            var path = "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
            {
                RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
                RoutePatternLiteralPart literal => literal.Content,
                RoutePatternSeparatorPart separator => separator.Content,
                _ => throw new InvalidOperationException("无法描述端点路由片段。")
            }))));
            var method = api.HttpMethod!.ToLowerInvariant();
            if (method is not ("get" or "post" or "put" or "delete" or "patch" or "head" or "options" or "trace")) continue;
            if (paths[path] is not JsonObject operations) { operations = new JsonObject(); paths[path] = operations; }
            if (operations.ContainsKey(method)) throw new InvalidOperationException("HTTP 契约存在重复路径和方法。");
            var operation = new JsonObject
            {
                ["operationId"] = "H" + Hash(component + ":" + method + ":" + path),
                ["x-qian-plugin"] = component,
                ["security"] = anonymous ? new JsonArray() : new JsonArray(new JsonObject { ["bearerAuth"] = new JsonArray() })
            };
            var parameters = new JsonArray();
            foreach (var parameter in api.ParameterDescriptions)
            {
                var type = parameter.Type ?? parameter.ModelMetadata?.ModelType;
                if (type is null) continue;
                var schema = Export(type, component, options, schemas);
                if (parameter.Source == BindingSource.Body)
                {
                    var content = new JsonObject();
                    foreach (var format in api.SupportedRequestFormats.Select(format => format.MediaType).Where(value => value is not null).Distinct(StringComparer.Ordinal))
                        content[format!] = new JsonObject { ["schema"] = schema.DeepClone() };
                    if (content.Count == 0) content["application/json"] = new JsonObject { ["schema"] = schema };
                    operation["requestBody"] = new JsonObject { ["required"] = parameter.IsRequired, ["content"] = content };
                }
                else if (parameter.Source == BindingSource.Path || parameter.Source == BindingSource.Query || parameter.Source == BindingSource.Header)
                    parameters.Add(new JsonObject { ["name"] = parameter.Name, ["in"] = parameter.Source == BindingSource.Path ? "path" : parameter.Source == BindingSource.Query ? "query" : "header", ["required"] = parameter.Source == BindingSource.Path || parameter.IsRequired, ["schema"] = schema });
                else throw new InvalidOperationException("HTTP 契约包含尚未正确描述的输入来源。");
            }
            if (parameters.Count != 0) operation["parameters"] = parameters;
            var responses = new JsonObject();
            foreach (var response in api.SupportedResponseTypes)
            {
                var value = new JsonObject { ["description"] = "HTTP " + response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                if (response.Type is { } type && type != typeof(void))
                {
                    var schema = Export(type, component, options, schemas); var content = new JsonObject();
                    foreach (var format in response.ApiResponseFormats.Select(format => format.MediaType).Where(value => value is not null).Distinct(StringComparer.Ordinal))
                        content[format!] = new JsonObject { ["schema"] = schema.DeepClone() };
                    if (content.Count == 0) content["application/json"] = new JsonObject { ["schema"] = schema };
                    value["content"] = content;
                }
                responses[response.IsDefaultResponse ? "default" : response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)] = value;
            }
            if (responses.Count == 0) responses["200"] = new JsonObject { ["description"] = "执行成功" };
            operation["responses"] = responses; operations[method] = operation;
        }
        using var document = JsonDocument.Parse(new JsonObject
        {
            ["openapi"] = "3.1.1", ["info"] = new JsonObject { ["title"] = "组件 HTTP 接口", ["version"] = "1.0.0" },
            ["paths"] = paths, ["components"] = new JsonObject
            { ["schemas"] = schemas, ["securitySchemes"] = new JsonObject { ["bearerAuth"] = new JsonObject { ["type"] = "http", ["scheme"] = "bearer", ["bearerFormat"] = "JWT" } } }
        }.ToJsonString());
        return document.RootElement.Clone();
    }
    private static JsonObject Export(Type type, string plugin, JsonSerializerOptions options, JsonObject schemas)
    {
        var key = "H" + Hash(plugin + ":" + type.AssemblyQualifiedName);
        if (!schemas.ContainsKey(key))
        {
            var info = options.GetTypeInfo(type);
            var node = info.GetJsonSchemaAsNode(new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
            using var schema = JsonDocument.Parse(node.ToJsonString()); schemas[key] = JsonSchemaReferences.RebaseToComponent(schema.RootElement, key);
        }
        return new JsonObject { ["$ref"] = "#/components/schemas/" + key };
    }
    private static bool SameIdentity(System.Security.Claims.ClaimsPrincipal principal, ICurrentUser user)
        => Kun.Security.Authentication.PrincipalClaims.TryRead(principal, out var subject, out var tenant, out var kind) && subject == user.UserId && tenant == user.Principal?.TenantId &&
            kind == (user.Principal?.Kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", PrincipalKind.Agent => "agent", _ => "" });
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

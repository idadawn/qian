using System.Text.Json.Serialization;
using Kun.EventBus;

namespace Qian.Registry;

/// <summary>注册表发布的集成事件（PRD 7.2.1「插件化定位」）：租户 scope patch 的挂载与卸载随写入同一事务进入 Outbox。</summary>
public static class RegistryEvents
{
    /// <summary>条目已挂载到 scope：写入（新增或替换）了一条未禁用的 scope patch。</summary>
    public const string ScopeMounted = "registry.scope.mounted.v1";
    /// <summary>条目已从 scope 卸载：撤销了本 scope 引入的 patch，或对平台条目写入了禁用 patch。</summary>
    public const string ScopeUnmounted = "registry.scope.unmounted.v1";

    /// <summary>登记注册表的全部集成事件及其源生成 JsonTypeInfo；平台宿主与契约测试共用这一处。</summary>
    public static IEventTypeRegistrar RegisterRegistryEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        return registrar.Register(ScopeMounted, RegistryEventJson.Default.RegistryScopeMounted)
            .Register(ScopeUnmounted, RegistryEventJson.Default.RegistryScopeUnmounted);
    }
}

/// <summary><see cref="RegistryEvents.ScopeMounted"/> 的载荷；不含配置原文，Plugin 为组件 ID，Version 为 patch 指定的版本（未指定时为 null）。</summary>
[IntegrationEventId(RegistryEvents.ScopeMounted)]
public sealed record RegistryScopeMounted(string Scope, string EntryId, string? Plugin, string? Version, int Revision);

/// <summary><see cref="RegistryEvents.ScopeUnmounted"/> 的载荷；Revision 为卸载后 patch 的修订（墓碑或禁用 patch）。</summary>
[IntegrationEventId(RegistryEvents.ScopeUnmounted)]
public sealed record RegistryScopeUnmounted(string Scope, string EntryId, int Revision);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(RegistryScopeMounted))]
[JsonSerializable(typeof(RegistryScopeUnmounted))]
internal sealed partial class RegistryEventJson : JsonSerializerContext;

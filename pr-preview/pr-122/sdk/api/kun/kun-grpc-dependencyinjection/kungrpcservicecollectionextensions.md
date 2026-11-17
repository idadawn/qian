> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunGrpcServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Grpc` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Grpc`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Grpc)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Grpc.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-grpc-dependencyinjection/index.md)\
Assembly: Kun.Grpc.dll

Extension methods for registering Kun gRPC services with an [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

```csharp
public static class KunGrpcServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunGrpcServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-grpc-dependencyinjection/kungrpcservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunGrpc(IServiceCollection, Action\<GrpcServiceOptions>?) \{#Kun_Grpc_DependencyInjection_KunGrpcServiceCollectionExtensions_AddKunGrpc_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Grpc_AspNetCore_Server_GrpcServiceOptions\_\_}

Registers Kun gRPC services and interceptors:

- `KunTracingInterceptor` — starts an `Activity` per RPC call.
- `KunServerAuthInterceptor` — enforces authenticated `tenant_id` claim.
- `KunResultToRpcInterceptor` — maps `ResultFailureException` to `RpcException`.

Optionally call `AddKunSecurity()` separately to wire up `JwtTenantContext`/`JwtCurrentUser`.

```csharp
public static IGrpcServerBuilder AddKunGrpc(this IServiceCollection services, Action<GrpcServiceOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<GrpcServiceOptions>?

Optional callback to further configure `GrpcServiceOptions`.

#### Returns

IGrpcServerBuilder

The `IGrpcServerBuilder` for further gRPC configuration.

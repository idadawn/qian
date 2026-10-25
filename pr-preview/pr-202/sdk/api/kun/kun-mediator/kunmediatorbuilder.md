> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunMediatorBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/pr-preview/pr-202/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

Fluent builder used to register request handlers, pipeline behaviors, and notification
handlers with [KunMediator](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediator.md). All registrations are performed via
compile-time-closed generics — no runtime reflection or `MakeGenericType` occurs.

```csharp
public sealed class KunMediatorBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMediatorBuilder](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

#### Extension Methods

[PermissionBehaviorExtensions.AddPermissionBehavior\<TRequest, TResponse>(KunMediatorBuilder, Func\<Error, TResponse>)](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/permissionbehaviorextensions.md#Kun_Mediator_Behaviors_PermissionBehaviorExtensions_AddPermissionBehavior__2_Kun_Mediator_KunMediatorBuilder_System_Func_Kun_Core_Results_Error___1__)

## Constructors

### KunMediatorBuilder(IServiceCollection) \{#Kun_Mediator_KunMediatorBuilder__ctor_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Initialises a new builder bound to the given `services`.

```csharp
public KunMediatorBuilder(IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to register DI entries into.

## Properties

### Services \{#Kun_Mediator_KunMediatorBuilder_Services}

The service collection that handler and behavior types are registered into.

```csharp
public IServiceCollection Services { get; }
```

#### Property Value

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Methods

### AddNotificationHandler\<TNotification, THandler>() \{#Kun_Mediator_KunMediatorBuilder_AddNotificationHandler__2}

Registers a notification handler. Multiple handlers may be registered for the same
notification type; they are invoked sequentially in registration order.
Also registers `THandler` as a transient service.

```csharp
public KunMediatorBuilder AddNotificationHandler<TNotification, THandler>() where TNotification : INotification where THandler : class, INotificationHandler<TNotification>
```

#### Returns

[KunMediatorBuilder](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

This builder for chaining.

#### Type Parameters

`TNotification`

The notification type.

`THandler`

The handler implementation type.

### AddPipelineBehavior\<TRequest, TResponse, TBehavior>() \{#Kun_Mediator_KunMediatorBuilder_AddPipelineBehavior__3}

Registers a pipeline behavior for the given request/response pair. Behaviors are
applied in registration order (outermost first). Also registers
`TBehavior` as a transient service.

```csharp
public KunMediatorBuilder AddPipelineBehavior<TRequest, TResponse, TBehavior>() where TRequest : IRequest<TResponse> where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
```

#### Returns

[KunMediatorBuilder](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

This builder for chaining.

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

`TBehavior`

The behavior implementation type.

### AddRequestHandler\<TRequest, TResponse, THandler>() \{#Kun_Mediator_KunMediatorBuilder_AddRequestHandler__3}

Registers a request handler. Also registers `THandler` as a
transient service so the DI container can resolve it.

```csharp
public KunMediatorBuilder AddRequestHandler<TRequest, TResponse, THandler>() where TRequest : IRequest<TResponse> where THandler : class, IRequestHandler<TRequest, TResponse>
```

#### Returns

[KunMediatorBuilder](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

This builder for chaining.

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

`THandler`

The handler implementation type.

### AddRequestHandler\<TRequest, TResponse, THandler>(string, JsonTypeInfo\<TRequest>, JsonTypeInfo\<TResponse>) \{#Kun_Mediator_KunMediatorBuilder_AddRequestHandler__3_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__System_Text_Json_Serialization_Metadata_JsonTypeInfo___1\_\_}

注册 handler 的同时登记稳定能力 ID 与源生成请求／响应 Schema。

```csharp
public KunMediatorBuilder AddRequestHandler<TRequest, TResponse, THandler>(string capabilityId, JsonTypeInfo<TRequest> request, JsonTypeInfo<TResponse> response) where TRequest : IRequest<TResponse> where THandler : class, IRequestHandler<TRequest, TResponse>
```

#### Parameters

`capabilityId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TRequest>

`response` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TResponse>

#### Returns

[KunMediatorBuilder](/pr-preview/pr-202/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

#### Type Parameters

`TRequest`

`TResponse`

`THandler`

> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunEndpointRouteBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.Endpoints](/pr-preview/pr-181/sdk/api/kun/kun-aspnetcore-endpoints/index.md)\
Assembly: Kun.AspNetCore.dll

Extension methods on [`IEndpointRouteBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder) for mapping Kun CQRS
requests to Minimal API endpoints in an AOT-safe manner.

```csharp
public static class KunEndpointRouteBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunEndpointRouteBuilderExtensions](/pr-preview/pr-181/sdk/api/kun/kun-aspnetcore-endpoints/kunendpointroutebuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### MapCommand\<TCommand, TResponse>(IEndpointRouteBuilder, string, JsonTypeInfo\<TCommand>, JsonTypeInfo\<TResponse>) \{#Kun_AspNetCore_Endpoints_KunEndpointRouteBuilderExtensions_MapCommand__2_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__System_Text_Json_Serialization_Metadata_JsonTypeInfo___1\_\_}

Maps a POST endpoint at `pattern` that deserializes the request body
into `TCommand` and dispatches it via [ISender](/pr-preview/pr-181/sdk/api/kun/kun-mediator/isender.md).

```csharp
public static RouteHandlerBuilder MapCommand<TCommand, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, JsonTypeInfo<TCommand> commandTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo) where TCommand : IRequest<TResponse>
```

#### Parameters

`endpoints` [IEndpointRouteBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder)

The endpoint route builder.

`pattern` [string](https://learn.microsoft.com/dotnet/api/system.string)

The URL pattern for the endpoint.

`commandTypeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TCommand>

AOT-safe [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) for `TCommand`.

`responseTypeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TResponse>

AOT-safe [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) for `TResponse`.

#### Returns

[RouteHandlerBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.routehandlerbuilder)

A [`RouteHandlerBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.routehandlerbuilder) for further configuration.

#### Type Parameters

`TCommand`

The command type (must implement [IRequest\<TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-core-cqrs/irequest-1.md)).

`TResponse`

The response type returned by the handler.

### MapQuery\<TQuery, TResponse>(IEndpointRouteBuilder, string, JsonTypeInfo\<TResponse>) \{#Kun_AspNetCore_Endpoints_KunEndpointRouteBuilderExtensions_MapQuery__2_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___1\_\_}

Maps a GET endpoint at `pattern` that binds query string / route parameters
into `TQuery` via `[AsParameters]` and dispatches it via [ISender](/pr-preview/pr-181/sdk/api/kun/kun-mediator/isender.md).

```csharp
public static RouteHandlerBuilder MapQuery<TQuery, TResponse>(this IEndpointRouteBuilder endpoints, string pattern, JsonTypeInfo<TResponse> responseTypeInfo) where TQuery : IRequest<TResponse>, new()
```

#### Parameters

`endpoints` [IEndpointRouteBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder)

The endpoint route builder.

`pattern` [string](https://learn.microsoft.com/dotnet/api/system.string)

The URL pattern for the endpoint.

`responseTypeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TResponse>

AOT-safe [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) for `TResponse`.

#### Returns

[RouteHandlerBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.routehandlerbuilder)

A [`RouteHandlerBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.routehandlerbuilder) for further configuration.

#### Type Parameters

`TQuery`

The query type (must implement [IRequest\<TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-core-cqrs/irequest-1.md)).

`TResponse`

The response type returned by the handler.

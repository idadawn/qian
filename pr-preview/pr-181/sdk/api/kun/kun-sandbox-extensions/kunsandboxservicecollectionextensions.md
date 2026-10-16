> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunSandboxServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Extensions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/index.md)\
Assembly: Kun.Sandbox.dll

Service-collection extensions that register a Kun sandbox backend.

```csharp
public static class KunSandboxServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSandboxServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/kunsandboxservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunSandbox(IServiceCollection, Action\<KunSandboxOptions>) \{#Kun_Sandbox_Extensions_KunSandboxServiceCollectionExtensions_AddKunSandbox_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Sandbox_Extensions_KunSandboxOptions\_\_}

Registers a single [ISandboxProvider](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) implementation
according to the backend selected in `configure`.

```csharp
public static IServiceCollection AddKunSandbox(this IServiceCollection services, Action<KunSandboxOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunSandboxOptions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)>

Callback that picks exactly one backend via [KunSandboxOptions.UseSupervisor](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseSupervisor_System_Uri_System_Security_Cryptography_X509Certificates_X509Certificate2_System_Action_System_Net_Http_HttpClient__) / [KunSandboxOptions.UseE2BCloud](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseE2BCloud_System_String_System_String_) / [KunSandboxOptions.UseInMemory](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseInMemory_System_Func_System_Collections_Generic_IReadOnlyList_System_String__Kun_Sandbox_Abstractions_ProcessRunOptions_Kun_Sandbox_Abstractions_ProcessResult__).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown when no backend was selected.

> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class IntegrationEventIdAttribute

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.Abstractions.dll

Declares the stable event-type identifier (token) for an integration event CLR type.
Use with [IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1) so the token lives next
to the type definition rather than being repeated in DI wiring.

```csharp
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IntegrationEventIdAttribute : Attribute
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ←
[IntegrationEventIdAttribute](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/integrationeventidattribute.md)

#### Inherited Members

[Attribute.GetCustomAttributes(MemberInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-memberinfo-system-type\)),
[Attribute.GetCustomAttributes(MemberInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-memberinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(MemberInfo)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-memberinfo\)),
[Attribute.GetCustomAttributes(MemberInfo, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-memberinfo-system-boolean\)),
[Attribute.IsDefined(MemberInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-memberinfo-system-type\)),
[Attribute.IsDefined(MemberInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-memberinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttribute(MemberInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-memberinfo-system-type\)),
[Attribute.GetCustomAttribute(MemberInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-memberinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(ParameterInfo)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-parameterinfo\)),
[Attribute.GetCustomAttributes(ParameterInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-parameterinfo-system-type\)),
[Attribute.GetCustomAttributes(ParameterInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-parameterinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(ParameterInfo, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-parameterinfo-system-boolean\)),
[Attribute.IsDefined(ParameterInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-parameterinfo-system-type\)),
[Attribute.IsDefined(ParameterInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-parameterinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttribute(ParameterInfo, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-parameterinfo-system-type\)),
[Attribute.GetCustomAttribute(ParameterInfo, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-parameterinfo-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(Module, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-module-system-type\)),
[Attribute.GetCustomAttributes(Module)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-module\)),
[Attribute.GetCustomAttributes(Module, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-module-system-boolean\)),
[Attribute.GetCustomAttributes(Module, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-module-system-type-system-boolean\)),
[Attribute.IsDefined(Module, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-module-system-type\)),
[Attribute.IsDefined(Module, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-module-system-type-system-boolean\)),
[Attribute.GetCustomAttribute(Module, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-module-system-type\)),
[Attribute.GetCustomAttribute(Module, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-module-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(Assembly, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-assembly-system-type\)),
[Attribute.GetCustomAttributes(Assembly, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-assembly-system-type-system-boolean\)),
[Attribute.GetCustomAttributes(Assembly)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-assembly\)),
[Attribute.GetCustomAttributes(Assembly, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes#system-attribute-getcustomattributes\(system-reflection-assembly-system-boolean\)),
[Attribute.IsDefined(Assembly, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-assembly-system-type\)),
[Attribute.IsDefined(Assembly, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined#system-attribute-isdefined\(system-reflection-assembly-system-type-system-boolean\)),
[Attribute.GetCustomAttribute(Assembly, Type)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-assembly-system-type\)),
[Attribute.GetCustomAttribute(Assembly, Type, bool)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute#system-attribute-getcustomattribute\(system-reflection-assembly-system-type-system-boolean\)),
[Attribute.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.attribute.equals),
[Attribute.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.attribute.gethashcode),
[Attribute.Match(object?)](https://learn.microsoft.com/dotnet/api/system.attribute.match),
[Attribute.IsDefaultAttribute()](https://learn.microsoft.com/dotnet/api/system.attribute.isdefaultattribute),
[Attribute.TypeId](https://learn.microsoft.com/dotnet/api/system.attribute.typeid),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

The token shape follows the framework convention
`{bounded-context}.{event-name}.v{version}`, e.g. `order.placed.v1`.
Applying this attribute does _not_ auto-register the type — registration
is still explicit via [IEventTypeRegistrar](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md). AOT-strict consumers that
must avoid reflection at startup can keep using
[IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_).

## Constructors

### IntegrationEventIdAttribute(string) \{#Kun_EventBus_IntegrationEventIdAttribute__ctor_System_String\_}

Initializes a new attribute with the given stable token.

```csharp
public IntegrationEventIdAttribute(string eventType)
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

The stable event-type token, e.g. `order.placed.v1`.

## Properties

### EventType \{#Kun_EventBus_IntegrationEventIdAttribute_EventType}

The stable event-type token declared by this attribute.

```csharp
public string EventType { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

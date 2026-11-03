> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class RequiredConfigAttribute

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Configuration)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Attributes](/sdk/api/kun/kun-configuration-attributes/index.md)\
Assembly: Kun.Configuration.dll

[`RequiredAttribute`](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.requiredattribute) tuned for configuration errors —
rejects [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) values and produces an error
message that names the missing key rather than the generic
"field is required" text.

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RequiredConfigAttribute : RequiredAttribute
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ←
[ValidationAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute) ←
[RequiredAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.requiredattribute) ←
[RequiredConfigAttribute](/sdk/api/kun/kun-configuration-attributes/requiredconfigattribute.md)

#### Inherited Members

[RequiredAttribute.IsValid(object?)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.requiredattribute.isvalid),
[RequiredAttribute.AllowEmptyStrings](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.requiredattribute.allowemptystrings),
[ValidationAttribute.FormatErrorMessage(string)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.formaterrormessage),
[ValidationAttribute.IsValid(object?)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.isvalid#system-componentmodel-dataannotations-validationattribute-isvalid\(system-object\)),
[ValidationAttribute.GetValidationResult(object?, ValidationContext)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.getvalidationresult),
[ValidationAttribute.Validate(object?, string)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate#system-componentmodel-dataannotations-validationattribute-validate\(system-object-system-string\)),
[ValidationAttribute.Validate(object?, ValidationContext)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate#system-componentmodel-dataannotations-validationattribute-validate\(system-object-system-componentmodel-dataannotations-validationcontext\)),
[ValidationAttribute.RequiresValidationContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.requiresvalidationcontext),
[ValidationAttribute.ErrorMessage](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessage),
[ValidationAttribute.ErrorMessageResourceName](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcename),
[ValidationAttribute.ErrorMessageResourceType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcetype),
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

## Constructors

### RequiredConfigAttribute() \{#Kun_Configuration_Attributes_RequiredConfigAttribute__ctor}

Creates the attribute with config-aware defaults.

```csharp
public RequiredConfigAttribute()
```

## Methods

### FormatErrorMessage(string) \{#Kun_Configuration_Attributes_RequiredConfigAttribute_FormatErrorMessage_System_String\_}

```csharp
public override string FormatErrorMessage(string name)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

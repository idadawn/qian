> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ErrorToProblemDetailsExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.AspNetCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.Mapping](/sdk/api/kun/kun-aspnetcore-mapping/index.md)\
Assembly: Kun.AspNetCore.dll

Extension methods for mapping [Error](/sdk/api/kun/kun-core-results/error.md) to [`ProblemDetails`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.problemdetails).

```csharp
public static class ErrorToProblemDetailsExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ErrorToProblemDetailsExtensions](/sdk/api/kun/kun-aspnetcore-mapping/errortoproblemdetailsextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### ToProblemDetails(Error) \{#Kun_AspNetCore_Mapping_ErrorToProblemDetailsExtensions_ToProblemDetails_Kun_Core_Results_Error\_}

Maps a Kun [Error](/sdk/api/kun/kun-core-results/error.md) to an RFC 7807 [`ProblemDetails`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.problemdetails) response,
translating [ErrorType](/sdk/api/kun/kun-core-results/errortype.md) to the appropriate HTTP status code.

```csharp
public static ProblemDetails ToProblemDetails(this Error error)
```

#### Parameters

`error` Error

The error to convert.

#### Returns

[ProblemDetails](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.problemdetails)

A [`ProblemDetails`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.problemdetails) populated from the error.

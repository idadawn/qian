> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IKunConfigurationValidator\<TOptions>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Configuration)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-configuration-abstractions/index.md)\
Assembly: Kun.Configuration.dll

Reflection-free options validator — the AOT-safe alternative to
DataAnnotations. Register one per options type via
`services.AddSingleton<IKunConfigurationValidator<T>, MyValidator>()`
and `AddKunOptions<T>` will merge the results with any
DataAnnotations-driven validation.

```csharp
public interface IKunConfigurationValidator<in TOptions> where TOptions : class
```

#### Type Parameters

`TOptions`

The options type being validated.

## Methods

### Validate(string?, TOptions) \{#Kun_Configuration_Abstractions_IKunConfigurationValidator_1_Validate_System_String__0\_}

Validates `options`. Return
[`Success`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.validateoptionsresult.success) on success or
[`Fail`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.validateoptionsresult.fail)
with section-qualified messages on failure.

```csharp
ValidateOptionsResult Validate(string? name, TOptions options)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The named-options name, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the default instance.

`options` TOptions

The populated options object.

#### Returns

[ValidateOptionsResult](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.validateoptionsresult)

> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Sandbox.Extensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[KunSandboxOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)

Builder passed to [KunSandboxServiceCollectionExtensions.AddKunSandbox](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxservicecollectionextensions.md#Kun_Sandbox_Extensions_KunSandboxServiceCollectionExtensions_AddKunSandbox_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Sandbox_Extensions_KunSandboxOptions__).
Exactly one of [KunSandboxOptions.UseSupervisor](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseSupervisor_System_Uri_System_Security_Cryptography_X509Certificates_X509Certificate2_System_Action_System_Net_Http_HttpClient__), [KunSandboxOptions.UseE2BCloud](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseE2BCloud_System_String_System_String_),
or [KunSandboxOptions.UseInMemory](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseInMemory_System_Func_System_Collections_Generic_IReadOnlyList_System_String__Kun_Sandbox_Abstractions_ProcessRunOptions_Kun_Sandbox_Abstractions_ProcessResult__) must be called; calling a second one
replaces the previous choice.

[KunSandboxServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxservicecollectionextensions.md)

Service-collection extensions that register a Kun sandbox backend.

> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Identity.AspNetCore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity.AspNetCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[ChallengeHelper](/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md)

Builds the `/authorize` redirect URL for an OIDC provider
registered via `AddKunIdentity(id => id.AddOidcClient(...))`.
Generates the one-shot PKCE `code_verifier` /
`code_challenge` pair, a `state`, and a `nonce`,
stashes the verifier + state + nonce + returnUrl in a short-lived
cookie so the callback handler can replay them, and returns the
URL the caller should redirect the user to.

[ChallengePayload](/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)

Payload written to the one-shot challenge cookie by
[ChallengeHelper.BuildLoginUrlAsync](/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_BuildLoginUrlAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_System_String_System_String_System_Threading_CancellationToken_). Consumer
callback handlers read it via [ChallengeHelper.ConsumeAsync](/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_ConsumeAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_Threading_CancellationToken_).

[KunCookieAuthApplicationBuilderExtensions](/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthapplicationbuilderextensions.md)

`IApplicationBuilder` extensions for the Kun cookie middleware.

[KunCookieAuthMiddleware](/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthmiddleware.md)

Middleware that hydrates `HttpContext.User` from a
[ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) entry keyed by the session cookie.
Requests without a valid session flow through anonymously; the
middleware never writes a 401 itself — downstream authorization
policies are responsible for that.

[KunCurrentUser](/sdk/api/kun/kun-identity-aspnetcore/kuncurrentuser.md)

Request-scoped [IAuthenticatedUser](/sdk/api/kun/kun-identity-abstractions/iauthenticateduser.md) (and
[ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md)) resolver. Reads claims off
`HttpContext.User` — which `UseKunCookieAuth` populates
from `ISessionStore` earlier in the pipeline. Anonymous when
no [`IHttpContextAccessor`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor) is available or the
authentication middleware has not run.

[KunIdentityAspNetCoreServiceCollectionExtensions](/sdk/api/kun/kun-identity-aspnetcore/kunidentityaspnetcoreservicecollectionextensions.md)

Registers the browser identity helpers without replacing the host's authentication scheme.

[OidcLoginService](/sdk/api/kun/kun-identity-aspnetcore/oidcloginservice.md)

Completes the protected browser transaction before creating a local session.

[OidcLogoutEndpoint](/sdk/api/kun/kun-identity-aspnetcore/oidclogoutendpoint.md)

Back-channel logout mapping; authentication comes from the signed provider token, not a browser cookie.

[OidcLogoutService](/sdk/api/kun/kun-identity-aspnetcore/oidclogoutservice.md)

Validates provider logout instructions before atomically revoking local sessions.

[SignInHelper](/sdk/api/kun/kun-identity-aspnetcore/signinhelper.md)

Turns a successful OIDC code-exchange into a signed-in browser
session: writes the [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md) into the configured
[ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) and emits the opaque session cookie on
the current response.

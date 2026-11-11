using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using OpenIddict.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Tenancy.Signup;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Platform.Host;

/// <summary>
/// 同进程身份分支（S1-DEP-011，R143 的 staging 过渡形状，装配方式按 R150 ①）：身份服务与开通申请组件进平台宿主的同一插件树，身份请求走自己的分支；
/// 网关仍是独立进程。只在配置 <c>Identity:InProcess=true</c> 时启用，否则宿主保持 S1-DEP-009 的形状（身份不在进程内）。
/// <list type="bullet">
/// <item><see cref="Extend"/>：登记 <c>qian.identity.server</c> 与 <c>qian.tenancy.signup</c> 的目录项与数据上下文，内置扩展 overlay（<see cref="OverlayFile"/>）把 platform profile 的
/// <c>identity</c> 条目换成身份服务并加 <c>tenancy.signup</c>；身份事件并进同一次事件与中介者登记；开通申请的运营接口挂进运营端点组（受众 <c>qian-operations</c>）。</item>
/// <item><see cref="Configure"/>：身份分支的中间件与端点：OIDC 协议面、账号接口、认证页族、申请人接口与公开页，不经平台管线（网关 S2S 认证、租户 scope、授权 PEP）。</item>
/// </list>
/// 同进程后经本宿主做的授权变更由 Authz 经进程内的身份账号端口递增身份修订并抬高 PEP 下限（S1-DEC-027 / S1-AZ-009），本宿主不登记身份侧的授权事件订阅（避免双递增）。
/// </summary>
public static class PlatformIdentity
{
    /// <summary>内置扩展 overlay 的文件名，随发布产物放在程序目录。</summary>
    public const string OverlayFile = "identity.overlay.yml";
    /// <summary>开关：为 true 时并入身份分支。</summary>
    public const string EnabledKey = "Identity:InProcess";
    /// <summary>开通申请运营接口的两个运营前缀（网关路由表里指向运营簇）。</summary>
    public static readonly IReadOnlyList<PathString> OperationPrefixes = [new("/api/signup/applications"), new("/api/signup/meta")];

    /// <summary>配置是否要求并入身份分支。</summary>
    public static bool Enabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue<bool>(EnabledKey);
    }

    /// <summary>平台宿主的扩展登记（<see cref="PlatformHost.RunAsync"/> 的 <c>extend</c>）。</summary>
    public static void Extend(PlatformHostComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        var configuration = composition.Builder.Configuration;
        if (!Enabled(configuration)) return;
        var environment = composition.Builder.Environment;
        composition
            .AddPatch(configuration["Identity:Overlay"] is { Length: > 0 } overlay ? overlay : Path.Combine(AppContext.BaseDirectory, OverlayFile))
            .AddComponent(new PlatformComponent("qian.identity.server", "Qian.Identity.Server.IdentityServerComponent, Qian.Identity.Server", _ => new IdentityServerComponent(),
                services => RegisterIdentity(services, environment)))
            .AddComponent(new PlatformComponent("qian.tenancy.signup", "Qian.Tenancy.Signup.TenancySignupComponent, Qian.Tenancy.Signup", _ => new TenancySignupComponent(),
                services => services.AddAppData<TenancySignupDbContext>(TenancySignupDbContext.Schema)))
            .AddEvents(registrar => registrar.RegisterIdentityEvents())
            .AddMediator(mediator => mediator.AddQianIdentityEvents())
            // 运营端点组：列表、详情、通过、驳回、重试与状态机元数据，经 Authz 超管旁路放行并写平台审计链；MapQianTenancySignupOperations 一次映射两个前缀下的端点。
            .AddOperations(OperationPrefixes[0], (endpoints, runtime) => endpoints.MapQianTenancySignupOperations(_ => runtime.Global("tenancy.signup")))
            .AddOperations(OperationPrefixes[1]);
    }

    /// <summary>身份分支的中间件与端点（<see cref="PlatformHost.RunAsync"/> 的 <c>configure</c>）：排在平台管线之前，按路径接手身份请求。</summary>
    public static void Configure(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enabled(builder.Configuration)) return;
        var pages = PagesDirectory("Identity:PagesRoot", builder.Configuration["Identity:PagesRoot"], required: true)!;
        var signupPages = PagesDirectory("Identity:SignupPagesRoot", builder.Configuration["Identity:SignupPagesRoot"], required: false);
        builder.Services.AddSingleton<IStartupFilter>(new IdentityBranch(pages, signupPages, ProxyHeaders(builder.Configuration)));
    }

    // 身份分支信任的反向代理（staging：边缘 hub 终止 TLS → WireGuard → Traefik → 本宿主）。身份服务只收 HTTPS，要由可信代理的 X-Forwarded-Proto 恢复原始 scheme，
    // X-Forwarded-For 恢复客户端地址（登录审计、会话与申请人限流都记它）。只信任 Identity:TrustedProxyNetworks 列出的网段（CIDR，前缀长度不能为 0）；
    // 没有配置时不处理任何转发头——清空 ASP.NET 的缺省名单不等于拒绝全部，所以干脆不挂中间件。Identity:ForwardLimit（1–5，缺省 1）是从右往左回溯的跳数上限，
    // 每回溯一跳，上一跳的地址都必须在可信网段里；客户端自己塞进 X-Forwarded-For 的条目在上限之外，不会被采信。
    internal static ForwardedHeadersOptions? ProxyHeaders(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var networks = configuration.GetSection("Identity:TrustedProxyNetworks").GetChildren().Select(item => item.Value).ToArray();
        if (networks.Length == 0) return null;
        var limit = configuration.GetValue("Identity:ForwardLimit", 1);
        if (limit is < 1 or > 5) throw new InvalidOperationException("身份分支配置 Identity:ForwardLimit 必须在 1 到 5 之间。");
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = limit };
        options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
        foreach (var value in networks)
            options.KnownIPNetworks.Add(value is { Length: > 0 } && System.Net.IPNetwork.TryParse(value, out var network) && network.PrefixLength > 0 ? network
                : throw new InvalidOperationException("身份分支配置 Identity:TrustedProxyNetworks 里的「" + value + "」不是合法网段（CIDR，前缀长度不能为 0）。"));
        return options;
    }

    // 身份服务的宿主 DI：数据上下文绑应用登录，账号 Cookie scheme，签名与加密证书、官方客户端取自 Secret 文件，数据保护密钥目录可持久。
    private static void RegisterIdentity(PlatformServices services, IHostEnvironment environment)
    {
        services.AddAppData<IdentityDbContext>(IdentityDbContext.Schema);
        var configuration = services.Configuration;
        var issuer = services.ConfigString("issuer") is { } value && Uri.TryCreate(value, UriKind.Absolute, out var parsed) ? parsed
            : throw new InvalidOperationException("identity 条目的配置缺少合法的 issuer（环境 overlay 给出身份发行方地址）。");
        var protection = services.Services.AddDataProtection().SetApplicationName("qian.identity");
        // 认证页族的账号 Cookie 与防伪令牌靠数据保护密钥解密；只读根文件系统下缺省位置不可写，staging 挂 emptyDir（重启即需重新登录），多副本时须共享目录。
        if (configuration["Identity:DataProtectionKeysDirectory"] is { Length: > 0 } keys) protection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keys)));
        // 账号 Cookie scheme：未登录的授权请求挑战到同源认证页族 /login?ReturnUrl=…（页面登录后回到授权请求签发授权码）。
        services.Services.AddAuthentication().AddCookie("qian.identity.signin", cookie => cookie.LoginPath = IdentityPagesHosting.PagesPath);
        services.Services.AddHttpClient();
        services.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = issuer, AllowLoopbackHttp = configuration.GetValue<bool>("Identity:AllowLoopbackHttp"),
            SigningCertificates = [Certificate(configuration, "Signing")], EncryptionCertificates = [Certificate(configuration, "Encryption")],
            Clients = [.. configuration.GetSection("Identity:Clients").GetChildren().Select(Client)]
        }, environment);
    }

    // 官方客户端（网关 BFF）：机密客户端，授权码 + PKCE 与 refresh；密钥只从 Secret 文件读，回调与 resource 精确登记。
    private static IdentityClientRegistration Client(IConfigurationSection section)
    {
        var id = Required(section, "ClientId");
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = id, ClientSecret = PlatformHostSettings.ReadSecret(Required(section, "SecretFile")), ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit, DisplayName = section["DisplayName"] is { Length: > 0 } name ? name : id
        };
        descriptor.RedirectUris.Add(new Uri(Required(section, "RedirectUri"), UriKind.Absolute));
        descriptor.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api",
            .. section.GetSection("Scopes").GetChildren().Select(scope => scope.Value).OfType<string>().Select(scope => Permissions.Prefixes.Scope + scope)]);
        return new IdentityClientRegistration { Application = descriptor, Resources = [Required(section, "Resource")] };
    }

    // 签名 / 加密证书：PEM 证书与私钥分别是 Secret 文件（RSA）；重新导入成可导出的实例，协议库在任何平台上都能用它签名与解密。
    private static X509Certificate2 Certificate(IConfiguration configuration, string purpose)
    {
        var certificate = Path(configuration, "Identity:" + purpose + "Certificate");
        var key = Path(configuration, "Identity:" + purpose + "Key");
        using var loaded = X509Certificate2.CreateFromPemFile(certificate, key);
        return X509CertificateLoader.LoadPkcs12(loaded.Export(X509ContentType.Pkcs12), null);

        static string Path(IConfiguration configuration, string name) =>
            configuration[name] is { Length: > 0 } value && System.IO.Path.IsPathFullyQualified(value) ? value
                : throw new InvalidOperationException("缺少身份分支配置 " + name + "（Secret 文件的绝对路径）。");
    }

    // 静态页的构建产物目录（镜像里在 /app/web 下）：必须带 index.html，缺了宿主不启动，而不是到首个请求才 404。
    private static string? PagesDirectory(string name, string? value, bool required)
    {
        if (value is not { Length: > 0 }) return required ? throw new InvalidOperationException("缺少身份分支配置 " + name + "。") : null;
        var directory = Path.GetFullPath(value);
        return File.Exists(Path.Combine(directory, "index.html")) ? directory : throw new InvalidOperationException(name + " 指向的目录里没有 index.html：" + directory);
    }

    private static string Required(IConfigurationSection section, string name) =>
        section[name] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少身份分支配置 " + section.Path + ":" + name + "。");

    // 同进程混合部署：身份的协议面、账号接口与认证页族不经平台管线（平台管线只认网关 S2S，对没有声明授权的端点也要求认证）。
    // 申请人接口在分支里自带 ErrorDetail 映射；公开页只在给出构建产物时托管。开通申请的运营接口不在分支里，走平台管线的运营端点组。
    // 目录接口（/api/directory）不在分支里：它由网关以 S2S 转来，与联调环境相同走平台管线。
    // 转发头只在分支里处理（配置了可信代理网段时排在最前）：平台管线只认网关 S2S，不需要也不采信客户端地址与 scheme。
    private sealed class IdentityBranch(string pages, string? signupPages, ForwardedHeadersOptions? forwarded) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(http => Owns(http.Request.Path), branch =>
            {
                if (forwarded is not null) branch.UseForwardedHeaders(forwarded);
                branch.UseQianIdentityServer();
                branch.UseQianIdentityPages(pages);
                branch.UseWhen(http => TenancySignupEndpoints.IsApplicantPath(http.Request.Path), errors => errors.UseQianErrors());
                if (signupPages is not null) branch.UseQianTenancySignupPages(signupPages);
                branch.UseRouting(); branch.UseAuthentication();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapQianIdentityServer(); endpoints.MapQianIdentityAccounts();
                    endpoints.MapQianTenancySignupApplicant(http => http.RequestServices.GetRequiredService<PlatformRuntime>().Global("tenancy.signup"));
                });
            });
            next(app);
        };

        private bool Owns(PathString path) => path.StartsWithSegments("/api/identity") || path.StartsWithSegments("/connect") || path.StartsWithSegments("/.well-known") ||
            path.StartsWithSegments(IdentityPagesHosting.PagesPath) || TenancySignupEndpoints.IsApplicantPath(path) ||
            (signupPages is not null && path.StartsWithSegments(TenancySignupPagesHosting.PagesPath));
    }
}

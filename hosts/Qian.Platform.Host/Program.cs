using Qian.Platform.Host;

// platform profile 的生产宿主（S1-DEP-009）：装配步骤见 PlatformHost，这里只按顺序调用。
// 同进程身份分支（S1-DEP-011，R150 ①）随宿主内置，只在 Identity:InProcess=true 时并入（staging 的过渡形状，R143）。
return await PlatformHost.RunAsync(args, PlatformIdentity.Extend, PlatformIdentity.Configure);

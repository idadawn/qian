using Qian.Platform.Host;

// platform profile 的生产宿主（S1-DEP-009）：装配步骤见 PlatformHost，这里只按顺序调用。
return await PlatformHost.RunAsync(args);

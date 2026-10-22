# 网关 Definition

- 只依赖 Runtime，不把 ASP.NET、YARP 或具体身份 Provider 放进此契约。
- cluster 的 Id 即唯一 S2S audience，不提供请求级覆盖。
- 公共 API 与 PublicAPI.Unshipped.txt 同步；配置不携带密钥值。

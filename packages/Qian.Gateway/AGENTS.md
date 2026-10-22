# 网关 Provider

- 权威边界为 GW-001、ADR-010 与 PRD 7.9；不要吞并 GW-002 / GW-003 / AUD-003。
- 只能组合 Kun.Gateway 的公开签发与轮换原语，不复制签名器或 Cookie 会话实现；组件库不能引用其他 Provider。
- 所有代理路由必须认证；显式 Authorization 失败不得降级到 Cookie。每跳 audience 固定到实际 cluster，禁止 Cookie 或原令牌透传。
- 转发地址只来自可信代理中间件处理后的连接信息；空信任名单必须禁用转发头处理。
- GatewayComponent 挂载、撤销与 /readyz 必须一致；所有动态登记经 ctx.Effect 回收。
- 保留 DEC-029 / DEC-030 的完整 Linux NativeAOT 验收，不通过删 Data/Audit、空 Provider 或 IL 抑制冒充完成。
- 清单与 Schema 从实际代码同步；私有源安装未通过时保留骨架标记。

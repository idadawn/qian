---
"@idadawn/yue-tokens": patch
"@idadawn/yue-core": patch
"@idadawn/yue-shell": patch
---

租户开通流程原型复刻的验收返工（S1-YUE-019）。

tokens：新增状态图标档 `--yue-{ok,warn,error,info,neutral}-icon`（Tailwind 类 `text-*-icon`），用于结果页、检查列表等处的状态图标字形：warn 取 amber-600，比圆点用的实色深一档，大图标在浅底上更清楚；其余语气与实色相同。`-solid` 改为只用于圆点与进度填充。令牌检查（`tokens:check`）新增一项：任何样式表再强制灰阶抗锯齿（`@apply antialiased`、`-webkit-font-smoothing: antialiased`、`-moz-osx-font-smoothing: grayscale`）即失败。

core：Result 的状态图标改用图标档，「审核中」等 warn 图标与原型一致。

shell：AuthLayout 的 sign-in 侧栏标题改为上下自动外边距居中，不传 `footer` 时不再沉到底部压住装饰条；新增部件 `auth-layout-headline` 与 `auth-layout-decoration`。BrandMark 带 `data-slot="brand-mark"` 与默认 testId `yue-brand-mark`，可传 `testId` 覆盖。外壳顶栏含底边为 56px（此前 57px）。PasswordLogin「其他登录方式」分隔线到入口 24px，多个入口之间 12px。

// 单独重新生成 components/、cli/、design/，不启动 Rspress：pnpm --filter website generate（需 Node ≥ 22.18）。
import { generateAll, summarize } from './sources-plugin.ts';

console.log(summarize(generateAll()));

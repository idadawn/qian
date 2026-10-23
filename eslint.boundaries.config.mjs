// 只跑 yue 分层边界门禁（pnpm yue:boundaries）；规则本体在 packages/yue/boundaries.config.mjs。
import { yueBoundariesConfig } from './packages/yue/boundaries.config.mjs';

export default [
  { ignores: ['**/node_modules/**', '**/dist/**', '**/dist-*/**', 'website/**', 'tests/**'] },
  ...yueBoundariesConfig,
];

// 循环依赖样例：a → b → a，depcruise 必须报 no-circular。
import { b } from './b';
export const a = () => b();

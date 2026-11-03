/** 顶层跳转的唯一出口：组件测试替换它来记录目标地址，而不真的离开页面。 */
export const browser = { assign(url: string): void { location.assign(url); } };

/** 只接受同源的登录页相对地址（/login 开头、不带协议与双斜杠），用于「去登录」按钮；其余一律丢弃。 */
export function safeNext(value: string | null): string | undefined {
  if (!value || !value.startsWith('/login') || value.startsWith('//') || value.includes('\\')) return undefined;
  try { const url = new URL(value, location.origin); return url.origin === location.origin ? url.pathname + url.search : undefined; } catch { return undefined; }
}
/** 公开页之间跳转时带上来源登录页地址，申请获批后可以直接回去登录。 */
export function withNext(path: string, next: string | undefined): string {
  return next ? `${path}${path.includes('?') ? '&' : '?'}next=${encodeURIComponent(next)}` : path;
}

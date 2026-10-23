import { safeNavigationUrl } from '@idadawn/yue-shell';

/** 认证页族的入口参数：账号 Cookie 挑战的 ReturnUrl（或选择页的 return_url）须是同源的授权端点地址，client_id 取自其查询串。 */
export interface AuthorizeEntry { returnUrl: string; clientId: string }

/**
 * 只接受同源相对路径且落在 /connect/authorize 的回跳地址：登录完成后浏览器顶层跳回授权请求签发授权码，其它任何值（跨源、别的路径、带令牌参数）
 * 一律视为无效入口，不渲染表单。这是认证页族唯一的跳转目标，不存在开放重定向。
 */
export function authorizeEntry(search: string, origin: string): AuthorizeEntry | null {
  const params = new URLSearchParams(search);
  const raw = params.get('ReturnUrl') ?? params.get('return_url');
  if (!raw) return null;
  let target: string;
  try { target = safeNavigationUrl(raw); } catch { return null; }
  const url = new URL(target, origin);
  if (url.pathname !== '/connect/authorize') return null;
  const clientId = url.searchParams.get('client_id');
  return clientId && /^[A-Za-z0-9._:-]{1,200}$/.test(clientId) ? { returnUrl: target, clientId } : null;
}

/** 回到登录页并保留授权请求；没有授权请求时回到裸登录页（只能显示无效入口）。 */
export function loginUrl(returnUrl: string | null): string {
  return returnUrl ? `/login?ReturnUrl=${encodeURIComponent(returnUrl)}` : '/login';
}

import type { Locale } from '@idadawn/yue-sdk';

/**
 * 外壳运行时配置：同源 `/app-config.json`（K8s ConfigMap 挂成的文件，网关每次现读、`no-store`）的已校验形状。
 * 构建产物不含任何环境值：改 ConfigMap 后刷新即生效，不重编译。配置是数据不是脚本：能写 ConfigMap 不等于能在外壳源执行脚本
 * （构建产物 index.html 的 CSP 只放行同源脚本与首帧主题脚本的哈希）。
 */
export interface AppConfig {
  /** 平台 API 在网关上的挂载前缀，缺省 `/api`。契约、远程模块与 bridge.invoke 写规范路径 `/api/...`，由外壳客户端的中间件映射到它。 */
  apiBase: string;
  /** 依赖方（Identity.Client / 网关 BFF）端点前缀，缺省 `/auth`。 */
  authBase: string;
  /** bootstrap 聚合端点，缺省 `/api/shell/bootstrap`（规范路径，随 apiBase 映射）。 */
  bootstrapUrl: string;
  /** 远程被拒（版本不符）的审计上报端点，缺省 `/api/shell/audit/remote-rejected`（规范路径，随 apiBase 映射）。 */
  auditUrl: string;
  /** 顶栏品牌文字。 */
  brand?: string;
  /** 缺省语言：只在用户没有保存过语言偏好时生效，不写进偏好存储。 */
  locale?: Locale;
  /** 平台代管常驻横幅；缺省按会话判断（平台身份即代管）。 */
  managed?: boolean;
}

/** 运行时配置的同源地址。 */
export const APP_CONFIG_PATH = '/app-config.json';
const LOCALES: readonly Locale[] = ['zh-CN', 'zh-TW', 'en-US'];
const KEYS = new Set(['apiBase', 'authBase', 'bootstrapUrl', 'auditUrl', 'brand', 'locale', 'managed']);
const DEFAULTS: AppConfig = Object.freeze({ apiBase: '/api', authBase: '/auth', bootstrapUrl: '/api/shell/bootstrap', auditUrl: '/api/shell/audit/remote-rejected' });

/**
 * 同源路径：以单个 `/` 开头，不含查询串、片段、反斜杠、空白与控制字符，不以 `/` 结尾，不含 `.` / `..` 段。
 * 绝对地址与协议相对地址一律拒绝——BFF Cookie 会话只对同源发送，跨源前缀会把请求（与将来的令牌承载方式）引到别处。
 */
export function isSameOriginPath(value: unknown): value is string {
  return typeof value === 'string' && value.length > 1 && value.length <= 256 && value.startsWith('/') && !value.startsWith('//') && !value.endsWith('/')
    && !/[\\?#\s]/.test(value) && ![...value].some(character => character.charCodeAt(0) <= 32 || character.charCodeAt(0) === 127)
    && !value.split('/').some(segment => segment === '.' || segment === '..');
}

/** 校验运行时配置；缺失（null / undefined）即全部缺省值，未知字段或字段非法抛 `shell.error.config`。 */
export function parseAppConfig(source: unknown): AppConfig {
  if (source === undefined || source === null) return { ...DEFAULTS };
  if (typeof source !== 'object' || Array.isArray(source)) throw new TypeError('shell.error.config');
  const raw = source as Record<string, unknown>;
  if (Object.keys(raw).some(key => !KEYS.has(key))) throw new TypeError('shell.error.config');
  const config: AppConfig = { ...DEFAULTS };
  for (const key of ['apiBase', 'authBase', 'bootstrapUrl', 'auditUrl'] as const) {
    if (raw[key] === undefined) continue;
    if (!isSameOriginPath(raw[key])) throw new TypeError('shell.error.config');
    config[key] = raw[key];
  }
  if (raw.brand !== undefined) {
    if (typeof raw.brand !== 'string' || raw.brand.trim().length === 0 || raw.brand.length > 64 || [...raw.brand].some(character => character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127)) throw new TypeError('shell.error.config');
    config.brand = raw.brand;
  }
  if (raw.locale !== undefined) {
    if (!LOCALES.includes(raw.locale as Locale)) throw new TypeError('shell.error.config');
    config.locale = raw.locale as Locale;
  }
  if (raw.managed !== undefined) {
    if (typeof raw.managed !== 'boolean') throw new TypeError('shell.error.config');
    config.managed = raw.managed;
  }
  return config;
}

/**
 * 读取并校验运行时配置（`no-store`、同源、不跟随重定向）。文件不存在（404）即全部缺省值；其余失败、非 JSON 或字段非法都抛 `shell.error.config`，
 * 外壳据此只渲染配置错误页、不再发任何请求。
 */
export async function loadAppConfig(options: { fetch?: typeof fetch; signal?: AbortSignal } = {}): Promise<AppConfig> {
  let response: Response;
  try {
    response = await (options.fetch ?? globalThis.fetch)(APP_CONFIG_PATH, { credentials: 'same-origin', redirect: 'error', cache: 'no-store', headers: { Accept: 'application/json' }, signal: options.signal });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new TypeError('shell.error.config', { cause: error });
  }
  if (response.status === 404) { await response.body?.cancel(); return parseAppConfig(undefined); }
  if (!response.ok || !(response.headers.get('content-type') ?? '').includes('json')) { await response.body?.cancel(); throw new TypeError('shell.error.config'); }
  let data: unknown;
  try { data = JSON.parse(await response.text()); } catch (error) { throw new TypeError('shell.error.config', { cause: error }); }
  return parseAppConfig(data);
}

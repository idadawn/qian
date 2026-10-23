import type { Locale } from '@idadawn/yue-sdk';

/**
 * `window.__APP_CONF__`（K8s ConfigMap 挂成同源的 `/app-conf.js`，在模块脚本之前以普通脚本加载）的已校验形状。
 * 构建产物不含任何环境值：改 ConfigMap 后刷新即生效，不重编译（S1-YUE-002 验收 4）。
 */
export interface AppConf {
  /** 平台 API 的同源路径前缀（网关路由前缀），缺省 `/api`；不接受跨源地址——BFF Cookie 会话只对同源发送。 */
  apiBase: string;
  /** 依赖方（Identity.Client / 网关 BFF）端点的同源路径前缀，缺省 `/auth`。 */
  authBase: string;
  /** 顶栏品牌文字。 */
  brand?: string;
  /** 缺省语言；用户偏好优先。 */
  locale?: Locale;
  /** 平台代管常驻横幅；缺省按会话判断（平台身份即代管）。 */
  managed?: boolean;
}

const LOCALES: readonly Locale[] = ['zh-CN', 'zh-TW', 'en-US'];

/** 同源路径前缀：以单个 `/` 开头，不含查询串、片段、反斜杠、空白与控制字符，不以 `/` 结尾（根前缀写作空串由调用方拒绝）。 */
export function isPathPrefix(value: unknown): value is string {
  return typeof value === 'string' && value.length > 1 && value.length <= 256 && value.startsWith('/') && !value.startsWith('//') && !value.endsWith('/')
    && !/[\\?#\s]/.test(value) && ![...value].some(character => character.charCodeAt(0) <= 32 || character.charCodeAt(0) === 127);
}

/** 读取并校验运行时配置；缺省来源是 `window.__APP_CONF__`，缺失即全部缺省值，字段非法抛 `shell.error.config`。 */
export function readAppConf(source: unknown = typeof window === 'undefined' ? undefined : (window as Window & { __APP_CONF__?: unknown }).__APP_CONF__): AppConf {
  if (source === undefined || source === null) return { apiBase: '/api', authBase: '/auth' };
  if (typeof source !== 'object' || Array.isArray(source)) throw new TypeError('shell.error.config');
  const raw = source as Record<string, unknown>;
  const conf: AppConf = { apiBase: '/api', authBase: '/auth' };
  for (const key of ['apiBase', 'authBase'] as const) {
    if (raw[key] === undefined) continue;
    if (!isPathPrefix(raw[key])) throw new TypeError('shell.error.config');
    conf[key] = raw[key];
  }
  if (raw.brand !== undefined) {
    if (typeof raw.brand !== 'string' || raw.brand.length === 0 || raw.brand.length > 64 || [...raw.brand].some(character => character.charCodeAt(0) < 32)) throw new TypeError('shell.error.config');
    conf.brand = raw.brand;
  }
  if (raw.locale !== undefined) {
    if (!LOCALES.includes(raw.locale as Locale)) throw new TypeError('shell.error.config');
    conf.locale = raw.locale as Locale;
  }
  if (raw.managed !== undefined) {
    if (typeof raw.managed !== 'boolean') throw new TypeError('shell.error.config');
    conf.managed = raw.managed;
  }
  return conf;
}

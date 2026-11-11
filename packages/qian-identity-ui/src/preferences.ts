import type { Locale } from '@idadawn/yue-sdk';
import { initPreferences } from '@idadawn/yue-shell';
import { i18n } from './i18n';

/** 认证页的主题与语言偏好（本源 localStorage，只存这两项）；没选过语言时沿用浏览器首选。 */
export const preferences = initPreferences({ namespace: 'qian-identity', defaults: { locale: i18n.instance.language as Locale } });

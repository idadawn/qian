import { AlertDialog, AlertDialogContent, AlertDialogDescription, AlertDialogTitle, Button } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale } from './layout';

/**
 * 会话过期对话框：不可被 Esc 或点遮罩关掉，只能重新登录。单独成模块（子路径 `@idadawn/yue-shell/session-dialog`），
 * 外壳可以只在会话结束时按需加载，不进首屏。
 */
export function SessionExpiredDialog({ open, i18n, onSignIn, testId = 'yue-session-expired' }: { open: boolean; i18n: PlatformI18n; onSignIn: () => void; testId?: string }) {
  useShellLocale(i18n);
  return <span data-slot="session-expired" data-testid={`${testId}-root`}><AlertDialog open={open}><AlertDialogContent showCloseButton={false} testId={testId}>
    <AlertDialogTitle>{i18n.t('shell.expired')}</AlertDialogTitle><AlertDialogDescription>{i18n.t('shell.expiredHelp')}</AlertDialogDescription><Button onClick={onSignIn}>{i18n.t('shell.reauthenticate')}</Button>
  </AlertDialogContent></AlertDialog></span>;
}

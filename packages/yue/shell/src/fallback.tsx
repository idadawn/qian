import { Component, useSyncExternalStore, type ErrorInfo, type ReactNode } from 'react';
import { Alert, Button, cn, Icon } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale, type ShellPartProps } from './layout';

export type FallbackStatus = 403 | 404 | 500 | 'offline';
export interface FallbackPageProps extends ShellPartProps { status: FallbackStatus; i18n: PlatformI18n; onRetry?: () => void; onHome?: () => void }
const keys = { 403: 'forbidden', 404: 'notFound', 500: 'serverError', offline: 'offline' } as const;
export function FallbackPage({ status, i18n, onRetry, onHome, className, testId = 'yue-fallback-page', children }: FallbackPageProps) {
  useShellLocale(i18n);
  const key = keys[status];
  return <section data-slot="fallback-page" data-testid={testId} className={cn('flex min-h-80 flex-col items-center justify-center gap-4 rounded-lg border border-border bg-card p-8 text-center', className)}>
    <Icon name={status === 404 ? 'info' : 'alert'} size={36} />
    {typeof status === 'number' && <p aria-hidden className="text-4xl font-semibold text-muted-foreground">{status}</p>}
    <h1 className="text-xl font-semibold">{i18n.t(`shell.${key}`)}</h1><p className="max-w-md text-muted-foreground">{i18n.t(`shell.${key}Help`)}</p>
    <div className="flex flex-wrap justify-center gap-2">{onHome && <Button variant="outline" onClick={onHome}>{i18n.t('shell.home')}</Button>}{onRetry && <Button onClick={onRetry}>{i18n.t('common.retry')}</Button>}</div>{children}
  </section>;
}
function subscribeOnline(listener: () => void) { window.addEventListener('online', listener); window.addEventListener('offline', listener); return () => { window.removeEventListener('online', listener); window.removeEventListener('offline', listener); }; }
export function OfflineBanner({ i18n, offline, onRetry, className, testId = 'yue-offline-banner' }: ShellPartProps & { i18n: PlatformI18n; offline?: boolean; onRetry?: () => void }) {
  useShellLocale(i18n);
  const online = useSyncExternalStore(subscribeOnline, () => navigator.onLine, () => true);
  if (!(offline ?? !online)) return null;
  return <div data-slot="offline-banner" data-testid={testId}><Alert tone="warn" className={cn('flex flex-wrap items-center justify-between gap-3', className)}>{i18n.t('shell.offline')}{onRetry && <Button variant="outline" onClick={onRetry}>{i18n.t('common.retry')}</Button>}</Alert></div>;
}
export function UpdateBanner({ i18n, available, onReload, className, testId = 'yue-update-banner' }: ShellPartProps & { i18n: PlatformI18n; available: boolean; onReload: () => void }) {
  useShellLocale(i18n);
  if (!available) return null;
  return <div data-slot="update-banner" data-testid={testId}><Alert className={cn('flex flex-wrap items-center justify-between gap-3', className)}>{i18n.t('shell.update')}<Button variant="outline" onClick={onReload}>{i18n.t('shell.reload')}</Button></Alert></div>;
}
export interface ShellErrorBoundaryProps { i18n: PlatformI18n; children: ReactNode; resetKey?: unknown; onError?: (error: Error, info: ErrorInfo) => void; onReset?: () => void }
export class ShellErrorBoundary extends Component<ShellErrorBoundaryProps, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error, info: ErrorInfo) { this.props.onError?.(error, info); }
  componentDidUpdate(previous: ShellErrorBoundaryProps) { if (this.state.failed && previous.resetKey !== this.props.resetKey) this.setState({ failed: false }); }
  render() { return this.state.failed ? <FallbackPage i18n={this.props.i18n} status={500} onRetry={() => { this.props.onReset?.(); this.setState({ failed: false }); }} /> : this.props.children; }
}

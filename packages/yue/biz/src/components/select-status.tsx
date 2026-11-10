import { Button } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';

export interface SelectStatusProps {
  slot: string;
  testId: string;
  i18n: PlatformI18n;
  loading?: boolean | undefined;
  /** 选择器在弹层里已能显示加载态时传 false，避免触发器旁边再闪一次。 */
  showLoading?: boolean | undefined;
  error?: unknown;
  onRetry?: (() => void) | undefined;
  notice?: string | undefined;
}

/** 选择器触发器旁的状态行：错误给出可键盘到达的重试按钮，加载与提示只是说明文字。 */
export function SelectStatus({ slot, testId, i18n, loading, showLoading = true, error, onRetry, notice }: SelectStatusProps) {
  if (error) {
    return <span role="alert" data-slot={`${slot}-error`} data-testid={`${testId}-error`} className="inline-flex items-center gap-2 text-sm text-destructive">
      {i18n.t('biz.error')}
      {onRetry && <Button variant="outline" size="sm" onClick={onRetry}>{i18n.t('biz.retry')}</Button>}
    </span>;
  }
  if (loading && showLoading) return <span role="status" data-slot={`${slot}-loading`} data-testid={`${testId}-loading`} className="text-sm text-muted-foreground">{i18n.t('biz.loading')}</span>;
  if (notice) return <span data-slot={`${slot}-notice`} data-testid={`${testId}-notice`} className="text-xs text-muted-foreground">{notice}</span>;
  return null;
}

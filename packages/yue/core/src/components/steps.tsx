import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { Icon } from './icon';

export type StepStatus = 'done' | 'current' | 'failed' | 'todo';
export interface StepItem { key: string; title: ReactNode; description?: ReactNode; status: StepStatus }
export type StepsProps = Omit<YueProps<ComponentProps<'ol'>>, 'children'> & {
  items: readonly StepItem[];
  /** 列表的可访问名称，例如「开通进度」。 */
  label: string;
  /** 每种状态读给辅助技术的文字，由宿主本地化。 */
  statusLabels: Readonly<Record<StepStatus, string>>;
  /** @default 'horizontal' */
  orientation?: 'horizontal' | 'vertical';
};
const markers: Record<StepStatus, string> = {
  done: 'bg-primary text-primary-foreground',
  current: 'border-2 border-primary bg-primary/10 text-primary',
  failed: 'bg-destructive text-destructive-foreground',
  todo: 'border-2 border-border bg-background text-muted-foreground',
};
/** 步骤条：只展示进度，不承担导航；当前步骤带 aria-current="step"，状态以文字读出，不只靠颜色。 */
export function Steps({ items, label, statusLabels, orientation = 'horizontal', className, testId, ...props }: StepsProps) {
  const vertical = orientation === 'vertical';
  return <ol {...props} aria-label={label} data-slot="steps" data-orientation={orientation} data-testid={testId ?? 'yue-steps'} className={cn('m-0 flex list-none p-0', vertical ? 'flex-col' : 'flex-row', className)}>
    {items.map((item, index) => {
      const last = index === items.length - 1;
      return <li key={item.key} aria-current={item.status === 'current' ? 'step' : undefined} data-slot="steps-item" data-status={item.status} data-testid={`${testId ?? 'yue-steps'}-item`}
        className={cn('flex min-w-0', vertical ? 'gap-3' : 'flex-1 flex-col gap-2.5')}>
        <span className={cn('flex items-center', vertical && 'flex-col self-stretch')}>
          <span data-slot="steps-marker" data-testid={`${testId ?? 'yue-steps'}-marker`} className={cn('inline-flex size-7 shrink-0 items-center justify-center rounded-full text-[13px] font-bold', markers[item.status])}>
            {item.status === 'done' ? <Icon name="check" /> : item.status === 'failed' ? <Icon name="close" /> : index + 1}
          </span>
          {!last && <span aria-hidden="true" data-slot="steps-connector" data-testid={`${testId ?? 'yue-steps'}-connector`} className={cn(item.status === 'done' ? 'bg-primary' : 'bg-border', vertical ? 'my-1.5 w-0.5 min-h-6 flex-1' : 'mx-2 h-0.5 flex-1')} />}
        </span>
        <span className={cn('flex min-w-0 flex-col', vertical ? 'pb-5' : 'pe-2')}>
          <span className={cn('font-semibold', item.status === 'todo' ? 'text-muted-foreground' : 'text-foreground')}>{item.title}<span className="sr-only">（{statusLabels[item.status]}）</span></span>
          {item.description && <span className="text-xs leading-[18px] text-muted-foreground">{item.description}</span>}
        </span>
      </li>;
    })}
  </ol>;
}

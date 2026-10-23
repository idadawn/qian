import { Badge, cn, Icon, Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@idadawn/yue-core/shell';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { useShellLocale, type ShellPartProps } from './layout';

/** 顶栏站内信条目；消息来源由宿主注入（S1 没有站内信后端接口，外壳只提供弹窗）。 */
export interface InboxMessage { id: string; title: string; body?: string; time?: string; unread?: boolean }
export interface InboxPopoverProps extends ShellPartProps { i18n: PlatformI18n; messages: readonly InboxMessage[]; onOpen?: (message: InboxMessage) => void; onOpenChange?: (open: boolean) => void }

export function InboxPopover({ i18n, messages, onOpen, onOpenChange, className, testId = 'yue-inbox' }: InboxPopoverProps) {
  useShellLocale(i18n);
  const unread = messages.filter(message => message.unread).length;
  return <Popover onOpenChange={onOpenChange}>
    <PopoverTrigger testId={`${testId}-trigger`} className={cn('relative inline-flex h-9 items-center gap-1 rounded-md px-2 text-sm hover:bg-accent focus-visible:outline-2 focus-visible:outline-ring', className)}>
      <Icon name="info" /><span className="sr-only">{i18n.t('shell.inbox')}</span>{unread > 0 && <Badge tone="info" testId={`${testId}-unread`}>{unread}</Badge>}
    </PopoverTrigger>
    <PopoverContent align="end" testId={`${testId}-content`} className="w-80 space-y-3 p-4">
      <PopoverTitle>{i18n.t('shell.inbox')}</PopoverTitle>
      {messages.length === 0 ? <p className="text-sm text-muted-foreground" data-slot="inbox-empty">{i18n.t('shell.inboxEmpty')}</p>
        : <ul className="max-h-80 space-y-2 overflow-y-auto" data-slot="inbox-list">{messages.map(message => <li key={message.id}>
          <button type="button" className={cn('w-full rounded-md border border-border p-2 text-left text-sm hover:bg-accent focus-visible:outline-2 focus-visible:outline-ring', message.unread && 'border-primary/40')} data-slot="inbox-message" onClick={() => onOpen?.(message)}>
            <span className="block truncate font-medium">{message.title}</span>
            {message.body && <span className="mt-1 block truncate text-muted-foreground">{message.body}</span>}
            {message.time && <time className="mt-1 block text-xs text-muted-foreground">{message.time}</time>}
          </button></li>)}</ul>}
    </PopoverContent>
  </Popover>;
}

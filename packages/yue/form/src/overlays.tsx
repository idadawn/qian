import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactElement, type ReactNode } from 'react';
import { AlertDialog, AlertDialogClose, AlertDialogContent, AlertDialogDescription, AlertDialogTitle, Button, Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, Popover, PopoverClose, PopoverContent, PopoverDescription, PopoverTitle, PopoverTrigger, Sheet, SheetClose, SheetContent, SheetDescription, SheetTitle, ToastList, ToastProvider, useToast } from '@idadawn/yue-core';
import { OverlayFormContext } from './overlay-form-context';
import type { PlatformI18n } from '@idadawn/yue-sdk';

export interface OverlayOptions { title: string; description?: string; content: ReactNode; onClose?: () => void }
export interface OverlayApi { open(options: OverlayOptions): void; close(): void; holder: ReactNode }

/** 三种 API 共享状态与生命周期，焦点、键盘及 ARIA 由 core 原语负责。 */
function useOverlay(kind: 'modal' | 'drawer' | 'popup', i18n: PlatformI18n, trigger?: ReactElement<Record<string, unknown>>, defaultOptions?: OverlayOptions): OverlayApi {
  const [forms, setForms] = useState(0);
  const register = useCallback(() => { setForms(count => count + 1); return () => setForms(count => Math.max(0, count - 1)); }, []);
  const [options, setOptions] = useState<OverlayOptions | null>(null);
  const activeOptions = useRef<OverlayOptions | null>(null);
  const lastFocus = useRef<HTMLElement | null>(null);
  const close = () => { const current = activeOptions.current; activeOptions.current = null; setOptions(null); current?.onClose?.(); lastFocus.current?.focus(); };
  const open = (next: OverlayOptions) => { lastFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null; activeOptions.current = next; setOptions(next); };
  const Root = kind === 'drawer' ? Sheet : kind === 'popup' ? Popover : Dialog;
  const Content = kind === 'drawer' ? SheetContent : kind === 'popup' ? PopoverContent : DialogContent;
  const Title = kind === 'drawer' ? SheetTitle : kind === 'popup' ? PopoverTitle : DialogTitle;
  const Description = kind === 'drawer' ? SheetDescription : kind === 'popup' ? PopoverDescription : DialogDescription;
  const Close = kind === 'drawer' ? SheetClose : kind === 'popup' ? PopoverClose : DialogClose;
  return { open, close, holder: <Root open={!!options} onOpenChange={(value) => { if (!value) close(); else if (defaultOptions) open(defaultOptions); }}>
    {kind === 'popup' && <PopoverTrigger render={trigger} />}
    {options && <Content {...(kind === 'popup' ? {} : { closeLabel: i18n.t('common.close') })} data-slot={`form-${kind}`} testId={`yue-form-${kind}`}>
      <Title>{options.title}</Title>
      {options.description && <Description>{options.description}</Description>}
      <OverlayFormContext value={{ cancel: <Close render={<Button variant="outline" type="button" />}>{i18n.t('common.cancel')}</Close>, register }}><div className="my-4">{options.content}</div></OverlayFormContext>
      {forms === 0 && <div className="flex justify-end gap-2"><Close render={<Button variant="outline" />}>{i18n.t('common.close')}</Close></div>}
    </Content>}
  </Root> };
}
export function useModal(i18n: PlatformI18n): OverlayApi { return useOverlay('modal', i18n); }
export function useDrawer(i18n: PlatformI18n): OverlayApi { return useOverlay('drawer', i18n); }
export function usePopup(i18n: PlatformI18n, trigger: ReactElement<Record<string, unknown>>, options?: OverlayOptions): OverlayApi { return useOverlay('popup', i18n, trigger, options); }

export interface ConfirmOptions { title: string; description?: string }
export interface MessageApi { info(key: string): void; success(key: string): void; error(key: string): void; notification(key: string): void }
interface Feedback { message: MessageApi; confirm(options: ConfirmOptions): Promise<boolean> }
const FeedbackContext = createContext<Feedback | null>(null);

function FeedbackContent({ i18n, children }: { i18n: PlatformI18n; children: ReactNode }) {
  const manager = useToast();
  const pending = useRef<((result: boolean) => void) | null>(null);
  const [confirmation, setConfirmation] = useState<ConfirmOptions | null>(null);
  const finish = (result: boolean) => { pending.current?.(result); pending.current = null; setConfirmation(null); };
  useEffect(() => () => { pending.current?.(false); }, []);
  const show = (key: string, type: string) => { manager.add({ title: i18n.t(key), type }); };
  const value: Feedback = {
    message: { info: (key) => show(key, 'info'), success: (key) => show(key, 'success'), error: (key) => show(key, 'error'), notification: (key) => show(key, 'notification') },
    confirm(options) {
      // 只允许一个确认框；替换时旧调用明确返回 false。
      pending.current?.(false);
      setConfirmation(options);
      return new Promise<boolean>((resolve) => { pending.current = resolve; });
    },
  };
  return <FeedbackContext.Provider value={value}>{children}<ToastList closeLabel={i18n.t('common.close')} />
    <AlertDialog open={!!confirmation} onOpenChange={(open) => { if (!open) finish(false); }}>
      {confirmation && <AlertDialogContent closeLabel={i18n.t('common.close')} testId="yue-form-confirm"><AlertDialogTitle>{confirmation.title}</AlertDialogTitle>
        {confirmation.description && <AlertDialogDescription>{confirmation.description}</AlertDialogDescription>}
        <div className="mt-4 flex justify-end gap-2"><AlertDialogClose render={<Button variant="outline" />}>{i18n.t('common.cancel')}</AlertDialogClose><Button onClick={() => finish(true)}>{i18n.t('common.confirm')}</Button></div>
      </AlertDialogContent>}
    </AlertDialog>
  </FeedbackContext.Provider>;
}
export function FeedbackProvider({ i18n, children }: { i18n: PlatformI18n; children: ReactNode }) {
  return <ToastProvider><FeedbackContent i18n={i18n}>{children}</FeedbackContent></ToastProvider>;
}
export function useMessage(): MessageApi {
  const context = useContext(FeedbackContext);
  if (!context) throw new Error('form.error.feedbackProvider');
  return context.message;
}
export function useConfirm(): Feedback['confirm'] {
  const context = useContext(FeedbackContext);
  if (!context) throw new Error('form.error.feedbackProvider');
  return context.confirm;
}

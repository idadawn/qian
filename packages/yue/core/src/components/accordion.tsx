import { Accordion as BaseAccordion } from '@base-ui/react/accordion';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { button } from '../lib/styles';

export type AccordionProps = YueProps<ComponentProps<typeof BaseAccordion.Root>>;
export function Accordion({ className, testId, ...props }: AccordionProps) {
  return <BaseAccordion.Root {...props} data-slot="accordion" data-testid={testId ?? 'yue-accordion'} className={cn('flex flex-col', className)} />;
}

export type AccordionItemProps = YueProps<ComponentProps<typeof BaseAccordion.Item>>;
export function AccordionItem({ className, testId, ...props }: AccordionItemProps) {
  return <BaseAccordion.Item {...props} data-slot="accordion-item" data-testid={testId ?? 'yue-accordion-item'} className={cn('border-b border-[var(--border)]', className)} />;
}

export type AccordionHeaderProps = YueProps<ComponentProps<typeof BaseAccordion.Header>>;
export function AccordionHeader({ className, testId, ...props }: AccordionHeaderProps) {
  return <BaseAccordion.Header {...props} data-slot="accordion-header" data-testid={testId ?? 'yue-accordion-header'} className={cn('flex', className)} />;
}

export type AccordionTriggerProps = YueProps<ComponentProps<typeof BaseAccordion.Trigger>>;
export function AccordionTrigger({ className, testId, ...props }: AccordionTriggerProps) {
  return <BaseAccordion.Trigger {...props} data-slot="accordion-trigger" data-testid={testId ?? 'yue-accordion-trigger'} className={cn(button + ' w-full justify-between', className)} />;
}

export type AccordionContentProps = YueProps<ComponentProps<typeof BaseAccordion.Panel>>;
export function AccordionContent({ className, testId, ...props }: AccordionContentProps) {
  return <BaseAccordion.Panel {...props} data-slot="accordion-content" data-testid={testId ?? 'yue-accordion-content'} className={cn('overflow-hidden py-3 text-sm', className)} />;
}

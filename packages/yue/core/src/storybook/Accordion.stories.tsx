import preview from './preview';
import { AccordionDemo } from '../test/catalog-demos';
import { Accordion } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Accordion', component: AccordionDemo, subcomponents: { Accordion }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '如何使用' }));
    await expect(await within(document.body).findByText('组件可以通过 props 配置。')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

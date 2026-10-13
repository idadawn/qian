import preview from './preview';
import { PopoverDemo } from '../test/catalog-demos';
import { Popover } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Popover', component: PopoverDemo, subcomponents: { Popover }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开浮层' }));
    await expect(await within(document.body).findByRole('dialog')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

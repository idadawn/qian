import preview from './preview';
import { DialogDemo } from '../test/catalog-demos';
import { Dialog } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Dialog', component: DialogDemo, subcomponents: { Dialog }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开对话框' }));
    await expect(await within(document.body).findByRole('dialog')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

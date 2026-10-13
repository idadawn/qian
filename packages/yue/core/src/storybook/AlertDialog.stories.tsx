import preview from './preview';
import { AlertDialogDemo } from '../test/catalog-demos';
import { AlertDialog } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/AlertDialog', component: AlertDialogDemo, subcomponents: { AlertDialog }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开确认框' }));
    await expect(await within(document.body).findByRole('alertdialog')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

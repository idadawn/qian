import preview from './preview';
import { SheetDemo } from '../test/catalog-demos';
import { Sheet } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Sheet', component: SheetDemo, subcomponents: { Sheet }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开侧栏' }));
    await expect(await within(document.body).findByRole('dialog')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

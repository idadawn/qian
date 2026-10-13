import preview from './preview';
import { DrawerDemo } from '../test/catalog-demos';
import { Drawer } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Drawer', component: DrawerDemo, subcomponents: { Drawer }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开抽屉' }));
    await expect(await within(document.body).findByRole('dialog')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

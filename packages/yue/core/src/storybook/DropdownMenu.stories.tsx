import preview from './preview';
import { DropdownMenuDemo } from '../test/catalog-demos';
import { DropdownMenu } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/DropdownMenu', component: DropdownMenuDemo, subcomponents: { DropdownMenu }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '打开菜单' }));
    await expect(await within(document.body).findByRole('menu')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

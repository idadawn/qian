import preview from './preview';
import { TooltipDemo } from '../test/catalog-demos';
import { Tooltip } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Tooltip', component: TooltipDemo, subcomponents: { Tooltip }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.hover(canvas.getByRole('button', { name: '查看提示' }));
    await expect(await within(document.body).findByText('保存当前更改')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

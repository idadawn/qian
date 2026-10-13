import preview from './preview';
import { HoverCardDemo } from '../test/catalog-demos';
import { HoverCard } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/HoverCard', component: HoverCardDemo, subcomponents: { HoverCard }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.hover(canvas.getByRole('link', { name: '查看用户' }));
    await expect(await within(document.body).findByText('示例用户')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

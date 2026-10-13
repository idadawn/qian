import preview from './preview';
import { NotificationDemo } from '../test/catalog-demos';
import { Notification } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Notification', component: NotificationDemo, subcomponents: { Notification }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '显示消息' }));
    await expect(await within(document.body).findByText('任务已完成')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

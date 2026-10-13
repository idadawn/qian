import preview from './preview';
import { ToastDemo } from '../test/catalog-demos';
import { Toast } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Toast', component: ToastDemo, subcomponents: { Toast }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '显示消息' }));
    await expect(await within(document.body).findByText('保存成功')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

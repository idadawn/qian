import preview from './preview';
import { SelectDemo } from '../test/catalog-demos';
import { Select } from '../index';
import { expect, within, userEvent } from 'storybook/test';

const meta = preview.meta({ title: 'Core/Select', component: SelectDemo, subcomponents: { Select }, tags: ['autodocs'] });
export const Default = meta.story({
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('combobox', { name: '城市' }));
    await expect(await within(document.body).findByRole('listbox')).toBeVisible();
  },
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

import { userEvent, within, expect } from 'storybook/test';
import preview from './preview';
import { ComboboxDemo } from '../test/catalog-demos';
import { Combobox } from '../index';

const meta = preview.meta({ title: 'Core/Combobox', component: ComboboxDemo, subcomponents: { Combobox }, tags: ['autodocs'] });
export const Default = meta.story({ play: async ({ canvasElement }) => { await userEvent.click(within(canvasElement).getByTestId('yue-combobox-trigger')); await expect(await within(document.body).findByRole('listbox')).toBeVisible(); } });
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

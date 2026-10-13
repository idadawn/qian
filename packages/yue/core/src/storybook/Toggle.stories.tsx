import { userEvent, within } from 'storybook/test';
import preview from './preview';
import { ToggleDemo } from '../test/catalog-demos';
import { Toggle } from '../index';

const meta = preview.meta({ title: 'Core/Toggle', component: ToggleDemo, subcomponents: { Toggle }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });
export const Hover = meta.story({ play: async ({ canvasElement }) => { await userEvent.hover(within(canvasElement).getByRole('button', { name: '加粗' })); } });

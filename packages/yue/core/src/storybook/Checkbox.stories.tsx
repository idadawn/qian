import preview from './preview';
import { CheckboxDemo } from '../test/catalog-demos';
import { Checkbox } from '../index';

const meta = preview.meta({ title: 'Core/Checkbox', component: CheckboxDemo, subcomponents: { Checkbox }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

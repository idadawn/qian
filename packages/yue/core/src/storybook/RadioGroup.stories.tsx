import preview from './preview';
import { RadioGroupDemo } from '../test/catalog-demos';
import { RadioGroup } from '../index';

const meta = preview.meta({ title: 'Core/RadioGroup', component: RadioGroupDemo, subcomponents: { RadioGroup }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

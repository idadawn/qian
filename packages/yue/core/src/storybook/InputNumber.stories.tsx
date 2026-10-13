import preview from './preview';
import { InputNumberDemo } from '../test/catalog-demos';
import { InputNumber } from '../index';

const meta = preview.meta({ title: 'Core/InputNumber', component: InputNumberDemo, subcomponents: { InputNumber }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

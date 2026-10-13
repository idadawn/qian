import preview from './preview';
import { FieldDemo } from '../test/catalog-demos';
import { Field } from '../index';

const meta = preview.meta({ title: 'Core/Field', component: FieldDemo, subcomponents: { Field }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Error = meta.story({ args: { invalid: true } });

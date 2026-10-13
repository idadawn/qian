import preview from './preview';
import { InputDemo } from '../test/catalog-demos';
import { Input } from '../index';

const meta = preview.meta({ title: 'Core/Input', component: InputDemo, subcomponents: { Input }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });
export const Error = meta.story({ args: { invalid: true } });

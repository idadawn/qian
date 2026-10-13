import preview from './preview';
import { InputPasswordDemo } from '../test/catalog-demos';
import { InputPassword } from '../index';

const meta = preview.meta({ title: 'Core/InputPassword', component: InputPasswordDemo, subcomponents: { InputPassword }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

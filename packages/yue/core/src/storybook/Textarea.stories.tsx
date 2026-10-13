import preview from './preview';
import { TextareaDemo } from '../test/catalog-demos';
import { Textarea } from '../index';

const meta = preview.meta({ title: 'Core/Textarea', component: TextareaDemo, subcomponents: { Textarea }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });
export const Error = meta.story({ args: { invalid: true } });

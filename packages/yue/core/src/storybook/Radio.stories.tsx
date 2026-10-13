import preview from './preview';
import { RadioDemo } from '../test/catalog-demos';
import { Radio } from '../index';

const meta = preview.meta({ title: 'Core/Radio', component: RadioDemo, subcomponents: { Radio }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

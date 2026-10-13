import preview from './preview';
import { SwitchDemo } from '../test/catalog-demos';
import { Switch } from '../index';

const meta = preview.meta({ title: 'Core/Switch', component: SwitchDemo, subcomponents: { Switch }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

import preview from './preview';
import { PinInputDemo } from '../test/catalog-demos';
import { PinInput } from '../index';

const meta = preview.meta({ title: 'Core/PinInput', component: PinInputDemo, subcomponents: { PinInput }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

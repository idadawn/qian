import preview from './preview';
import { SeparatorDemo } from '../test/catalog-demos';
import { Separator } from '../index';

const meta = preview.meta({ title: 'Core/Separator', component: SeparatorDemo, subcomponents: { Separator }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

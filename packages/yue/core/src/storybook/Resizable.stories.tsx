import preview from './preview';
import { ResizableDemo } from '../test/catalog-demos';
import { Resizable } from '../index';

const meta = preview.meta({ title: 'Core/Resizable', component: ResizableDemo, subcomponents: { Resizable }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

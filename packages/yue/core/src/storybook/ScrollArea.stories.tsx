import preview from './preview';
import { ScrollAreaDemo } from '../test/catalog-demos';
import { ScrollArea } from '../index';

const meta = preview.meta({ title: 'Core/ScrollArea', component: ScrollAreaDemo, subcomponents: { ScrollArea }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

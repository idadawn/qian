import preview from './preview';
import { ContextMenuDemo } from '../test/catalog-demos';
import { ContextMenu } from '../index';

const meta = preview.meta({ title: 'Core/ContextMenu', component: ContextMenuDemo, subcomponents: { ContextMenu }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

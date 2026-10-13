import preview from './preview';
import { TagDemo } from '../test/catalog-demos';
import { Tag } from '../index';

const meta = preview.meta({ title: 'Core/Tag', component: TagDemo, subcomponents: { Tag }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

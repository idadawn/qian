import preview from './preview';
import { EmptyDemo } from '../test/catalog-demos';
import { Empty } from '../index';

const meta = preview.meta({ title: 'Core/Empty', component: EmptyDemo, subcomponents: { Empty }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

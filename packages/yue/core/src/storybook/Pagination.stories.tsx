import preview from './preview';
import { PaginationDemo } from '../test/catalog-demos';
import { Pagination } from '../index';

const meta = preview.meta({ title: 'Core/Pagination', component: PaginationDemo, subcomponents: { Pagination }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

import preview from './preview';
import { BreadcrumbDemo } from '../test/catalog-demos';
import { Breadcrumb } from '../index';

const meta = preview.meta({ title: 'Core/Breadcrumb', component: BreadcrumbDemo, subcomponents: { Breadcrumb }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

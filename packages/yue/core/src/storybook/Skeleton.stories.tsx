import preview from './preview';
import { SkeletonDemo } from '../test/catalog-demos';
import { Skeleton } from '../index';

const meta = preview.meta({ title: 'Core/Skeleton', component: SkeletonDemo, subcomponents: { Skeleton }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

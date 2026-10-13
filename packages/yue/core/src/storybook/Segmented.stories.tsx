import preview from './preview';
import { SegmentedDemo } from '../test/catalog-demos';
import { Segmented } from '../index';

const meta = preview.meta({ title: 'Core/Segmented', component: SegmentedDemo, subcomponents: { Segmented }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });
export const Disabled = meta.story({ args: { disabled: true } });

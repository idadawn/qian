import preview from './preview';
import { ProgressDemo } from '../test/catalog-demos';
import { Progress } from '../index';

const meta = preview.meta({ title: 'Core/Progress', component: ProgressDemo, subcomponents: { Progress }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

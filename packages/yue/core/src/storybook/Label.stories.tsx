import preview from './preview';
import { LabelDemo } from '../test/catalog-demos';
import { Label } from '../index';

const meta = preview.meta({ title: 'Core/Label', component: LabelDemo, subcomponents: { Label }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

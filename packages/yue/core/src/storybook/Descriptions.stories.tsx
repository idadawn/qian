import preview from './preview';
import { DescriptionsDemo } from '../test/catalog-demos';
import { Descriptions } from '../index';

const meta = preview.meta({ title: 'Core/Descriptions', component: DescriptionsDemo, subcomponents: { Descriptions }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

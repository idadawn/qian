import preview from './preview';
import { IconDemo } from '../test/catalog-demos';
import { Icon } from '../index';

const meta = preview.meta({ title: 'Core/Icon', component: IconDemo, subcomponents: { Icon }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

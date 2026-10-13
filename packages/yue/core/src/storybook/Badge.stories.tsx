import preview from './preview';
import { BadgeDemo } from '../test/catalog-demos';
import { Badge } from '../index';

const meta = preview.meta({ title: 'Core/Badge', component: BadgeDemo, subcomponents: { Badge }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

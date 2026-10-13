import preview from './preview';
import { CardDemo } from '../test/catalog-demos';
import { Card } from '../index';

const meta = preview.meta({ title: 'Core/Card', component: CardDemo, subcomponents: { Card }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

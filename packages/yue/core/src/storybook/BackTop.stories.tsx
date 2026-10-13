import preview from './preview';
import { BackTopDemo } from '../test/catalog-demos';
import { BackTop } from '../index';

const meta = preview.meta({ title: 'Core/BackTop', component: BackTopDemo, subcomponents: { BackTop }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

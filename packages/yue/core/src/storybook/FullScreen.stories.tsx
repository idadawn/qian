import preview from './preview';
import { FullScreenDemo } from '../test/catalog-demos';
import { FullScreen } from '../index';

const meta = preview.meta({ title: 'Core/FullScreen', component: FullScreenDemo, subcomponents: { FullScreen }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

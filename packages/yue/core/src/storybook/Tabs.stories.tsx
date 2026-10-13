import preview from './preview';
import { TabsDemo } from '../test/catalog-demos';
import { Tabs } from '../index';

const meta = preview.meta({ title: 'Core/Tabs', component: TabsDemo, subcomponents: { Tabs }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

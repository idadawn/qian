import preview from './preview';
import { AlertDemo } from '../test/catalog-demos';
import { Alert } from '../index';

const meta = preview.meta({ title: 'Core/Alert', component: AlertDemo, subcomponents: { Alert }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

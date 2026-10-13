import preview from './preview';
import { SpinnerDemo } from '../test/catalog-demos';
import { Spinner } from '../index';

const meta = preview.meta({ title: 'Core/Spinner', component: SpinnerDemo, subcomponents: { Spinner }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

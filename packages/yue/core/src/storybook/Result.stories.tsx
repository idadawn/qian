import preview from './preview';
import { ResultDemo } from '../test/catalog-demos';
import { Result } from '../index';

const meta = preview.meta({ title: 'Core/Result', component: ResultDemo, subcomponents: { Result }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

import preview from './preview';
import { ButtonGroupDemo } from '../test/catalog-demos';
import { ButtonGroup } from '../index';

const meta = preview.meta({ title: 'Core/ButtonGroup', component: ButtonGroupDemo, subcomponents: { ButtonGroup }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

import preview from './preview';
import { AvatarDemo } from '../test/catalog-demos';
import { Avatar } from '../index';

const meta = preview.meta({ title: 'Core/Avatar', component: AvatarDemo, subcomponents: { Avatar }, tags: ['autodocs'] });
export const Default = meta.story({
});
export const Dark = Default.extend({ globals: { theme: 'dark' } });

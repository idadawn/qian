import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import {
  Combobox,
  ComboboxContent,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  Menu,
  MenuContent,
  MenuItem,
  MenuTrigger,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  Tabs,
  TabsList,
  TabsPanel,
  TabsTab,
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from './index';

// SSR 兼容（docs/prd/specs/yue.md §4.4 验证项）：五个原语在 Node 里 renderToString 不抛错，触发器带 data-slot / data-testid。
describe('SSR', () => {
  it('Menu / Select / Combobox / Tabs / Tooltip 可在 Node 里渲染', () => {
    const html = renderToString(
      <TooltipProvider>
        <Menu>
          <MenuTrigger>操作</MenuTrigger>
          <MenuContent>
            <MenuItem>新建</MenuItem>
          </MenuContent>
        </Menu>
        <Select items={[{ label: '苹果', value: 'apple' }]} defaultValue="apple">
          <SelectTrigger aria-label="水果" />
          <SelectContent listLabel="可选水果">
            <SelectItem value="apple">苹果</SelectItem>
          </SelectContent>
        </Select>
        <Combobox items={['苹果']}>
          <ComboboxInput aria-label="筛选" />
          <ComboboxContent>
            <ComboboxList>{(item: string) => <ComboboxItem key={item} value={item}>{item}</ComboboxItem>}</ComboboxList>
          </ComboboxContent>
        </Combobox>
        <Tabs defaultValue="a">
          <TabsList>
            <TabsTab value="a">A</TabsTab>
          </TabsList>
          <TabsPanel value="a">内容</TabsPanel>
        </Tabs>
        <Tooltip>
          <TooltipTrigger>保存</TooltipTrigger>
          <TooltipContent>提示</TooltipContent>
        </Tooltip>
      </TooltipProvider>,
    );
    for (const id of ['yue-menu-trigger', 'yue-select-trigger', 'yue-combobox-input', 'yue-tabs-tab', 'yue-tabs-panel', 'yue-tooltip-trigger']) {
      expect(html, id).toContain(`data-testid="${id}"`);
    }
    expect(html).toContain('data-slot="menu-trigger"');
    expect(html).toContain('苹果');
    expect(html).toContain('role="tab"');
  });
});

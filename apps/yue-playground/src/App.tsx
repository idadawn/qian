import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxTrigger,
  ComboboxValue,
  DirectionProvider,
  Menu,
  MenuContent,
  MenuItem,
  MenuSeparator,
  MenuSub,
  MenuSubTrigger,
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
  type TextDirection,
} from '@idadawn/yue-core';
import { Tree, type TreeNode } from '@idadawn/yue-data';
import { useState } from 'react';

// Base UI 验证门靶场（docs/prd/specs/yue.md §4.4）：六个原语各一节，Playwright + axe 与 Lighthouse 对本页跑。
// 文案是演示数据，不走 i18n（i18n 约定随 @idadawn/yue-sdk 落地）。

const FRUITS = ['苹果', '香蕉', '樱桃', '葡萄', '橙子'];

const TREE: TreeNode[] = [
  {
    id: 'hq',
    label: '总部',
    children: [
      { id: 'rd', label: '研发部', children: [{ id: 'fe', label: '前端组' }, { id: 'be', label: '后端组' }] },
      { id: 'sales', label: '销售部' },
    ],
  },
  { id: 'branch', label: '分公司', children: [{ id: 'sh', label: '上海' }] },
];

function Section({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
  return (
    <section id={id} aria-labelledby={`${id}-title`} className="flex flex-col gap-3 rounded-lg border border-neutral-200 p-4">
      <h2 id={`${id}-title`} className="text-base font-semibold">
        {title}
      </h2>
      {children}
    </section>
  );
}

export function App() {
  const [dir, setDir] = useState<TextDirection>('ltr');
  const [fruit, setFruit] = useState<string | null>(null);
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [lastAction, setLastAction] = useState('');

  return (
    <DirectionProvider direction={dir}>
      <main dir={dir} className="mx-auto flex max-w-3xl flex-col gap-6 p-6 text-neutral-900">
        <a href="/core.html" className="text-sm underline">查看完整基础组件目录</a>
        <header className="flex items-center justify-between">
          <h1 className="text-xl font-semibold">yue · Base UI 验证门</h1>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={dir === 'rtl'} onChange={(e) => setDir(e.target.checked ? 'rtl' : 'ltr')} data-testid="gate-rtl" />
            RTL
          </label>
        </header>

        <Section id="menu" title="Menu">
          <Menu>
            <MenuTrigger>操作</MenuTrigger>
            <MenuContent>
              <MenuItem onClick={() => setLastAction('新建')}>新建</MenuItem>
              <MenuItem onClick={() => setLastAction('编辑')}>编辑</MenuItem>
              <MenuSeparator />
              <MenuSub>
                <MenuSubTrigger>更多</MenuSubTrigger>
                <MenuContent>
                  <MenuItem onClick={() => setLastAction('导出')}>导出</MenuItem>
                  <MenuItem onClick={() => setLastAction('归档')}>归档</MenuItem>
                </MenuContent>
              </MenuSub>
              <MenuItem disabled>删除</MenuItem>
            </MenuContent>
          </Menu>
          <p className="text-sm text-neutral-600" data-testid="gate-last-action">
            最近动作：{lastAction || '无'}
          </p>
        </Section>

        <Section id="select" title="Select">
          <Select value={fruit} onValueChange={setFruit} items={FRUITS.map((f) => ({ label: f, value: f }))}>
            <SelectTrigger placeholder="选一种水果" aria-label="水果" />
            <SelectContent>
              {FRUITS.map((f) => (
                <SelectItem key={f} value={f}>
                  {f}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-sm text-neutral-600" data-testid="gate-select-value">
            当前值：{fruit ?? '无'}
          </p>
        </Section>

        <Section id="combobox" title="Combobox">
          <Combobox items={FRUITS}>
            <ComboboxTrigger aria-label="选择水果"><ComboboxValue placeholder="选择水果" /></ComboboxTrigger>
            <ComboboxContent aria-label="水果选项">
              <ComboboxInput placeholder="输入筛选" aria-label="水果筛选" />
              <ComboboxEmpty>没有匹配项</ComboboxEmpty>
              <ComboboxList>{(item: string) => <ComboboxItem key={item} value={item}>{item}</ComboboxItem>}</ComboboxList>
            </ComboboxContent>
          </Combobox>
        </Section>

        <Section id="tabs" title="Tabs">
          <Tabs defaultValue="basic">
            <TabsList aria-label="设置">
              <TabsTab value="basic">基本</TabsTab>
              <TabsTab value="advanced">高级</TabsTab>
              <TabsTab value="danger" disabled>
                危险
              </TabsTab>
            </TabsList>
            <TabsPanel value="basic">基本设置内容</TabsPanel>
            <TabsPanel value="advanced">高级设置内容</TabsPanel>
            <TabsPanel value="danger">危险区域</TabsPanel>
          </Tabs>
        </Section>

        <Section id="tooltip" title="Tooltip">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger>保存</TooltipTrigger>
              <TooltipContent>保存当前更改（Ctrl+S）</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </Section>

        <Section id="tree" title="Tree">
          <Tree nodes={TREE} aria-label="组织" defaultExpandedIds={['hq']} selectedId={selectedNode} onSelectedChange={setSelectedNode} />
          <p className="text-sm text-neutral-600" data-testid="gate-tree-selected">
            选中：{selectedNode ?? '无'}
          </p>
        </Section>
      </main>
    </DirectionProvider>
  );
}

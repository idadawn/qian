import { useId, useState, type ComponentType } from 'react';
import * as C from '../index';

export interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }
export function ButtonDemo({ disabled, rootProps }: DemoProps) { return <C.Button disabled={disabled} {...rootProps}>保存更改</C.Button>; }
export function IconButtonDemo({ disabled, rootProps }: DemoProps) { return <C.IconButton label="关闭" disabled={disabled} {...rootProps}><C.Icon name="close" /></C.IconButton>; }
export function ButtonGroupDemo({ rootProps }: DemoProps) { return <C.ButtonGroup aria-label="编辑操作" {...rootProps}><C.Button>保存</C.Button><C.Button variant="outline">取消</C.Button></C.ButtonGroup>; }
export function InputDemo({ disabled, invalid, rootProps }: DemoProps) { return <C.Input aria-label="名称" disabled={disabled} aria-invalid={invalid} placeholder="请输入名称" {...rootProps} />; }
export function TextareaDemo({ disabled, invalid, rootProps }: DemoProps) { return <C.Textarea aria-label="说明" disabled={disabled} aria-invalid={invalid} placeholder="请输入说明" {...rootProps} />; }
export function InputNumberDemo({ disabled, rootProps }: DemoProps) { return <C.InputNumber label="数量" decreaseLabel="减少数量" increaseLabel="增加数量" defaultValue={2} min={0} max={10} disabled={disabled} {...rootProps} />; }
export function InputPasswordDemo({ disabled, rootProps }: DemoProps) { return <C.InputPassword aria-label="密码" showLabel="显示密码" hideLabel="隐藏密码" defaultValue="test-password" disabled={disabled} {...rootProps} />; }
export function PinInputDemo({ disabled, rootProps }: DemoProps) { return <C.PinInput label="验证码" length={4} getSlotLabel={index => `验证码第 ${index + 1} 位`} disabled={disabled} {...rootProps} />; }
export function CheckboxDemo({ disabled, rootProps }: DemoProps) { return <label className="flex items-center gap-2"><C.Checkbox disabled={disabled} {...rootProps} />允许通知</label>; }
export function RadioDemo({ disabled, rootProps }: DemoProps) { return <C.RadioGroup aria-label="单选项目"><label className="flex items-center gap-2"><C.Radio value="one" disabled={disabled} {...rootProps} />第一项</label></C.RadioGroup>; }
export function RadioGroupDemo({ disabled, rootProps }: DemoProps) { return <C.RadioGroup aria-label="通知频率" defaultValue="daily" disabled={disabled} {...rootProps}><label className="flex items-center gap-2"><C.Radio value="daily" />每天</label><label className="flex items-center gap-2"><C.Radio value="weekly" />每周</label></C.RadioGroup>; }
export function SwitchDemo({ disabled, rootProps }: DemoProps) { return <label className="flex items-center gap-2"><C.Switch disabled={disabled} {...rootProps} />启用提醒</label>; }
export function ToggleDemo({ disabled, rootProps }: DemoProps) { return <C.Toggle aria-label="加粗" disabled={disabled} {...rootProps}>B</C.Toggle>; }
export function SegmentedDemo({ disabled, rootProps }: DemoProps) { return <C.Segmented aria-label="视图" defaultValue="list" disabled={disabled} {...rootProps}><C.SegmentedItem value="list">列表</C.SegmentedItem><C.SegmentedItem value="grid">网格</C.SegmentedItem></C.Segmented>; }
export function SelectDemo({ disabled, rootProps }: DemoProps) { return <C.Select items={[{ label: '北京', value: 'beijing' }, { label: '上海', value: 'shanghai' }]} disabled={disabled} {...rootProps}><C.SelectTrigger aria-label="城市" placeholder="选择城市" /><C.SelectContent><C.SelectItem value="beijing">北京</C.SelectItem><C.SelectItem value="shanghai">上海</C.SelectItem></C.SelectContent></C.Select>; }
export function ComboboxDemo({ disabled, rootProps }: DemoProps) { return <C.Combobox items={['北京', '上海', '杭州']} disabled={disabled} {...rootProps}><C.ComboboxTrigger aria-label="选择城市"><C.ComboboxValue placeholder="选择城市" /></C.ComboboxTrigger><C.ComboboxContent aria-label="城市选项"><C.ComboboxInput aria-label="搜索城市" /><C.ComboboxList>{(city: string) => <C.ComboboxItem value={city} key={city}>{city}</C.ComboboxItem>}</C.ComboboxList></C.ComboboxContent></C.Combobox>; }
export function LabelDemo({ rootProps }: DemoProps) { const id = useId(); return <div className="grid gap-2"><C.Label htmlFor={id} {...rootProps}>名称</C.Label><C.Input id={id} /></div>; }
export function FieldDemo({ disabled, invalid, rootProps }: DemoProps) { return <C.Field name="name" disabled={disabled} invalid={invalid} {...rootProps}><C.FieldLabel>姓名</C.FieldLabel><C.Input required /><C.FieldDescription>使用真实姓名。</C.FieldDescription>{invalid && <C.FieldError match>请输入姓名。</C.FieldError>}</C.Field>; }
export function DialogDemo({ disabled, rootProps }: DemoProps) { return <C.Dialog {...rootProps}><C.DialogTrigger disabled={disabled}>打开对话框</C.DialogTrigger><C.DialogContent><C.DialogTitle>编辑资料</C.DialogTitle><C.DialogDescription>确认信息后保存。</C.DialogDescription><C.Input aria-label="资料名称" /><C.DialogClose>关闭</C.DialogClose></C.DialogContent></C.Dialog>; }
export function AlertDialogDemo({ rootProps }: DemoProps) { return <C.AlertDialog {...rootProps}><C.AlertDialogTrigger>打开确认框</C.AlertDialogTrigger><C.AlertDialogContent><C.AlertDialogTitle>确认操作</C.AlertDialogTitle><C.AlertDialogDescription>请确认是否继续。</C.AlertDialogDescription><C.AlertDialogClose>取消</C.AlertDialogClose><C.AlertDialogClose>继续</C.AlertDialogClose></C.AlertDialogContent></C.AlertDialog>; }
export function SheetDemo({ rootProps }: DemoProps) { return <C.Sheet {...rootProps}><C.SheetTrigger>打开侧栏</C.SheetTrigger><C.SheetContent><C.SheetTitle>筛选条件</C.SheetTitle><C.SheetDescription>设置查询条件。</C.SheetDescription><C.Input aria-label="关键词" /><C.SheetClose>关闭</C.SheetClose></C.SheetContent></C.Sheet>; }
export function DrawerDemo({ rootProps }: DemoProps) { return <C.Drawer {...rootProps}><C.DrawerTrigger>打开抽屉</C.DrawerTrigger><C.DrawerContent><C.DrawerTitle>更多选项</C.DrawerTitle><C.DrawerDescription>选择需要的操作。</C.DrawerDescription><C.DrawerClose>关闭</C.DrawerClose></C.DrawerContent></C.Drawer>; }
export function PopoverDemo({ rootProps }: DemoProps) { return <C.Popover {...rootProps}><C.PopoverTrigger>打开浮层</C.PopoverTrigger><C.PopoverContent><C.PopoverTitle>快捷设置</C.PopoverTitle><C.PopoverDescription>设置立即生效。</C.PopoverDescription><C.PopoverClose>关闭</C.PopoverClose></C.PopoverContent></C.Popover>; }
export function TooltipDemo({ rootProps }: DemoProps) { return <C.TooltipProvider delay={0}><C.Tooltip {...rootProps}><C.TooltipTrigger>查看提示</C.TooltipTrigger><C.TooltipContent>保存当前更改</C.TooltipContent></C.Tooltip></C.TooltipProvider>; }
export function HoverCardDemo({ rootProps }: DemoProps) { return <C.HoverCard {...rootProps}><C.HoverCardTrigger href="#profile">查看用户</C.HoverCardTrigger><C.HoverCardContent aria-label="用户信息"><h3 className="font-semibold">示例用户</h3><p>产品研发团队</p></C.HoverCardContent></C.HoverCard>; }
export function MenuDemo({ rootProps }: DemoProps) { return <C.Menu {...rootProps}><C.MenuTrigger>打开菜单</C.MenuTrigger><C.MenuContent><C.MenuItem>编辑</C.MenuItem><C.MenuItem disabled>归档</C.MenuItem><C.MenuSeparator /><C.MenuItem>删除</C.MenuItem></C.MenuContent></C.Menu>; }
export const DropdownMenuDemo = MenuDemo;
export function ContextMenuDemo({ rootProps }: DemoProps) { return <C.ContextMenu {...rootProps}><C.ContextMenuTrigger className="rounded border p-6">右键或按菜单快捷键</C.ContextMenuTrigger><C.ContextMenuContent><C.ContextMenuItem>复制</C.ContextMenuItem><C.ContextMenuSeparator /><C.ContextMenuItem>粘贴</C.ContextMenuItem></C.ContextMenuContent></C.ContextMenu>; }
export function TabsDemo({ rootProps }: DemoProps) { return <C.Tabs defaultValue="overview" {...rootProps}><C.TabsList aria-label="内容分类"><C.TabsTab value="overview">概览</C.TabsTab><C.TabsTab value="settings">设置</C.TabsTab></C.TabsList><C.TabsPanel value="overview">概览内容</C.TabsPanel><C.TabsPanel value="settings">设置内容</C.TabsPanel></C.Tabs>; }
export function AccordionDemo({ disabled, rootProps }: DemoProps) { return <C.Accordion disabled={disabled} {...rootProps}><C.AccordionItem value="one"><C.AccordionHeader><C.AccordionTrigger>如何使用</C.AccordionTrigger></C.AccordionHeader><C.AccordionContent>组件可以通过 props 配置。</C.AccordionContent></C.AccordionItem><C.AccordionItem value="two"><C.AccordionHeader><C.AccordionTrigger>如何扩展</C.AccordionTrigger></C.AccordionHeader><C.AccordionContent>使用插槽组合组件。</C.AccordionContent></C.AccordionItem></C.Accordion>; }
export function CardDemo({ rootProps }: DemoProps) { return <C.Card {...rootProps}><C.CardHeader><C.CardTitle>项目概览</C.CardTitle><C.CardDescription>项目的基础信息。</C.CardDescription></C.CardHeader><C.CardContent>当前运行正常。</C.CardContent><C.CardFooter><C.Button>查看详情</C.Button></C.CardFooter></C.Card>; }
export function SeparatorDemo({ rootProps }: DemoProps) { return <div className="w-64 space-y-3"><p>第一节</p><C.Separator {...rootProps} /><p>第二节</p></div>; }
export function ScrollAreaDemo({ rootProps }: DemoProps) { return <C.ScrollArea label="滚动列表" className="h-28 w-64" {...rootProps}><div className="space-y-2 p-3">{Array.from({ length: 20 }, (_, index) => <p key={index}>第 {index + 1} 项</p>)}</div></C.ScrollArea>; }
export function ResizableDemo({ rootProps }: DemoProps) { return <C.Resizable className="h-32 w-full min-w-80 max-w-xl rounded border" {...rootProps}><C.ResizablePanel defaultSize="50%" minSize="20%"><div className="p-4">左侧内容</div></C.ResizablePanel><C.ResizableHandle label="调整面板宽度" /><C.ResizablePanel minSize="20%"><div className="p-4">右侧内容</div></C.ResizablePanel></C.Resizable>; }
export function BadgeDemo({ rootProps }: DemoProps) { return <C.Badge tone="ok" {...rootProps}>已完成</C.Badge>; }
export function TagDemo({ rootProps }: DemoProps) { const [visible, setVisible] = useState(true); if (!visible) return null; return <C.Tag tone="info" removeLabel="移除研发标签" onRemove={() => setVisible(false)} {...rootProps}>研发</C.Tag>; }
export function AvatarDemo({ rootProps }: DemoProps) { return <C.Avatar aria-label="示例用户" {...rootProps}><C.AvatarFallback>用户</C.AvatarFallback></C.Avatar>; }
export function SkeletonDemo({ rootProps }: DemoProps) { return <div className="w-64 space-y-2"><C.Skeleton {...rootProps} /><C.Skeleton className="w-2/3" /></div>; }
export function SpinnerDemo({ rootProps }: DemoProps) { return <C.Spinner label="加载中" {...rootProps} />; }
export function ProgressDemo({ rootProps }: DemoProps) { return <C.Progress label="任务进度" value={40} {...rootProps} />; }
export function AlertDemo({ rootProps }: DemoProps) { return <C.Alert tone="info" {...rootProps}>配置已保存，稍后生效。</C.Alert>; }
function ToastButton({ notification = false }: { notification?: boolean }) { const manager = C.useToast(); return <C.Button onClick={() => manager.add({ title: notification ? '任务已完成' : '保存成功', description: '可以继续处理其他任务。', type: notification ? 'notification' : 'info', timeout: 0 })}>显示消息</C.Button>; }
export function ToastDemo() { return <C.ToastProvider><ToastButton /><C.ToastList closeLabel="关闭消息" /></C.ToastProvider>; }
export function NotificationDemo() { return <C.ToastProvider><ToastButton notification /><C.ToastList closeLabel="关闭消息" /></C.ToastProvider>; }
export function EmptyDemo({ rootProps }: DemoProps) { return <C.Empty title="暂无数据" description="创建第一条记录开始使用。" action={<C.Button>新建记录</C.Button>} {...rootProps} />; }
export function ResultDemo({ rootProps }: DemoProps) { return <C.Result title="操作成功" description="更改已经保存。" action={<C.Button>返回列表</C.Button>} {...rootProps} />; }
export function BreadcrumbDemo({ rootProps }: DemoProps) { return <C.Breadcrumb label="当前位置" items={[{ key: 'home', label: '首页', href: '#home' }, { key: 'settings', label: '设置' }]} {...rootProps} />; }
export function PaginationDemo({ rootProps }: DemoProps) { return <C.Pagination label="列表分页" previousLabel="上一页" nextLabel="下一页" getPageLabel={value => `第 ${value} 页`} totalPages={10} {...rootProps} />; }
export function BackTopDemo({ rootProps }: DemoProps) { return <C.BackTop label="返回顶部" visibilityHeight={0} {...rootProps} />; }
export function FullScreenDemo({ rootProps }: DemoProps) { const [error, setError] = useState(false); return <div><C.FullScreen enterLabel="进入全屏" exitLabel="退出全屏" onError={() => setError(true)} {...rootProps} />{error && <p role="alert">当前浏览器无法进入全屏。</p>}</div>; }
export function IconDemo({ rootProps }: DemoProps) { return <C.Icon name="check" label="已完成" size={24} {...rootProps} />; }
export function DescriptionsDemo({ rootProps }: DemoProps) { return <C.Descriptions items={[{ key: 'name', label: '名称', children: '示例项目' }, { key: 'status', label: '状态', children: '运行中' }]} {...rootProps} />; }

export const DEMOS = {
  Button: ButtonDemo, IconButton: IconButtonDemo, ButtonGroup: ButtonGroupDemo, Input: InputDemo, Textarea: TextareaDemo,
  InputNumber: InputNumberDemo, InputPassword: InputPasswordDemo, PinInput: PinInputDemo, Checkbox: CheckboxDemo,
  Radio: RadioDemo, RadioGroup: RadioGroupDemo, Switch: SwitchDemo, Toggle: ToggleDemo, Segmented: SegmentedDemo,
  Select: SelectDemo, Combobox: ComboboxDemo, Label: LabelDemo, Field: FieldDemo, Dialog: DialogDemo,
  AlertDialog: AlertDialogDemo, Sheet: SheetDemo, Drawer: DrawerDemo, Popover: PopoverDemo, Tooltip: TooltipDemo,
  HoverCard: HoverCardDemo, Menu: MenuDemo, DropdownMenu: DropdownMenuDemo, ContextMenu: ContextMenuDemo,
  Tabs: TabsDemo, Accordion: AccordionDemo, Card: CardDemo, Separator: SeparatorDemo, ScrollArea: ScrollAreaDemo,
  Resizable: ResizableDemo, Badge: BadgeDemo, Tag: TagDemo, Avatar: AvatarDemo, Skeleton: SkeletonDemo, Spinner: SpinnerDemo,
  Progress: ProgressDemo, Alert: AlertDemo, Toast: ToastDemo, Notification: NotificationDemo, Empty: EmptyDemo, Result: ResultDemo,
  Breadcrumb: BreadcrumbDemo, Pagination: PaginationDemo, BackTop: BackTopDemo, FullScreen: FullScreenDemo, Icon: IconDemo, Descriptions: DescriptionsDemo,
} satisfies Record<string, ComponentType<DemoProps>>;

import { ArrowRight, ArrowUp, Bell, Building2, Calendar, Check, ChevronDown, ChevronLeft, ChevronRight, ChevronsUpDown, CircleAlert, CircleCheck, CircleHelp, CircleX, ClipboardCheck, Clock, Columns3, Ellipsis, ExternalLink, Eye, EyeOff,
  FileText, Globe, House, Inbox, Info, Key, Languages, LayoutDashboard, LayoutGrid, LoaderCircle, Lock, LogOut, Mail, Maximize, MessageCircle, Minimize, Minus, Monitor, Moon, Network, Package, PanelLeftClose, PanelLeftOpen, Pencil, Plus,
  RefreshCw, Search, Settings, Shield, ShieldCheck, SlidersVertical, Smartphone, Sun, Undo2, User, Users, X } from 'lucide-react';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

const icons = {
  arrowUp: ArrowUp, arrowRight: ArrowRight, check: Check, chevronDown: ChevronDown, chevronLeft: ChevronLeft, chevronRight: ChevronRight, chevronsUpDown: ChevronsUpDown,
  alert: CircleAlert, circleCheck: CircleCheck, circleX: CircleX, help: CircleHelp, eye: Eye, eyeOff: EyeOff, info: Info, spinner: LoaderCircle, maximize: Maximize, minimize: Minimize, minus: Minus, plus: Plus, close: X,
  home: House, dashboard: LayoutDashboard, building: Building2, users: Users, user: User, inbox: Inbox, clipboardCheck: ClipboardCheck, settings: Settings, package: Package, fileText: FileText,
  search: Search, refresh: RefreshCw, more: Ellipsis, columns: Columns3, pencil: Pencil, undo: Undo2, logOut: LogOut, bell: Bell, sun: Sun, moon: Moon, monitor: Monitor, languages: Languages,
  shieldCheck: ShieldCheck, clock: Clock, calendar: Calendar, mail: Mail, phone: Smartphone, externalLink: ExternalLink, panelLeftClose: PanelLeftClose, panelLeftOpen: PanelLeftOpen,
  globe: Globe, key: Key, lock: Lock, message: MessageCircle, network: Network, shield: Shield, sliders: SlidersVertical, layoutGrid: LayoutGrid,
};
export type IconName = keyof typeof icons;
/** 已登记的全部图标名（登记顺序），供图标选择器等列出可选项；与清单 Icon 的 name 枚举一致。 */
export const ICON_NAMES: readonly IconName[] = Object.freeze(Object.keys(icons) as IconName[]);
/** 后端或清单给出的图标名是任意字符串：先用它判断是否已登记，未登记的按调用方的缺省图标显示。 */
export function isIconName(value: unknown): value is IconName { return typeof value === 'string' && Object.hasOwn(icons, value); }
export type IconProps = YueProps<Omit<ComponentProps<'svg'>, 'name'>> & {
  name: IconName; label?: string; size?: number;
  /** 透传 SVG 的 title 属性；可访问名称由 label 提供。 */
  title?: string;
};
/** 统一 Lucide 入口；无 label 时为装饰，有 label 时提供独立可访问名称。线宽默认 1.75，与控件文字的笔画粗细相当。 */
export function Icon({ name, label, size = 16, strokeWidth = 1.75, className, testId, ...props }: IconProps) {
  const Glyph = icons[name];
  return <Glyph {...props} size={size} strokeWidth={strokeWidth} role={label ? 'img' : undefined} aria-label={label} aria-hidden={label ? undefined : true} focusable="false" data-slot="icon" data-testid={testId ?? 'yue-icon'} className={cn('shrink-0', className)} />;
}

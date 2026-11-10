import { useMemo } from 'react';
import { ICON_NAMES, Icon, isIconName, type IconName } from '@idadawn/yue-core';
import { OptionCombobox, type BizSelectBaseProps } from './option-combobox';

export type IconPickerProps = BizSelectBaseProps & {
  /** 选中的图标名（@yue/core Icon 的登记名，即 UI 清单 nav.icon 的取值）；未选为 null。 */
  value: IconName | null;
  onChange: (name: IconName | null) => void;
  /** 可选图标；默认全部已登记图标（ICON_NAMES）。 */
  icons?: readonly IconName[] | undefined;
};

/** 图标选择器：从 @yue/core 的统一 Icon 登记表里选，按图标名过滤。图标集是本地登记表，不需要取数。 */
export function IconPicker({ value, onChange, icons = ICON_NAMES, i18n, label, disabled, className, testId }: IconPickerProps) {
  const options = useMemo(() => icons.map((name) => ({ value: name, label: name, prefix: <Icon name={name} /> })), [icons]);
  return <OptionCombobox slot="icon-picker" defaultTestId="yue-icon-picker" selection={{ value, onChange: (next) => onChange(isIconName(next) ? next : null) }} options={options}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}

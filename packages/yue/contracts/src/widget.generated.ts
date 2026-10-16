// 由 widget.schema.json 生成；运行 contracts:generate。

export interface WidgetMeta {
  type: string;
  version: string;
  name: string;
  category: "basic" | "advanced" | "system" | "layout" | "biz" | "ocr";
  props: {
    [k: string]: unknown;
  };
  valueType:
    "string" | "number" | "boolean" | "date" | "datetime" | "time" | "array" | "object" | "file" | "ref" | "none";
  events: string[];
  /**
   * @minItems 1
   */
  devices: ["pc" | "h5" | "miniapp", ...("pc" | "h5" | "miniapp")[]];
  capabilities: {
    bindsField: boolean;
    canBeColumn: boolean;
    canBeSearch: boolean;
    canBeGroup: boolean;
    canBeInSubtable: boolean;
    supportsReadonly: boolean;
    participatesInLinkage: boolean;
  };
  defaultOperators: string[];
  authAware: boolean;
  sensitivityHint?: "public" | "internal" | "pii" | "finance" | "secret";
  i18nKeys: string[];
  a11y: {
    role: string;
    labelledBy: "label" | "aria-label";
  };
  tokens: string[];
}

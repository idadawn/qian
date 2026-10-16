// 由 form.schema.json 生成；运行 contracts:generate。

export interface FormSchema {
  $schema: "https://schema.qian.dev/form/v1";
  version: string;
  id: string;
  data: {
    type: "object";
    properties: {
      [k: string]: unknown;
    };
    additionalProperties: false;
    [k: string]: unknown;
  };
  ui: {
    layout: UiNode[];
    device?: {
      h5?: {
        overrides?: {
          path: string;
          value: unknown;
        }[];
      };
    };
  };
  rules: {
    dependencies?: {
      source: string;
      equals: unknown;
      target: string;
      effect: "required" | "disabled" | "visible" | "value";
      value: unknown;
    }[];
    actions?: {
      [k: string]: unknown;
    }[];
    expressions?: {
      [k: string]: unknown;
    };
    serverHooks?: string[];
  };
  binding: {
    entity?: string;
    fields?: {
      [k: string]: string;
    };
    systemColumns?: string[];
  };
  auth: {
    fields?: {
      [k: string]: ("read" | "write")[];
    };
  };
}
export interface UiNode {
  field?: string;
  widget?: string;
  labelKey?: string;
  descriptionKey?: string;
  props?: {
    [k: string]: unknown;
  };
  children?: UiNode[];
}

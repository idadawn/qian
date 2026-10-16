// 由 list.schema.json 生成；运行 contracts:generate。

export interface ListSchema {
  $schema: "https://schema.qian.dev/list/v1";
  version: string;
  id: string;
  base: {
    dataSource: {
      kind: string;
      ref: string;
    };
    layout: "table" | "left-tree" | "tree-table" | "group" | "card" | "inline-edit";
    query?: {
      field?: string;
      widget?: string;
      labelKey?: string;
      descriptionKey?: string;
      props?: {
        [k: string]: unknown;
      };
      children?: UiNode[];
    }[];
    columns: {
      field: string;
      titleKey?: string;
      width?: number;
      fixed?: "left" | "right";
      align?: "left" | "center" | "right";
      summary?: string;
      resolver?: string;
    }[];
    actions?: {
      [k: string]: unknown;
    };
    filter?: {
      [k: string]: unknown;
    };
    sort?: {
      field: string;
      dir: "asc" | "desc";
    }[];
  };
  overrides: {
    h5?: {
      layout?: "table" | "left-tree" | "tree-table" | "group" | "card" | "inline-edit";
      columns?:
        | string[]
        | {
            field: string;
            titleKey?: string;
            width?: number;
            fixed?: "left" | "right";
            align?: "left" | "center" | "right";
            summary?: string;
            resolver?: string;
          }[];
      query?: {
        field?: string;
        widget?: string;
        labelKey?: string;
        descriptionKey?: string;
        props?: {
          [k: string]: unknown;
        };
        children?: UiNode[];
      }[];
      actions?: {
        [k: string]: unknown;
      };
    };
    pc?: {
      layout?: "table" | "left-tree" | "tree-table" | "group" | "card" | "inline-edit";
      columns?:
        | string[]
        | {
            field: string;
            titleKey?: string;
            width?: number;
            fixed?: "left" | "right";
            align?: "left" | "center" | "right";
            summary?: string;
            resolver?: string;
          }[];
      query?: {
        field?: string;
        widget?: string;
        labelKey?: string;
        descriptionKey?: string;
        props?: {
          [k: string]: unknown;
        };
        children?: UiNode[];
      }[];
      actions?: {
        [k: string]: unknown;
      };
    };
  };
  auth: {
    buttons: boolean;
    columns: boolean;
    fields: boolean;
    dataScope: boolean;
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

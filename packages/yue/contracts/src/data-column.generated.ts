// 由 data-column.schema.json 生成；运行 contracts:generate。

export interface DataColumnMeta {
  field: string;
  titleKey: string;
  sortable?: boolean;
  filterable?: boolean;
  width?: number;
  fixed?: "left" | "right";
}

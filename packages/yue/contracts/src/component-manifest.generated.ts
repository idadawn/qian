// 由 component-manifest.schema.json 生成；运行 contracts:generate。

export interface ComponentManifest {
  name: string;
  package: "@idadawn/yue-core";
  version: string;
  props: {
    [k: string]: unknown;
  };
  /**
   * @minItems 1
   */
  slots: [string, ...string[]];
  events: string[];
  a11y: {
    role: string;
    keyboard: string[];
  };
  tokens: string[];
  /**
   * @minItems 1
   */
  examples: [
    {
      title: string;
      props: {
        [k: string]: unknown;
      };
    },
    ...{
      title: string;
      props: {
        [k: string]: unknown;
      };
    }[]
  ];
  status: "stable" | "beta" | "deprecated";
}

/** 顶层跳转的唯一出口：页面只经它离开，组件测试据此替换成记录器而不真的导航。 */
export const browser = {
  assign(url: string): void { location.assign(url); },
  replace(url: string): void { location.replace(url); },
};

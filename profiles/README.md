# profile 装配基线

当前交付 platform、gateway、identity、standalone 四份静态配置及对应组合包。默认 minimal 的两个现有口径冲突，待 S1-DEC-012 裁决后补齐，不以测试空树代替产品决策。

业务叶节点均显式标记 `x-skeleton: true`，可解析、可校验、可 dump；这不代表对应业务 Provider、平台或身份服务器已可启动。测试中的无业务表面组件则实际交给 Qian.Runtime 装载并验证生命周期。

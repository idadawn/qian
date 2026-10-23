namespace Qian.Config;

/// <summary>只读租户灰度求值；不读取权益，不改变租户 scope 的组件挂载。</summary>
public interface IFeatureFlags
{
    /// <summary>
    /// 求值指定开关。名称区分大小写，只接受小写 ASCII 名称；tenantId 必须非空。
    /// 未定义的开关返回 false；规则依次检查租户白名单、稳定百分比桶，最后使用 default。
    /// </summary>
    bool IsEnabled(string name, Guid tenantId);
}

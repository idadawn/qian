/* 由 theme.ts 生成的首帧脚本；勿手改。 */
;(function bootstrap(settings) {
    const root = document.documentElement;
    const validTheme = (value)=>'light' === value || 'dark' === value || 'system' === value;
    const validBrand = (value)=>'string' == typeof value && /^[a-z][a-z0-9-]{0,63}$/.test(value);
    let stored = null;
    try {
        stored = window.localStorage.getItem(settings.storageKey);
    } catch  {}
    const preference = validTheme(stored) ? stored : settings.defaultTheme;
    const theme = 'system' === preference ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light' : preference;
    const brand = validBrand(settings.brand) ? settings.brand : validBrand(root.dataset.brand) ? root.dataset.brand : 'default';
    root.dataset.theme = theme;
    root.dataset.themePreference = preference;
    root.dataset.brand = brand;
    root.classList.toggle('dark', 'dark' === theme);
    root.style.colorScheme = theme;
    return {
        preference,
        theme,
        brand
    };
})({"storageKey":"yue-theme","defaultTheme":"system"});

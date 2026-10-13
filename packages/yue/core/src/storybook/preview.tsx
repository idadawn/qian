import { definePreview } from '@storybook/react-vite';
import addonA11y from '@storybook/addon-a11y';
import addonDocs from '@storybook/addon-docs';

export default definePreview({
  addons: [addonDocs(), addonA11y()],
  parameters: { layout: 'padded', a11y: { test: 'error' }, docs: { story: { inline: false } } },
  globalTypes: { theme: { description: '颜色主题', toolbar: { icon: 'circlehollow', items: ['light', 'dark'], dynamicTitle: true } } },
  initialGlobals: { theme: 'light' },
  beforeEach: ({ globals }) => {
    const previous = document.documentElement.dataset.theme;
    document.documentElement.dataset.theme = globals.theme === 'dark' ? 'dark' : 'light';
    return () => { if (previous === undefined) delete document.documentElement.dataset.theme; else document.documentElement.dataset.theme = previous; };
  },
  decorators: [Story => <main className="min-h-48 p-4"><Story /></main>],
});

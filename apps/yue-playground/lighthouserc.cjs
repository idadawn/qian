// Lighthouse CI（pnpm yue:lighthouse）：对构建后的靶场页跑一次，首屏 script 体积超预算即失败（error 级，PRD §14 C14）。
// 预算值在 docs/prd/14-quality.md「性能预算」维护，这里只是落地；改预算先改文档再改这里。
const SCRIPT_BUDGET_BYTES = 250 * 1024; // 压缩后传输体积（lhci 静态服务器开 gzip）；靶场基线约 151 KB

module.exports = {
  ci: {
    collect: {
      staticDistDir: './dist',
      numberOfRuns: 1,
      settings: { preset: 'desktop', chromeFlags: '--no-sandbox --headless=new' },
    },
    assert: {
      assertions: {
        'resource-summary:script:size': ['error', { maxNumericValue: SCRIPT_BUDGET_BYTES }],
        'categories:accessibility': ['error', { minScore: 1 }],
        'categories:best-practices': ['warn', { minScore: 0.9 }],
        'categories:performance': ['warn', { minScore: 0.9 }],
      },
    },
    upload: { target: 'filesystem', outputDir: '.lighthouseci' },
  },
};

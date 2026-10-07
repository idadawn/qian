// 首页「最新动态」：直接读仓库 docs/CHANGELOG.md，取最近几条（docs-site.md §2a.1），不另维护一份。
import { Link } from '@rspress/core/theme';
import changelog from '../../../docs/CHANGELOG.md?raw';

export interface ChangeEntry {
  date: string;
  title: string;
  summary: string;
}

/** 解析 `## YYYY-MM-DD 标题` 形式的条目，摘要取标题下第一行正文（去掉列表符与链接语法）。 */
export function parseChangelog(markdown: string): ChangeEntry[] {
  const entries: ChangeEntry[] = [];
  let current: ChangeEntry | null = null;
  for (const line of markdown.split('\n')) {
    const heading = /^##\s+(\d{4}-\d{2}-\d{2})\s+(.+)$/.exec(line);
    if (heading) {
      current = { date: heading[1] ?? '', title: (heading[2] ?? '').trim(), summary: '' };
      entries.push(current);
      continue;
    }
    if (current && !current.summary) {
      const text = line.replace(/^\s*[-*]\s+/, '').trim();
      if (text && !text.startsWith('#')) {
        current.summary = text.replace(/\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/`([^`]*)`/g, '$1');
      }
    }
  }
  return entries;
}

export function LatestChanges({ count = 4 }: { count?: number }) {
  const entries = parseChangelog(changelog).slice(0, count);
  return (
    <ul className="qian-latest rp-not-doc">
      {entries.map((entry) => (
        <li key={`${entry.date}-${entry.title}`}>
          <time dateTime={entry.date}>{entry.date}</time>
          <div>
            <Link href="/design/changelog">{entry.title}</Link>
            {entry.summary ? <p>{entry.summary}</p> : null}
          </div>
        </li>
      ))}
    </ul>
  );
}

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
const root = path.resolve('doc_build');
const base = process.env.DOCS_BASE ?? '/';
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.md': 'text/plain; charset=utf-8' };
http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (!url.pathname.startsWith(base)) { res.writeHead(404).end(); return; }
  const name = path.resolve(root, decodeURIComponent(url.pathname.slice(base.length) || 'index.html'));
  if (!name.startsWith(`${root}${path.sep}`)) { res.writeHead(403).end(); return; }
  const file = name.endsWith(path.sep) ? `${name}index.html` : name;
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) { res.writeHead(404).end(); return; }
  res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}).listen(6014, '127.0.0.1');

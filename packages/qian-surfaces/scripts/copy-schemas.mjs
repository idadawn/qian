import { cpSync, mkdirSync } from 'node:fs';
const target = new URL('../dist/schemas/', import.meta.url);
mkdirSync(target, { recursive: true });
cpSync(new URL('../../../schemas/v2/', import.meta.url), target, { recursive: true });
cpSync(new URL('../../../contracts/v0/capability-entry.schema.json', import.meta.url), new URL('capability-entry.schema.json', target));

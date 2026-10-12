// Kiểm tra cú pháp Mermaid trong mọi file .md của repo.
// Dùng: node tools/check-mermaid.mjs
// Cần: npm i -D mermaid jsdom   (đã cài trong tools/package.json)
//
// Vì sao có script này: Mermaid lỗi cú pháp KHÔNG làm hỏng build — nó chỉ hiện
// "Mermaid render error" khi ai đó mở file. Script này biến lỗi đó thành lỗi CI.

import fs from 'node:fs';
import path from 'node:path';
import { JSDOM } from 'jsdom';

// ---- shim DOM tối thiểu để mermaid.parse() chạy được trong Node ----
const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>', { pretendToBeVisual: true });
globalThis.window = dom.window;
globalThis.document = dom.window.document;
// Node 24: globalThis.navigator chỉ có getter ⇒ phải defineProperty thay vì gán trực tiếp
Object.defineProperty(globalThis, 'navigator', { value: dom.window.navigator, configurable: true, writable: true });
globalThis.DOMParser = dom.window.DOMParser;
globalThis.HTMLElement = dom.window.HTMLElement;
globalThis.SVGElement = dom.window.SVGElement;
globalThis.Element = dom.window.Element;
globalThis.Node = dom.window.Node;
globalThis.MutationObserver = dom.window.MutationObserver;

const mermaid = (await import('mermaid')).default;
mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

const root = process.argv[2] ?? process.cwd();

function* walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name === '.git' || entry.name === 'bin' || entry.name === 'obj') continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) yield* walk(full);
    else if (entry.name.endsWith('.md')) yield full;
  }
}

const blocks = [];
for (const file of walk(root)) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  let start = -1;
  for (let i = 0; i < lines.length; i++) {
    if (/^\s*```mermaid/.test(lines[i])) { start = i + 1; continue; }
    if (start !== -1 && /^\s*```/.test(lines[i])) {
      blocks.push({ file: path.relative(root, file), line: start + 1, code: lines.slice(start, i).join('\n') });
      start = -1;
    }
  }
}

let failed = 0;
for (const b of blocks) {
  try {
    const result = await mermaid.parse(b.code, { suppressErrors: false });
    if (result === false) throw new Error('mermaid.parse trả về false');
    console.log(`PASS  ${b.file}:${b.line}`);
  } catch (e) {
    failed++;
    const msg = String(e?.message ?? e).split('\n').slice(0, 6).join(' | ');
    console.log(`FAIL  ${b.file}:${b.line}  →  ${msg}`);
  }
}

console.log(`\nTổng: ${blocks.length} khối mermaid · lỗi: ${failed}`);
process.exit(failed === 0 ? 0 : 1);

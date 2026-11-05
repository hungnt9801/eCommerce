#!/usr/bin/env node
// Bắt console/exception khi TẢI một trang — dùng khi form "câm" (bấm gì cũng không phản ứng).
// Dùng: node tools/bat-loi-trang.mjs <url> [giay]

const PORT = Number(process.env.CDP_PORT || 9222);
const target = process.argv[2];
const SECONDS = Number(process.argv[3] || 10);

const targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn'));
const ws = new WebSocket(tab.webSocketDebuggerUrl);
let id = 0;
const pending = new Map();
const events = [];
ws.onmessage = (ev) => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) {
    const p = pending.get(m.id); pending.delete(m.id);
    m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result);
    return;
  }
  if (m.method === 'Runtime.exceptionThrown') {
    const d = m.params.exceptionDetails;
    events.push({ kind: 'exception', text: (d.exception?.description || d.text || '').slice(0, 400) });
  }
  if (m.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(m.params.type)) {
    const txt = (m.params.args || []).map((a) => a.value ?? a.description ?? a.type).join(' ').slice(0, 300);
    events.push({ kind: 'console-' + m.params.type, text: txt });
  }
  if (m.method === 'Log.entryAdded') {
    const e = m.params.entry;
    if (['error', 'warning'].includes(e.level)) events.push({ kind: 'log', text: `${e.source}: ${e.text}`.slice(0, 300) });
  }
  if (m.method === 'Network.responseReceived' && m.params.response.status >= 400) {
    events.push({ kind: 'http', status: m.params.response.status, url: m.params.response.url.slice(0, 180) });
  }
};
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const i = ++id; pending.set(i, { resolve, reject });
  ws.send(JSON.stringify({ id: i, method, params }));
});
await new Promise((r) => { ws.onopen = r; });
await send('Runtime.enable');
await send('Log.enable');
await send('Network.enable');
await send('Page.enable');
if (target) await send('Page.navigate', { url: target });
await new Promise((r) => setTimeout(r, SECONDS * 1000));
console.log(JSON.stringify(events.slice(0, 25), null, 1));
console.log(`Tổng: ${events.length}`);
ws.close();
process.exit(0);

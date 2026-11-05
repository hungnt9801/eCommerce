#!/usr/bin/env node
// Bắt lỗi ngầm khi bấm Lưu: theo dõi Network + Console + Exception trong lúc click.
// Dùng: node tools/bat-loi-luu.mjs "<selector nút Lưu>" [giay]
//
// Vì sao cần: nút Lưu của form đơn hàng "im lặng" — không toast, không field lỗi, không chuyển trang.
// Chỉ có log mạng/console mới nói được là API trả 4xx/5xx hay JS ném exception.

const PORT = Number(process.env.CDP_PORT || 9222);
const BUTTON_SEL = process.argv[2] || '';
const SECONDS = Number(process.argv[3] || 12);

const targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn'));
if (!tab) throw new Error('Không thấy tab nhanh.vn');

const ws = new WebSocket(tab.webSocketDebuggerUrl);
let id = 0;
const pending = new Map();
const events = [];

ws.onmessage = (ev) => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) {
    const p = pending.get(m.id);
    pending.delete(m.id);
    m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result);
    return;
  }
  if (!m.method) return;
  if (m.method === 'Network.responseReceived') {
    const r = m.params.response;
    if (r.status >= 400) events.push({ kind: 'http', status: r.status, url: r.url.slice(0, 160) });
    else if (/order|bill|api/i.test(r.url)) events.push({ kind: 'api', status: r.status, url: r.url.slice(0, 160) });
  }
  if (m.method === 'Network.loadingFailed') {
    events.push({ kind: 'netfail', err: m.params.errorText, url: (m.params.requestId || '').slice(0, 20) });
  }
  if (m.method === 'Runtime.exceptionThrown') {
    const d = m.params.exceptionDetails;
    events.push({ kind: 'exception', text: (d.exception?.description || d.text || '').slice(0, 300) });
  }
  if (m.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(m.params.type)) {
    const txt = (m.params.args || []).map((a) => a.value ?? a.description ?? a.type).join(' ').slice(0, 240);
    events.push({ kind: 'console-' + m.params.type, text: txt });
  }
};

const send = (method, params = {}) => new Promise((resolve, reject) => {
  const i = ++id;
  pending.set(i, { resolve, reject });
  ws.send(JSON.stringify({ id: i, method, params }));
});

await new Promise((r) => { ws.onopen = r; });
await send('Network.enable');
await send('Runtime.enable');

// Bấm nút: ưu tiên chuột thật theo toạ độ (nhiều nút của Nhanh nghe pointerdown, .click() không ăn)
const rect = await send('Runtime.evaluate', {
  expression: `(() => {
    const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /${BUTTON_SEL}/.test(e.innerText || ''));
    if (!b) return null;
    b.scrollIntoView({ block: 'center' });
    const r = b.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  })()`,
  returnByValue: true,
});
const pos = rect.result.value;
if (!pos) { console.log('Không thấy nút'); process.exit(1); }
for (const type of ['mousePressed', 'mouseReleased']) {
  await send('Input.dispatchMouseEvent', { type, x: pos.x, y: pos.y, button: 'left', clickCount: 1 });
}
console.log('Đã click chuột thật tại', pos);

await new Promise((r) => setTimeout(r, SECONDS * 1000));

// Lấy thêm body của các response lỗi
const bad = events.filter((e) => e.kind === 'http');
console.log(JSON.stringify(events, null, 1));
console.log(`\nTổng sự kiện: ${events.length} (lỗi HTTP: ${bad.length})`);

ws.close();
process.exit(0);

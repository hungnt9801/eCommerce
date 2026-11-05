#!/usr/bin/env node
// Tạo hóa đơn bán lẻ (POS) trong Nhanh.vn qua UI — Chrome CDP.
//
// Dùng: node tools/tao-hoa-don-pos.mjs --limit 49 [--start 0] [--dry]
//
// Vì sao dùng POS thay vì "Đơn hàng": form đơn hàng bắt buộc "Nguồn đơn hàng" nhưng tài khoản
// chưa có nguồn nào và không có quyền tạo -> form tự huỷ, không gửi request. POS không cần field đó.
//
// BÀI HỌC (đừng bỏ):
//  1. Phải Page.bringToFront + Emulation.setFocusEmulationEnabled trước khi click, nếu không
//     CDP nuốt cú click: không request, không toast, không lỗi.
//  2. scrollIntoView có thể smooth-scroll -> chờ rồi mới đo toạ độ, đo ngay là click trượt.
//  3. Ô tìm sản phẩm có placeholder "(F3) Tìm sản phẩm"; gợi ý là td.infoName trong một table,
//     click vào <tr> chứa nó. Gõ bằng Input.insertText (set value không ra gợi ý).
//  4. Ô số lượng là AutoNumeric -> chỉ nhận qua ClipboardEvent('paste').

import fs from 'node:fs';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const URL_POS = 'https://nhanh.vn/pos/bill/add?businessId=230222';
const STATE = 'tools/_hoa-don-pos.json';

const argv = process.argv.slice(2);
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : null; };
const LIMIT = Number(arg('limit') || 10);
const START = Number(arg('start') || 0);
const DRY = argv.includes('--dry');

class CDP {
  constructor(wsUrl) {
    this.id = 0; this.pending = new Map(); this.ws = new WebSocket(wsUrl);
    this.ready = new Promise((res, rej) => { this.ws.onopen = () => res(); this.ws.onerror = () => rej(new Error('WS lỗi')); });
    this.ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data); const p = this.pending.get(m.id);
      if (p) { this.pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    };
  }
  async send(method, params = {}) {
    await this.ready; const id = ++this.id;
    return await new Promise((resolve, reject) => { this.pending.set(id, { resolve, reject }); this.ws.send(JSON.stringify({ id, method, params })); });
  }
  async evaluate(expression, timeoutMs = 60000) {
    const r = await Promise.race([
      this.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true }),
      new Promise((_, rej) => setTimeout(() => rej(new Error('evaluate quá hạn')), timeoutMs)),
    ]);
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
    return r.result.value;
  }
}

const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const targets = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn'));
if (!tab) throw new Error('Không thấy tab nhanh.vn');
const cdp = new CDP(tab.webSocketDebuggerUrl);

async function focusTab() {
  try { await cdp.send('Page.bringToFront'); } catch { /* ignore */ }
  try { await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: true }); } catch { /* ignore */ }
}
async function clickAt(x, y) {
  await cdp.send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
  await cdp.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });
}

const customers = Object.values(JSON.parse(fs.readFileSync('tools/_khach-hang.json', 'utf8')).customers);
const stocked = JSON.parse(fs.readFileSync('tools/_phieu-nhap.json', 'utf8')).stocked;
const catalog = JSON.parse(fs.readFileSync('tools/_woo-catalog.json', 'utf8'));
const bySku = Object.fromEntries(catalog.products.map((p) => [p.sku, p]));
const pool = Object.keys(stocked).filter((sku) => bySku[sku] && bySku[sku].price > 0);
if (!customers.length || !pool.length) throw new Error('thiếu dữ liệu');

const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : { invoices: [] };

function scenario(i) {
  const kh = customers[i % customers.length];
  const nLines = 1 + (i % 3);
  const lines = [];
  for (let k = 0; k < nLines; k++) {
    const sku = pool[(i * 11 + k * 17) % pool.length];
    lines.push({ sku, qty: 1 + ((i + k) % 3) });
  }
  return { kh, lines };
}

async function createInvoice(i) {
  const sc = scenario(i);
  await cdp.send('Page.navigate', { url: URL_POS });
  await wait(3000);
  await focusTab();

  const ready = await cdp.evaluate(`(async () => {
    for (let i = 0; i < 80; i++) {
      if (document.querySelector('input[placeholder="(F3) Tìm sản phẩm"]')) return true;
      await new Promise((r) => setTimeout(r, 250));
    }
    return false;
  })()`);
  if (!ready) throw new Error('form POS chưa render');

  await focusTab();

  // khách hàng: nhập SĐT (POS tra theo SĐT), tên điền kèm
  const cust = await cdp.evaluate(`(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const ins = [...document.querySelectorAll('input')];
    const set = async (ph, v) => {
      const e = ins.find((x) => (x.placeholder || '') === ph);
      if (!e) return null;
      e.focus();
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(e, String(v));
      for (const ev of ['input', 'change', 'blur']) e.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(150);
      return e.value;
    };
    const phone = await set('Số điện thoại (F4)', ${JSON.stringify(sc.kh.phone)});
    const name = await set('Tên khách hàng', ${JSON.stringify(sc.kh.name)});
    return { phone, name };
  })()`);
  await wait(800);

  for (const ln of sc.lines) {
    let added = { ok: false };
    for (let attempt = 0; attempt < 3 && !added.ok; attempt++) {
      const before = await cdp.evaluate(`document.querySelectorAll('table')[0].querySelectorAll('tbody tr').length`);
      await cdp.evaluate(`(() => { const f = document.querySelector('input[placeholder="(F3) Tìm sản phẩm"]'); if (!f) return false; f.id='pos-f3'; f.focus(); f.select && f.select(); return true; })()`);
      await cdp.send('Input.insertText', { text: ln.sku });
      await wait(2300 + attempt * 800);
      added = await cdp.evaluate(`(async () => {
        const wait = (ms) => new Promise((r) => setTimeout(r, ms));
        const td = [...document.querySelectorAll('td.infoName')].find(
          (e) => e.offsetParent && (e.innerText || '').includes(${JSON.stringify(ln.sku)})
        );
        if (!td) return { ok: false, err: 'no-suggest' };
        (td.closest('tr') || td).click();
        await wait(1500);
        const after = document.querySelectorAll('table')[0].querySelectorAll('tbody tr').length;
        return { ok: after > ${before}, after };
      })()`);
    }
    if (!added.ok) throw new Error('không thêm được dòng ' + ln.sku);

    await cdp.evaluate(`(async () => {
      const wait = (ms) => new Promise((r) => setTimeout(r, ms));
      const rows = [...document.querySelectorAll('table')[0].querySelectorAll('tbody tr')];
      const tr = rows[rows.length - 1];
      const nums = [...tr.querySelectorAll('input')].filter((e) => e.hasAttribute('autonumeric'));
      const sl = nums[nums.length - 1];
      if (!sl) return false;
      sl.focus();
      const dt = new DataTransfer();
      dt.setData('text/plain', String(${ln.qty}));
      sl.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
      await wait(400);
      document.activeElement && document.activeElement.blur();
      return true;
    })()`);
  }

  if (DRY) return { dry: true, sc, cust };

  // lưu
  let clicked = false;
  for (let attempt = 0; attempt < 3 && !clicked; attempt++) {
    await focusTab();
    await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /(Lưu \\(F9\\)|\\(F9\\) Lưu)/.test(e.innerText || ''));
      if (b) b.scrollIntoView({ block: 'center' });
      return true;
    })()`);
    await wait(700);
    const pos = await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /(Lưu \\(F9\\)|\\(F9\\) Lưu)/.test(e.innerText || ''));
      if (!b) return null;
      const r = b.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    })()`);
    if (!pos) throw new Error('không thấy nút Lưu');
    await cdp.evaluate('window.__saved = 0; document.addEventListener("click", () => { window.__saved = 1; }, { once: true, capture: true })');
    await clickAt(pos.x, pos.y);
    await wait(900);
    clicked = (await cdp.evaluate('window.__saved === 1')) === true;
  }
  if (!clicked) throw new Error('click không tới nút Lưu');
  await wait(4500);

  const after = await cdp.evaluate(`({ url: location.href, rows: document.querySelectorAll('table')[0] ? document.querySelectorAll('table')[0].querySelectorAll('tbody tr').length : -1 })`);
  const saved = after.rows === 0; // POS reset form sau khi lưu
  return { saved, after, sc };
}

const todo = Array.from({ length: LIMIT }, (_, i) => START + i);
console.log(`Tạo ${todo.length} hóa đơn POS${DRY ? ' [DRY]' : ''}`);

let ok = 0;
for (const [n, idx] of todo.entries()) {
  try {
    const r = await createInvoice(idx);
    if (r.saved || r.dry) {
      ok++;
      state.invoices.push({ idx, at: new Date().toISOString(), customer: r.sc.kh.name, phone: r.sc.kh.phone, lines: r.sc.lines });
      fs.writeFileSync(STATE, JSON.stringify(state, null, 1), 'utf8');
      console.log(`  ${n + 1}/${todo.length} OK ${r.sc.kh.name} | ${r.sc.lines.map((l) => l.sku + 'x' + l.qty).join(', ')}${r.dry ? ' [DRY]' : ''}`);
    } else {
      console.log(`  ${n + 1}/${todo.length} ?? chưa xác nhận (rows=${r.after.rows}, url=...${r.after.url.slice(-24)})`);
    }
  } catch (e) {
    console.log(`  ${n + 1}/${todo.length} ERR — ${e.message}`);
  }
  await wait(700);
}
console.log(`\nXong: ${ok}/${todo.length}`);
process.exit(0);

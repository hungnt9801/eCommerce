#!/usr/bin/env node
// Tạo đơn hàng trong Nhanh.vn qua UI (Chrome CDP).
//
// Dùng: node tools/tao-don-hang.mjs --limit 50 [--start 0] [--dry]
//
// BÀI HỌC QUAN TRỌNG NHẤT (đã mất rất nhiều thời gian để tìm ra):
//   Tab phải được FOCUS thì CDP mới chuyển được cú click vào trang.
//   Trước đó mọi cú click vào nút "Lưu" đều bị nuốt im lặng: KHÔNG có request mạng,
//   KHÔNG có toast, KHÔNG có lỗi console. Fix: Page.bringToFront + Emulation.setFocusEmulationEnabled.
//
// Các bẫy khác của form đơn hàng:
//   - Ô "Tên" là autocomplete khách hàng: gõ tên -> click gợi ý "Tên (SĐT)".
//   - Ô (F3) Tìm sản phẩm không có placeholder: phải tìm theo label, gán id rồi mới gõ.
//   - Gợi ý sản phẩm là <tr class="productItem">, click vào div.cursor-pointer bên trong.
//   - Nút Lưu: phải click chuột thật theo toạ độ (JS .click() không kích hoạt handler).

import fs from 'node:fs';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const ADD_URL = 'https://nhanh.vn/order/manage/add2?businessId=230222';
const STATE = 'tools/_don-hang-da-tao.json';

const argv = process.argv.slice(2);
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : null; };
const LIMIT = Number(arg('limit') || 5);
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

// FIX QUAN TRỌNG: phải focus tab thì click mới tới nơi
async function focusTab() {
  try { await cdp.send('Page.bringToFront'); } catch { /* ignore */ }
  try { await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: true }); } catch { /* ignore */ }
}

async function typeInto(js, text) {
  await cdp.evaluate(js);
  await cdp.send('Input.insertText', { text });
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
if (!customers.length || !pool.length) throw new Error('thiếu dữ liệu khách hoặc sản phẩm');

const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : { orders: [] };

// sinh kịch bản đơn
function pick(i) {
  const kh = customers[i % customers.length];
  const nLines = 1 + (i % 3); // 1..3 dòng
  const lines = [];
  for (let k = 0; k < nLines; k++) {
    const sku = pool[(i * 7 + k * 13) % pool.length];
    lines.push({ sku, qty: 1 + ((i + k) % 3) });
  }
  const payMode = i % 3 === 0 ? 'transfer' : i % 3 === 1 ? 'cash' : 'cod';
  return { kh, lines, payMode };
}

async function createOrder(idx) {
  const sc = pick(idx);
  await cdp.send('Page.navigate', { url: ADD_URL });
  await wait(3000);
  await focusTab();

  const ready = await cdp.evaluate(`(async () => {
    for (let i = 0; i < 80; i++) {
      if ([...document.querySelectorAll('input')].some((e) => (e.placeholder || '') === 'Tên')) return true;
      await new Promise((r) => setTimeout(r, 250));
    }
    return false;
  })()`);
  if (!ready) throw new Error('form đơn hàng chưa render');

  await focusTab();
  // 1) khách hàng: điền trực tiếp tên/SĐT/địa chỉ.
  //    (Gợi ý khách hàng của Nhanh chỉ hiện card thông tin, click card KHÔNG tự điền SĐT —
  //     đã thử; điền tay vẫn tạo được đơn và Nhanh tự khớp khách theo SĐT khi lưu.)
  const filled = await cdp.evaluate(`(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const ins = [...document.querySelectorAll('input')];
    const set = async (ph, v) => {
      const e = ins.find((x) => (x.placeholder || '') === ph);
      if (!e) return null;
      e.focus();
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(e, String(v));
      for (const ev of ['input', 'change', 'blur']) e.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(120);
      return e.value;
    };
    const ten = await set('Tên', ${JSON.stringify(sc.kh.name)});
    const dt = await set('Điện thoại', ${JSON.stringify(sc.kh.phone)});
    const dc = await set('Địa chỉ', ${JSON.stringify(sc.kh.addr || 'Bến Tre')});
    return { ten, dt, dc };
  })()`);
  if (!filled?.dt) throw new Error('không điền được khách: ' + JSON.stringify(filled));
  await wait(600);

  // 2) các dòng sản phẩm (có thử lại: gợi ý có thể chưa render kịp)
  for (const ln of sc.lines) {
    let added = { ok: false };
    for (let attempt = 0; attempt < 3 && !added.ok; attempt++) {
      const before = await cdp.evaluate(`document.querySelectorAll('table')[0].querySelectorAll('tbody tr').length`);
      const f3ok = await cdp.evaluate(`(() => {
        const all = [...document.querySelectorAll('input')];
        const lab = (el) => { const b = el.closest('.form-floating') || el.parentElement; const l = b && b.querySelector('label'); return l ? l.innerText.replace(/\\s+/g, ' ').trim() : ''; };
        const f3 = all.find((e) => lab(e).startsWith('(F3) Tìm sản phẩm'));
        if (!f3) return false;
        f3.id = 'order-f3';
        f3.focus();
        return true;
      })()`);
      if (!f3ok) throw new Error('không thấy ô tìm sản phẩm');
      await cdp.send('Input.insertText', { text: ln.sku });
      await wait(2400 + attempt * 800);
      added = await cdp.evaluate(`(async () => {
        const wait = (ms) => new Promise((r) => setTimeout(r, ms));
        const tr = [...document.querySelectorAll('tr.productItem')].find(
          (e) => e.offsetParent && (e.innerText || '').includes(${JSON.stringify(ln.sku)})
        );
        if (!tr) return { ok: false, err: 'no-suggest' };
        (tr.querySelector('div.cursor-pointer') || tr).click();
        await wait(1600);
        const after = document.querySelectorAll('table')[0].querySelectorAll('tbody tr').length;
        return { ok: after > ${before}, before: ${before}, after };
      })()`);
    }
    if (!added.ok) throw new Error('không thêm được dòng ' + ln.sku);
    // set số lượng ở dòng cuối
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

  if (DRY) return { dry: true, sc };

  // 3) lưu — click chuột thật sau khi focus tab.
  //    Lưu ý: scrollIntoView có thể là smooth-scroll -> phải chờ rồi mới đo lại toạ độ,
  //    nếu đo ngay thì click rơi vào chỗ cũ và KHÔNG có request nào (đã từng bị).
  let clicked = false;
  for (let attempt = 0; attempt < 3 && !clicked; attempt++) {
    await focusTab();
    await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /\\(F9\\) Lưu/.test(e.innerText || ''));
      if (b) b.scrollIntoView({ block: 'center' });
      return true;
    })()`);
    await wait(700);
    const pos = await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /\\(F9\\) Lưu/.test(e.innerText || ''));
      if (!b) return null;
      const r = b.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2, w: r.width };
    })()`);
    if (!pos) throw new Error('không thấy nút Lưu');
    await cdp.evaluate('window.__saved = 0; document.addEventListener("click", () => { window.__saved = 1; }, { once: true, capture: true })');
    await clickAt(pos.x, pos.y);
    await wait(900);
    clicked = (await cdp.evaluate('window.__saved === 1')) === true;
  }
  if (!clicked) throw new Error('click không tới nút Lưu');
  await wait(5000);

  const after = await cdp.evaluate(`({ url: location.href, ten: (() => { const f=[...document.querySelectorAll('input')].find(e=>(e.placeholder||'')==='Tên'); return f ? f.value : null; })() })`);
  const saved = after.url.includes('/order/manage/index') || after.ten === '';
  return { saved, after, sc };
}

const todo = Array.from({ length: LIMIT }, (_, i) => START + i);
console.log(`Tạo ${todo.length} đơn${DRY ? ' [DRY]' : ''}`);

let ok = 0;
for (const [n, idx] of todo.entries()) {
  try {
    const r = await createOrder(idx);
    if (r.saved || r.dry) {
      ok++;
      state.orders.push({ idx, at: new Date().toISOString(), customer: r.sc.kh.name, lines: r.sc.lines, payMode: r.sc.payMode });
      fs.writeFileSync(STATE, JSON.stringify(state, null, 1), 'utf8');
      console.log(`  ${n + 1}/${todo.length} OK ${r.sc.kh.name} | ${r.sc.lines.map((l) => l.sku + 'x' + l.qty).join(', ')} | ${r.sc.payMode}`);
    } else {
      console.log(`  ${n + 1}/${todo.length} ?? không xác nhận được lưu (url=${r.after.url.slice(-30)}, ten="${r.after.ten}")`);
    }
  } catch (e) {
    console.log(`  ${n + 1}/${todo.length} ERR — ${e.message}`);
  }
  await wait(800);
}
console.log(`\nXong: ${ok}/${todo.length}`);
process.exit(0);

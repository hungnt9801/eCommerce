#!/usr/bin/env node
// Tạo phiếu nhập kho trong Nhanh.vn qua UI (Chrome CDP) để sản phẩm có tồn + giá vốn.
//
// Dùng: node tools/tao-phieu-nhap.mjs [--batch 36] [--qty-min 30] [--qty-max 90] [--ratio 0.7]
//
// Cách hoạt động:
//  - Mở /inventory/bill/import2, gõ mã SP vào ô (F3), chọn gợi ý, rồi set Giá + SL cho dòng vừa thêm.
//  - Đủ --batch dòng thì bấm "Lưu (F9)" và sang phiếu mới.
//  - Ghi lại SKU đã nhập vào tools/_phieu-nhap.json để không nhập trùng.
//
// Bài học:
//  - Ô tìm sản phẩm CHỈ gợi ý khi gõ bằng key event thật (Input.insertText), set value + input event KHÔNG ra gợi ý.
//  - Các ô Giá/SL/Chiết khấu là AutoNumeric -> phải paste.
//  - Không dùng index cột (form đổi layout); sắp input theo toạ độ x: [hiddenKey, Giá, SL, Chiết khấu].

import fs from 'node:fs';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const URL_IMPORT = 'https://nhanh.vn/inventory/bill/import2?businessId=230222';
const STATE = 'tools/_phieu-nhap.json';

const argv = process.argv.slice(2);
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : null; };
const BATCH = Number(arg('batch') || 36);
const QTY_MIN = Number(arg('qty-min') || 30);
const QTY_MAX = Number(arg('qty-max') || 90);
const RATIO = Number(arg('ratio') || 0.7); // giá nhập = 70% giá bán

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

const catalog = JSON.parse(fs.readFileSync('tools/_woo-catalog.json', 'utf8'));
const imported = JSON.parse(fs.readFileSync('tools/_da-nhap.json', 'utf8')).done;
const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : { stocked: {}, receipts: [] };

const LIMIT = Number(arg('limit') || 999);
const todo = catalog.products.filter((p) => imported[p.sku] && !state.stocked[p.sku] && p.price > 0).slice(0, LIMIT);
console.log(`Cần nhập kho: ${todo.length} sản phẩm (đã có: ${Object.keys(state.stocked).length})`);

let receiptNo = state.receipts.length;
function qtyFor(sku) {
  let h = 0;
  for (const ch of sku) h = (h * 31 + ch.charCodeAt(0)) % 97;
  return QTY_MIN + (h % Math.max(1, QTY_MAX - QTY_MIN + 1));
}

async function newReceipt() {
  await cdp.send('Page.navigate', { url: URL_IMPORT });
  await wait(2500);
  const ok = await cdp.evaluate(`(async () => {
    for (let i = 0; i < 60; i++) {
      const f = [...document.querySelectorAll('input')].find((e) => (e.placeholder || '').includes('Tìm sản phẩm'));
      if (f) return true;
      await new Promise((r) => setTimeout(r, 250));
    }
    return false;
  })()`);
  if (!ok) throw new Error('form nhập kho chưa render');
}

async function addLine(sku, price, qty, name) {
  // 1) focus ô tìm + Ctrl+A + gõ mã bằng key event thật.
  //    Set value + input event KHÔNG ra gợi ý; phải là text do trình duyệt chèn.
  const box = await cdp.evaluate(`(() => {
    const f = [...document.querySelectorAll('input')].find((e) => (e.placeholder || '').includes('Tìm sản phẩm'));
    if (!f) return null;
    const r = f.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  })()`);
  if (!box) throw new Error('không thấy ô tìm sản phẩm');

  let picked = false;
  let lastDebug = '';
  for (let attempt = 0; attempt < 4 && !picked; attempt++) {
    const before = await cdp.evaluate('document.querySelectorAll("tbody tr").length');

    // Focus bằng JS: click chuột theo toạ độ KHÔNG focus được ô này (value vẫn rỗng, đã kiểm chứng)
    await cdp.evaluate(`(() => {
      const f = [...document.querySelectorAll('input')].find((e) => (e.placeholder || '').includes('Tìm sản phẩm'));
      if (!f) return false;
      f.focus();
      return true;
    })()`);
    // Xoá bằng phím thật: Ctrl+A rồi Backspace -> Angular nhận input event, danh sách gợi ý reset
    await cdp.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'a', code: 'KeyA', windowsVirtualKeyCode: 65, modifiers: 2 });
    await cdp.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'a', code: 'KeyA', windowsVirtualKeyCode: 65, modifiers: 2 });
    await cdp.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Backspace', code: 'Backspace', windowsVirtualKeyCode: 8 });
    await cdp.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Backspace', code: 'Backspace', windowsVirtualKeyCode: 8 });
    await wait(250);
    await cdp.send('Input.insertText', { text: sku });
    await wait(2000 + attempt * 1000);

    const dbg = await cdp.evaluate(`(() => {
      const f = [...document.querySelectorAll('input')].find((e) => (e.placeholder || '').includes('Tìm sản phẩm'));
      return { val: f ? f.value : null, focused: document.activeElement === f };
    })()`);
    lastDebug = JSON.stringify(dbg);

    // Click gợi ý — quan trọng: danh sách có thể bị re-render nên phải lấy lại node NGAY tại thời điểm click
    await cdp.evaluate(`(() => {
      const items = [...document.querySelectorAll('div.d-flex.pt-1')].filter(
        (e) => e.offsetParent && (e.innerText || '').includes(${JSON.stringify(sku)})
      );
      if (items.length) items[0].click();
      return true;
    })()`);
    await wait(1500);

    // Xác minh bằng SỐ DÒNG tăng, không chỉ tin vào text: nếu không tăng nghĩa là click trượt
    const after = await cdp.evaluate('document.querySelectorAll("tbody tr").length');
    picked = after > before;
  }
  if (!picked) throw new Error(`không thêm được dòng cho ${sku} (ô tìm: ${lastDebug})`);

  // 3) set Giá + SL + Chiết khấu theo toạ độ x
  const res = await cdp.evaluate(`(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const rows = [...document.querySelectorAll('tbody tr')];
    const tr = rows[rows.length - 1];
    const ins = [...tr.querySelectorAll('input')].sort(
      (a, b) => a.getBoundingClientRect().x - b.getBoundingClientRect().x
    );
    if (ins.length < 4) return { ok: false, err: 'số input lạ: ' + ins.length };
    const [key, gia, sl, ck] = ins;
    const setNum = async (el, v) => {
      if (!el) return null;
      el.focus();
      if (el.hasAttribute('autonumeric')) {
        const dt = new DataTransfer();
        dt.setData('text/plain', String(v));
        el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
      } else {
        Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, String(v));
        for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
      }
      await wait(220);
      return el.value;
    };
    const g = await setNum(gia, ${price});
    const s = await setNum(sl, ${qty});
    await setNum(ck, 0);
    document.activeElement && document.activeElement.blur();
    await wait(300);
    return { ok: true, gia: g, sl: s, text: tr.innerText.replace(/\\s+/g, ' ').slice(-60), rowText: tr.innerText.replace(/\\s+/g, ' ') };
  })()`);
  if (!res?.ok) throw new Error(res?.err || 'set dòng thất bại');
  const norm = (s) => String(s || '').replace(/\s+/g, ' ').trim();
  const nameKey = norm(name).slice(0, 14);
  if (!norm(res.rowText).includes(sku) || !norm(res.rowText).includes(nameKey)) throw new Error('dòng thêm không đúng sản phẩm');
  return res;
}

let pending = [];   // các dòng đã thêm trong phiếu hiện tại
let current = 0;

async function flush() {
  const clicked = await cdp.evaluate(`(() => {
    const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /Lưu \\(F9\\)/.test(e.innerText));
    if (!b) return false;
    b.click();
    return true;
  })()`);
  if (!clicked) throw new Error('không thấy nút Lưu (F9)');
  await wait(6000);
  const url = await cdp.evaluate('location.href');
  const id = (url.match(/[?&]id=(\d+)/) || [])[1];
  if (!id) throw new Error(`lưu phiếu không thành công (url=${url})`);
  receiptNo++;
  state.receipts.push({ id: Number(id), at: new Date().toISOString(), lines: pending.length, skus: pending.map((x) => x.sku) });
  for (const x of pending) state.stocked[x.sku] = { receiptId: Number(id), qty: x.qty, price: x.price };
  fs.writeFileSync(STATE, JSON.stringify(state, null, 1), 'utf8');
  console.log(`  -> Phiếu #${id} lưu xong: ${pending.length} dòng`);
  pending = [];
}

await newReceipt();
for (const [i, p] of todo.entries()) {
  if (pending.length >= BATCH) {
    await flush();
    current = i;
  }
  const qty = qtyFor(p.sku);
  const price = Math.max(1000, Math.round((p.price * RATIO) / 500) * 500);
  try {
    const r = await addLine(p.sku, price, qty, p.name);
    pending.push({ sku: p.sku, qty, price });
    console.log(`  ${i + 1}/${todo.length} ${p.sku} SL=${qty} Giá=${price} -> ${r.text}`);

  } catch (e) {
    console.log(`  ${i + 1}/${todo.length} ERR ${p.sku} — ${e.message}`);
    // lưu những dòng đã thêm trước khi mở phiếu mới, tránh mất công
    try { if (pending.length) await flush(); } catch (e2) { console.log(`     ! flush lỗi: ${e2.message}`); }
    await newReceipt();
    pending = [];
  }
}
if (pending.length) await flush();

console.log(`\nXong. Phiếu: ${state.receipts.length}, sản phẩm đã có tồn: ${Object.keys(state.stocked).length}`);
process.exit(0);



#!/usr/bin/env node
// Tạo danh mục sản phẩm trong Nhanh.vn qua UI (Chrome CDP).
//
// Dùng: node tools/tao-danh-muc.mjs            -> tạo bộ 12 danh mục mặc định
//        node tools/tao-danh-muc.mjs --list-only  -> chỉ in danh sách sẽ tạo
//
// Mỗi danh mục: mở /product/category/add, điền Tên + Mã, bấm Lưu, xác minh.

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const BASE = 'https://nhanh.vn/product/category/add?businessId=230222';

// 12 nhóm phủ hết 45 danh mục nguồn của thucphamnhanh.com
const CATEGORIES = [
  { name: 'Thực phẩm chế biến', code: 'TP-CHE-BIEN' },
  { name: 'Thịt & Gia cầm', code: 'THIT-GIA-CAM' },
  { name: 'Thủy hải sản', code: 'THUY-HAI-SAN' },
  { name: 'Rau củ quả', code: 'RAU-CU-QUA' },
  { name: 'Gia vị & Nguyên liệu', code: 'GIA-VI' },
  { name: 'Lương thực & Gạo', code: 'LUONG-THUC' },
  { name: 'Đặc sản', code: 'DAC-SAN' },
  { name: 'Bánh kẹo & Mứt', code: 'BANH-KEO' },
  { name: 'Đồ uống & Trà', code: 'DO-UONG' },
  { name: 'Dụng cụ bếp & Bar', code: 'DUNG-CU' },
  { name: 'Thực phẩm tươi sống', code: 'TUOI-SONG' },
  { name: 'Sản phẩm khác', code: 'KHAC' },
];

if (process.argv.includes('--list-only')) {
  CATEGORIES.forEach((c, i) => console.log(`${i + 1}. ${c.name}  [${c.code}]`));
  process.exit(0);
}

class CDP {
  constructor(wsUrl) {
    this.id = 0;
    this.pending = new Map();
    this.ws = new WebSocket(wsUrl);
    this.ready = new Promise((resolve, reject) => {
      this.ws.onopen = () => resolve();
      this.ws.onerror = () => reject(new Error('không mở được WebSocket CDP'));
    });
    this.ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      const p = this.pending.get(m.id);
      if (p) { this.pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    };
  }
  async send(method, params = {}) {
    await this.ready;
    const id = ++this.id;
    return await new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }
  async evaluate(expression, timeoutMs = 45000) {
    const r = await Promise.race([
      this.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true }),
      new Promise((_, rej) => setTimeout(() => rej(new Error('CDP evaluate quá hạn')), timeoutMs)),
    ]);
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
    return r.result.value;
  }
}

const targets = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn'));
if (!tab) throw new Error('Không thấy tab nhanh.vn trong Chrome');
const cdp = new CDP(tab.webSocketDebuggerUrl);

const wait = (ms) => new Promise((r) => setTimeout(r, ms));

async function createOne(cat) {
  await cdp.send('Page.enable');
  await cdp.send('Page.navigate', { url: BASE });
  await wait(3000);

  const ready = await cdp.evaluate(`(async () => {
    for (let i = 0; i < 60; i++) {
      if (document.querySelectorAll('input').length > 5) return true;
      await new Promise((r) => setTimeout(r, 250));
    }
    return false;
  })()`);
  if (!ready) throw new Error('form danh mục chưa render');

  const filled = await cdp.evaluate(`(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const inputs = [...document.querySelectorAll('input')];
    const labelOf = (el) => {
      const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
      const l = box && box.querySelector('label');
      return (l ? l.innerText : '').replace(/\\*/g, '').replace(/\\s+/g, ' ').trim();
    };
    const byLabel = (t) => inputs.find((e) => labelOf(e) === t) || inputs.find((e) => labelOf(e).startsWith(t));
    const typeIn = async (el, v) => {
      if (!el) return false;
      el.focus();
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
      setter ? setter.call(el, String(v)) : (el.value = v);
      for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(80);
      return el.value;
    };
    const nameEl = byLabel('Tên');
    const codeEl = byLabel('Mã');
    const nameVal = await typeIn(nameEl, ${JSON.stringify(cat.name)});
    const codeVal = await typeIn(codeEl, ${JSON.stringify(cat.code)});
    return { name: nameVal, code: codeVal };
  })()`);

  if (!filled || !filled.name || !filled.code) throw new Error(`không điền được (${JSON.stringify(filled)})`);

  const clicked = await cdp.evaluate(`(() => {
    const b = [...document.querySelectorAll('button')].find((e) => (e.innerText || '').trim() === 'Lưu' && e.offsetParent);
    if (!b) return false;
    b.scrollIntoView({ block: 'center' });
    b.click();
    return true;
  })()`);
  if (!clicked) throw new Error('không thấy nút Lưu');
  await wait(3500);

  const after = await cdp.evaluate(`({ url: location.href, errs: [...document.querySelectorAll('.toast-error,.invalid-feedback,.text-danger')].map(e => e.innerText.trim()).filter(Boolean).slice(0,3) })`);
  return { filled, after };
}

const results = [];
for (const [i, cat] of CATEGORIES.entries()) {
  const t0 = Date.now();
  try {
    const r = await createOne(cat);
    const ok = !r.after.errs.length;
    results.push({ i: i + 1, name: cat.name, code: cat.code, ok, ms: Date.now() - t0, ...r });
    console.log(`${ok ? 'OK ' : 'ERR'} ${i + 1}/${CATEGORIES.length} ${cat.name} — ${r.after.url.split('?')[0]}`);
  } catch (e) {
    results.push({ i: i + 1, name: cat.name, code: cat.code, ok: false, error: e.message });
    console.log(`ERR ${i + 1}/${CATEGORIES.length} ${cat.name} — ${e.message}`);
  }
}

const okCount = results.filter((r) => r.ok).length;
console.log(`\nTạo thành công: ${okCount}/${CATEGORIES.length}`);
console.log(JSON.stringify(results.filter((r) => !r.ok), null, 2));
process.exit(0);

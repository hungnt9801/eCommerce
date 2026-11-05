#!/usr/bin/env node
// Nhập hàng loạt sản phẩm từ catalog WooCommerce vào Nhanh.vn qua UI (Chrome CDP).
//
// Dùng:
//   node tools/nhap-hang-loat.mjs --limit 20 --images 5 [--dry] [--start 0]
//
// - Chọn sản phẩm RẢI ĐỀU theo 12 nhóm đã tạo trong Nhanh (round-robin).
// - Bỏ qua sản phẩm giá = 0, không có tên, hoặc đã nhập trước đó (tools/_da-nhap.json).
// - Mỗi sản phẩm: điền cơ bản -> danh mục -> ảnh -> mô tả/meta -> Lưu -> xác minh.
//
// Kỹ thuật bắt buộc (đã kiểm chứng, đừng "tối ưu" lại):
//  1. Ô tiền/khối lượng (AutoNumeric): chỉ nhận qua ClipboardEvent('paste') + DataTransfer.
//  2. Ô text thường: native setter + input/change/blur.
//  3. Dropdown & tab của Angular: phải bắn chuỗi pointerdown/mousedown/pointerup/mouseup/click,
//     click chuột thật qua CDP cũng không mở (component nghe pointerdown trên input).
//  4. Editor là HugeRTE: window.hugerte.get(id).setContent(html) + .save().

import fs from 'node:fs';
import path from 'node:path';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const ADD_URL = 'https://nhanh.vn/product/item/add2?businessId=230222';
const STATE_FILE = 'tools/_da-nhap.json';

const argv = process.argv.slice(2);
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : null; };
const flag = (n) => argv.includes(`--${n}`);

const LIMIT = Number(arg('limit') || 20);
const IMAGES = Number(arg('images') || 5);
const DRY = flag('dry');
const START = Number(arg('start') || 0);
const onlySkus = arg('skus') ? arg('skus').split(',').map((s) => s.trim()).filter(Boolean) : null;

// ---------- map 45 danh mục nguồn -> 12 nhóm trong Nhanh ----------
const CAT_MAP = {
  'Thực phẩm chế biến': 'Thực phẩm chế biến',
  'Thịt chế biến': 'Thực phẩm chế biến',
  'Xúc xích': 'Thực phẩm chế biến',
  'Lạp xưởng': 'Thực phẩm chế biến',
  'Thịt viên': 'Thực phẩm chế biến',
  'Thực phẩm chế biến khác': 'Thực phẩm chế biến',
  'Thịt đóng hộp': 'Thực phẩm chế biến',
  'Thực phẩm đóng hộp': 'Thực phẩm chế biến',
  'Thủy Hải sản đóng hộp': 'Thực phẩm chế biến',
  'Rau củ chế biến': 'Thực phẩm chế biến',
  'Rau củ đóng hộp': 'Thực phẩm chế biến',
  'Thủy sản chế biến': 'Thực phẩm chế biến',
  'Thịt heo': 'Thịt & Gia cầm',
  'Thịt bò': 'Thịt & Gia cầm',
  'Thịt gia cầm': 'Thịt & Gia cầm',
  'Thịt nhập khẩu': 'Thịt & Gia cầm',
  'Trứng gia cầm': 'Thịt & Gia cầm',
  'Thủy hải sản': 'Thủy hải sản',
  'Rau, củ, quả': 'Rau củ quả',
  'Dạng lá': 'Rau củ quả',
  'Dạng quả (trái)': 'Rau củ quả',
  'Dạng củ': 'Rau củ quả',
  'Dạng hạt': 'Rau củ quả',
  'Dạng nấm': 'Rau củ quả',
  'Trái cây': 'Rau củ quả',
  'Rau, củ, quả khác': 'Rau củ quả',
  'Rau gia vị tây': 'Rau củ quả',
  'Gia vị': 'Gia vị & Nguyên liệu',
  'Thực phẩm gia vị': 'Gia vị & Nguyên liệu',
  'Nguyên liệu khô': 'Gia vị & Nguyên liệu',
  'Bột các loại': 'Gia vị & Nguyên liệu',
  'Lương thực': 'Lương thực & Gạo',
  'Gạo': 'Lương thực & Gạo',
  'Sợi các loại': 'Lương thực & Gạo',
  'Nui mì': 'Lương thực & Gạo',
  'Đặc sản': 'Đặc sản',
  'Đặc sản Miền Trung': 'Đặc sản',
  'Bánh các loại': 'Bánh kẹo & Mứt',
  'Bánh, kẹo, mứt, đồ ngọt': 'Bánh kẹo & Mứt',
  'Thức uống': 'Đồ uống & Trà',
  'Trà': 'Đồ uống & Trà',
  'Dụng cụ bếp': 'Dụng cụ bếp & Bar',
  'Dụng cụ bar': 'Dụng cụ bếp & Bar',
  'Thực phẩm tươi sống': 'Thực phẩm tươi sống',
  'Sản phẩm khác': 'Sản phẩm khác',
};
const GROUPS = ['Thực phẩm chế biến', 'Thịt & Gia cầm', 'Thủy hải sản', 'Rau củ quả', 'Gia vị & Nguyên liệu', 'Lương thực & Gạo', 'Đặc sản', 'Bánh kẹo & Mứt', 'Đồ uống & Trà', 'Dụng cụ bếp & Bar', 'Thực phẩm tươi sống', 'Sản phẩm khác'];

// ---------- chọn sản phẩm ----------
const catalog = JSON.parse(fs.readFileSync('tools/_woo-catalog.json', 'utf8'));
const state = fs.existsSync(STATE_FILE) ? JSON.parse(fs.readFileSync(STATE_FILE, 'utf8')) : { done: {} };

const groupOf = (p) => {
  // Ưu tiên danh mục ĐẶC THÙ hơn danh mục chung: sản phẩm hải sản thường được gắn
  // "Thực phẩm tươi sống" đứng trước "Thủy hải sản" trong danh sách -> nếu lấy danh mục
  // đầu tiên thì cả 156 SP hải sản rơi hết vào nhóm "Tươi sống" (đã từng xảy ra).
  if (p.categories.some((c) => c.name === 'Thủy hải sản')) return 'Thủy hải sản';
  for (const c of p.categories) if (CAT_MAP[c.name]) return CAT_MAP[c.name];
  return 'Sản phẩm khác';
};

const buckets = Object.fromEntries(GROUPS.map((g) => [g, []]));
for (const p of catalog.products) {
  if (!p.name || p.price == null || p.price <= 0) continue;
  if (state.done[p.sku]) continue;
  if (onlySkus && !onlySkus.includes(p.sku)) continue;
  buckets[groupOf(p)].push(p);
}
// trong mỗi nhóm: ưu tiên sản phẩm có nhiều ảnh, rồi tới giá
for (const g of GROUPS) buckets[g].sort((a, b) => b.images.length - a.images.length || b.price - a.price);

const picked = [];
let guard = 0;
while (picked.length < LIMIT + START && guard < 10000) {
  let added = false;
  for (const g of GROUPS) {
    if (picked.length >= LIMIT + START) break;
    const p = buckets[g].shift();
    if (p) {
      picked.push({ ...p, group: g });
      added = true;
    }
  }
  if (!added) break;
  guard++;
}
const queue = picked.slice(START);

// ---------- CDP ----------
class CDP {
  constructor(wsUrl) {
    this.id = 0;
    this.pending = new Map();
    this.ws = new WebSocket(wsUrl);
    this.ready = new Promise((res, rej) => { this.ws.onopen = () => res(); this.ws.onerror = () => rej(new Error('WS CDP lỗi')); });
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

// ---------- chạy ----------
const targets = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn'));
if (!tab) throw new Error('Không thấy tab nhanh.vn trong Chrome');
const cdp = new CDP(tab.webSocketDebuggerUrl);

// Chờ form THẬT SỰ sẵn sàng: phải thấy cả ô Tên sản phẩm và ô Giá bán lẻ.
// Chỉ đếm số input là không đủ — form render từng đợt, đếm được 13 input nhưng ô giá chưa có
// => paste() nhận null => sản phẩm được lưu với giá rỗng (đã từng xảy ra).
const READY_JS = `(async () => {
  const labelOf = (el) => {
    const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
    const l = box && box.querySelector('label');
    return (l ? l.innerText : '').replace(/\\*/g, '').replace(/\\s+/g, ' ').trim();
  };
  let stable = 0;
  let last = -1;
  for (let i = 0; i < 120; i++) {
    const inputs = [...document.querySelectorAll('input')];
    const labels = inputs.map(labelOf);
    const hasName = labels.includes('Tên sản phẩm');
    const hasPrice = labels.includes('Giá bán lẻ');
    const n = inputs.length;
    if (hasName && hasPrice) {
      if (n === last) stable++;
      last = n;
      if (stable >= 3) return { ok: true, inputs: n };
    } else {
      stable = 0;
    }
    await new Promise((r) => setTimeout(r, 300));
  }
  return { ok: false, inputs: document.querySelectorAll('input').length };
})()`;

function buildFillJs(p, weightGram, unit) {
  return `(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const all = [...document.querySelectorAll('input,select,textarea')];
    const out = { ok: true, errors: [] };
    const labelOf = (el) => {
      const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
      const l = box && box.querySelector('label');
      return (l ? l.innerText : '').replace(/\\*/g, '').replace(/\\s+/g, ' ').trim();
    };
    const byLabel = (t) => all.find((e) => labelOf(e) === t) || all.find((e) => labelOf(e).startsWith(t)) || null;
    const typeIn = async (el, v, name) => {
      if (!el) { out.errors.push('missing:' + name); return; }
      el.focus();
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
      setter ? setter.call(el, String(v)) : (el.value = String(v));
      for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(70);
    };
    const paste = async (el, v, name) => {
      if (!el) { out.errors.push('missing:' + name); return; }
      el.focus();
      const dt = new DataTransfer();
      dt.setData('text/plain', String(v));
      el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
      await wait(140);
    };

    await typeIn(byLabel('Tên sản phẩm'), ${JSON.stringify(p.name)}, 'name');
    await typeIn(byLabel('Mã'), ${JSON.stringify(p.sku)}, 'code');
    ${unit ? `await typeIn(byLabel('Đơn vị tính'), ${JSON.stringify(unit)}, 'unit');` : ''}
    await paste(byLabel('Giá bán lẻ'), ${JSON.stringify(String(p.price))}, 'price');
    ${weightGram ? `await paste(byLabel('Khối lượng'), ${JSON.stringify(String(weightGram))}, 'weight');` : ''}
    document.activeElement?.blur();
    await wait(250);

    out.values = {
      name: byLabel('Tên sản phẩm')?.value,
      code: byLabel('Mã')?.value,
      price: byLabel('Giá bán lẻ')?.value,
      weight: byLabel('Khối lượng')?.value,
      unit: byLabel('Đơn vị tính')?.value,
    };
    return out;
  })()`;
}

function buildCategoryJs(group) {
  return `(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const fire = (el) => {
      for (const t of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
        const C = t.startsWith('pointer') ? PointerEvent : MouseEvent;
        el.dispatchEvent(new C(t, { bubbles: true, cancelable: true, view: window, button: 0 }));
      }
    };
    const labelOf = (el) => {
      const box = el.closest('.form-floating') || el.parentElement;
      const l = box && box.querySelector('label');
      return (l ? l.innerText : '').replace(/\\*/g, '').replace(/\\s+/g, ' ').trim();
    };
    const field = [...document.querySelectorAll('input')].find((e) => labelOf(e) === 'Danh mục');
    if (!field) return { ok: false, err: 'no-category-field' };
    fire(field);
    await wait(700);
    const item = [...document.querySelectorAll('div.item')].find(
      (e) => e.offsetParent && e.innerText.trim() === ${JSON.stringify(group)}
    );
    if (!item) return { ok: false, err: 'no-option:' + ${JSON.stringify(group)} };
    fire(item);
    await wait(400);
    return { ok: true, value: field.value };
  })()`;
}

function buildExtrasJs(p, shortText) {
  return `(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const out = { ok: true, errors: [] };
    const fire = (el) => {
      for (const t of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
        const C = t.startsWith('pointer') ? PointerEvent : MouseEvent;
        el.dispatchEvent(new C(t, { bubbles: true, cancelable: true, view: window, button: 0 }));
      }
    };
    const tab = [...document.querySelectorAll('.nav-link')].find(
      (e) => e.innerText.trim() === 'Website' && e.getBoundingClientRect().y > 100
    );
    if (!tab) return { ok: false, err: 'no-website-tab' };
    fire(tab);
    await wait(1200);

    const inputs = [...document.querySelectorAll('input')];
    const labelOf = (el) => {
      const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
      const l = box && box.querySelector('label');
      return (l ? l.innerText : '').replace(/\\s+/g, ' ').trim();
    };
    const byLabel = (t) => inputs.find((e) => labelOf(e) === t) || inputs.find((e) => labelOf(e).startsWith(t)) || null;
    // SEO fields nằm ở layout ngang: label và input là 2 cột, label KHÔNG nằm trong box của input.
    // Nên tìm label theo text rồi đi lên tìm input trong cùng hàng.
    const byRow = (t) => {
      const leaf = [...document.querySelectorAll('label,div,span,td')].find(
        (e) => e.children.length === 0 && (e.innerText || '').trim() === t
      );
      if (!leaf) return null;
      let n = leaf;
      for (let d = 0; d < 4 && n; d++) {
        n = n.parentElement;
        const f = n && n.querySelector('input');
        if (f) return f;
      }
      return null;
    };
    const typeIn = async (el, v, name) => {
      if (!el) { out.errors.push('missing:' + name); return; }
      el.focus();
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
      setter ? setter.call(el, String(v)) : (el.value = String(v));
      for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(60);
    };

    await typeIn(byLabel('Tiêu đề trang (Meta title)') || byRow('Tiêu đề trang (Meta title)'), ${JSON.stringify(p.name)}, 'metaTitle');
    await typeIn(byLabel('Mô tả trang (Meta description)') || byRow('Mô tả trang (Meta description)'), ${JSON.stringify(shortText.slice(0, 158))}, 'metaDesc');

    const g = window.hugerte;
    if (!g || typeof g.get !== 'function') { out.errors.push('no-hugerte'); return out; }
    const setEd = (match, html, name) => {
      const f = [...document.querySelectorAll('iframe.tox-edit-area__iframe')].find((x) => {
        const c = x.closest('.card');
        return c && c.innerText.includes(match);
      });
      if (!f) { out.errors.push('no-iframe:' + name); return; }
      const ed = g.get(f.id.replace(/_ifr$/, ''));
      if (!ed) { out.errors.push('no-editor:' + name); return; }
      ed.setContent(html);
      ed.save();
      ed.fire('change');
    };
    setEd('Mô tả ngắn', ${JSON.stringify(p.shortDescription || '')}, 'short');
    setEd('Bài viết chi tiết', ${JSON.stringify(p.descriptionHtml || '')}, 'full');
    await wait(200);
    return out;
  })()`;
}

async function processOne(p, idx) {
  const t0 = Date.now();
  const weightMatch = p.name.match(/(\d+[.,]?\d*)\s*(kg|g|gam|gram)\b/i);
  let weightGram = null;
  if (weightMatch) {
    const n = Number(weightMatch[1].replace(',', '.'));
    weightGram = /kg/i.test(weightMatch[2]) ? Math.round(n * 1000) : Math.round(n);
    if (weightGram > 100000) weightGram = null;
  }
  const unitMatch = p.name.match(/\b(gói|hộp|chai|lọ|can|túi|thùng|hũ|bịch|combo)\b/i);
  const unit = unitMatch ? unitMatch[1].toLowerCase() : '';
  const shortText = (p.shortText || String(p.descriptionHtml || '').replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim()).slice(0, 300);

  await cdp.send('Page.navigate', { url: ADD_URL });
  await wait(1500);
  const ready = await cdp.evaluate(READY_JS);
  if (!ready.ok) throw new Error(`form chưa sẵn sàng (inputs=${ready.inputs})`);

  let basic = await cdp.evaluate(buildFillJs(p, weightGram, unit));
  if (!basic.values?.name || !basic.values?.price) {
    await wait(1500);
    basic = await cdp.evaluate(buildFillJs(p, weightGram, unit));
  }
  if (!basic.values?.name || !basic.values?.price) {
    throw new Error(`điền thiếu (name="${basic.values?.name}" price="${basic.values?.price}")`);
  }

  let category = await cdp.evaluate(buildCategoryJs(p.group));
  if (!category.ok) {
    await wait(800);
    category = await cdp.evaluate(buildCategoryJs(p.group));
  }
  if (!category.ok) throw new Error(`danh mục lỗi: ${category.err}`);

  // ảnh
  let uploaded = 0;
  if (IMAGES > 0 && p.images.length) {
    const tmpDir = path.resolve('tools/_anh-tam');
    fs.mkdirSync(tmpDir, { recursive: true });
    const files = [];
    for (const [i, src] of p.images.slice(0, IMAGES).entries()) {
      const ext = (src.match(/\.(png|jpe?g|webp)(\?|$)/i)?.[1] || 'jpg').toLowerCase();
      const file = path.join(tmpDir, `${p.sku.replace(/[^\w-]/g, '')}-${i + 1}.${ext}`);
      try {
        const res = await fetch(src, { headers: { 'user-agent': 'Mozilla/5.0 Chrome/156' }, signal: AbortSignal.timeout(30000) });
        if (!res.ok) continue;
        fs.writeFileSync(file, Buffer.from(await res.arrayBuffer()));
        files.push(file);
      } catch { /* ảnh lỗi: bỏ qua */ }
    }
    if (files.length) {
      await cdp.send('DOM.enable');
      const { root } = await cdp.send('DOM.getDocument', { depth: -1 });
      const { nodeIds } = await cdp.send('DOM.querySelectorAll', { nodeId: root.nodeId, selector: 'input[type=file]' });
      if (nodeIds.length) {
        await cdp.send('DOM.setFileInputFiles', { files, nodeId: nodeIds[0] });
        uploaded = files.length;
        await wait(2500 + files.length * 500);
      }
    }
  }

  const extras = await cdp.evaluate(buildExtrasJs(p, shortText));

  if (!DRY) {
    // Chọn "Danh sách sản phẩm" trước khi lưu: mặc định là "Thêm mới" nên app Ở LẠI form add2,
    // làm bước xác minh "đã lưu" không phân biệt được với "chưa lưu" (đã từng gây nhập trùng).
    await cdp.evaluate(`(() => {
      const r = document.querySelector('#radioproductList');
      if (!r) return false;
      r.scrollIntoView({ block: 'center' });
      r.click();
      return true;
    })()`);
    await wait(400);

    const clicked = await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find(
        (e) => e.className.includes('btn-add-new') || /^(Lưu|Cập nhật)$/.test((e.innerText || '').trim())
      );
      if (!b) return false;
      b.scrollIntoView({ block: 'center' });
      b.click();
      return true;
    })()`);
    if (!clicked) throw new Error('không thấy nút Lưu');
    await wait(4000);
  }

  const urlAfter = await cdp.evaluate('location.href');
  const saved = !DRY && urlAfter.includes('/product/item/index');
  let saveErrors = [];
  if (!DRY && !saved) {
    try {
      saveErrors = await cdp.evaluate(`[...document.querySelectorAll('.invalid-feedback,.toast-error,.text-danger,.toast-body')]
        .map((e) => (e.innerText || '').trim()).filter(Boolean).slice(0, 5)`);
    } catch { /* ignore */ }
  }
  return { sku: p.sku, name: p.name, group: p.group, price: p.price, basic, category, uploaded, extras, saved, saveErrors, urlAfter, ms: Date.now() - t0 };
}

console.log(`Nhập ${queue.length} sản phẩm (rải đều ${GROUPS.length} nhóm)${DRY ? ' [DRY]' : ''}`);
const results = [];
for (const [i, p] of queue.entries()) {
  try {
    const r = await processOne(p, i);
    results.push(r);
    const bad = [...(r.basic?.errors || []), ...(r.extras?.errors || []), ...(r.category?.ok ? [] : [r.category?.err]), ...(r.saveErrors || [])].filter(Boolean);
    console.log(`${String(i + 1).padStart(2)}/${queue.length} ${r.saved || DRY ? 'OK ' : '?? '} [${r.group}] ${p.sku} — ${p.name.slice(0, 40)} | ảnh:${r.uploaded} | ${(r.ms / 1000).toFixed(1)}s${bad.length ? ' | LỖI: ' + bad.join(', ') : ''}`);
    if (r.saved) {
      state.done[p.sku] = { at: new Date().toISOString(), name: p.name, group: p.group };
      fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 1), 'utf8');
    }
  } catch (e) {
    results.push({ sku: p.sku, name: p.name, error: e.message });
    console.log(`${String(i + 1).padStart(2)}/${queue.length} ERR ${p.sku} — ${e.message}`);
  }
  await wait(800);
}

const ok = results.filter((r) => r.saved || (DRY && !r.error)).length;
console.log(`\nXong: ${ok}/${queue.length}${DRY ? ' (dry, không lưu)' : ''}`);
process.exit(0);

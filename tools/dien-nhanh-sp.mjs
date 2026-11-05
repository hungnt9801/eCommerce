#!/usr/bin/env node
// Điền 1 sản phẩm (từ catalog WooCommerce) vào form Nhanh.vn đang mở trong Chrome CDP.
//
// Dùng:
//   node tools/dien-nhanh-sp.mjs --catalog tools/_woo-catalog.json --sku CB010CT0005 \
//        [--images 5] [--extras] [--save] [--shot tools/_dien.png]
//
// --extras : điền tab Website (Meta title/description, Mô tả ngắn, Bài viết chi tiết)
// --save   : bấm nút "Lưu" (mặc định KHÔNG lưu)
//
// Bài học kỹ thuật (đừng đổi nếu chưa đọc):
//  - Ô tiền/khối lượng là AutoNumeric: CHỈ nhận giá trị qua ClipboardEvent('paste') + DataTransfer.
//    Gán .value, Input.insertText, gõ phím đều bị Angular ghi đè về rỗng khi blur.
//  - Ô text thường thì ngược lại: phải dùng native setter + input/change.
//  - Tra field theo NHÃN (form-floating label), không theo index: form render lúc 25 lúc 34 input.
//  - Editor là HugeRTE (bản fork TinyMCE): window.hugerte.get(<id>).setContent(html) rồi .save().

import fs from 'node:fs';
import path from 'node:path';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const argv = process.argv.slice(2);
const arg = (name) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 ? argv[i + 1] : null;
};
const flag = (name) => argv.includes(`--${name}`);

const catalogPath = arg('catalog') || 'tools/_woo-catalog.json';
const sku = arg('sku');
const shotPath = arg('shot');
const imageCount = Number(arg('images') || 0);
const doExtras = flag('extras');
const doSave = flag('save');
const extrasOnly = flag('extras-only');

if (!sku) {
  console.error('Thiếu --sku');
  process.exit(2);
}

const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));
const product = catalog.products.find((p) => p.sku === sku);
if (!product) {
  console.error(`Không thấy SKU ${sku} trong ${catalogPath}`);
  process.exit(2);
}

// ---- suy ra vài trường từ tên ----
const name = product.name.replace(/\s+/g, ' ').trim();
const weightMatch = name.match(/(\d+[.,]?\d*)\s*(kg|g|gam|gram)\b/i);
let weightGram = null;
if (weightMatch) {
  const n = Number(weightMatch[1].replace(',', '.'));
  weightGram = /kg/i.test(weightMatch[2]) ? Math.round(n * 1000) : Math.round(n);
  if (weightGram > 100000) weightGram = null; // "500G" trong tên hộp, không phải khối lượng vận chuyển
}
const unitMatch = name.match(/\b(gói|hộp|chai|lọ|can|túi|thùng|hũ|bịch|combo)\b/i);
const unit = unitMatch ? unitMatch[1].toLowerCase() : '';

const stripTags = (s) => String(s || '').replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
const shortText = (product.shortText || stripTags(product.descriptionHtml)).slice(0, 300);

// ---- CDP ----
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
    // Chặn treo vô hạn: nếu page có dialog chặn JS hoặc target chết, promise sẽ không bao giờ resolve
    const r = await Promise.race([
      this.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true }),
      new Promise((_, reject) => setTimeout(() => reject(new Error('CDP evaluate quá hạn (page bị chặn?)')), timeoutMs)),
    ]);
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
    return r.result.value;
  }
}

const targets = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/list`)).json();
const tab = targets.find((t) => t.type === 'page' && t.url.includes('nhanh.vn/product/item/'));
if (!tab) throw new Error('Không thấy tab sản phẩm (add2/detail) trong Chrome.');

const cdp = new CDP(tab.webSocketDebuggerUrl);

const readyCount = await cdp.evaluate(`(async () => {
  for (let i = 0; i < 80; i++) {
    const n = document.querySelectorAll('input').length;
    if (n > 12) return n;
    await new Promise((r) => setTimeout(r, 250));
  }
  return document.querySelectorAll('input').length;
})()`);
if (readyCount <= 12) throw new Error(`form chưa render xong (chỉ có ${readyCount} input)`);

// ---- B1: thông tin cơ bản ----
const FILL_JS = `(async () => {
  const all = [...document.querySelectorAll('input,select,textarea')];
  const report = { set: {}, missing: [] };
  const wait = (ms) => new Promise((r) => setTimeout(r, ms));
  const labelOf = (el) => {
    const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
    const l = box && box.querySelector('label');
    return (l ? l.innerText : '').replace(/\\*/g, '').replace(/\\s+/g, ' ').trim();
  };
  const byLabel = (txt) => all.find((e) => labelOf(e) === txt) || all.find((e) => labelOf(e).startsWith(txt)) || null;

  const typeIn = async (el, value, label) => {
    if (!el) { report.missing.push(label); return; }
    el.focus();
    const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
    setter ? setter.call(el, String(value)) : (el.value = String(value));
    for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
    await wait(80);
    report.set[label] = el.value;
  };
  const paste = async (el, value, label) => {
    if (!el) { report.missing.push(label); return; }
    el.focus();
    const dt = new DataTransfer();
    dt.setData('text/plain', String(value));
    el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
    await wait(150);
    report.set[label] = el.value;
  };

  await typeIn(byLabel('Tên sản phẩm'), ${JSON.stringify(name)}, 'Tên sản phẩm');
  await typeIn(byLabel('Mã'), ${JSON.stringify(product.sku)}, 'Mã');
  await typeIn(byLabel('Đơn vị tính'), ${JSON.stringify(unit)}, 'Đơn vị tính');
  await paste(byLabel('Giá bán lẻ'), ${JSON.stringify(String(product.price ?? ''))}, 'Giá bán lẻ');
  ${weightGram ? `await paste(byLabel('Khối lượng'), ${JSON.stringify(String(weightGram))}, 'Khối lượng');` : ''}

  document.activeElement?.blur();
  await wait(300);
  report.afterBlur = {
    name: byLabel('Tên sản phẩm')?.value,
    code: byLabel('Mã')?.value,
    unit: byLabel('Đơn vị tính')?.value,
    price: byLabel('Giá bán lẻ')?.value,
    weight: byLabel('Khối lượng')?.value,
  };
  return report;
})()`;
const basic = extrasOnly ? { skipped: true } : await cdp.evaluate(FILL_JS);

// ---- B2: ảnh ----
let uploaded = 0;
if (!extrasOnly && imageCount > 0 && product.images.length) {
  const tmpDir = path.resolve('tools/_anh-tam');
  fs.mkdirSync(tmpDir, { recursive: true });
  const files = [];
  for (const [i, src] of product.images.slice(0, imageCount).entries()) {
    const ext = (src.match(/\.(png|jpe?g|webp)(\?|$)/i)?.[1] || 'jpg').toLowerCase();
    const file = path.join(tmpDir, `${product.sku.replace(/[^\w-]/g, '')}-${i + 1}.${ext}`);
    const res = await fetch(src, { headers: { 'user-agent': 'Mozilla/5.0 Chrome/156' }, signal: AbortSignal.timeout(30000) });
    if (!res.ok) continue;
    fs.writeFileSync(file, Buffer.from(await res.arrayBuffer()));
    files.push(file);
  }
  if (files.length) {
    await cdp.send('DOM.enable');
    const { root } = await cdp.send('DOM.getDocument', { depth: -1 });
    const { nodeIds } = await cdp.send('DOM.querySelectorAll', { nodeId: root.nodeId, selector: 'input[type=file]' });
    if (!nodeIds.length) throw new Error('không thấy input[type=file]');
    await cdp.send('DOM.setFileInputFiles', { files, nodeId: nodeIds[0] });
    uploaded = files.length;
    await new Promise((r) => setTimeout(r, 2500 + files.length * 600));
  }
}

// ---- B3: tab Website ----
let extras = null;
if (doExtras) {
  // Tab thật nằm ở hàng tab (y>100). Menu điều hướng cũng có chữ "Website" nhưng y=0,
  // click vào đó sẽ điều hướng sang trang khác -> phải lọc theo toạ độ.
  const switched = await cdp.evaluate(`(() => {
    const t = [...document.querySelectorAll('.nav-link')].find(
      (e) => e.innerText.trim() === 'Website' && e.getBoundingClientRect().y > 100
    );
    if (!t) return false;
    t.click();
    return true;
  })()`);
  await new Promise((r) => setTimeout(r, 1500));
  const activeTab = await cdp.evaluate(`(() => {
    const a = [...document.querySelectorAll('.nav-link.active')].find((e) => e.getBoundingClientRect().y > 100);
    return a ? a.innerText.trim() : null;
  })()`);
  if (!switched || activeTab !== 'Website') {
    throw new Error(`không mở được tab Website (active=${activeTab})`);
  }

  extras = await cdp.evaluate(`(async () => {
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const out = { meta: {}, editors: {}, missing: [] };
    const inputs = [...document.querySelectorAll('input')];
    const labelOf = (el) => {
      const box = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
      const l = box && box.querySelector('label');
      return (l ? l.innerText : '').replace(/\\s+/g, ' ').trim();
    };
    const byLabel = (txt) => inputs.find((e) => labelOf(e) === txt) || inputs.find((e) => labelOf(e).startsWith(txt)) || null;
    const typeIn = async (el, value, label) => {
      if (!el) { out.missing.push(label); return; }
      el.focus();
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
      setter ? setter.call(el, String(value)) : (el.value = String(value));
      for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
      await wait(60);
      out.meta[label] = el.value;
    };
    const paste = async (el, value, label) => {
      if (!el) { out.missing.push(label); return; }
      el.focus();
      const dt = new DataTransfer();
      dt.setData('text/plain', String(value));
      el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
      await wait(120);
      out.meta[label] = el.value;
    };

    await typeIn(byLabel('Tiêu đề trang (Meta title)'), ${JSON.stringify(name)}, 'Meta title');
    await typeIn(byLabel('Mô tả trang (Meta description)'), ${JSON.stringify(shortText.slice(0, 160))}, 'Meta description');
    ${product.onSale && product.regularPrice ? `await paste(byLabel('Giá cũ'), ${JSON.stringify(String(product.regularPrice))}, 'Giá cũ');` : ''}

    const g = window.hugerte;
    if (g && typeof g.get === 'function') {
      const setEd = (match, html, label) => {
        const f = [...document.querySelectorAll('iframe.tox-edit-area__iframe')].find((x) => {
          const c = x.closest('.card');
          return c && c.innerText.includes(match);
        });
        if (!f) { out.missing.push('editor ' + match); return; }
        const ed = g.get(f.id.replace(/_ifr$/, ''));
        if (!ed) { out.missing.push('hugerte ' + match); return; }
        ed.setContent(html);
        ed.save();
        ed.fire('change');
        out.editors[label] = (ed.getContent() || '').length;
      };
      setEd('Mô tả ngắn', ${JSON.stringify(product.shortDescription || '')}, 'Mô tả ngắn');
      setEd('Bài viết chi tiết', ${JSON.stringify(product.descriptionHtml || '')}, 'Bài viết chi tiết');
    } else {
      out.missing.push('window.hugerte');
    }
    return out;
  })()`);
}

// ---- B4: lưu ----
let saved = false;
if (doSave) {
  saved = await cdp.evaluate(`(() => {
    const b = [...document.querySelectorAll('button')].find(
      (e) => e.className.includes('btn-add-new') || /^(Lưu|Cập nhật)$/.test((e.innerText || '').trim())
    );
    if (!b) return false;
    b.scrollIntoView({ block: 'center' });
    b.click();
    return true;
  })()`);
  await new Promise((r) => setTimeout(r, 4000));
}

// ---- B5: ảnh chụp ----
if (shotPath) {
  await cdp.send('Page.enable');
  const { data } = await cdp.send('Page.captureScreenshot', { format: 'png' });
  fs.writeFileSync(shotPath, Buffer.from(data, 'base64'));
}

const afterUrl = await cdp.evaluate('location.href');
console.log(JSON.stringify({
  sku: product.sku,
  name,
  price: product.price,
  inferred: { weightGram, unit },
  basic,
  uploadedImages: uploaded,
  extras,
  saveClicked: saved,
  urlAfterSave: afterUrl,
  screenshot: shotPath || null,
}, null, 2));

process.exit(0);

#!/usr/bin/env node
// Trích xuất thông tin sản phẩm từ 1 URL bán hàng -> JSON chuẩn hoá.
//
// Dùng: node tools/trich-xuat-san-pham.mjs <url> [--out file.json] [--no-browser] [--browser]
//   --no-browser  chỉ tải HTML tĩnh (nhanh, nhưng site Next.js/Vue sẽ thiếu giá)
//   --browser     luôn render bằng Chrome CDP (dùng profile đang đăng nhập nhanh.vn)
//
// Mặc định: tải tĩnh trước; nếu thiếu giá/tên thì tự render lại bằng Chrome qua CDP :9222.
// Ưu tiên dữ liệu: JSON-LD schema.org/Product -> OpenGraph/product meta -> itemprop.

const argv = process.argv.slice(2);
const url = argv.find((a) => !a.startsWith('--'));
const outIdx = argv.indexOf('--out');
const outFile = outIdx >= 0 ? argv[outIdx + 1] : null;
const forceBrowser = argv.includes('--browser');
const noBrowser = argv.includes('--no-browser');
const CDP_PORT = Number(process.env.CDP_PORT || 9222);

if (!url) {
  console.error('Dùng: node tools/trich-xuat-san-pham.mjs <url> [--out file.json] [--no-browser] [--browser]');
  process.exit(2);
}

const UA =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36';

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };
const decode = (s) =>
  String(s || '')
    .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(parseInt(d, 10)))
    .replace(/&([a-z#0-9]+);/gi, (m, k) => ENTITIES[k.toLowerCase()] ?? m);

const stripTags = (s) => decode(String(s || '').replace(/<[^>]*>/g, ' ')).replace(/\s+/g, ' ').trim();

function meta(html, key) {
  const esc = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const patterns = [
    new RegExp(`<meta[^>]+(?:property|name)=["']${esc}["'][^>]*content=["']([^"']*)["']`, 'i'),
    new RegExp(`<meta[^>]+content=["']([^"']*)["'][^>]*(?:property|name)=["']${esc}["']`, 'i'),
  ];
  for (const re of patterns) {
    const m = html.match(re);
    if (m) return decode(m[1]).trim();
  }
  return '';
}

function jsonLdBlocks(html) {
  const out = [];
  const re = /<script[^>]+type=["']application\/ld\+json["'][^>]*>([\s\S]*?)<\/script>/gi;
  let m;
  while ((m = re.exec(html))) {
    const raw = m[1].trim().replace(/^<!--/, '').replace(/-->$/, '');
    try {
      out.push(JSON.parse(raw));
    } catch {
      try { out.push(JSON.parse(raw.replace(/,\s*([}\]])/g, '$1'))); } catch { /* block hỏng: bỏ */ }
    }
  }
  return out;
}

function flattenLd(node, acc = []) {
  if (!node) return acc;
  if (Array.isArray(node)) { node.forEach((n) => flattenLd(n, acc)); return acc; }
  if (typeof node === 'object') {
    acc.push(node);
    if (node['@graph']) flattenLd(node['@graph'], acc);
  }
  return acc;
}

const isProduct = (n) => [].concat(n['@type'] || []).some((x) => String(x).toLowerCase() === 'product');
const asArray = (v) =>
  (v ? (Array.isArray(v) ? v : [v]) : [])
    .map((x) => (typeof x === 'string' ? x : x?.url || x?.contentUrl || ''))
    .filter(Boolean);

/** Số kiểu VN: "1.299.000₫" | "1,299,000đ" | "1299000.00" -> number */
function parseMoney(text) {
  if (text == null) return null;
  let s = String(text).replace(/[^\d.,]/g, '');
  if (!s) return null;
  const hasComma = s.includes(',');
  const hasDot = s.includes('.');
  if (hasComma && hasDot) {
    // dấu xuất hiện sau cùng là dấu thập phân
    s = s.lastIndexOf(',') > s.lastIndexOf('.') ? s.replace(/\./g, '').replace(',', '.') : s.replace(/,/g, '');
  } else if (hasComma) {
    s = /,\d{3}(\D|$)/.test(s) || (s.match(/,/g) || []).length > 1 ? s.replace(/,/g, '') : s.replace(',', '.');
  } else if (hasDot) {
    s = /\.\d{3}(\D|$)/.test(s) || (s.match(/\./g) || []).length > 1 ? s.replace(/\./g, '') : s;
  }
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

async function fetchText(u) {
  const res = await fetch(u, {
    headers: { 'user-agent': UA, 'accept-language': 'vi,en;q=0.8', accept: 'text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8' },
    redirect: 'follow',
    signal: AbortSignal.timeout(25000),
  });
  return { res, text: await res.text() };
}

// ---------- CDP (Chrome đang mở, profile có phiên đăng nhập) ----------
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
      let m;
      try { m = JSON.parse(ev.data); } catch { return; }
      const p = this.pending.get(m.id);
      if (p) {
        this.pending.delete(m.id);
        m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result);
      }
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
  async evaluate(expression) {
    const r = await this.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.text || 'lỗi JS');
    return r.result.value;
  }
  close() {
    try { this.ws.onclose = null; this.ws.onerror = null; if (this.ws.readyState === 1) this.ws.close(); } catch { /* ignore */ }
  }
}

const COLLECT_JS = `(() => {
  const metas = [...document.querySelectorAll('meta[property],meta[name]')]
    .map(m => [m.getAttribute('property') || m.getAttribute('name'), m.getAttribute('content') || ''])
    .filter(([k, v]) => k && v);
  const lds = [...document.querySelectorAll('script[type="application/ld+json"]')].map(s => s.textContent || '');
  const priceTexts = [...document.querySelectorAll('[class*="price" i],[id*="price" i],[itemprop="price"],[data-price]')]
    .map(e => (e.innerText || e.getAttribute('content') || e.getAttribute('data-price') || '').trim())
    .filter(t => t && /\\d/.test(t)).slice(0, 20);
  const h1 = document.querySelector('h1')?.innerText?.trim() || '';
  const full = document.body?.innerText || '';
  const moneyRe = /\\d{1,3}(?:[.,]\\d{3}){1,3}\\s*(?:₫|đ|vnd|vnđ)/gi;
  // Cửa sổ 1200 ký tự ngay sau tiêu đề sản phẩm: nơi giá thường nằm
  const anchor = h1 ? full.indexOf(h1) : -1;
  const near = anchor >= 0 ? full.slice(anchor, anchor + 1200) : full.slice(0, 1200);
  const moneyNear = (near.match(moneyRe) || []).slice(0, 4);
  return { title: document.title, url: location.href, h1, metas, lds, priceTexts, moneyNear };
})()`;

async function browserSnapshot(target) {
  const res = await fetch(`http://127.0.0.1:${CDP_PORT}/json/new?${encodeURIComponent(target)}`, { method: 'PUT' });
  if (!res.ok) throw new Error(`mở tab CDP thất bại (HTTP ${res.status})`);
  const tab = await res.json();
  const cdp = new CDP(tab.webSocketDebuggerUrl);
  try {
    await cdp.send('Page.enable');
    await cdp.send('Page.navigate', { url: target });
    const deadline = Date.now() + 30000;
    while (Date.now() < deadline) {
      await new Promise((r) => setTimeout(r, 500));
      try {
        const st = await cdp.evaluate('document.readyState');
        if (st === 'complete') break;
      } catch { /* tab đang chuyển trang */ }
    }
    await new Promise((r) => setTimeout(r, 2500)); // chờ hydrate/RSC chèn JSON-LD
    return await cdp.evaluate(COLLECT_JS);
  } finally {
    cdp.close();
    try { await fetch(`http://127.0.0.1:${CDP_PORT}/json/close/${tab.id}`); } catch { /* ignore */ }
  }
}

async function cdpAlive() {
  try {
    const r = await fetch(`http://127.0.0.1:${CDP_PORT}/json/version`, { signal: AbortSignal.timeout(1500) });
    return r.ok;
  } catch { return false; }
}

// ---------- Chuẩn hoá ----------
function applyHtml(html, result) {
  const product = jsonLdBlocks(html).flatMap((b) => flattenLd(b)).find(isProduct);
  if (product) {
    result.method = result.method || 'json-ld';
    if (!result.name) result.name = decode(product.name || '');
    if (!result.sku) result.sku = String(product.sku || product.mpn || '');
    if (!result.barcode) result.barcode = String(product.gtin13 || product.gtin || product.gtin12 || '');
    if (!result.brand) result.brand = typeof product.brand === 'object' ? decode(product.brand?.name || '') : decode(product.brand || '');
    if (!result.description) result.description = product.description || '';
    if (!result.images.length) result.images = asArray(product.image);
    const offer = [].concat(product.offers || [])[0] || {};
    if (result.price == null && offer.price != null) result.price = Number(offer.price);
    if (result.oldPrice == null && offer.highPrice != null) result.oldPrice = Number(offer.highPrice);
    if (!result.currency) result.currency = offer.priceCurrency || '';
    if (result.inStock == null && offer.availability) result.inStock = /InStock/i.test(offer.availability);
    for (const k of ['color', 'material', 'size', 'pattern']) {
      if (product[k]) result.attributes[k] = String(product[k]);
    }
  }

  if (!result.name) {
    const t = html.match(/<title[^>]*>([\s\S]*?)<\/title>/i);
    result.name = stripTags(meta(html, 'og:title') || (t && t[1]) || '');
    result.method = result.method || 'og-meta';
  }
  if (!result.description) result.description = meta(html, 'og:description') || meta(html, 'description');
  if (result.price == null) {
    const p = meta(html, 'product:price:amount') || meta(html, 'og:price:amount');
    if (p) result.price = parseMoney(p);
  }
  if (!result.currency) result.currency = meta(html, 'product:price:currency') || meta(html, 'og:price:currency') || '';
  if (!result.images.length) {
    const og = [meta(html, 'og:image'), meta(html, 'og:image:secure_url')].filter(Boolean);
    result.images = [...new Set(og)];
  }
}

async function main() {
  const result = {
    sourceUrl: url,
    extractedAt: new Date().toISOString(),
    method: null,
    name: '',
    sku: '',
    barcode: '',
    brand: '',
    price: null,
    oldPrice: null,
    currency: '',
    inStock: null,
    description: '',
    shortDescription: '',
    images: [],
    attributes: {},
    variants: [],
    warnings: [],
  };

  // Shopify JSON: đầy đủ biến thể + giá, không cần render
  const shopMatch = url.match(/^(https?:\/\/[^/]+)\/(?:[a-z-]+\/)?products\/([^/?#]+)/i);
  if (shopMatch) {
    try {
      const { res, text } = await fetchText(`${shopMatch[1]}/products/${shopMatch[2]}.json`);
      if (res.ok) {
        const p = JSON.parse(text).product;
        result.method = 'shopify-json';
        result.name = p.title || '';
        result.description = p.body_html || '';
        result.brand = p.vendor || '';
        result.images = (p.images || []).map((i) => i.src);
        result.variants = (p.variants || []).map((v) => ({
          title: v.title, sku: v.sku, barcode: v.barcode, price: Number(v.price),
          compareAtPrice: v.compare_at_price ? Number(v.compare_at_price) : null,
          weightGram: v.grams, available: !!v.available,
        }));
        const v0 = result.variants[0];
        if (v0) {
          result.price = v0.price;
          result.oldPrice = v0.compareAtPrice;
          result.sku = v0.sku || '';
          result.inStock = v0.available;
        }
        result.currency = 'VND';
      }
    } catch (e) { result.warnings.push(`shopify-json: ${e.message}`); }
  }

  if (!forceBrowser) {
    try {
      const { res, text } = await fetchText(url);
      if (!res.ok) result.warnings.push(`HTTP ${res.status} khi tải trang`);
      applyHtml(text, result);
    } catch (e) { result.warnings.push(`fetch: ${e.message}`); }
  }

  const needRender = forceBrowser || !result.price || !result.name || result.name.length < 3;
  if (needRender && !noBrowser && (await cdpAlive())) {
    try {
      const snap = await browserSnapshot(url);
      const syntheticHtml =
        snap.metas.map(([k, v]) => `<meta property="${k}" content="${String(v).replace(/"/g, '&quot;')}">`).join('') +
        snap.lds.map((s) => `<script type="application/ld+json">${s}</script>`).join('');
      applyHtml(syntheticHtml, result);
      if (snap.h1 && (!result.name || result.name.length < 3)) result.name = decode(snap.h1);
      if (!result.name) result.name = decode(snap.title || '');
      if (result.price == null && snap.moneyNear?.length) {
        // Heuristic: 2 mốc tiền đầu tiên ngay dưới tiêu đề -> giá bán = số nhỏ, giá gốc = số lớn
        const vals = snap.moneyNear.map(parseMoney).filter((n) => n && n >= 1000);
        if (vals.length) {
          result.price = Math.min(vals[0], vals[1] ?? vals[0]);
          const hi = Math.max(vals[0], vals[1] ?? vals[0]);
          if (hi > result.price) result.oldPrice = hi;
          result.warnings.push('giá đọc bằng heuristic trên trang render — cần bạn xác nhận');
        }
      }
      if (result.price == null) {
        const hit = snap.priceTexts.map(parseMoney).find((n) => n && n >= 1000);
        if (hit) result.price = hit;
      }
      result.method = `${result.method || 'dom'}+render`;
    } catch (e) {
      result.warnings.push(`render: ${e.message}`);
    }
  }

  if (result.description && /<[a-z][\s\S]*>/i.test(result.description)) result.descriptionHtml = result.description;
  if (!result.shortDescription) result.shortDescription = stripTags(result.description).slice(0, 400);
  result.name = (result.name || '').slice(0, 255);

  const json = JSON.stringify(result, null, 2);
  if (outFile) {
    const fs = await import('node:fs');
    fs.writeFileSync(outFile, json, 'utf8');
  }
  console.log(json);
}

main().catch((e) => {
  console.error(`LỖI: ${e.message}`);
  process.exit(1);
});

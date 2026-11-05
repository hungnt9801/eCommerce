#!/usr/bin/env node
// Lấy toàn bộ catalog từ 1 shop WooCommerce qua Store API công khai.
//
// Dùng: node tools/lay-catalog-woo.mjs [baseUrl] [--out file.json] [--pages N]
// Mặc định baseUrl = https://thucphamnhanh.com
//
// Endpoint dùng: /wp-json/wc/store/products (và /products/categories).
// Không cần key vì đây là Store API công khai; nếu shop khoá thì script báo lỗi rõ ràng.

const argv = process.argv.slice(2);
const base = (argv.find((a) => !a.startsWith('--') && /^https?:\/\//.test(a)) || 'https://thucphamnhanh.com').replace(/\/+$/, '');
const outIdx = argv.indexOf('--out');
const outFile = outIdx >= 0 ? argv[outIdx + 1] : 'tools/_woo-catalog.json';
const pagesIdx = argv.indexOf('--pages');
const maxPages = pagesIdx >= 0 ? Number(argv[pagesIdx + 1]) : 0;

const UA =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36';
const PER_PAGE = 100;

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };
const decode = (s) =>
  String(s ?? '')
    .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(parseInt(d, 10)))
    .replace(/&([a-z#0-9]+);/gi, (m, k) => ENTITIES[k.toLowerCase()] ?? m);

const stripTags = (s) => decode(String(s || '').replace(/<[^>]*>/g, ' ')).replace(/\s+/g, ' ').trim();

async function getJson(path) {
  const res = await fetch(`${base}${path}`, {
    headers: { 'user-agent': UA, accept: 'application/json' },
    signal: AbortSignal.timeout(30000),
  });
  if (!res.ok) throw new Error(`${path} -> HTTP ${res.status}`);
  return { json: await res.json(), headers: res.headers };
}

function normalize(p) {
  const pr = p.prices || {};
  const minor = Number(pr.currency_minor_unit ?? 0);
  const scale = 10 ** (Number.isFinite(minor) ? minor : 0);
  const num = (v) => (v == null || v === '' ? null : Number(v) / scale);
  const price = num(pr.price);
  const regular = num(pr.regular_price);
  return {
    sourceId: p.id,
    source: base,
    name: decode(p.name || ''),
    sku: decode(p.sku || ''),
    type: p.type,
    permalink: p.permalink,
    price,
    regularPrice: regular,
    onSale: !!p.on_sale && regular != null && price != null && regular > price,
    currency: pr.currency_code || 'VND',
    shortDescription: decode(p.short_description || ''),
    descriptionHtml: p.description || '',
    shortText: stripTags(p.short_description || '').slice(0, 300),
    images: (p.images || []).map((i) => i.src).filter(Boolean),
    categories: (p.categories || []).map((c) => ({ id: c.id, name: decode(c.name), slug: c.slug })),
    attributes: (p.attributes || []).map((a) => ({
      name: decode(a.name),
      terms: (a.terms || []).map((t) => decode(t.name)),
    })),
    variationIds: p.variations || [],
    inStock: !!p.is_in_stock,
    stockText: p.stock_availability?.text ? stripTags(p.stock_availability.text) : '',
  };
}

async function main() {
  console.log(`Nguồn: ${base}`);

  let cats = [];
  try {
    const { json } = await getJson('/wp-json/wc/store/products/categories?per_page=100');
    cats = json.map((c) => ({ id: c.id, name: decode(c.name), slug: c.slug, count: c.count, parent: c.parent }));
    console.log(`Danh mục: ${cats.length}`);
  } catch (e) {
    console.warn(`! không lấy được danh mục: ${e.message}`);
  }

  const first = await getJson(`/wp-json/wc/store/products?per_page=${PER_PAGE}&page=1`);
  const total = Number(first.headers.get('x-wp-total') || first.json.length);
  const totalPages = Number(first.headers.get('x-wp-totalpages') || 1);
  const pagesToGet = maxPages ? Math.min(maxPages, totalPages) : totalPages;
  console.log(`Sản phẩm: ${total} (${totalPages} trang @${PER_PAGE}) — lấy ${pagesToGet} trang`);

  const products = first.json.map(normalize);
  for (let page = 2; page <= pagesToGet; page++) {
    const { json } = await getJson(`/wp-json/wc/store/products?per_page=${PER_PAGE}&page=${page}`);
    products.push(...json.map(normalize));
    process.stdout.write(`\r  trang ${page}/${pagesToGet} — ${products.length} sản phẩm`);
    await new Promise((r) => setTimeout(r, 250)); // lịch sự với server
  }
  process.stdout.write('\n');

  const withPrice = products.filter((p) => p.price != null && p.price > 0);
  const prices = withPrice.map((p) => p.price).sort((a, b) => a - b);
  const summary = {
    base,
    pulledAt: new Date().toISOString(),
    totalReported: total,
    totalPulled: products.length,
    categories: cats,
    stats: {
      withPrice: withPrice.length,
      noPrice: products.length - withPrice.length,
      noImage: products.filter((p) => !p.images.length).length,
      noSku: products.filter((p) => !p.sku).length,
      variable: products.filter((p) => p.type === 'variable').length,
      onSale: products.filter((p) => p.onSale).length,
      priceMin: prices[0] ?? null,
      priceMax: prices[prices.length - 1] ?? null,
    },
    products,
  };

  const fs = await import('node:fs');
  fs.writeFileSync(outFile, JSON.stringify(summary, null, 1), 'utf8');
  console.log(`Đã ghi: ${outFile}`);
  console.log(JSON.stringify(summary.stats, null, 2));
  console.log('\n5 sản phẩm đầu:');
  for (const p of products.slice(0, 5)) {
    console.log(`- [${p.sku || 'no-sku'}] ${p.name} | ${p.price}${p.onSale ? ` (giá gốc ${p.regularPrice})` : ''} | ${p.images.length} ảnh | ${p.categories.map((c) => c.name).join(' / ')}`);
  }
}

main().catch((e) => {
  console.error(`LỖI: ${e.message}`);
  process.exit(1);
});

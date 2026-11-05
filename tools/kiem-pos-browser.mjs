#!/usr/bin/env node
// 🔎 KIỂM BROWSER THẬT cho **POS (`WP-06`)** — những thứ **chỉ** đo được trong trình duyệt:
//   ① tab hoá đơn lưu **IndexedDB** ⇒ **F5 KHÔNG mất tab** (`04 §6.3`);
//   ② phím tắt **F1** mở tab mới, **F9** lưu;
//   ③ **rớt mạng vẫn lưu**: F9 khi offline ⇒ hoá đơn vào **outbox**, mạng lại ⇒ **Đồng bộ** tạo hoá đơn thật.
//
// 📌 Chạy sau khi `pnpm build` (Next `start` ở 3000) và Host ở 54275 — `tools/kiem-pos-browser.ps1` lo phần
// khởi động Chrome + Next + Host. Dùng **cầu CDP tối giản** (không thư viện) như `nhanh-cdp.mjs`.
//
// ENV: CDP_PORT (9222) · WEB (http://localhost:3000) · BUSINESS (230222) · ADMIN_PASS (1q2w3E*)

const PORT = Number(process.env.CDP_PORT || 9222);
const WEB = process.env.WEB || 'http://localhost:3000';
const BUSINESS = process.env.BUSINESS || '230222';
const PASS = process.env.ADMIN_PASS || '1q2w3E*';

const canhBao = [];
const ketQua = [];

function ghi(nhan, ok, chiTiet) {
  ketQua.push({ nhan, ok, chiTiet });
  console.log(`${ok ? '✅' : '❌'} ${nhan}${chiTiet ? ` — ${chiTiet}` : ''}`);
  if (!ok) canhBao.push(`${nhan}${chiTiet ? ` (${chiTiet})` : ''}`);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function listTargets() {
  const r = await fetch(`http://127.0.0.1:${PORT}/json/list`);
  if (!r.ok) throw new Error(`CDP /json/list -> HTTP ${r.status}`);
  return await r.json();
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
      let m;
      try {
        m = JSON.parse(ev.data);
      } catch {
        return;
      }
      if (m.id && this.pending.has(m.id)) {
        const { resolve, reject } = this.pending.get(m.id);
        this.pending.delete(m.id);
        if (m.error) reject(new Error(`${m.error.message} ${m.error.data ?? ''}`.trim()));
        else resolve(m.result);
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

  async eval(expression) {
    const r = await this.send('Runtime.evaluate', {
      expression,
      awaitPromise: true,
      returnByValue: true,
      userGesture: true,
    });
    if (r.exceptionDetails) {
      throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
    }
    return r.result?.value;
  }

  async goto(url) {
    await this.send('Page.navigate', { url });
    await sleep(1500);
    await this.cho(() => document.readyState === 'complete' || document.readyState === 'interactive', 15000);
  }

  /** Chờ tới khi biểu thức JS trả **truthy** (poll 250 ms) — `goto` một mình KHÔNG đủ. */
  async cho(ham, timeoutMs = 10000) {
    const batDau = Date.now();
    let cuoi = null;

    while (Date.now() - batDau < timeoutMs) {
      try {
        const kq = await this.eval(`(${ham.toString()})()`);
        cuoi = kq;
        if (kq) return kq;
      } catch (e) {
        cuoi = e.message;
      }
      await sleep(250);
    }

    throw new Error(`CHỜ QUÁ ${timeoutMs}ms — kết quả cuối: ${JSON.stringify(cuoi)}`);
  }

  close() {
    try {
      this.ws.close();
    } catch {}
  }
}

async function main() {
  const targets = await listTargets();
  const page = targets.find((t) => t.type === 'page');

  if (!page) throw new Error('Chrome KHÔNG có tab nào — mở một tab trắng trước.');

  const cdp = new CDP(page.webSocketDebuggerUrl);
  await cdp.send('Page.enable');
  await cdp.send('Runtime.enable');
  await cdp.send('Network.enable');

  // ── Đăng nhập: form thật (server action) — nếu hydration chưa xong thì fallback **cùng endpoint**
  // NextAuth credentials (đúng thứ form gửi đi). 📌 Cần fallback vì `requestSubmit()` trước khi React
  // hydrate sẽ submit **native** (không có action) ⇒ quay lại `/login`.
  await cdp.goto(`${WEB}/login`);
  await sleep(600);

  // 📌 `/login` tự chuyển hướng khi **đã** có phiên ⇒ nhánh đó coi như đã đăng nhập (không phải lỗi).
  let urlSauDangNhap = await cdp.eval('location.pathname');

  if (String(urlSauDangNhap).startsWith('/login')) {
    await cdp.cho(() => !!document.querySelector('input[name=username]'), 20000);
    await sleep(1200);
    await cdp.eval(`
      (() => {
        const u = document.querySelector('input[name=username]');
        const p = document.querySelector('input[name=password]');
        u.value = 'admin@abp.io';
        p.value = ${JSON.stringify(PASS)};
        document.querySelector('form').requestSubmit();
        return true;
      })()
    `);
    await sleep(3000);

    urlSauDangNhap = await cdp.eval('location.pathname');
  }

  if (String(urlSauDangNhap).startsWith('/login')) {
    await cdp.eval(`
      (async () => {
        const csrf = await (await fetch('/api/auth/csrf')).json();
        const than = new URLSearchParams({
          csrfToken: csrf.csrfToken,
          username: 'admin@abp.io',
          password: ${JSON.stringify(PASS)},
          callbackUrl: location.origin + '/${BUSINESS}',
        });
        await fetch('/api/auth/callback/credentials', {
          method: 'POST',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          body: than,
        });
        return true;
      })()
    `);
    await sleep(1500);
    await cdp.goto(`${WEB}/${BUSINESS}`);
    urlSauDangNhap = await cdp.eval('location.pathname');
  }

  ghi('Đăng nhập NextAuth và vào được app', !String(urlSauDangNhap).startsWith('/login'), `path=${urlSauDangNhap}`);

  // ── SETUP qua BFF (dùng đúng phiên đăng nhập của trình duyệt) ────────────────────────────────
  const setup = await cdp.eval(`
    (async () => {
      const goi = async (duong, than) => {
        const r = await fetch(duong, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(than),
        });
        const body = await r.json().catch(() => null);
        if (!r.ok) return { ok: false, status: r.status, body };
        return { ok: true, status: r.status, body };
      };

      const ran = Math.floor(Math.random() * 900000 + 100000);

      const kho = await goi('/api/proxy/app/warehouse', { code: 'WP06B-' + ran, name: 'Quầy browser ' + ran });
      if (!kho.ok) return { loi: 'warehouse', chiTiet: kho };

      // Phải có SẢN PHẨM thật: ô tìm của POS tra /api/v1/products (SKU do BE cấp) — dùng SKU tự bịa
      // thì ô tìm **không ra gì** và bài kiểm "thêm dòng" sẽ xanh **giả** (đã dặp: innerText chứa SKU chỉ
      // vì dòng *"Không thấy sản phẩm …"* cũng in SKU ra).
      const tao = await goi('/api/proxy/v1/products', {
        code: 'WP06B-' + ran,
        name: 'SP POS ' + ran,
        units: [{ name: 'Cái', conversionFactor: 1, isBase: true, salePrice: 25000 }],
      });
      if (!tao.ok) return { loi: 'product', chiTiet: tao };

      // SKU của mô hình Nhanh nằm ở BIẾN THỂ (CatProductVariants.Sku) — ĐVT (CatProductUnits)
      // KHÔNG có cột SKU. Tạo sản phẩm xong phải thêm MỘT biến thể thì /api/v1/products mới trả sku.
      const bienThe = await goi('/api/proxy/app/product/variant/' + tao.body.id, {
        sku: 'WP06B-SKU-' + ran,
        price: 25000,
        barcode: '893' + ran + '01',
      });
      if (!bienThe.ok) return { loi: 'variant', chiTiet: bienThe };

      const chiTiet = await (await fetch('/api/proxy/v1/products/' + tao.body.id)).json();
      const sku = chiTiet.sku;
      if (!sku) return { loi: 'sku-rong', chiTiet };

      const phieu = await goi('/api/proxy/v1/stock/bills', {
        number: 'PN-WP06B-' + ran,
        type: 0,
        mode: 5,
        warehouseId: kho.body.id,
        idempotencyKey: 'WP06B-NHAP-' + ran,
        lines: [{ sku, quantity: 5, unitPrice: 25000 }],
      });
      if (!phieu.ok) return { loi: 'stock-doc', chiTiet: phieu };

      const ghiSo = await goi('/api/proxy/v1/stock/bills/' + phieu.body.id + '/post', {});
      if (!ghiSo.ok) return { loi: 'post', chiTiet: ghiSo };

      const ca = await goi('/api/proxy/v1/pos/sessions/open', { warehouseId: kho.body.id, openingCash: 300000 });
      if (!ca.ok) return { loi: 'session', chiTiet: ca };

      return { khoId: kho.body.id, sku, caId: ca.body.id, ran, tenSP: 'SP POS ' + ran };
    })()
  `);

  if (!setup || setup.loi) {
    throw new Error(`SETUP qua BFF hỏng ở bước ${setup?.loi}: ${JSON.stringify(setup?.chiTiet)}`);
  }
  console.log(`   (setup: kho ${setup.khoId} · SKU ${setup.sku} · ca ${setup.caId})`);

  // ── ① Mở màn POS ─────────────────────────────────────────────────────────────────────────────
  await cdp.goto(`${WEB}/${BUSINESS}/pos/bill?warehouseId=${setup.khoId}`);
  await sleep(1500);

  const trangPos = await cdp.eval('document.body.innerText');
  ghi(
    '① Trang POS render (có ca đang mở)',
    String(trangPos).includes('Bán lẻ tại quầy') && String(trangPos).includes('POS-CA'),
    String(trangPos).split('\n')[0],
  );

  // ── ② Quét/gõ sản phẩm ⇒ thêm dòng (dùng setter native để React nhận `input`) ───────────────
  // 📌 Gõ **TÊN sản phẩm** (không phải SKU): ô tìm gọi `/api/v1/products?q=` — tìm theo mã/tên/SKU, nhưng
  // chỉ **tên** là chắc chắn khớp (SKU của mô hình Nhanh nằm ở biến thể, không phải trường tìm chính).
  const themDong = await cdp.eval(`
    (async () => {
      const o = document.querySelector('input[placeholder*="Quét mã vạch"]');
      if (!o) return { loi: 'khong-co-o-tim' };

      const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
      setter.call(o, ${JSON.stringify(setup.tenSP)});
      o.dispatchEvent(new Event('input', { bubbles: true }));

      await new Promise((r) => setTimeout(r, 900));

      // Enter chọn kết quả ĐẦU TIÊN (đúng hành vi máy quét: gõ mã + Enter).
      o.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));

      await new Promise((r) => setTimeout(r, 700));

      return { soDong: document.querySelectorAll('[data-pos-dong]').length };
    })()
  `);

  ghi(
    '② Gõ tên SP + Enter ⇒ thêm ĐÚNG 1 dòng vào hoá đơn',
    Number(themDong?.soDong) === 1,
    `số dòng = ${themDong?.soDong ?? themDong?.loi}`,
  );

  // ── ②b Thêm thanh toán TIỀN MẶT đủ số còn lại (nếu không, F9 sẽ bị `Pos:0006` — đúng luật:
  //        hoá đơn còn NỢ phải có khách + hạn. 📌 Đây là hành vi ĐÚNG, đã đo được ở lượt chạy trước.) ──
  const themTienMat = await cdp.eval(`
    (() => {
      const nut = Array.from(document.querySelectorAll('button')).find((b) =>
        (b.getAttribute('title') || '').startsWith('Thêm Tiền mặt'),
      );
      if (!nut) return { loi: 'khong-co-nut-tien-mat' };
      nut.click();
      return { ok: true };
    })()
  `);
  await sleep(600);
  ghi('②b Thêm thanh toán TIỀN MẶT đủ số còn lại', !!themTienMat?.ok, themTienMat?.loi ?? '');

  // ⚠️ Bắn keydown **một lần** trên `window`: listener nằm ở `window`, bắn thêm trên `body` sẽ **bubble 2 lần**
  // ⇒ mở 2 tab (đã đo: "số tab = 3" ở lượt chạy đầu).
  await cdp.eval(`window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F1', bubbles: true })); true`);
  await sleep(500);

  const soTab = await cdp.eval(`document.querySelectorAll('[data-pos-tab]').length`);
  ghi('③ F1 mở tab hoá đơn thứ hai', Number(soTab) >= 2, `số tab = ${soTab}`);

  // ── ④ F5 (reload) KHÔNG mất tab — đọc lại từ IndexedDB ──────────────────────────────────────
  await cdp.send('Page.reload');
  await sleep(2500);

  const soTabSauF5 = await cdp.eval(`document.querySelectorAll('[data-pos-tab]').length`);
  ghi(
    '④ F5 (reload) ⇒ tab hoá đơn KHÔNG mất (IndexedDB)',
    Number(soTabSauF5) >= 2,
    `số tab sau F5 = ${soTabSauF5}`,
  );

  // ── ⑤ Rớt mạng ⇒ F9 vẫn LƯU (vào outbox) ───────────────────────────────────────────────────
  // Chọn **tab 1** (tab có dòng hàng — tab 2 do F1 tạo là tab rỗng), rồi ngắt mạng.
  await cdp.eval(`(() => { const t = document.querySelector('[data-pos-tab="0"]'); if (t) t.click(); return !!t; })()`);
  await sleep(600);

  const coDongHang = await cdp.eval(`document.querySelectorAll('[data-pos-dong]').length`);
  ghi('⑤a Tab 1 còn ĐÚNG 1 dòng hàng sau khi F5', Number(coDongHang) === 1, `số dòng = ${coDongHang}`);

  await cdp.send('Network.emulateNetworkConditions', {
    offline: true,
    latency: 0,
    downloadThroughput: 0,
    uploadThroughput: 0,
  });

  await cdp.eval(`window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F9', bubbles: true })); true`);
  await sleep(2500);

  const sauOffline = await cdp.eval('document.body.innerText');
  ghi(
    '⑤ Rớt mạng + F9 ⇒ hoá đơn vào OUTBOX (báo "MẤT MẠNG")',
    String(sauOffline).includes('MẤT MẠNG') || String(sauOffline).includes('Đồng bộ'),
    String(sauOffline).split('\n').find((x) => x.includes('MẤT MẠNG') || x.includes('Đồng bộ')) ?? '',
  );

  // ── ⑥ Có mạng lại ⇒ "Đồng bộ" tạo hoá đơn THẬT (idempotent theo ClientBillId) ──────────────
  await cdp.send('Network.emulateNetworkConditions', {
    offline: false,
    latency: 0,
    downloadThroughput: -1,
    uploadThroughput: -1,
  });
  // 🚨 `Network.disable` KHÔNG luôn khôi phục kết nối đã bị chặn ⇒ **tải lại trang** sau khi có mạng.
  // 📌 Đây cũng là hành vi thật của thu ngân (mạng lại → F5 → Đồng bộ); outbox nằm ở IndexedDB nên không mất.
  await cdp.send('Network.disable');
  await cdp.send('Page.reload');
  await sleep(2500);
  await cdp.cho(() => !!document.querySelector('[data-pos-dong-bo]'), 15000);

  const dongBo = await cdp.eval(`
    (async () => {
      const nut = document.querySelector('[data-pos-dong-bo]');
      if (!nut) return { loi: 'khong-co-nut-dong-bo' };

      nut.click();
      await new Promise((r) => setTimeout(r, 3000));

      return { text: document.body.innerText };
    })()
  `);

  const dongBoXong = String(dongBo?.text ?? '').split('\n').find((x) => x.includes('Đồng bộ xong')) ?? '';
  const soThanhCong = Number((dongBoXong.match(/(\d+) thành công/) ?? [])[1] ?? 0);

  ghi(
    '⑥ Mạng lại + "Đồng bộ" ⇒ hoá đơn được tạo trên SERVER',
    soThanhCong >= 1,
    dongBoXong || dongBo?.loi,
  );

  if (soThanhCong < 1) {
    console.log('   (debug: các dòng có chữ "lỗi"/"Đồng bộ":)');
    for (const d of String(dongBo?.text ?? '').split('\n').filter((x) => /lỗi|Đồng bộ|MẤT MẠNG/i.test(x))) {
      console.log('     ·', d);
    }
  }

  const soHoaDon = await cdp.eval(`
    (async () => {
      const r = await fetch('/api/proxy/v1/pos/bills?maxResultCount=50');
      const b = await r.json();
      return (b.items || []).filter((x) => x.warehouseId === ${JSON.stringify(setup.khoId)}).length;
    })()
  `);

  ghi('⑦ Server có ĐÚNG 1 hoá đơn cho quầy này (không tạo trùng)', Number(soHoaDon) === 1, `số hoá đơn = ${soHoaDon}`);

  try {
    const shot = await cdp.send('Page.captureScreenshot', { format: 'png' });
    const fs = await import('node:fs');
    const tep = new URL('./_pos-browser.png', import.meta.url);
    fs.writeFileSync(tep, Buffer.from(shot.data, 'base64'));
    console.log(`   (ảnh màn hình: ${tep.pathname})`);
  } catch {
    /* ảnh chỉ để người đọc xem, không phải điều kiện */
  }

  cdp.close();

  console.log('');
  if (canhBao.length === 0) {
    console.log('KET LUAN: TAT CA CA BROWSER DEU KHOP.');
    process.exit(0);
  }

  console.log('KET LUAN: CO CA KHONG KHOP:');
  for (const x of canhBao) console.log(`  - ${x}`);
  process.exit(1);
}

main().catch((e) => {
  console.error(`❌ LỖI: ${e.message}`);
  process.exit(2);
});

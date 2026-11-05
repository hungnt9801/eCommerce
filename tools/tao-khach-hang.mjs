#!/usr/bin/env node
// Tạo khách hàng trong Nhanh.vn qua UI (Chrome CDP).
//
// Dùng: node tools/tao-khach-hang.mjs [--start 0] [--limit N]
// Ghi id đã tạo vào tools/_khach-hang.json để dùng cho bước tạo đơn.
//
// Lưu ý: form khách hàng KHÔNG có AutoNumeric nên chỉ cần native setter.
// Nút Lưu phải click bằng JS (click chuột thật vào split-button dễ trúng mũi tên).

import fs from 'node:fs';

const CDP_PORT = Number(process.env.CDP_PORT || 9222);
const ADD_URL = 'https://nhanh.vn/customer/code/add?businessId=230222';
const OUT = 'tools/_khach-hang.json';

const argv = process.argv.slice(2);
const arg = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : null; };
const START = Number(arg('start') || 0);
const LIMIT = Number(arg('limit') || 999);

// 20 khách, tên và địa chỉ thật (Bến Tre), SĐT không trùng
const CUSTOMERS = [
  { name: 'Nguyễn Thị Hồng Nhung', phone: '0987311245', addr: 'Ấp 2, Xã Phú Hưng, Huyện Châu Thành, Bến Tre' },
  { name: 'Trần Văn Minh', phone: '0908452371', addr: 'Ấp 4, Xã Sơn Đông, Thành phố Bến Tre, Bến Tre' },
  { name: 'Lê Thị Kim Chi', phone: '0913265847', addr: 'Số 18, Đường Nguyễn Thị Định, Phường Phú Khương, Bến Tre' },
  { name: 'Phạm Quốc Cường', phone: '0974112365', addr: 'Ấp 1, Xã Tân Thạch, Huyện Châu Thành, Bến Tre' },
  { name: 'Huỳnh Ngọc Mai', phone: '0938251476', addr: 'Ấp 5, Xã Mỹ Thạnh An, Thành phố Bến Tre, Bến Tre' },
  { name: 'Võ Thành Đạt', phone: '0907329841', addr: 'Số 45, Đường Đồng Khởi, Phường An Hội, Bến Tre' },
  { name: 'Đặng Thị Thu Hà', phone: '0981523674', addr: 'Ấp 3, Xã Phước Mỹ Trung, Huyện Mỏ Cày Bắc, Bến Tre' },
  { name: 'Bùi Văn Tân', phone: '0918774523', addr: 'Ấp 6, Xã Hữu Định, Huyện Châu Thành, Bến Tre' },
  { name: 'Ngô Thị Bích Trâm', phone: '0965338127', addr: 'Số 92, Đường 30/4, Phường 3, Bến Tre' },
  { name: 'Trương Công Lý', phone: '0932116754', addr: 'Ấp 2, Xã Long Hòa, Huyện Bình Đại, Bến Tre' },
  { name: 'Lâm Thị Diễm', phone: '0903788451', addr: 'Ấp 7, Xã Thới Thuận, Huyện Bình Đại, Bến Tre' },
  { name: 'Đỗ Minh Tuấn', phone: '0978461327', addr: 'Ấp 1, Xã An Phước, Huyện Châu Thành, Bến Tre' },
  { name: 'Nguyễn Văn Hậu', phone: '0912365847', addr: 'Số 7, Đường Trần Quốc Tuấn, Phường 4, Bến Tre' },
  { name: 'Trần Thị Mỹ Duyên', phone: '0989231475', addr: 'Ấp 4, Xã Vĩnh Thành, Huyện Chợ Lách, Bến Tre' },
  { name: 'Cao Thị Thanh Tuyền', phone: '0937445261', addr: 'Ấp 8, Xã Tân Phú Tây, Huyện Mỏ Cày Bắc, Bến Tre' },
  { name: 'Dương Quốc Huy', phone: '0906558741', addr: 'Ấp 3, Xã Phú Nhuận, Thành phố Bến Tre, Bến Tre' },
  { name: 'Mai Thị Ánh Tuyết', phone: '0977123458', addr: 'Số 121, Đường Nguyễn Huệ, Phường 2, Bến Tre' },
  { name: 'Hồ Văn Phúc', phone: '0914523678', addr: 'Ấp 5, Xã Tam Hiệp, Huyện Bình Đại, Bến Tre' },
  { name: 'Lý Thị Kim Ngân', phone: '0963451287', addr: 'Ấp 2, Xã Thạnh Trị, Huyện Thạnh Phú, Bến Tre' },
  { name: 'Tạ Minh Khoa', phone: '0938874912', addr: 'Ấp 9, Xã Quới Điền, Huyện Thạnh Phú, Bến Tre' },
];

class CDP {
  constructor(wsUrl) {
    this.id = 0;
    this.pending = new Map();
    this.ws = new WebSocket(wsUrl);
    this.ready = new Promise((res, rej) => { this.ws.onopen = () => res(); this.ws.onerror = () => rej(new Error('WS lỗi')); });
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

const state = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : { customers: {} };
// khách đã tạo tay ở bước thử
if (!state.customers['0987311245']) {
  state.customers['0987311245'] = { id: 1, name: 'Nguyễn Thị Hồng Nhung', addedAt: new Date().toISOString() };
  fs.writeFileSync(OUT, JSON.stringify(state, null, 1), 'utf8');
}

const todo = CUSTOMERS.slice(START).filter((c) => !state.customers[c.phone]).slice(0, LIMIT);
console.log(`Tạo ${todo.length} khách hàng`);

for (const [i, c] of todo.entries()) {
  try {
    await cdp.send('Page.navigate', { url: ADD_URL });
    await wait(1800);
    const ok = await cdp.evaluate(`(async () => {
      for (let i = 0; i < 60; i++) {
        if ([...document.querySelectorAll('input')].some((e) => (e.placeholder || '') === 'Họ và tên')) return true;
        await new Promise((r) => setTimeout(r, 250));
      }
      return false;
    })()`);
    if (!ok) throw new Error('form chưa render');

    const filled = await cdp.evaluate(`(async () => {
      const wait = (ms) => new Promise((r) => setTimeout(r, ms));
      const all = [...document.querySelectorAll('input,textarea')];
      const lab = (el) => {
        const b = el.closest('.form-floating') || el.closest('.form-group') || el.parentElement;
        const l = b && b.querySelector('label');
        return l ? l.innerText.replace(/\\s+/g, ' ').trim() : '';
      };
      const by = (t) => all.find((e) => lab(e).startsWith(t));
      const type = async (el, v) => {
        if (!el) return null;
        el.focus();
        const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
        Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, String(v));
        for (const ev of ['input', 'change', 'blur']) el.dispatchEvent(new Event(ev, { bubbles: true }));
        await wait(100);
        return el.value;
      };
      const name = await type(by('Họ và tên'), ${JSON.stringify(c.name)});
      const phone = await type(by('Số điện thoại'), ${JSON.stringify(c.phone)});
      const addr = await type(all.find((e) => e.tagName === 'TEXTAREA' && lab(e).startsWith('Địa chỉ')), ${JSON.stringify(c.addr)});
      return { name, phone, addr };
    })()`);
    if (filled?.phone !== c.phone) throw new Error(`điền lỗi: ${JSON.stringify(filled)}`);

    const clicked = await cdp.evaluate(`(() => {
      const b = [...document.querySelectorAll('button')].find((e) => e.offsetParent && /^Lưu/.test((e.innerText || '').trim()));
      if (!b) return false;
      b.click();
      return true;
    })()`);
    if (!clicked) throw new Error('không thấy nút Lưu');
    await wait(3500);

    const url = await cdp.evaluate('location.href');
    const id = (url.match(/[?&]id=(\d+)/) || [])[1];
    if (!id) throw new Error(`không lấy được id (url=${url})`);

    state.customers[c.phone] = { id: Number(id), name: c.name, addr: c.addr, addedAt: new Date().toISOString() };
    fs.writeFileSync(OUT, JSON.stringify(state, null, 1), 'utf8');
    console.log(`  ${i + 1}/${todo.length} OK id=${id} ${c.name} — ${c.phone}`);
  } catch (e) {
    console.log(`  ${i + 1}/${todo.length} ERR ${c.name} — ${e.message}`);
  }
  await wait(600);
}

console.log(`\nTổng khách đã tạo: ${Object.keys(state.customers).length}`);
process.exit(0);

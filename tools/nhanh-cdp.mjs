#!/usr/bin/env node
// Cầu CDP tối giản (không phụ thuộc thư viện) để lái Chrome đang đăng nhập nhanh.vn.
// Dùng: node tools/nhanh-cdp.mjs <lenh> [tham so...]
//   targets                      liệt kê tab
//   url                          URL tab đang chọn
//   eval <js>                    chạy JS trong tab, in JSON kết quả
//   goto <url>                   điều hướng
//   shot <file.png>              chụp màn hình
//   fill <selector> <text>       điền input/textarea (bắn input+change, hợp với Vue/React)
//   click <selector>             click phần tử
//   text <selector>              innerText của phần tử
//   cookie [domain]              liệt kê cookie (mặc định .nhanh.vn)
// ENV: CDP_PORT (9222), CDP_MATCH (nhanh.vn)

const PORT = Number(process.env.CDP_PORT || 9222);
const MATCH = process.env.CDP_MATCH || 'nhanh.vn';

async function listTargets() {
  const r = await fetch(`http://127.0.0.1:${PORT}/json/list`);
  if (!r.ok) throw new Error(`CDP /json/list -> HTTP ${r.status}`);
  return await r.json();
}

function pickTarget(targets) {
  const pages = targets.filter((t) => t.type === 'page');
  return pages.find((t) => (t.url || '').includes(MATCH)) || pages[0] || null;
}

class CDP {
  constructor(wsUrl) {
    this.id = 0;
    this.pending = new Map();
    this.ws = new WebSocket(wsUrl);
    this.ready = new Promise((resolve, reject) => {
      this.ws.onopen = () => resolve();
      this.ws.onerror = () => reject(new Error('không mở được WebSocket CDP'));
    });    this.ws.onmessage = (ev) => {
      let m;
      try { m = JSON.parse(ev.data); } catch { return; }
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
  close() {
    try {
      this.ws.onerror = null;
      this.ws.onclose = null;
      this.ws.onmessage = null;
      if (this.ws.readyState === 1) this.ws.close();
    } catch { /* ignore */ }
  }
}

async function evaluate(cdp, expression) {
  const res = await cdp.send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
    userGesture: true,
  });
  if (res.exceptionDetails) {
    const d = res.exceptionDetails;
    throw new Error(d.exception?.description || d.text || 'lỗi JS');
  }
  return res.result.value;
}

const js = {
  fill: (sel, text) => `(() => {
    const el = document.querySelector(${JSON.stringify(sel)});
    if (!el) return { ok: false, err: 'không thấy ' + ${JSON.stringify(sel)} };
    el.focus();
    const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
    setter ? setter.call(el, ${JSON.stringify(text)}) : (el.value = ${JSON.stringify(text)});
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
    return { ok: true, value: el.value };
  })()`,
  click: (sel) => `(() => {
    const el = document.querySelector(${JSON.stringify(sel)});
    if (!el) return { ok: false, err: 'không thấy ' + ${JSON.stringify(sel)} };
    el.scrollIntoView({ block: 'center' });
    el.click();
    return { ok: true, tag: el.tagName, text: (el.innerText || '').slice(0, 80) };
  })()`,
  text: (sel) => `(() => {
    const el = document.querySelector(${JSON.stringify(sel)});
    return el ? { ok: true, text: el.innerText } : { ok: false, err: 'không thấy ' + ${JSON.stringify(sel)} };
  })()`,
};

async function main() {
  const [cmd, ...rest] = process.argv.slice(2);
  const targets = await listTargets();
  const target = pickTarget(targets);
  if (!target) throw new Error('không có tab nào trong Chrome CDP');

  if (cmd === 'targets') {
    console.log(JSON.stringify(targets.map((t) => ({ type: t.type, title: t.title, url: t.url })), null, 2));
    return;
  }

  const cdp = new CDP(target.webSocketDebuggerUrl);
  try {
    let out;
    switch (cmd) {
      case 'url':
        out = { title: target.title, url: target.url };
        break;
      case 'eval':
        out = await evaluate(cdp, rest.join(' '));
        break;
      case 'goto':
        await cdp.send('Page.enable');
        await cdp.send('Page.navigate', { url: rest[0] });
        await new Promise((r) => setTimeout(r, 2500));
        out = { navigatedTo: rest[0] };
        break;
      case 'shot': {
        await cdp.send('Page.enable');
        const { data } = await cdp.send('Page.captureScreenshot', { format: 'png' });
        const fs = await import('node:fs');
        fs.writeFileSync(rest[0], Buffer.from(data, 'base64'));
        out = { saved: rest[0] };
        break;
      }
      case 'fill':
        out = await evaluate(cdp, js.fill(rest[0], rest.slice(1).join(' ')));
        break;
      // Đưa tab lên trước + bật focus emulation: input CDP bị nuốt nếu tab không được focus
      case 'front': {
        try { await cdp.send('Page.bringToFront'); } catch { /* ignore */ }
        try { await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: true }); } catch { /* ignore */ }
        out = { ok: true };
        break;
      }
      // Click chuột thật theo toạ độ — cần cho component Vue (dropdown, tab...)
      // Click theo toạ độ tuyệt đối (viewport CSS px)
      // Nạp file vào input[type=file] (không cần bấm hộp thoại)
      case 'upload': {
        const sel = rest[0];
        const file = rest.slice(1).join(' ');
        await cdp.send('DOM.enable');
        const { root } = await cdp.send('DOM.getDocument', { depth: -1 });
        const { nodeIds } = await cdp.send('DOM.querySelectorAll', { nodeId: root.nodeId, selector: sel });
        if (!nodeIds.length) { out = { ok: false, err: `không thấy ${sel}` }; break; }
        await cdp.send('DOM.setFileInputFiles', { files: [file], nodeId: nodeIds[0] });
        out = { ok: true, uploaded: file };
        break;
      }
      case 'tapxy': {
        const x = Number(rest[0]);
        const y = Number(rest[1]);
        for (const type of ['mousePressed', 'mouseReleased']) {
          await cdp.send('Input.dispatchMouseEvent', { type, x, y, button: 'left', clickCount: 1 });
        }
        out = { ok: true, x, y };
        break;
      }
      case 'tap': {
        const rect = await evaluate(cdp, `(() => {
          const el = document.querySelector(${JSON.stringify(rest[0])});
          if (!el) return null;
          el.scrollIntoView({ block: 'center' });
          const r = el.getBoundingClientRect();
          return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
        })()`);
        if (!rect) { out = { ok: false, err: `không thấy ${rest[0]}` }; break; }
        for (const type of ['mousePressed', 'mouseReleased']) {
          await cdp.send('Input.dispatchMouseEvent', { type, x: rect.x, y: rect.y, button: 'left', clickCount: 1 });
        }
        out = { ok: true, at: rect };
        break;
      }
      case 'click':
        out = await evaluate(cdp, js.click(rest[0]));
        break;
      // Gõ như người thật: focus -> Input.insertText (bắt buộc với ô có mask số tiền)
      // Gửi 1 phím thật (Tab, Enter, Escape...) để component commit giá trị
      case 'key': {
        const map = { Tab: 9, Enter: 13, Escape: 27, Backspace: 8 };
        const name = rest[0];
        const code = map[name] ?? 0;
        await cdp.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: name, code: name, windowsVirtualKeyCode: code, nativeVirtualKeyCode: code });
        await cdp.send('Input.dispatchKeyEvent', { type: 'keyUp', key: name, code: name, windowsVirtualKeyCode: code, nativeVirtualKeyCode: code });
        out = { key: name };
        break;
      }
      // Gõ từng ký tự bằng key event thật (cho input có mask chỉ nghe keydown/keypress)
      case 'typekeys': {
        const sel = rest[0];
        const text = rest.slice(1).join(' ');
        const ok = await evaluate(cdp, `(() => {
          const el = document.querySelector(${JSON.stringify(sel)});
          if (!el) return false;
          el.focus();
          if (el.select) el.select();
          return true;
        })()`);
        if (!ok) { out = { ok: false, err: `không thấy ${sel}` }; break; }
        for (const ch of text) {
          await cdp.send('Input.dispatchKeyEvent', { type: 'char', text: ch, unmodifiedText: ch });
        }
        out = await evaluate(cdp, `document.querySelector(${JSON.stringify(sel)}).value`);
        break;
      }
      case 'type': {
        const sel = rest[0];
        const text = rest.slice(1).join(' ');
        const ok = await evaluate(cdp, `(() => {
          const el = document.querySelector(${JSON.stringify(sel)});
          if (!el) return false;
          el.focus();
          if (el.select) el.select();
          return true;
        })()`);
        if (!ok) { out = { ok: false, err: `không thấy ${sel}` }; break; }
        await cdp.send('Input.insertText', { text });
        out = await evaluate(cdp, `document.querySelector(${JSON.stringify(sel)}).value`);
        break;
      }
      case 'text':
        out = await evaluate(cdp, js.text(rest[0]));
        break;
      case 'cookie': {
        const { cookies } = await cdp.send('Network.getCookies', { urls: [`https://${rest[0] || 'nhanh.vn'}/`] });
        out = cookies.map((c) => ({ name: c.name, domain: c.domain, value: c.value.length > 12 ? `${c.value.slice(0, 8)}…` : c.value, expires: c.expires }));
        break;
      }
      default:
        throw new Error(`lệnh không hỗ trợ: ${cmd}`);
    }
    console.log(typeof out === 'string' ? out : JSON.stringify(out, null, 2));
  } finally {
    cdp.close();
  }
  // Thoát ngay: handler WebSocket của Chrome có thể bắn 'error' khi ta chủ động đóng.
  process.exit(0);
}

main().catch((e) => {
  console.error(`LỖI: ${e.message}`);
  process.exit(1);
});

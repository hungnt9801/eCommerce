# 06 — Cách khảo sát: REA + cầu CDP, bằng chứng và giới hạn

> Tài liệu này trả lời câu hỏi *"dùng tool nào, chạy thế nào, tin được đến đâu"* cho phần khảo sát Nhanh.vn.
> Kết quả khảo sát ở [01](01-khao-sat-nhanh-vn.md); đây là **cách làm lại được**.

---

## 1. Hai công cụ, hai mục đích

| | **REA** (`morluto/rea`) | **Cầu CDP trong repo** (`tools/nhanh-cdp.mjs`) |
|---|---|---|
| Bản chất | MCP/CLI reverse-engineering: attach Chrome qua CDP, chụp DOM/network/bundle, **route inventory**, so ảnh | Script 200 dòng, chỉ gửi lệnh CDP thô (`eval`, `goto`, `fill`, `click`, `shot`) |
| Điểm mạnh | Xuất **evidence JSON** có provenance; phân tích bundle; so sánh ảnh pixel; chạy kịch bản | Nhanh, không cần cài, chạy được ngay, kiểm soát 100% |
| Điểm yếu | Cần Node ≥ 24.22.1, MCP client, học schema | Không có evidence chuẩn, tự viết JS |
| Dùng khi | Khảo sát **có hệ thống** (route/endpoint/asset), so sánh UI định kỳ, làm hồ sơ | Thao tác nhanh hằng ngày, lấy dữ liệu DOM cụ thể |

**Kết luận thực dụng:** dùng **REA** cho phần *hồ sơ khảo sát* (để tái lập và so sánh),
dùng **cầu CDP** cho phần *lấy dữ liệu cụ thể* (menu, cột bảng, route). Cả hai đều attach vào **cùng
một Chrome** đang đăng nhập.

---

## 2. Trạng thái trên máy này (đo lúc 02/11/2026)

| Hạng mục | Giá trị | Hệ quả |
|---|---|---|
| Node | **v24.19.0** | ❌ REA yêu cầu `^22.23.4 \|\| ^24.22.1 \|\| >=26.12.1` → **phải nâng** |
| npm | 11.17.0 | ✅ |
| Chrome | 156.0.8078.79 tại `C:\Program Files\Google\Chrome\Application\chrome.exe` | ✅ |
| CDP | **đang mở** ở `http://127.0.0.1:9222` (profile `%LOCALAPPDATA%\DSH\nhanh-chrome-profile`) | ✅ attach được ngay |
| Phiên Nhanh | đã đăng nhập, `businessId=230222`, đang ở `/product/item/index` | ✅ |
| `rea` trên PATH | ❌ chưa cài | — |
| `tools/node_modules` | chỉ có `jsdom`, `mermaid` (không có playwright) | cầu CDP tự dùng `WebSocket` built-in của Node |

⚠️ **Chrome ≥ 136 bỏ qua `--remote-debugging-port` nếu dùng profile mặc định.** Đây là lý do profile
phải là thư mục riêng (`--user-data-dir=...`) — đúng như `tools/README-nhanh.md` đã ghi.

---

## 3. Cài REA (khi đã nâng Node)

```powershell
node -v                                   # phải >= 24.22.1
npm install --global rea-agents
rea --version --json
rea setup --client dsh --yes              # đăng ký MCP cho DeepSeek Harness; hoặc claude_code / codex
rea doctor --client dsh --json
```

- `rea setup --dry-run --json` để **xem trước** thay đổi (có backup config).
- `--tool-families browser,javascript-runtime,application,session` để giảm context MCP.
- Không cần `playwright install` (REA dùng `playwright-core` + Chrome có sẵn), không cần quyền admin,
  không cần `ps`/procps (chỉ Linux).
- Cập nhật: `rea update`.

Tham chiếu: `d:\Projects\Tools\rea\docs\installation.md`, `docs\browser-observation.md`,
`docs\browser-scenario-contract.md`, `docs\javascript-route-inventory.md`.

---

## 4. Recipe: khảo sát một app SaaS đã đăng nhập

### 4.1 Chuẩn bị phiên (làm 1 lần)

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" `
  --remote-debugging-port=9222 `
  --user-data-dir="$env:LOCALAPPDATA\DSH\nhanh-chrome-profile" `
  "https://nhanh.vn/login"
# → đăng nhập BẰNG TAY (kể cả OTP). Sau đó đóng/mở lại vẫn còn phiên.
```

### 4.2 Liệt kê target và lấy `target_id`

```powershell
rea list-browser-targets http://127.0.0.1:9222 --allowed-origins https://nhanh.vn --json
```

### 4.3 Route inventory + endpoint candidate (bằng chứng có provenance)

```powershell
$EP = "http://127.0.0.1:9222"; $tid = "<target_id>"
rea analyze-web-bundle $EP $tid --fetch-source-maps `
  --detail summary `
  --view '{"kind":"javascript-routes","collection":"routes","limit":100}' `
  --evidence_output D:\Projects\Tools\eCommerce\artifacts\nhanh-survey\rea-routes.json --json
```

### 4.4 Chụp DOM + network (kịch bản `connect`, không đóng browser của bạn)

```json
{
  "browser": { "mode": "connect", "cdp_endpoint": "http://127.0.0.1:9222", "target_id": "<tid>" },
  "start_url": { "url": "https://nhanh.vn/order/manage/index?businessId=230222" },
  "actions": [
    { "step_id": "goto", "action": "goto", "destination": { "url": "https://nhanh.vn/order/manage/index?businessId=230222" }, "wait_until": "load" },
    { "step_id": "settle", "action": "wait_for_timeout", "duration_ms": 6000 }
  ],
  "capture": {
    "at_end": ["dom", "accessibility", "url"],
    "events": ["network", "console", "page-errors"],
    "network": { "request_body": true, "response_body": true, "header_values": true }
  }
}
```

```powershell
rea validate-browser-scenario artifacts\nhanh-survey\scenario.json --json
rea capture-browser-scenario artifacts\nhanh-survey\scenario.json --json > artifacts\nhanh-survey\capture.json
```

### 4.5 So sánh UI của ta với Nhanh (nghiệm thu "giống")

```powershell
rea capture-web-screenshot http://127.0.0.1:9222 $tid --output artifacts\nhanh-survey\ref-product-list.png
# chụp màn hình của ta cùng kích thước, rồi:
rea compare-web-screenshots artifacts\nhanh-survey\ref-product-list.png artifacts\ours-product-list.png --json
```

> Mục tiêu: **cấu trúc khớp** (vùng, cột, mật độ), không phải pixel-perfect — pixel-perfect sẽ vi phạm
> bản sắc thương hiệu và cả bản quyền.

### 4.6 Giới hạn phải biết trước khi tin kết quả

| Giới hạn | Hệ quả |
|---|---|
| Passive DOM **không giữ text/attribute value** | Muốn đọc chữ phải bật `--include-accessibility-text` |
| Network passive **không giữ cookie/Authorization**, body chỉ là *shape* | Muốn body thật phải dùng `capture_browser_scenario` với `network.response_body=true` |
| `prior_activity_available` luôn `false` | Sự kiện **trước khi attach không có**; phải reload/goto sau khi arm |
| REA **không xuất HAR** (chỉ đọc HAR/SAZ) | Cần HAR thì dùng Chrome DevTools hoặc Playwright |
| Output chỉ JSON/JSONL/TOON/YAML | Tự render markdown từ JSON |

---

## 5. Cách đã dùng trong phiên này (để tái lập đúng kết quả ở 01)

```powershell
cd D:\Projects\Tools\eCommerce

# 1) Xem tab đang mở
node tools/nhanh-cdp.mjs targets

# 2) Đếm route nội bộ + lưu
$js = @'
(() => { const s = new Set(); document.querySelectorAll('a[href]').forEach(a => {
  const u = a.getAttribute('href') || '';
  if (u.startsWith('/') && !u.startsWith('//')) s.add(u.split('?')[0]); });
  return [...s].sort(); })()
'@
node tools/nhanh-cdp.mjs eval $js | Set-Content -Encoding utf8 artifacts\nhanh-survey\routes-raw.json

# 3) Cây menu sidebar (180 mục)
$menu = @'
(() => { const root = document.querySelector('.sidebar-content'); const out = [];
  const walk = el => { for (const ch of el.children) { const cls = ch.className?.toString() || '';
    if (ch.matches('a[href^="/"]')) out.push({t: ch.innerText.trim(), h: ch.getAttribute('href').split('?')[0]});
    else if (/text-uppercase/.test(cls) && ch.innerText.trim()) { out.push({group: ch.innerText.trim()}); walk(ch); }
    else walk(ch); } };
  walk(root); return out; })()
'@
node tools/nhanh-cdp.mjs eval $menu | Set-Content -Encoding utf8 artifacts\nhanh-survey\menu-sidebar.json

# 4) Cột bảng + toolbar của trang đang mở
node tools/nhanh-cdp.mjs eval '(() => ({th: [...document.querySelectorAll("table thead th")].map(t=>t.innerText.trim())}))()'
```

**Kết quả đo được** (bản commit: `docs/nhanh/evidence/`; bản đầy đủ kèm ảnh: `artifacts/nhanh-survey/`):
- `routes-raw.json` — **204 route** nội bộ (kèm vài link blog, lọc theo prefix module khi dùng).
- `menu-sidebar.json` — **180 mục** menu 2 cấp.
- `product-list.png`, `order-list.png`, `bill-detail.png` — ảnh UI thật (không commit — `artifacts/` bị gitignore).
- Trích DOM: 20 cột bảng sản phẩm, 18 cột đơn hàng, 14 tab trạng thái, 16 bulk action.

> 🚨 **Sự thật cần ghi rõ:** phiên này **chưa cài REA** (vì Node 24.19 < 24.22.1).
> Toàn bộ số liệu trên lấy bằng **cầu CDP trong repo**. REA là bước tiếp theo để chuẩn hoá
> thành evidence tái lập + so sánh ảnh tự động.

---

## 6. Ranh giới pháp lý & đạo đức (không thương lượng)

1. **Chỉ dùng tài khoản của chính bạn** và dữ liệu bạn được phép xem. Không dò credential, không vượt auth.
2. **Không tấn công, không phá rate limit**, không quét toàn bộ dữ liệu khách hàng.
3. **Không copy mã nguồn/asset** của Nhanh. Được học **bố cục, luồng, thuật ngữ nghiệp vụ**;
   không được bê HTML/CSS/JS/logo/hình ảnh.
4. **Không mở cổng CDP ra ngoài loopback.** Port 9222 = toàn quyền điều khiển browser.
5. Dữ liệu chụp có thể chứa thông tin cá nhân → lưu trong `artifacts/` (đã gitignore? kiểm tra trước khi commit)
   và **không** đưa lên repo công khai.
6. Tuân thủ Điều khoản sử dụng của Nhanh và `d:\Projects\Tools\rea\DISCLAIMER.md`.

---

## 7. Khi nào cần khảo sát thêm

| Cần biết | Cách lấy |
|---|---|
| Payload API nội bộ (không phải Open API) | `capture_browser_scenario` + `network.response_body=true` |
| Hành vi realtime (thông báo đơn mới) | `observe_web_session` + lọc `network` type `websocket` |
| Bundle/route đầy đủ kể cả lazy chunk | `analyze_web_bundle --fetch-source-maps` |
| Màn hình chưa mở (kế toán, bảo hành) | Đổi tài khoản/bật module, hoặc dựng từ tài liệu + API v3 |
| Đối chiếu UI sau khi làm | `compare_web_screenshots` theo từng màn P0 |

---

## 8. Đề xuất chuẩn hoá (làm ở GĐ0)

1. Nâng Node → cài REA → `rea doctor` xanh.
2. Viết `tools/khao-sat-nhanh.ps1` gói 4 lệnh ở §4 (target → routes → scenario → screenshot) để
   **một lệnh** tạo lại toàn bộ hồ sơ.
3. Lưu baseline 18 ảnh P0 vào `artifacts/nhanh-survey/baseline/`.
4. Thêm CI job (thủ công) chạy `compare_web_screenshots` sau mỗi sprint cho 3 màn đã làm.

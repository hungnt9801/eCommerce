# Handover N120 — chuỗi `ui-1` · `U41-1` (WP-41 bước U1 thiết kế)

- **Ngày:** 28/11/2026 · **WP:** WP-41 · **Hàng:** `U41-1` (`loai = ui`, bước `U1 thiết kế`) · **Phiên:** N120
- **Nhánh:** `feat/ui-wp41-nen` · **Base:** `origin/tich-hop/ui` @ `80ab7a5` · **Trạng thái hàng:** ✅ XONG
- **Vai đã chạy:** `qa` (sửa cổng probe) · `ux` · `arch` · `docrev` (2 vòng) · `decider` (2 lần: cổng + gói).

## 1. Đã xong

### 1.1 Baseline đo lại (mở phiên)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 342 tệp `.md`, 1064 link, 2037 đường dẫn backtick, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối mermaid, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 49/49 pass, 0 fail |
| `docker compose ps` | 0 | `ecommerce-postgres` (healthy, 5433) + `ecommerce-redis` (healthy, 6380) |

### 1.2 Cổng vào WP-41 (chặn đã gỡ)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` (trước) | **2** | "WP = 41 NGOAI khoang 15…34" — trần cứng cũ |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` (sau sửa) | 0 | **19/19** ca DAT (CA13/CA14 mới) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` (sau sửa) | 0 | XANH — "07 ghi Không có tài liệu feature" |
| `... -WP WP-42` | 0 | XANH |
| `... -WP WP-14` / `-WP WP-99` | 2 / 2 | Fail-closed giữ nguyên |
| `... -WP WP-20` / `-WP WP-21` | 0 / 0 | Hồi quy D-328 ⑥ xanh |

Sửa bởi `qa` theo `D-328` (1 tệp `tools/probe-cong-tai-lieu.ps1`, +23/−8): bỏ trần `>34`, giữ sàn `<15`, "xác định được WP" do `Lay-HangWp` (có hàng trong `07` §WP-15+). Negative control NC-1/NC-2/NC-3 đỏ→xanh có bằng chứng (sha256 tệp trùng trước/sau).

### 1.3 Sản phẩm U1 (chỉ tài liệu, 0 mã sản phẩm)

| Tệp | Dòng | Nội dung |
|---|---|---|
| `docs/ui/00-kiem-ke.md` | 312 | 28 route trang thật (27 chụp được), 32 component export (+7 cục bộ, 4 tab con), 15 nhóm pattern lặp, 23 dòng lệch số đo Nhanh, 22 dòng nợ a11y (19 mục nợ; 0 P1). BR-01…BR-09. |
| `docs/ui/01-kien-truc-ui.md` | 482 | 4 tầng `token → primitive → pattern → domain`; gói `@ecommerce/ui` (source TS + `transpilePackages`); quy tắc import; theme/mật độ/i18n; Storybook + so ảnh 27 route; ngân sách < 200 KB gzip; chuyển dần giữ selector/URL; bảng 36 gói đề xuất; 12 rủi ro + đường lùi; 9 câu `UI-41-AQ1…AQ9`. |
| `docs/reviews/UI-41-docreview.md` | 81 | Vòng 1 DOC-CHANGES (P1=0, 9 P2 + 4 P3); vòng 2 **DOC-OK** (P1=0, P2=0; 2 P3 định dạng đã vá). |

Số đo kiểm kê: 28 route trang / 39 node Next / 32 export / 15 pattern / 23 lệch / 22 dòng a11y. Chênh kế hoạch (37 route, 47 component) đã ghi tại `00-kiem-ke.md` §2.4; **không** sửa số kế hoạch.

### 1.4 Cổng DoD U1 (ORCH tự chạy lại)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/UI-41-docreview.md` | 0 | `verdict=DOC-OK`, P1=0, P2=0 |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | 114 dòng `D-xx` DAT |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (cuối) | 0 | PASS — 345 tệp, 1074 link, 2054 backtick, 0 chết |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` | 0 | XANH |

**0 mã sản phẩm đổi** (không `modules/**`, `src/**`, `shared/**`, `packages/**`, `apps/**`, `test/**`). Ngoại lệ `D-328` ⑤: 1 tệp **công cụ cổng** `tools/probe-cong-tai-lieu.ps1` (vùng `qa`), đã kê riêng.

## 2. Chưa xong / cắt giữa

**Không có việc dở của `U41-1`.** Các mục dưới là **nợ có nhận chủ**:

- 🟡 **`check-feature-doc.ps1` exit 1 là N/A** — `07` dòng 765 ghi WP-41…51 "Không có tài liệu feature"; `ui-pipeline` §4 dùng khung 7 mục, không phải mẫu 16 mục feature. Không sửa script.
- 🟡 **`test-cong-kiem.ps1 -ChiKiemMoc` exit 1 — 52/54**, 2 ca **hỏng sẵn có môi trường**, không do phiên này: (A) `dotnet build --no-incremental` lỗi `MSB3027/MSB3021` do DLL bị khoá bởi `dotnet.exe` PID 22404 (host đang chạy; không có lỗi biên dịch mã nguồn); (B) nhóm Y1 — 2 script `ecommerce-run-chung.ps1`/`ecommerce-start.ps1` còn API chỉ-Windows. Chủ: người/phiên có quyền dừng host.
- 🟡 **`00-kiem-ke.md` §8 Q1/Q2** (27 vs 37 route; 32 vs 47 component) đã được `D-329` chốt: 27 route ảnh chuẩn, 32 export baseline; số kế hoạch giữ nguyên.
- 🟡 **`01-kien-truc-ui.md` §10.2** còn các dòng "cần xác minh (npm view)" cho nhóm ESLint và `@next/bundle-analyzer`; `D-329` quy định **ORCH phải tra bản + ghi `D-xx` trước `pnpm add`** (không suy đoán).

## 3. Migration đã áp

**Không có migration, 0 thay đổi schema, 0 `pg_dump`.**

## 4. Quyết định mới

| `D-xx` | Trạng thái | Tóm tắt |
|---|---|---|
| **`D-328`** | TẠM CHỐT (DEC, M2) | Cổng `probe-cong-tai-lieu.ps1` chặn oan WP-41…51: bỏ trần `>34`, giữ sàn `<15`, "xác định được WP" = có hàng trong `07` §WP-15+; `qa` sửa 1 tệp + CA13/CA14; `tools/probe-*.ps1` không tính là "mã sản phẩm". |
| **`D-329`** | TẠM CHỐT (DEC, M2) | 36 gói có **bản ghim chính xác** (9 đang cài + 27 mới; hoãn `react-query`, `nuqs`, DataTable/virtual tới đúng WP có bên gọi); ngoại lệ `next-auth 5.0.0-beta.32` + **biểu đồ SVG thuần, không Recharts**; chốt `AQ1…AQ9` + `Q4/Q5`; điều kiện bắt buộc `U41-2` (`transpilePackages`, 2 dòng `COPY` Dockerfile, `@source`, script `lint`, pin `next-auth`). |

Cả hai đã có dòng trong `docs/agents/hang-doi-xem-lai.md` (chờ người xem lại trước merge `tich-hop/ui` → `main`). `check-agent-artifact -Kind decision` exit 0.

## 5. Nợ / rủi ro

- 🔴 **Hai tài liệu U1 là `Draft`, chưa có người duyệt** — `D-328`/`D-329` là **TẠM CHỐT (DEC, M2)**; người xem lại trước khi merge `tich-hop/ui` → `main` (biên bản người ký, `D-115` ⑤). Không tự coi là đã duyệt.
- 🟠 **`U41-2` bắt buộc sửa 6 chỗ** (nếu thiếu, cổng đỏ): `apps/admin-web/next.config.ts` thêm `transpilePackages: ["@ecommerce/ui"]`; `apps/admin-web/Dockerfile` thêm **2 dòng** `COPY packages/ui/package.json` + `COPY packages/ui` (thiếu ⇒ `docker-build` đỏ); `globals.css` thêm `@import "@ecommerce/ui/tokens.css"` + `@source "../../../packages/ui/src"`; `apps/admin-web/package.json` thêm script `lint` + pin `next-auth`; root `"lint"` → `pnpm -r --if-present lint`; giữ nguyên `test`/`build`/`e2e`.
- 🟠 **`@storybook/addon-essentials` 8.6.14 lệch major với Storybook 10.6.9** — `D-329` cấm dùng gói này; dùng `addon-a11y`/`addon-themes` + built-in docs/controls.
- 🟠 **`@tanstack/react-table 9.3.1`** chọn theo `latest` (v8 là major trước); nếu `U45-1` đo được blocker thì mở `D-xx` thay thế — chưa cài ở WP-41/42.
- 🟡 **Nhóm ESLint + `@next/bundle-analyzer` chưa ghim** — phải tra `npm view` + ghi `D-xx` trước `pnpm add`; không tra được thì hoãn lint sang WP-51 và giữ 3 lệnh grep tạm (`typecheck`, cấm deep import, cấm hex).
- 🟡 **Chuỗi `ui-1` chỉ vào `tich-hop/ui` ở `U41-4`** — WP-42 trở đi cắt từ đỉnh `tich-hop/ui` sẽ **chưa có** bản sửa probe `D-328`; trong WP-41 thì đã có trên nhánh. Người không phải làm gì; `U41-4` đẩy fast-forward.
- 🟡 **Ảnh chuẩn 27 route** (không phải 37) — `U41-3` phải dùng đúng 27 (AQ1); mọi khác biệt ngoài danh sách ảnh dự kiến đổi = đỏ.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `U41-1` (WP-41 bước U1) → mở `U41-2`.**

- Hàng kế: `U41-2` (`loai = ui`, vai `ds`, depends `U41-1`): khung `packages/ui` (`@ecommerce/ui`: build/typecheck/test) + Tailwind `@source`; ESLint flat config (next, react-hooks, jsx-a11y); Vitest + Testing Library + axe; `next/font` thay `@import` Google Fonts; ghim `next-auth` chính xác. **0 thay đổi giao diện.**
- Cài đúng bảng ghim `D-329`; tra `npm view` nhóm ESLint + ghi `D-xx` trước `pnpm add`; sửa 6 chỗ ở §5.
- Cổng vào: `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` = 0 (đã xanh).

```text
/chay-phien-code chuoi=ui-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/ui-wp41-nen`; base `origin/tich-hop/ui` @ `80ab7a5`.
- Commit phiên này: `99e922e` — `fix(tools): N120 - U41-1 D-328 remove probe WP upper bound` (đã push `origin/feat/ui-wp41-nen`). **Lưu ý định dạng:** subject commit bị tiền tố `@` do here-string Bash khi tạo; nội dung đúng, **không** sửa lịch sử đã push.
- Commit tài liệu U1 (handover + `docs/ui/**` + `docs/reviews/UI-41-docreview.md` + `decisions.md` + `hang-doi-xem-lai.md` + registry/bảng): message `docs(nhanh): N120 - U41-1 WP-41 U1 kiem ke kien truc UI D-328 D-329`; SHA ghi ở câu trả lời cuối.
- **Push** `origin/feat/ui-wp41-nen`; **chưa** đẩy `tich-hop/ui` (chỉ ở hàng `dong-wp` `U41-4`). **Không** force/merge, **không** mở PR, **không** đụng `main`.

---

**Hàng rào agent** (phiên `N120`): `qa` ×1 · `ux` ×2 · `arch` ×2 · `docrev` ×2 · `decider` ×2. Cảnh báo hook: (1) 3 cảnh báo "thiếu khối JSON" cho `decider`/`arch`/`docrev` — **dương tính giả** đã biết (`chanDungKhiThieuJson=false`; khối JSON vẫn có trong báo cáo); (2) 1 cảnh báo "`arch` ghi ngoài vùng `docs/ui/00-kiem-ke.md`" — **dương tính giả do chạy song song** với `ux` (arch chỉ sửa `01`; `00` do `ux` sửa theo DOCREV; DOCREV vòng 2 đọc lại toàn văn xác nhận). `git status` cuối chỉ có `docs/ui/**`, `docs/reviews/UI-41-docreview.md`, `docs/nhanh/decisions.md`, `docs/agents/hang-doi-xem-lai.md`, `agents/state/**` — đúng vùng.

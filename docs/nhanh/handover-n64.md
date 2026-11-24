# Handover N64 — WP-14, hàng `P-20` (ĐÓNG WP-14)

> Phiên `N64` · 24/11/2026 · nhánh `docs/n22-wp14-dot-a` · WP-14 (thuần tài liệu, `D-71`: 0 `.cs`).
> Hàng đã làm: `P-20` (`dong-wp`).

## 1. Đã xong

- **`P-20` XONG — WP-14 ĐÓNG.** DoD của hàng: 3 biên bản tạm duyệt đều có · `README` §2 khớp · cổng link PASS.
- **`docs/features/README.md` §2** — thêm khối "24/11/2026 — WP-14: TẠM DUYỆT (DEC), KHÔNG phải duyệt của người":
  **27/55** người duyệt **giữ nguyên** · **18/18** tài liệu GĐ3 có `VERDICT: DOC-OK` · **9/18 tạm duyệt có điều kiện**
  (A 4/9 · B 2/3 · C 3/6, riêng `NOT-002` chỉ phần thiết kế) · **9/18 "chưa"** · **0/18 có chữ ký người** ⇒ DoR §A.5 GĐ3 **chưa đạt**.
- **`decider` → `D-209`** (TẠM CHỐT DEC, uỷ quyền `D-114`/`D-70`): thứ tự **20 WP code GĐ3 `WP-15`…`WP-34`**, phủ đủ `D-70`,
  mỗi WP một cổng tài liệu. `WP-15` nhận 2 nợ `D-71` ④ (`C10` của `D-67` + map mã `409`); **giá vốn `WP-17` đứng trước lãi gộp `WP-32`**
  (`D-177` ⑦/`D-186` ⑦`); WP có cổng "chưa" ⇒ chỉ xếp lịch, không code. 1 dòng hàng đợi xem lại.
- **`07`** — thêm mục **`§WP-15+`** (bảng 20 WP + quy tắc chạy + phủ `D-70` + cổng máy còn thiếu) và **§13** một dòng cho `N64`.

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** — **94** dòng |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS 0 chết — **205** tệp · **920** link · **1344** backtick |
| `npm --prefix tools run check:mermaid` | 0 | **212** khối · **0** lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **48/48** pass |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK — bảng khớp registry |
| `dotnet build` / `dotnet test` | — | **bỏ qua có lý do**: WP-14 thuần tài liệu (`D-71`, 0 `.cs`); build dev đỏ `MSB3491` đã biết |

## 2. Chưa xong / cắt giữa

- **Không có.** `P-20` đạt DoD; WP-14 đóng theo DoD toàn WP (`18/18` tài liệu có trạng thái cuối trong biên bản · `§WP-15+` có thứ tự).
- `P-21` (`nguoi`) **chưa làm** — mở + merge PR, **vùng cấm** (`camUyQuyen: true`), không uỷ quyền được.

## 3. Migration đã áp

- **Không có** (WP thuần tài liệu; 0 `.cs`, 0 migration).

## 4. Quyết định mới

- **`D-209`** — `TẠM CHỐT (DEC, uỷ quyền `D-114`/`D-70`)`: thứ tự 20 WP code GĐ3 `WP-15`…`WP-34`, phủ đủ `D-70`; `WP-15` nhận 2 nợ `D-71` ④; giá vốn `WP-17` trước lãi gộp `WP-32`; WP có cổng "chưa" ⇒ chỉ xếp lịch. 1 dòng hàng đợi xem lại.
- Không có `D-xx` người chốt mới. Không `TIEN_LE` mới.

## 5. Nợ / rủi ro

- **0/18 tài liệu GĐ3 có chữ ký người** ⇒ DoR `implementation-plan.md` §A.5 **chưa đạt**; merge `main` vẫn cần biên bản **người ký** `docs/bien-ban-duyet-gd3-<đợt>-<ngày>.md`. Ba biên bản hiện có là **tạm duyệt của DEC**, không thay thế được.
- **Cổng máy còn thiếu** (`D-209` ⑥): "cổng tài liệu chưa đạt ⇒ cấm code WP" + "merge `main` khi biên bản chưa ký" (`D-115` ⑤) — **OPS** dựng trước `WP-15`; **QA** thêm ca quét `07` §WP-15+ với 3 biên bản.
- **`WP-15` cần build/test chạy được**: nợ `MSB3491` (quyền ghi `obj\`) là **việc người/máy dev**, không phải code.
- **Điều kiện "trước WP code" của từng dòng tạm duyệt vẫn còn hiệu lực** — WP tương ứng chỉ code khi điều kiện dòng đó đã đạt; DOCREV ra `DOC-CHANGES` hoặc tài liệu đổi không qua DOCREV ⇒ tạm duyệt dòng đó **tự mất hiệu lực**.
- **Chờ người:** `P-21` — mở + merge PR `chore/agents-b1-b2` trước, rồi `docs/n22-wp14-dot-a` (mô tả: `artifacts/pr/mo-ta-pr-wp14-dot-a.md`, dòng đầu `Refs: D-71`).
- **Hàng rào:** 1 lượt `decider`; lượt đầu chạm trần **40 lượt** trước khi kịp trả khối JSON (đã ghi `D-209` + hàng đợi), lượt nối lại trả JSON `TAM_CHOT`. **0** lần chặn dừng, **0** ghi ngoài vùng.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-14 → mở WP-15 (WP code đầu tiên của GĐ3, sau khi `P-21` merge).**

- Registry WP-14 chỉ còn `P-21` (**người**, vùng cấm) ⇒ `/chay-phien` sẽ dừng và in bảng chờ người, không mở phiên mới.
- Sau khi người merge PR: mở **`WP-15`** theo `07` §WP-15+ — nhánh mới, việc đầu = gỡ `MSB3491` (việc người/máy dev) + 2 nợ `D-71` ④ (`C10` của `D-67`, map mã `409`); OPS dựng cổng tài liệu trước.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- Nhánh `docs/n22-wp14-dot-a`; commit việc `N64` = **`470e5dd`** (6 tệp, +55/−5). Chưa push tại lúc viết; commit đóng phiên + handover ở bước sau, rồi push `origin/docs/n22-wp14-dot-a`. Chưa PR.

---

**Hàng rào agent** (phiên `N64`): **1** lượt gọi vai — `decider` ×1 (`D-209`; lượt đầu chạm trần 40 lượt, nối lại 1 tin để trả JSON). **0** lần chặn dừng; **1** cảnh báo (thiếu khối JSON cuối ở lượt đầu — lượt nối lại có). **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`). ORCH **không** gọi MCP đồ thị.

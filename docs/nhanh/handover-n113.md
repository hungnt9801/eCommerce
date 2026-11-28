# Handover N113 — WP-17 / hàng `W17-0` (doc-gate `D-186` ⑥) XONG — G1 tạm duyệt cho `PRC-001` + `INV-001`; `REP-003` để người ký ở `W17-0n`

> Phiên `N113` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-0` (loại `doc-gate`, vai `ba · docrev`) · WP-17 "giá vốn" · mã `PRC-001` · `INV-001` · `REP-003` ·
> nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: đồng bộ `D-186` ⑥ (chủ động cơ giá vốn = **Pricing/`PRC-001`**) vào `prc-001` §15.1/§20.4 + `inv-001` §0 (+ 2 P2 DOCREV),
> `rep-003` đã khớp sẵn **không sửa**; `decider` tạm duyệt **G1** cho `prc-001`+`inv-001` (`D-319`); `REP-003` (đỏ cứng) để `W17-0n` (người).
> **0 `.cs` sản phẩm · 0 tệp FE · 0 migration · 0 `pg_dump`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| BA đối chiếu | `ba` ×1 | `prc-001`: §15.1 thêm **Q8** `✅ Closed (D-186)`; §20.4 mục 1 (đường ghi + động cơ thuộc PRC-001; **Q4 vẫn Open**), §15.4 **v0.7**. `inv-001`: §0 tách rõ giá vốn/định giá = `PRC-001`, `ACC-001` chỉ tiêu thụ, `INV-001` chỉ cấp đơn giá nhập (ánh xạ `UnitCost`↔`UnitPrice`, `D-09`); §15.4 **v0.2**. `rep-003` đối chiếu: §0:47 + §15.1 Q11 `Closed (D-186)` đã khớp ⇒ **không sửa** |
| DOCREV | `docrev` ×3 | `PRC-001` **DOC-OK** (P1=0 · P2=0 · P3=3) · `INV-001` vòng 1 **DOC-OK** 0/2 → BA sửa 2 P2 (`§5.2.2`, `§15.1 Q8`) → vòng 2 **DOC-OK** (P1=0 · P2=0 · P3=2) |
| BA sửa P2 | `ba` ×1 | `inv-001` §5.2.2 bỏ "giá trị tồn kho thuộc `ACC-001`" → `PRC-001`; §15.1 Q8 → `Closed (D-186)` |
| DEC tạm duyệt G1 | `decider` ×1 | **`D-319`** `TẠM CHỐT (DEC)` — tạm duyệt G1 cho `prc-001`+`inv-001` đúng `D-186` ⑥; **KHÔNG** `REP-003`, **KHÔNG** đóng Q4; C5(vi) **chưa đạt ở G1** ⇒ dòng không mở cổng code; + dòng hàng đợi |
| Biên bản tạm duyệt | ORCH | `docs/bien-ban-tam-duyet-gd3-wp17-2026-11-28.md` (2 dòng: `PRC-001`, `INV-001`; mục "Phạm vi KHÔNG gồm `REP-003`" đính 2 vị trí đã ký cho `W17-0n`) |

**Baseline mở phiên (`N113`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650 passed · 0 failed · 0 skipped** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · **319** tệp · 1030 link · 1939 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **214** khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |

**DoD của hàng — ORCH tự chạy (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| DOCREV `PRC-001` | `check-agent-artifact -Kind docreview -Path docs/reviews/PRC-001-docreview.md` | **exit 0** · DOC-OK · P1=0 · P2=0 (P3=3) |
| DOCREV `INV-001` | `… -Path docs/reviews/INV-001-docreview.md` | **exit 0** · DOC-OK vòng 2 · P1=0 · P2=0 (P3=2) |
| DOCREV `REP-003` (cũ) | `… -Path docs/reviews/REP-003-docreview.md` | **exit 0** · DOC-OK · P1=0 · P2=0 |
| Quyết định | `check-agent-artifact -Kind decision -Path docs/nhanh/decisions.md` | **exit 0** · 114 dòng (gồm `D-319`) |
| Cổng `Code` WP-17 | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | **exit 0 · XANH** (`PRC-001` nhờ GD1; mọi mã WP-17 đạt) |
| Cổng đầy đủ | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 bước · 314 s** — build 0 error/9 warning · unit 2650/2650 · integration pass · mermaid 0 · cổng TL 0,8 s · FE typecheck+Vitest · `gen:api` drift 0 · DB 25/25 · test cổng 60/60 |
| Tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** — 322 tệp · 1039 link · 1953 backtick · 0 chết |
| Mermaid | `npm --prefix tools run check:mermaid` | **214 khối · 0 lỗi** |
| Hàng rào hook | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49** |
| `check-feature-doc` | `… -Path prc-001` / `inv-001` / `rep-003` | **1 / 1 / 0** — 2 lỗi **CÓ SẴN từ trước** (không do N113): `BR-PRC-001-008` chưa có test §14; `inv-001` "không tìm thấy BR ở §1" (định dạng BR không khớp regex); cả hai còn thiếu mục "Đối chiếu mô hình Nhanh" (nợ WP-14). Ngoài changedSections |
| Bảng phiên | `PH check` | **OK: bang khop registry** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W17-0`.** DoD đạt toàn bộ. Các mục dưới là **cắt có quyết định**, không phải dở dang:

- **`REP-003` §0/Q11 (đỏ cứng)**: **không** tạm duyệt và **không** sửa trong phiên. `D-318` C3/C8: chủ dự án ký kiểu (c) tại U-17; `W17-0n` (người) đối chiếu diff 2 vị trí + tạo `docs/bien-ban-duyet-gd3-wp17-<ngày>.md`. ORCH đã đính 2 vị trí (`rep-003` §0:47 · §15.1 Q11:938) trong biên bản tạm duyệt.
- **Q4** (`CostPrice` luồng duyệt 2 bước): câu **kế toán**, `D-318` C2 nói rõ decider không đóng ⇒ giữ `⬜ Open`; chuyển người (M4/R3-3).
- **C5(vi)** ca domain invariant K-06 + `check-agent-artifact -Kind testplan` exit 0: **chưa đạt ở G1** (chưa có ARCH/testplan) ⇒ điều kiện cứng của `W17-1` (ARCH chứa ca K-06) và `W17-4`; `D-319` **không** mở cổng code.

## 3. Migration đã áp

**Không có.** 0 `.cs`, 0 migration, không chạm DB ⇒ không `pg_dump` (K-01 không kích hoạt).

## 4. Quyết định mới

- **`D-319` — `TẠM CHỐT (DEC)`** (uỷ quyền `D-318` C2/C5): tạm duyệt **G1** cho `prc-001` §15.1/§20.4 + `inv-001` §0/§5.2.2/§15.1, đúng phạm vi `D-186` ⑥; **không** gồm `REP-003`; **không** đóng Q4; C5(vi) chưa đạt ⇒ không mở cổng code. Có dòng ở `docs/agents/hang-doi-xem-lai.md`; `reviewBefore = release`; cách lùi cụ thể trong dòng `D-319`.
- Không có `D-xx` nào của người trong phiên (phiên này không có người ngồi cùng).

## 5. Nợ / rủi ro

- 🔴 **`W17-0n` (người) — chặn `W17-1`**: ký `REP-003` §0/Q11 (2 vị trí: §0:47 đã bỏ nhãn Q11 mở; §15.1 Q11 `Closed (D-186)`), xác nhận mức **L1**, tạo `docs/bien-ban-duyet-gd3-wp17-<ngày>.md`. Lệch ⇒ chữ ký vô hiệu.
- 🟡 **Q4** (`CostPrice` duyệt 2 bước) — kế toán trưởng (M4/R3-3), giữ `Open`.
- 🟡 **C5(vi)** K-06/testplan — điều kiện cứng `W17-1`/`W17-4`; `D-319` không mở cổng code.
- 🟡 **`D-186` dẫn tệp không tồn tại** `StockDocumentLine.cs:362-367` (as-built thật `StockDocument.cs:367`/`:466`) — phát hiện ở DOCREV `PRC-001`. `D-186` đã `ĐÃ CHỐT (người)` (`D-318` C1) ⇒ **không sửa** dòng đó; ghi nhận để lượt được phép chạm `decisions.md` (khi đó vẫn cần người/decider ghi dòng thay thế).
- 🟡 **Nợ `check-feature-doc` của `prc-001`/`inv-001`** (BR coverage + mục "Đối chiếu Nhanh") — nợ **WP-14** có trước, cần lượt WP-14 riêng; không do N113.
- 🟡 Nợ kế thừa WP-23 (không thuộc phiên): P2 retry storm (`D-263`), `D-317` ca Host boot, NCC/HMAC (`R3-6`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-0` (doc-gate WP-17) → hàng kế `W17-0n` (👤 chờ người ký `REP-003`).**

- `W17-0n`: người ký `REP-003` §0/Q11 (2 vị trí), xác nhận L1; ORCH đối chiếu diff 2 vị trí + tạo `docs/bien-ban-duyet-gd3-wp17-<ngày>.md` (không dùng `bien-ban-tam-duyet-*`).
- Sau chữ ký: `W17-1` — ARCH động cơ giá vốn (G2 do `decider` duyệt thay theo `D-318` C4, đủ 7 điều kiện; ARCH phải có ca K-06).
- Trạng thái chờ người gom ở bảng câu trả lời cuối phiên.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**.
- Commit việc + đóng phiên: **`git log -1`** (SHA ngắn ghi ở câu trả lời cuối) — gồm 2 tài liệu feature, 2 `docs/reviews/*-docreview.md`, `D-319` + hàng đợi, biên bản tạm duyệt, `07` §13, handover, registry.
- **Đã push** `origin/feat/gd3-wp17-gia-von`. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

---

**Hàng rào agent** (phiên `N113`): `ba` ×2 · `docrev` ×3 · `decider` ×1. **4 cảnh báo** `artifacts/agents/canh-bao.jsonl` mới (ba ×1, docrev ×3) — đều "thiếu khối JSON cuối" do hook không đọc được transcript; **cả 4 báo cáo đều có khối JSON đầy đủ** ở cuối, ORCH tự kiểm mọi số đo bằng máy. **0 tệp ngoài vùng**; **0 lần hook chặn dừng**.

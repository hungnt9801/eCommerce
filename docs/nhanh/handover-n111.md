# Handover N111 — WP-17 / hàng `U-17` (PHIÊN UỶ QUYỀN WP-17) XONG — chốt `D-318`, mở `W17-q`

> Phiên `N111` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `U-17` (loại `thao-luan`) · WP-17 "giá vốn" · mã `PRC-001` · `REP-003` · `INV-001` ·
> nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: **phiên uỷ quyền có chủ dự án ngồi cùng** — decider soạn câu hỏi + tiêu chí, chủ dự án chốt 9 câu, ghi `D-318`, cập nhật hồ sơ uỷ quyền, chuyển `W17-1n` cho decider.
> **0 dòng code sản phẩm · 0 tệp FE · 0 migration · 0 `pg_dump` · 0 `artifacts/pg-dump`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Baseline mở phiên (bản tài liệu — 0 `.cs`) | ORCH | `kiem-link` **PASS** (317 tệp · 1029 link · 1931 backtick · 0 chết) · `check:mermaid` **214/0** · hooks **49/49**; build/test **bỏ qua** có lý do "phiên thảo luận/uỷ quyền thuần tài liệu" |
| `decider` chế độ CHUẨN BỊ | `decider` ×1 | Đọc hồ sơ §2/§2.1/§5/§6 + `D-176/177/181/186/195/208/217` + `prc-001`/`rep-003`/`inv-001` + `codegraph_explore` as-built ⇒ **8 câu (C1–C8)** + 1 câu grain (C9, decider cắt vì trần), mỗi câu có tiêu chí duyệt thay + khuyến nghị + rủi ro + chi phí lùi. **Không ghi tệp nào.** 48 lượt tool, 0 ngoài vùng |
| Chủ dự án trả lời (3 lượt `AskUserQuestion`) | chủ dự án ngồi cùng | **C1** chuẩn y cả 4 · **C2** giao G1 kèm 5 điều kiện · **C4** giao G2 kèm 7 điều kiện · **C7** giữ **L1** · **C3** ký `REP-003` kiểu **(c)** · **C5** giữ đủ 8 tiêu chí + luật "nới ⇒ dừng" · **C6** thêm backfill & đổi grain · **C8** chỉ ghi biên bản Đã duyệt (Code không kiểm `REP-003`) · **C9** grain **theo SKU toàn hệ thống** |
| Ghi `D-318` | ORCH | 1 dòng `D-318` (ĐÃ CHỐT người, U-17 28/11/2026) ghi đủ C1–C9 + ①②③④⑤⑥ (nguồn/lùi/xem lại/đỏ khi vi phạm) |
| Chuẩn y 4 quyết định | ORCH | `D-177`/`D-181`/`D-186`/`D-195` đổi ô trạng thái `TẠM CHỐT (DEC)` → `ĐÃ CHỐT (người, phiên uỷ quyền U-17 2026-11-28)`; **nội dung không đổi một chữ**; dọn 4 dòng khỏi "Đang chờ" → "Đã xử lý" (`hang-doi-xem-lai.md`) |
| Hồ sơ uỷ quyền | ORCH | `uy-quyen-chu-du-an.md` §2.1 thêm **1 dòng ngoại lệ WP-17** trỏ `D-318` (không sửa dòng cũ) |
| Bảng phiên | ORCH | `PH done -Id U-17` (ĐÃ CHỐT người, D-318) → `PH uyquyen -Id W17-1n -Evidence D-318` (G2 → decider) |

**DoD của hàng — ORCH tự chạy (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| Artifact quyết định | `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **DAT** (exit 0 · 113 dòng kiểm) |
| Tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** — 317 tệp · 1029 link · **1939** backtick · 0 chết |
| Mermaid | `npm --prefix tools run check:mermaid` | **214 khối · 0 lỗi** |
| Hàng rào hook | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49 pass** |
| Bảng phiên | `PH check` | **OK: bang khop registry** (exit 0) |
| `D-xx` | `D-318` (ĐÃ CHỐT người) · `D-177/181/186/195` (ĐÃ CHỐT) | — |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `U-17`.** Cơ chế đã chốt cho hàng kế tiếp (ghi lại để phiên `W17-0` làm đúng):

1. **`W17-0n` (vẫn là hàng 👤)** — chủ dự án **ký `REP-003` §0/Q11** kiểu **(c)** (`D-318` ①(3)): chữ ký đã **pre-authorize** trong `D-318`; tại **`W17-0`**, ORCH tạo `docs/bien-ban-duyet-gd3-wp17-<ngày>.md` (3 cột `Mã` · `Kết luận` · `Người ký`, **KHÔNG** dùng `bien-ban-tam-duyet-gd3-*`) **sau khi** diff `REP-003` khớp **từng dòng** phạm vi (2 vị trí: §0 :47 bỏ nhãn Q11 mở; §15.1 Q11 ⇒ `Closed (D-186)`); lệch ⇒ chữ ký **vô hiệu**, quay lại người. Rồi `PH done -Id W17-0n`.
2. **Cổng `Code` KHÔNG kiểm `REP-003`** (`D-318` ①(8), C8=(c)): chỉ `MergeMain` đọc biên bản `bien-ban-duyet-gd3-*`. `W17-q` **giữ đúng phạm vi** `D-217` ④ (sửa probe đọc biên bản người GĐ1), **không** mở rộng cho `REP-003`.
3. **`W17-1n` → `duyet-dec`** (`D-318`): decider duyệt ARCH giá vốn ở `W17-1` **chỉ khi** thoả đủ 7 điều kiện C4 (additive, enum cuối dải, 0 chạm `XC-001`/làm tròn, mọi tiền `Money`, ca K-06, bảng thay đổi schema + revert); thiếu ⇒ chuyển người.

## 3. Migration đã áp

**Không có.** Phiên thuần tài liệu + registry: **0** migration, **0** `pg_dump`, không chạm schema.
`git diff origin/tich-hop/gd3...HEAD -- '**/migrations/**'` rỗng.

## 4. Quyết định mới

- **`D-318`** — **ĐÃ CHỐT (người, phiên uỷ quyền U-17, 28/11/2026)**: 9 câu C1–C9 (chuẩn y 4 quyết định; giao G1+G2 cho decider kèm tiêu chí; chủ dự án ký `REP-003` kiểu (c); giữ L1; grain giá vốn theo SKU toàn hệ thống).
- **`D-177` / `D-181` / `D-186` / `D-195`** — chuẩn y ⇒ **ĐÃ CHỐT (người)**, nội dung không đổi (chỉ đổi ô trạng thái).
- **Không** có `D-xx` TẠM CHỐT (DEC) mới trong `N111`.

## 5. Nợ / rủi ro

- 🟠 **`REP-003` lần đầu có chữ ký người** — trước đây đỏ cứng chưa từng được người ký; nay chữ ký **pre-authorize** ở `D-318`, **có điều kiện diff khớp** tại `W17-0`. Nếu BA sửa rộng ngoài 2 vị trí ⇒ chữ ký vô hiệu (rủi ro "ký khống" đã chặn bằng điều kiện).
- 🟡 **Cổng `Code` không kiểm `REP-003`** (`C8=(c)`, chủ dự án chọn) — chỉ `MergeMain` đọc biên bản `bien-ban-duyet-gd3-*`; rủi ro mã đỏ `REP-003` lọt cổng `Code` nếu không ai đọc cột `Cổng tài liệu` của `07`. Chủ: chủ dự án đã chấp nhận; nếu muốn cổng thật ⇒ dòng `D-yy` + mở rộng `W17-q` (đã bị loại ở C8).
- 🟡 **Grain per-SKU** — `D-318` ①(9) chốt theo **SKU toàn hệ thống** (khớp `PrcCostPrices` không `WarehouseId`); ARCH `W17-1` phải dựng Setting `Pricing:GiaVonTheoKho=false` + `Pricing:ChoNhapLuiNgay`; thêm chiều kho sau = additive.
- 🔴 **Duyệt lô M4** — kế toán (R3-3) duyệt lô trước **release**; `REP-003`/`REP-002`/`ACC-001` đỏ cứng vẫn của người.
- 🟡 **`U-17` không có link handover trên registry** — `PH done` gọi trước khi viết handover (đúng thứ tự `thao-luan` §3.1 bước 5); phiên sau đọc handover theo **số lớn nhất**, không qua registry. Không chặn dây chuyền.
- 🟡 **Nợ kế thừa WP-23** (không thuộc phiên này): P2 retry storm (`D-263`), `D-317` ca Host boot, NCC/HMAC (`R3-6`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-17 (hàng `U-17`) → mở `W17-q` (nợ `D-217` ④).**

- Hàng kế = **`W17-q`** (loại `code`, vai `qa`, nhánh `feat/gd3-wp17-gia-von`, phụ thuộc `U-17` nay đã xong): `tools/probe-cong-tai-lieu.ps1` đọc biên bản người GĐ1 (`docs/bien-ban-duyet-gd1-*.md`, `Hình thức: DUYỆT` + cột `Mã`) làm nguồn ĐẠT cho `Code` và `MergeMain` + ca `-TuKiem` mới + ca ngược (mã không có trong biên bản nào ⇒ ĐỎ) + `Y9`; **cùng commit xoá** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25`. **KHÔNG** mở rộng cho `REP-003` (`D-318` ①(8)).
- Sau `W17-q` ⇒ `W17-0` (doc-gate: BA sửa `prc-001` §15.1/§20.4 + `inv-001` §0 + `rep-003` §0/Q11 cùng nhánh → DOCREV; G1 của `prc-001`/`inv-001` do `decider` duyệt thay theo `D-318`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**; nhánh mới cắt từ đỉnh tích hợp (chưa có commit WP-17 nào trước phiên).
- HEAD trước commit đóng phiên: `5c902ed`.
- Commit phiên: **`git log -1`** (D-318 + status 4 dòng + queue prune + §2.1 ngoại lệ + handover + `07` §13 + registry).
- **Chưa/đã push:** push nhánh WP `feat/gd3-wp17-gia-von` (§5 `chay-phien-code`); **KHÔNG** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

**Hàng rào agent** (phiên `N111`): `decider` ×1 (chế độ **chuẩn bị**, 48 lượt tool, **0** ngoài vùng theo handback, **0** ghi tệp); ORCH tự ghi 4 tệp tài liệu + registry qua `PH`. **0** lần hook chặn dừng.

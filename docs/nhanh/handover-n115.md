# Handover N115 — WP-17 / hàng `W17-1n` (G2) XONG: `decider` tạm duyệt thiết kế ARCH giá vốn `PRC-001` (`D-320`)

> Phiên `N115` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-1n` (loại `duyet-dec`, bước `G2 người`, vai `ORCH · decider` — uỷ quyền `D-318` C4) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: `decider` đối chiếu `docs/features/pricing/PRC-001-arch.md` với `D-318` C4 (7 điều) + C5 (8 tiêu chí) ⇒ **`D-320`** `TẠM CHỐT (DEC)`;
> ORCH ghi dòng biên bản G2 + hàng đợi. **0 `.cs` sản phẩm · 0 tệp FE · 0 migration · 0 `pg_dump`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Chọn hàng + nhánh | ORCH | `PH next -Chuoi gd3-1` ⇒ `W17-1n`; `PH check` **OK**; nhánh đúng `feat/gd3-wp17-gia-von`, cây sạch; `PH start` ⇒ **`PHIEN=N115`** |
| G2 đối chiếu | `decider` (Opus) | **C4 7/7 ĐẠT** (additive · 4 cột nullable · enum dải mới `D-17` · 0 chạm `XC-001`/`RoundingPolicy` · `Money` · **14 ca K-06** K06-01…13 + 08b · bảng schema §4.1 + revert §4.3/§9) · **C5 7/8 ĐẠT** · **(vi)** nửa thiết kế đạt, nửa máy (testplan) **không thể đạt ở cổng thiết kế** (testplan thuộc `W17-4`) ⇒ **không chặn G2**, thành **điều kiện cứng của `W17-4`** (cấm bỏ/nới ca) |
| Ghi quyết định | `decider` | Thêm **`D-320`** vào `docs/nhanh/decisions.md` (dòng 334), trạng thái `TẠM CHỐT (DEC, uỷ quyền D-318 C4)`; **một dòng** `D-320` vào `docs/agents/hang-doi-xem-lai.md` |
| Biên bản G2 | ORCH | `docs/bien-ban-tam-duyet-gd3-wp17-2026-11-28.md` thêm mục **"G2 — Tạm duyệt tài liệu ARCH giá vốn `PRC-001`"**, dòng `PRC-001 (ARCH)` (khuôn tiền lệ `PRO-002 (ARCH)`/`D-277`); sửa câu lùi ở "Tóm tắt" `D-320` ⇒ `D-321` (tránh trùng số) |
| `07` §13 | ORCH | Thêm **một dòng** tiến độ `N115` có số đo |

**Baseline mở phiên (`N115`) — phiên thuần tài liệu, không chạy build/test đầu phiên:**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đầu phiên) | 0 | PASS · **1043** link · **1976** backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **214** khối · **0** lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` (đầu phiên) | 0 | **XANH** |
| `docker compose ps` | 0 | `ecommerce-postgres` + `ecommerce-redis` healthy (5433/6380) |

**DoD của hàng — ORCH tự chạy (không tin số vai báo), sau khi ghi biên bản:**

| Cổng | Lệnh | Số đo |
|---|---|---|
| `check-agent-artifact -Kind decision` | `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **exit 0** · 114 dòng |
| DOCREV ARCH | `check-agent-artifact -Kind docreview -Path docs/reviews/PRC-001-arch-docreview.md` | **exit 0** · `verdict=DOC-OK` · P1=0 · P2=0 |
| Biên bản tạm duyệt có dòng G2 | `docs/bien-ban-tam-duyet-gd3-wp17-2026-11-28.md` §G2 | dòng `PRC-001 (ARCH)` · `Kết luận DEC` = **Tạm duyệt (M2)** |
| Cổng `Code` WP-17 | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | **exit 0 · XANH** (sau khi thêm mục G2) |
| Tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** · **1046** link · **1988** backtick · 0 chết |
| Build/test (giữa phiên, `decider` đo) | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 (377,2 s)** — build **0 Error · 9 Warning** (không mới) · unit **2650/2650** · integration **162/162** · DB bất biến **25/25** · test cổng **60/60** · `schema.d.ts` drift 0 |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W17-1n`.** Các mục dưới là **cắt có quyết định**:

- **C5 (vi) nửa máy** — `check-agent-artifact -Kind testplan` chưa có testplan `PRC-001-testplan.md`; là **điều kiện cứng của `W17-4`** (`D-320`), không phải việc của G2.
- **`PRC-001-AQ2`** (nhập lùi đụng kỳ đã duyệt: revision thay vì từ chối) — **người** quyết nếu muốn (đổi unique = phá huỷ schema, `D-318` C6).
- **`PRC-001-AQ3`/`AQ4`/`AQ5`** (loại chuyển động + tiền tệ đơn giá nhập · nhập thiếu `UnitPrice` · nhịp chạy động cơ) — chưa chốt; ảnh hưởng trực tiếp code `W17-2` ⇒ ORCH phiên `W17-2` phải gửi `decider` (AQ3 kèm `ba`) **trước** khi code.
- **P3=10** của DOCREV ARCH — không chặn G2; dọn dần khi chạm code (`W17-2`).
- **Nợ chữ `D-186` ②** — ARCH dùng port trần `ICostPriceLookup` (2 method) thay "method của `ICostPriceAppService`" vì `[Authorize]` cấp class; **giữ đúng thực chất**; `arch`+`ba` ghi chú/khớp chữ `prc-001` §20.4 **cùng PR `W17-2`**; muốn đổi hẳn chữ = **người**.

## 3. Migration đã áp

**Không có.** 0 `.cs`, 0 migration, không chạm DB ⇒ không `pg_dump` (K-01 không kích hoạt). ARCH chỉ **mô tả** migration cho `W17-2` (expand-only: 1 bảng mới `PrcCostEngineRuns` + 4 cột nullable `PrcCostPrices` + 2 index NND; `pg_dump` **bắt buộc** trước khi áp vì `PrcCostPrices` có thể có dữ liệu).

## 4. Quyết định mới

- **`D-320`** — `TẠM CHỐT (DEC, uỷ quyền D-318 C4)`: tạm duyệt **G2** = thiết kế `PRC-001-arch.md` cho code `W17-2` trên nhánh WP; C4 7/7 · C5 7/8; (vi) → điều kiện cứng `W17-4`; `Q4` giữ `Open`; `REP-003` không mở lại; điều kiện `W17-2` (`0014`/`0015` sau `0011`; khoá `pricing:cost-sku:` đúng `XC-003`; khớp chữ `D-186` ②). `reviewBefore = release`.
- **Không có quyết định của người** trong phiên. `D-318`/`D-319` giữ nguyên hiệu lực.

## 5. Nợ / rủi ro

- 🟠 **`Q4` / `PRC-001-AQ1`** (`CostPrice` duyệt 2 bước) — câu **kế toán trưởng** (M4/R3-3), vẫn `Open`; **chặn release**.
- 🟠 **`PRC-001-AQ3`/`AQ4`/`AQ5`** — cần DEC (AQ3 kèm BA) **trước `W17-2`**; AQ2 = người.
- 🟡 **`W17-2` là hàng code đầu tiên của WP-17** — migration chạm `PrcCostPrices` có thể có dữ liệu ⇒ **`pg_dump` trước khi áp** (K-01); một chủ migration (chuỗi tuần tự).
- 🟡 **P3=10** của DOCREV ARCH — dọn khi chạm code.
- 🟡 **Nợ chữ `D-186` ②** — `arch`+`ba` trong PR `W17-2`; đổi chữ = người.
- 🟡 Nợ kế thừa (không thuộc phiên): `check-feature-doc` của `prc-001`/`inv-001` (nợ WP-14); `D-186` dẫn tệp sai dòng; P2 retry storm WP-23.

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng cách nào |
|---|---|---|---|---|
| 1 | `Q4`/`PRC-001-AQ1`: `CostPrice` có bắt buộc luồng duyệt 2 bước (nhập → kế toán trưởng duyệt) không? | Giữ **(a) 2 bước** cho GĐ3 (thiết kế đã chạy được cả 2; (b) chỉ đổi 1 chỗ trong `CreateAsync`, 0 schema) | Thiết kế theo (a); `Q4` giữ `Open`, không chặn `W17-2` | Kế toán duyệt lô M4/R3-3 trước release — dán sau `/chay-phien-code chuoi=gd3-1` |
| 2 | `PRC-001-AQ2`: nhập lùi ngày đụng kỳ **đã duyệt** có cần revision không? | Giữ **(a) từ chối** (`NÉM 0015`) cho GĐ3; (b) revision = đổi unique = phá huỷ schema | Hiện thực (a) ở `W17-2`; (b) không làm | Người quyết nếu muốn (b) — dán sau lệnh phiên kế |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-1n` → mở `W17-2` (F3 BE — đường ghi + duyệt + động cơ giá vốn; migration expand-only + `pg_dump`).**

- `W17-2`: đọc `D-320` (điều kiện `0014`/`0015` sau `0011`, khoá `XC-003`) + ARCH §4/§5/§6; gửi `decider` chốt **AQ3/AQ4/AQ5** (AQ3 kèm `ba`) **trước** khi code.
- Migration: `pg_dump` **trước** khi áp (bảng `PrcCostPrices` có thể có dữ liệu — K-01); áp bằng `dotnet run --project src/eCommerce.DbMigrator` (cấm `dotnet ef database update`).
- `W17-4`: dựng testplan `PRC-001-testplan.md` (đặt trong `docs/tests/`) 14 ca K-06 + negative control ⇒ `check-agent-artifact -Kind testplan` exit 0 (điều kiện cứng của `D-320`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**.
- Commit phiên: **`8b2d300`** — `docs(nhanh): N115 - W17-1n G2 tam duyet ARCH gia von D-320 (PRC-001)` (6 tệp: biên bản G2 · `D-320` · hàng đợi · `07` §13 · registry 2 tệp).
- **Đã push** `origin/feat/gd3-wp17-gia-von`. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

---

**Hàng rào agent** (phiên `N115`): `decider` ×1 agent (41 lượt công cụ, 5 truy vấn đồ thị) · **0 tệp ngoài vùng** · **3 cảnh báo** `artifacts/agents/canh-bao.jsonl` (2 của `docrev` phiên N114 còn lưu + 1 của `decider` "thiếu khối JSON cuối" do hook không đọc được transcript — **báo cáo có đủ khối JSON**, ORCH tự kiểm mọi số đo bằng máy).

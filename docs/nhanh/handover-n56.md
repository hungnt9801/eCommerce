# Handover N56 — WP-14 · hàng P-14 (doc-gate đợt C: `REP-001` DỞ, `REP-002` chưa chạy)

> Phiên `N56` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N55`.
> Hàng `P-14` (`doc-gate`) **DỞ**: `REP-001` bị chặn ở DOCREV vòng 2 (0 P1 · **8 P2**), `REP-002` chưa chạy (luật "≥ 5 câu gửi decider ⇒ một tài liệu/phiên" — `REP-001` đã dùng 5 câu).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (**190** tệp · **869** link · **1159** đường dẫn backtick) — *baseline: 188/859/1137* |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi (baseline và cuối phiên như nhau) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/reporting/rep-001-read-model-job-tong-hop.md -WP14` | 0 | **DAT** — 16/16 heading · **17/17 BR** ở §1 có test ở §14 · mục "Đối chiếu mô hình Nhanh" dòng 1008 · 3 dòng `Open` còn lại là **mô tả lịch sử** trong bảng đối chiếu |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/REP-001-docreview.md` | 0 | DAT — verdict **`DOC-CHANGES`** (vòng 2: P1=0 · P2=8 · P3=7; vòng 1: P1=1 · P2=8 · P3=4) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — **59** dòng kiểm (tới `D-174`) |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK: bảng khớp registry (đầu phiên; cuối phiên chạy lại sau `PH dodang`) |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N56):** link PASS 188 tệp/859 link/1137 backtick · mermaid 211/0 · hooks 40/40 · `PH check` OK. Build/test **bỏ qua có lý do** "WP thuần tài liệu" (`D-71`; build máy dev đỏ `MSB3491` — chủ `WP-15`).
- **`REP-001` — BA đối chiếu (v0.1 → v0.2, 471 thêm/613 xoá):** tài liệu viết trước và mâu thuẫn với code as-built `WP-08` (module `Reporting` + **8** bảng `Rpt*` + job tổng hợp + `ReportAppService` đã tồn tại). Sửa thân theo `D-47`/`D-48` (grain dòng chứng từ, thay thế theo `(ngày, nguồn)`, doanh thu `Σ thành tiền dòng − chiết khấu chứng từ`, VAT tách, loại chứng từ huỷ), thêm POS `RetailBill` làm nguồn (`D-18`/`D-42`), bỏ `ProductVariantId` (`D-07`), tồn 9–10 chiều (`D-27`), số lượng `decimal(18,3)` (`D-31`), mã lỗi `eCommerce:Reports:*`, quyền `eCommerce.Reports`.
- **D1q — 5 câu → 5 quyết định `TẠM CHỐT (DEC)`** (uỷ quyền `D-114` M4, hồ sơ HIỆU LỰC):
  - `D-170` (Q1): INV-001 ghi **2 bút toán đối ứng** `TransferOut`/`TransferIn`, cùng `ReferenceLineId`, một UoW; `REP-003` đọc `ClosingQty` theo **từng `(WarehouseId, Sku)`**, cấm `StockMovementType.Transfer=6`, `numeric(18,3)`.
  - `D-171` (Q2): `EXCLUDE USING gist` + `btree_gist` **đã chạy thật** trên `postgres:17-alpine` (Tax migration + `PostgreSqlSchemaTests` 96/96); bắt buộc **4 ca integration** trước `migrations add` của bảng `RepPeriodClose`, mã hoá `Status` vào khoá (EXCLUDE không có `WHERE`), fallback `UNIQUE` + trigger.
  - `D-172` (Q4): ABP Setting **theo tenant**, mặc định **job 5 phút · lookback 3 ngày · IsStale 900 s** (KHÔNG 15 phút); code hiện 15 phút là chỗ lệch cần sửa (`ReportingOptions.cs:17-18`, `ReportingAggregationWorker.cs:31`); một `Timer.Period`, cấm cache Setting lúc khởi động.
  - `D-173` (Q6): `REP-001` **không** có bảng/cột/job COD; số COD lấy từ bảng đối soát `REC-003`, chỉ dòng đã khớp/đã xử lý, cờ `CodUnreconciled` ở `REP-003`; không tạm duyệt `REC-003`.
  - `D-174` (Q8): **không** thêm `RepSalesHourlyFact` ở GĐ3; tổng hợp giờ lúc đọc bằng `GROUP BY date_trunc('hour', "OccurredAtUtc")`; mở lại khi `CR-P2` xong + vượt `NFR-REP-001-010`.
- BA áp 5 quyết định (D1s exit 0), rồi **DOCREV vòng 1** `DOC-CHANGES` (1 P1 · 8 P2) → BA sửa đúng P1/P2 → **DOCREV vòng 2** `DOC-CHANGES` (**0 P1 · 8 P2**) ⇒ hết trần 2 vòng ⇒ ghi [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md) mục `REP-001`.
- Tạo [`docs/agents/nhap-bien-ban-gd3-c.md`](../agents/nhap-bien-ban-gd3-c.md) (**NHÁP — chưa có hiệu lực duyệt**) — dòng `REP-001` đề xuất **"chưa"**, 5 dòng còn lại của đợt C ghi "chưa bắt đầu"; hàng `P-18` điền đủ.
- Cập nhật `docs/nhanh/07-ke-hoach-trien-khai.md` §13 thêm dòng `N56`.

## 2. Chưa xong / cắt giữa

Hàng `P-14` **chưa đạt DoD** (DoD: mỗi mã `check-feature-doc` exit 0 · DOCREV `DOC-OK` · D3 có dòng · D4 xanh). Trạng thái từng mã:

1. **`REP-001` — chặn ở D2 sau vòng 2** (`DOC-CHANGES`, 0 P1 · 8 P2 · 7 P3). 8 P2 còn mở (chi tiết + dẫn chứng code trong [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md)):
   1. §0 Tóm tắt còn "sự kiện tăng dần + job đối chiếu" (trái `D-47`).
   2. §5.2.1 + BR-004 ghi tiền `numeric(18,4)`; schema as-built `numeric(18,2)`.
   3. TC-20 đòi `TenantId` NOT NULL; entity/migration/§5.2.1 đều nullable.
   4. §14.2/F6 nói chưa có `test/eCommerce.IntegrationTests`/Testcontainers — thực tế đã có.
   5. BR-002 cấm API đọc bảng giao dịch nhưng `GetTongQuanDonHangAsync` đọc thẳng `OrdOrders`/`OrdOrderLines`; TC-02 không bắt.
   6. §1 bước 7 dùng route `/api/v1/reports/danh-thu`; thật là `/revenue`.
   7. §3.1 khoá `report:job:{jobName}:{tenantId}` không khớp §8.2/as-built `report:tong-hop`.
   8. §6.2 khoá JSON `dinhNghia`; DTO là `DinhNghiaTien`.
2. **`REP-002` — chưa chạy.** Luật `/doc-gate` §0: tài liệu dự kiến ≥ 5 câu decider ⇒ **một tài liệu/phiên**; `REP-001` đã dùng đủ 5 câu. `REP-002` là **đỏ cứng** (`D-114` §2.1) — cần người/kế toán ký, DEC chỉ tạm chốt câu thiết kế theo M4; chốt giá vốn (`D-114` P2) là đầu vào bắt buộc.

## 3. Migration đã áp

Không có (WP thuần tài liệu; 0 tệp `.cs`, 0 migration).

## 4. Quyết định mới

- **`D-170`** — `TẠM CHỐT (DEC)` (uỷ quyền `D-114` M4): INV-001 ghi 2 bút toán `TransferOut`/`TransferIn`; `REP-003` đọc theo từng kho; cấm `Transfer=6`; `numeric(18,3)`. Lùi = thêm dòng thay thế; 0 code.
- **`D-171`** — `TẠM CHỐT (DEC)`: dùng `EXCLUDE USING gist` + `btree_gist` cho `RepPeriodClose`, kèm điều kiện 4 ca integration + fallback `UNIQUE` + trigger. Lùi = revert tài liệu; khi có bảng = migration additive `DROP CONSTRAINT`.
- **`D-172`** — `TẠM CHỐT (DEC)`: ABP Setting theo tenant, mặc định **5 phút/3 ngày/900 s**; code 15 phút là lệch cần sửa. Lùi = 3 giá trị Setting.
- **`D-173`** — `TẠM CHỐT (DEC)`: COD ở `REC-003`, `REP-001` không có bảng/cột/job COD. Lùi = 1 dòng `D-xx` + mở lại Q6; 0 code.
- **`D-174`** — `TẠM CHỐT (DEC)`: không bảng fact giờ ở GĐ3; tổng hợp giờ lúc đọc. Lùi = xoá dòng; 0 code.
- **Không** dòng `ĐÃ CHỐT` nào mới; không đảo dòng cũ. `decisions.md` = **59** dòng kiểm, DAT.

## 5. Nợ / rủi ro

- **`REP-001` chưa `DOC-OK`** ⇒ **không** trình duyệt, không dùng làm căn cứ code. Còn **8 P2** (sai as-built: kiểu tiền, route, DTO, `TenantId`, Testcontainers, cơ chế bảo vệ BR-002) — đều là lỗi **sai im lặng** nếu người code làm theo.
- **`REP-002` (đỏ cứng)** chưa chạy — cần kế toán; `ACC-001` cùng đợt cũng đỏ cứng (`P-19`).
- **Nợ chéo bắt buộc** (chủ khác, không sửa ở WP-14): `REP-003` §5.2 dòng 256 phải đổi `StockMovementType.Transfer=6` → `TransferIn`/`TransferOut` + `numeric(18,3)` (`D-170`); `XC-006` §14.5 cảnh báo `EXCLUDE` "chưa từng chạy thật" đã lạc hậu (`D-171`).
- **P3 #10–#13** (7 dòng) để người duyệt: ERD 6/8 bảng · "23" vs 25 test · metric `*_upserted` · `NhipMacDinhPhut` vs `NhipJobPhut` · empty-state bảo chạy Reconciliation/Rebuild (chưa có) · TC-13 quét route `ReportAppService` là no-op.
- **Chờ người:** `P-19` (duyệt đợt C — bắt buộc kế toán cho `ACC-001`/`REP-002`), `P-21` (merge PR, người), `P13` (mở khoá `LOY-001`). Bảng đầy đủ ở `docs/agents/nhap-bien-ban-gd3-c.md` §3 + `hang-doi-xem-lai.md`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG `P-14` → mở lại `P-14`.**

- BA sửa **8 P2** `REP-001` theo [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md) → `check-feature-doc.ps1 -WP14` → **DOCREV vòng kiểm nhanh** (`changedSections`; nếu sửa P2 buộc chạm P3 cùng dòng thì ghi rõ).
- `REP-001` `DOC-OK` ⇒ chạy `REP-002` (dự kiến nhiều câu kế toán → gửi decider theo M4 tuần tự; nếu ≥ 5 câu thì **một tài liệu/phiên**).
- `REP-002` `DOC-OK` ⇒ `PH done -Id P-14`; nếu vẫn dở ⇒ `PH dodang` với lý do mới.
- Lệnh xác nhận hàng: `pwsh -File tools/agents/phien.ps1 next`.

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; `HEAD` trước commit phiên = `be9e6b9`, `main` = `be301c5`. Commit phiên `N56` = commit tạo handover này (SHA ghi ở commit cập nhật kế tiếp), đã push lên `origin/docs/n22-wp14-dot-a` ở bước §5. Chưa PR.

---

**Hàng rào agent** (phiên N56): **10** lượt gọi vai — `ba` ×3 (+1 lượt tiếp sau trần 80 lượt của lượt đầu), `decider` ×5 (`D-170…D-174`), `docrev` ×2. **0** lần chặn dừng (`lanChan = 0`). **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`; `canh-bao.jsonl`: `ngoaiVung: []` ở mọi lượt). **4** cảnh báo `SubagentStop` "thiếu khối JSON cuối" (ba ×2, decider ×1, docrev ×1) — **dương tính giả** đã biết (`D-104`: hook chỉ ghi nhật ký; mọi báo cáo thật đều có đủ khối JSON). Decider dùng đồ thị: `codegraph_explore` (Q1, Q2, Q4, Q8) · `gitnexus query` (Q6).

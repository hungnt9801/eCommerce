# Handover N59 — WP-14 · hàng P-15 (doc-gate đợt C): `REP-003` + `ACC-001` `DOC-OK`, P-15 XONG

> Phiên `N59` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N58`.
> Hàng `P-15` (`doc-gate`) **XONG**: `REP-003` và `ACC-001` đều đạt `DOC-OK` (0 P1 · 0 P2) sau 2 vòng đầy đủ + 1 vòng kiểm nhanh.
> **21 quyết định mới** `D-176`…`D-196` (toàn bộ `TẠM CHỐT (DEC)`, uỷ quyền `D-114`).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (**196** tệp · **884** link · **1259** đường dẫn backtick) — *baseline N59: 194/882/1172* |
| `npm --prefix tools run check:mermaid` | 0 | **212** khối · 0 lỗi (*baseline: 212/0*) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **48/48** pass |
| `check-feature-doc -WP14` `rep-003` | 0 | **DAT** — 16/16 heading · 14/14 BR có test · mục đối chiếu Nhanh dòng 974 · 0 câu `Open` |
| `check-feature-doc -WP14` `acc-001` | 0 | **DAT** — 16/16 heading · **18/18** BR có test · mục đối chiếu Nhanh dòng 1093 · 1 câu `Open` (`Q12`) |
| `check-agent-artifact -Kind docreview` `REP-003` | 0 | DAT — **`DOC-OK`** vòng 3 kiểm nhanh (P1=0 · P2=0 · P3=4) |
| `check-agent-artifact -Kind docreview` `ACC-001` | 0 | DAT — **`DOC-OK`** vòng 3 kiểm nhanh (P1=0 · P2=0 · P3=5) |
| `check-agent-artifact -Kind decision` | 0 | DAT — **81** dòng kiểm (tới `D-196`) |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK: bảng khớp registry (đầu phiên; cuối phiên đo lại sau `done`) |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N59):** link PASS 194 tệp/882 link/1172 backtick · mermaid 212/0 · hooks 48/48 · `PH check` OK. Build/test **bỏ qua có lý do** "WP thuần tài liệu" (`D-71`).
- **`REP-003`** (nợ chéo `D-170` đã đóng): BA đối chiếu as-built + áp `D-170` — đọc `StockLedger` theo từng `(WarehouseId, Sku)`, `TransferOut`/`TransferIn`, cấm `StockMovementType.Transfer=6` legacy, mọi cột số lượng `numeric(18,3)`. 11 câu §15 → decider **11 dòng** `D-176`…`D-186`; BA áp ở lượt 2 (thêm TC-27…TC-37) và lượt 3 (`D-186`). DOCREV vòng 1 `DOC-CHANGES` (**2 P1** · 7 P2): `QuantityDelta` lưu độ lớn **dương** nên `ClosingOnHand` phải cộng **có dấu** theo `MovementType`; `OrderLine.LineTotal` **gồm** `TaxAmount` nên doanh thu phải dùng `ThanhTienDongDonHang`. BA sửa hết → **vòng 2 `DOC-OK`** (0 P1 · 1 P2: tiền tố khoá `ecommerce:` đôi ở §8.2) → BA sửa → **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 4 P3).
- **`ACC-001`**: BA đối chiếu as-built (Reporting có một phần; **chưa** có `IReportFactReader`/`AccExport*`/`AccountingHub`, cột `CostUnitAmount`/`CostMissing` là kế hoạch) + áp `D-183`/`D-186`/`D-177`/`D-181`/`D-179`. 10 câu §15 → decider **10 dòng** `D-187`…`D-196`; BA áp ở lượt 2 (thêm BR-17, TC-25…TC-33) và lượt 3. DOCREV vòng 1 `DOC-CHANGES` (0 P1 · 6 P2: metric registry thật **16** không phải 12, khoá kho **không** tiền tố `ecommerce:`, `TongThanhToan` bỏ `ChietKhau` ⇒ sai tiền, `AccExportRuns` thiếu cột lý do huỷ/supersede, `TienTe` mâu thuẫn `D-195`, thiếu cạnh `DangTao→DaHuy`) → BA sửa hết (thêm BR-18, TC-34) → **vòng 2 `DOC-OK`** (0 P1 · 1 P2: hàng rào huỷ `Status = DaTao`) → BA sửa → **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 5 P3).
- **D3:** cập nhật [`docs/agents/nhap-bien-ban-gd3-c.md`](../agents/nhap-bien-ban-gd3-c.md) — `REP-003` + `ACC-001` đều **đề xuất trình duyệt**; mục §2 thêm 2 gạch đầu dòng N59; §3 cập nhật việc còn lại.
- **Điểm xanh:** 2 commit (`17e6b38` REP-003, `3f8059b` ACC-001), đều đã push.

## 2. Chưa xong / cắt giữa

Không. Hàng `P-15` đạt DoD cho cả hai mã (mỗi mã `check-feature-doc` exit 0 · DOCREV `DOC-OK` · D3 có dòng · D4 xanh).

- **`ACC-001` `Q12`** (hạn lưu trữ artifact kế toán) giữ `Open` — cố ý: số pháp lý thuộc R3-3 (kế toán trưởng), `D-196` chỉ chốt **mặc định TẮT** (không xoá). Không chặn code xuất file.
- **`REP-003` 4 P3 + `ACC-001` 5 P3** để người duyệt (không sửa sau `DOC-OK`).
- **Nợ chéo tài liệu** (chủ khác, không thuộc P-15): `D-186` yêu cầu `inv-001` §0 (:56-57) và `prc-001` §15.1/§20.4 sửa cùng PR — **chưa sửa** trong phiên này (ngoài phạm vi 2 mã của hàng); `D-193` yêu cầu đóng `not-002` Q9 (:954) khi chạy `P-16`/`P-17`.

## 3. Migration đã áp

Không có (WP thuần tài liệu; 0 tệp `.cs`, 0 migration).

## 4. Quyết định mới

**21 dòng `TẠM CHỐT (DEC)`** (uỷ quyền `D-114`/`D-115`, hồ sơ HIỆU LỰC), mọi dòng có ④ Lùi + ⑤ Xem lại trước = release:

| Mã | Chủ đề |
|---|---|
| `D-176` | `REP-003` Q1 — vòng quay = tồn bình quân 2 đầu kỳ; `CoveredDays` thiếu ⇒ banner, không nội suy |
| `D-177` | `REP-003` Q2 — giá vốn bình quân gia quyền di động, snapshot theo **ngày bán**, chỉ `DaDuyet`, thiếu ⇒ NULL + `CostMissing` |
| `D-178` | `REP-003` Q3 — làn trong ngày <15 phút bằng **một** job `D-172`; L2 tắt ⇒ ghi T+1, không hạ NFR |
| `D-179` | `REP-003` Q4 — phí sàn dự kiến + `SettlementPending`, `ChannelFeeActualAmount` luôn NULL ở GĐ3 |
| `D-180` | `REP-003` Q5 — grain `(ngày, ChannelCode, ChannelShopId)`, **bỏ sentinel** `Guid.Empty`, `NULLS NOT DISTINCT` |
| `D-181` | `REP-003` Q6 — tách `GrossMargin1Amount`/`GrossMargin2Amount`; không trừ VAT lần hai |
| `D-182` | `REP-003` Q7 — retention chi tiết ngày mặc định **TẮT**; 24 tháng chỉ là đề xuất |
| `D-183` | `REP-003` Q8 — **ACC-001 tự đọc read-model** qua `IReportFactReader`; ACC-001 sở hữu file + hash (khác khuyến nghị BA) |
| `D-184` | `REP-003` Q9 — công cụ so sánh read-model ↔ nguồn: một method GET + script mỏng + permission riêng |
| `D-185` | `REP-003` Q10 — `SaleQty` gồm `Sale` + `RetailSaleOut` + `ShippingOut` |
| `D-186` | `REP-003` Q11 — chủ giá vốn = **Pricing/PRC-001**; INV-001 chỉ cấp đơn giá; ACC-001 chỉ tiêu thụ |
| `D-187` | `ACC-001` Q1 — không seed bảng tài khoản Bravo/MISA; `LegalBasisNote` bắt buộc; mã mới `0012` |
| `D-188` | `ACC-001` Q2 — CSV cấu hình được, 1 hồ sơ `csv-generic`; `Xlsx`/`htkk` fail-closed; 0 gói NuGet |
| `D-189` | `ACC-001` Q3 — `htkk-mau01` **bị chặn** tới khi có file mẫu thật (R3-3) |
| `D-190` | `ACC-001` Q4 — `IAccArtifactStore` + đĩa app + `File.Move` atomic + SHA-256; job chỉ dọn file tạm/mồ côi |
| `D-191` | `ACC-001` Q5 — PII mặc định **tắt**; chỉ `HoaDon` + permission `Export.BuyerPii` + cờ `ContainsBuyerPii` |
| `D-192` | `ACC-001` Q6 — không mã hoá/ZIP; tải đúng byte + audit `[ACC-DOWNLOAD]` |
| `D-193` | `ACC-001` Q9 — chi phí thông báo là **chi phí kỳ**, không vào giá vốn; kỳ theo `OccurredAtUtc` |
| `D-194` | `ACC-001` Q10 — `Reporting:StaleSeconds` 900 s (không tạo ngưỡng thứ hai), fail-closed `0004` |
| `D-195` | `ACC-001` Q11 — **không** xuất dòng giá vốn; giữ 18 cột |
| `D-196` | `ACC-001` Q12 — hạn lưu artifact mặc định `0` = TẮT; `Q12` giữ `Open` cho kế toán trưởng |

## 5. Nợ / rủi ro

- **`REP-003` + `ACC-001` vẫn đỏ cứng** (`D-114` §2.1): `DOC-OK` chỉ là điều kiện **trình duyệt**; **kế toán phải ký** biên bản người (`P-19`) trước khi dùng làm căn cứ code.
- **Chờ người:** `P-19` duyệt đợt C (bắt buộc kế toán cho `ACC-001`/`REP-002`/`REP-003`) · `P-21` merge PR (vùng cấm) · `ACC-001-Q12` hạn lưu trữ · R3-3 (bảng tài khoản thật, file mẫu HTKK, hạn lưu) · R3-8 (hạ tầng lưu file production).
- **Nợ chéo bắt buộc** (chủ khác, không sửa ở WP-14): `inv-001` §0 :56-57 và `prc-001` §15.1/§20.4 phải khớp `D-186`; `not-002` Q9 :954 đóng theo `D-193` khi chạy `P-16`/`P-17`; `D-179` (3) còn ngoặc "khác `Guid.Empty`" — hiểu là "khác `NULL`" theo `D-180`, không sửa dòng `ĐÃ CHỐT` nào.
- **Mâu thuẫn tài liệu đã xử lý bằng quyết định** (cần kế toán xác nhận khi duyệt lô): `REP-003` `NetRevenueAmount` gồm ship thu vs `REP-002` `RevenueNet` không gồm (`D-181` (7)(ii)); `NFR-REP-003-005` (job chỉ 01:00–05:00) vs `D-172`/`D-178` (job 5 phút) — BA đã rà lại trong `REP-003`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-15` → mở `P-16`** (`doc-gate`: `NOT-001`, 133 KB ⇒ một mình).

- `/chay-phien` (sẽ chọn `P-16` qua `PH next`); chạy dây chuyền `/doc-gate NOT-001` — tài liệu 133 KB nên **chỉ một mã** trong phiên.
- Khi chạy `P-16`/`P-17`, đóng thêm `not-002` Q9 theo `D-193` và đối chiếu `D-191` (PII mặc định tắt) với template/hàng đợi thông báo.
- `ACC-001-Q12` + R3-3/R3-8 chỉ vào bảng chờ người ở `P-18`/`P-19`; không chặn `P-16`.

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; `HEAD` = `3f8059b`, `main` = `be301c5`. Hai commit phiên `N59`: `17e6b38` (REP-003) + `3f8059b` (ACC-001), **đã push** lên `origin/docs/n22-wp14-dot-a` (bản cập nhật handover là commit kế). Chưa PR.

---

**Hàng rào agent** (phiên N59): **36** lượt spawn vai — `ba` ×9 (REP-003 ×5, ACC-001 ×4) + `docrev` ×6 (mỗi tài liệu 3 vòng) + `decider` ×21 (11 REP-003 + 10 ACC-001). **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`). **1** lần chạm trần lượt của công cụ: `decider` REP-003 Q9 dừng ở 40 lượt (đã ghi `D-184`, ORCH tự chạy lại cổng) và `ba` ACC-001 áp quyết định dừng ở 80 lượt (ORCH gọi tiếp `SendMessage`, vai hoàn tất + trả JSON). **14** cảnh báo `SubagentStop` "thiếu khối JSON cuối" — **dương tính giả** đã biết (`D-104`: hook chỉ ghi nhật ký; mọi báo cáo đều có khối JSON). Đồ thị: BA + DOCREV dùng `codegraph_explore` trước `Grep`, mọi phát hiện P1/P2 về code đều có dòng `graph:`.

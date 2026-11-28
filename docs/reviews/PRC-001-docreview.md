# Doc review — PRC-001 Bảng giá và giá theo kênh

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` (1234 dòng) |
| Ngày · vòng | 28/11/2026 · **vòng 1** (kiểm nhanh `changedSections` của phiên N113 / WP-17) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1234 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit **1** (**2** lỗi: `BR-PRC-001-008` không có test §14; thiếu mục "Đối chiếu mô hình Nhanh") — **cả hai có trước N113**, ngoài `changedSections` |
| Đối chiếu | `D-186` (dòng 200 `decisions.md`) · `rep-003` §0:47 + Q11:938 · `inv-001` §0:56-57 · `acc-001` §0:43,73 · as-built Pricing/Inventory |

## Đối chiếu as-built (graph — bằng chứng lệnh)

- `graph: codegraph_explore "CostPrice PrcCostPrices ICostPriceAppService StockDocumentLine UnitPrice"` → `CostPrice` class `modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:36`; `DaDuyet` **:68**; `CoHieuLucTai` **:114**; `Duyet` **:121-140**; `Amount` computed **:54**; `ICostPriceAppService` tại `modules/Pricing/eCommerce.Pricing.Application.Contracts/IPriceAppServices.cs:42` — **đúng 2** method đọc `GetListAsync` **:49** / `GetAsync` **:51**; `StockDocumentLine` + `UnitPrice` khai báo ở `modules/Inventory/eCommerce.Inventory.Domain/StockDocument.cs:367`, gán ở **:466**.
- `PrcCostPrices` là **tên bảng thật**: `modules/Pricing/eCommerce.Pricing.EntityFrameworkCore/PricingDbContextModelCreatingExtensions.cs:102` (`b.ToTable("PrcCostPrices")`) + 3 `CHECK` **:108-113** + unique `(TenantId, Sku, EffectiveFrom)` **:128-130**; `CostPriceAppService` gắn `[Authorize(CostPriceView)]` **:24** cấp class và **chỉ đọc** (`…/CostPriceAppService.cs:24-36`).
- Dự án `Inventory.Application.Contracts` **có thật** (`modules/Inventory/eCommerce.Inventory.Application.Contracts/StockDocumentDtos.cs`) ⇒ Q8 không nhắc một project không tồn tại.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc toàn văn + `grep` các cặp dễ mâu thuẫn của phần đổi: Q8 (:897) ↔ §20.4 mục 1 (:1217-1219) ↔ Q4 (:893) ↔ §20.1 #5 (:1187) ↔ §20.3 #3-4 (:1211-1212) ↔ §6.1 `ApproveCostAsync` (:441) ↔ §9 `.Approve` (:645); `grep 'INV-001\|ACC-001'` toàn tài liệu = **chỉ Q8/v0.7** (không có chỗ nào nói giá vốn thuộc Inventory/ACC) | 1 phát hiện (#1, tham chiếu `Q2` cũ) |
| D2 chéo tài liệu / code / `D-xx` | Q8 vs `D-186` (dòng 200 `decisions.md`): chủ Pricing · `PrcCostPrices` · đường ghi + động cơ bình quân gia quyền di động + duyệt · INV-001 cấp đơn giá · ACC-001 tiêu thụ · port chỉ đọc ở `Inventory.Application.Contracts` · WP GĐ3 **trước** `REP-002` (D-186 (7)) — **khớp từng vế**; `rep-003` Q11:938, `inv-001`:56, `acc-001`:73 **khớp**; as-built Pricing **đúng** | 0 |
| D3 cơ chế thực thi | Q8 là hàng câu hỏi chủ sở hữu, **không** phải BR ⇒ không cần hàng rào mới. Các BR cũ (BR-008/BR-012) **không đổi**; cơ chế cũ vẫn nguyên (UNIQUE §5.2 :391-398) | 0 |
| D4 SQL chạy được | `grep '^```sql\|^```powershell\|^```bash'` = **0** khối ⇒ không có gì để chạy trên PostgreSQL; `npm --prefix tools run check:mermaid` → **214 khối · 0 lỗi** | 0 |
| D5 test/probe/tệp có thật | Không có tên test/probe mới trong phần đổi. Tên được nhắc đều thật: `PrcCostPrices`, `Inventory.Application.Contracts`, `REP-002`, `INV-001`, `ACC-001`, `D-186` (graph + grep). `CostPrice.Duyet` được nhắc **gián tiếp** ("+ duyệt") và có thật :121-140 | 0 |
| D6 nhãn tin cậy | Q8 dán `✅ Closed (D-186)` — **đo được**: dòng `D-186` tồn tại, nội dung trùng khớp; §20.4 dùng thì tương lai ("sẽ có ở WP code GĐ3") **không** dán ✅. Không có số liệu mới không nguồn | 0 |
| D7 14 quy tắc + đối chiếu Nhanh | Phần đổi **không** chạm BR/NFR/Mermaid/API; tiền vẫn `decimal`/`Money` (BR-001/002). Mục "Đối chiếu mô hình Nhanh" **vẫn thiếu** — lỗi **có trước**, ngoài `changedSections` (xem mục "Ngoài phạm vi") | 0 (trong phạm vi) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §20 mở đầu :1177 · §20.3 #3 :1211 | Hai chỗ này vẫn ghi *"nguồn giá vốn là **`Q2`** chưa chốt"*, trong khi bản sửa đã chốt chủ sở hữu + WP GĐ3 ở Q8/§20.4. `Q2` **trong tài liệu này** là `MissingPricePolicy` (§15.1 :891) — người đọc dễ hiểu nhầm thành câu hỏi về giá vốn còn mở. Đây là văn bản **có trước**, nhưng nay đứng cạnh câu đã chốt nên thành hai phát biểu trái nhau về *vì sao* chưa mở đường ghi. | Ghi rõ `Q2` là câu của bản bàn giao (`ban-giao-gd1-con-lai`) **khác** Q2 §15.1, hoặc thay bằng: *"đường ghi chưa mở ở `S12`; nay thuộc WP code GĐ3 — `D-186`"*. |
| 2 | P3 | §15.4 :925 · header :11 | Dòng **v0.7** đặt ở §15.4, còn **v0.6** nằm ở §20.5 (:1234) — người đọc §20 (lát cắt mới nhất về giá vốn) thấy v0.6 là bản mới nhất; header vẫn `v0.1 — 13/10/2026`. Không ảnh hưởng hành vi. | Thêm dòng v0.7 vào §20.5 (hoặc ghi "xem §15.4") và đồng bộ header `Phiên bản / ngày`. |
| 3 | P3 | §15.1 Q8 :897 | Q8 nói "`INV-001` chỉ cấp đơn giá nhập để tra cứu" nhưng **không** nêu as-built; `D-186` yêu cầu BA ghi rõ ánh xạ *"`UnitCost` (tài liệu) ↔ `UnitPrice` (as-built)"*. Người code WP GĐ3 có thể đi tìm cột `UnitCost` không tồn tại (as-built: `StockDocumentLine.UnitPrice`, `StockDocument.cs:367`/`:466`). | Thêm vào Q8: *"as-built `StockDocumentLine.UnitPrice` (`StockDocument.cs:367`) — ánh xạ `UnitCost` (tài liệu) ↔ `UnitPrice`, không đổi tên (D-09)"* như `rep-003` Q11 đã làm. |

## Ngoài phạm vi thay đổi (ghi nhận, **không** tính vào verdict vòng 1)

- `check-feature-doc` lỗi 1 — **`BR-PRC-001-008` chưa có test ở §14**: đọc `§14.1` (:751-774) không có TC nào phủ BR-008; `§14.3` :853 lại khẳng định *"22 test case … phủ hết 14 BR"* ⇒ nhãn sai. **Có trước N113**, ngoài `changedSections` ⇒ không tính P1/P2 lần này; cần bổ sung 1 TC (unique `PriceListItems`/`ChannelPrices`) ở lượt chạm toàn tài liệu.
- `check-feature-doc` lỗi 2 — **thiếu mục "Đối chiếu mô hình Nhanh (dd/mm/yyyy)"**: `grep` = **0**, `git log -S` = **không có commit nào từng thêm** ⇒ lỗi **có trước**, không do N113. Theo `.claude/agents/docrev.md` §5, đây là lỗi DoD WP-14 (P1 **toàn tài liệu**), nhưng nằm ngoài `changedSections` nên **không** tính vào verdict của vòng kiểm nhanh này; cần một lượt WP-14 riêng cho `PRC-001`.
- `decisions.md` dòng `D-186` dẫn `StockDocumentLine.cs:362-367` — **không có tệp tên này**; as-built là `StockDocument.cs` (khai báo `UnitPrice` :367, gán :466; `rep-003` :993 dẫn đúng `StockDocument.cs:411,466`). Q8 **không** dẫn dòng nên `PRC-001` không bị ảnh hưởng; nên sửa `decisions.md` ở lượt được phép ghi.
- Ghi nhận **đúng** theo yêu cầu phiên: `Q4` (:893) **vẫn `⬜ Open`**, `§14.4` (:878) **vẫn** chưa tick "Chốt Q4", và §20.4 (:1218) nói rõ *"luồng duyệt 2 bước … vẫn `Open` ở `Q4`"* ⇒ `D-318` C2 được tôn trọng, **không** phải lỗi.
- Ghi nhận as-built ngoài changedSections (không thành phát hiện): `§5.2` (:383) ghi `Currency char(3)` cho `CostPrices` nhưng mapping đặt `HasMaxLength(8)` (`PricingDbContextModelCreatingExtensions.cs:121`); `CHECK '^[A-Z]{3}$'` vẫn chặn dữ liệu sai ⇒ rủi ro thấp, có trước.

## Không kiểm được

- **D4**: tài liệu **0** khối `sql`/`powershell`/`bash` ⇒ không có gì phải chạy trên PostgreSQL (không phải "bỏ qua vì chắc đúng").
- **Test/`dotnet test`**: `PRC-001` vẫn là tài liệu thiết kế; 22 TC §14 là **kế hoạch**, phần giá vốn GĐ3 **chưa có code** ⇒ không có gì để chạy.
- Không đọc lời giải thích của BA; chỉ đối chiếu tài liệu, `D-xx`, tài liệu liên quan và as-built.

## Vòng trước

Không áp dụng — đây là vòng 1.

```json
{
  "agent": "DOCREV",
  "doc": "docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md",
  "artifact": "docs/reviews/PRC-001-docreview.md",
  "round": 1,
  "verdict": "DOC-OK",
  "p1": 0,
  "p2": 0,
  "p3": 3,
  "sqlRun": "không có khối sql",
  "readRanges": ["1-1234"],
  "mustFix": []
}
```

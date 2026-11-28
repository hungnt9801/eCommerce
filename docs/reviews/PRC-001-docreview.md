# Doc review — PRC-001 Bảng giá và giá theo kênh

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` (1319 dòng) |
| Ngày · vòng | 28/11/2026 · **vòng 2** — kiểm lại 3 P2 + 3 P3 `ba` đã sửa ở phiên N116 / `W17-2` |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Phạm vi | Các mục vừa đổi: §15.1 `Q9` · §12 (:727) · §20 (:1189-1196) · §20.3 #3/#4 · §20.4 (mục 1 + bullet fail-closed + bullet tiền tệ + dẫn dòng) · §9 · §6.1 |
| Đã đọc | dòng 1–16 · 417–442 · 631–660 · 701–736 · 892–940 · 1187–1319 (đọc lại trọn mọi mục trong phạm vi; **không** đọc lại toàn văn §0–§19 theo yêu cầu kiểm nhanh) |
| Máy | `check-feature-doc.ps1 -WP14` → **exit 1**, **2 lỗi** (`BR-PRC-001-008` chưa có test §14 · thiếu mục "Đối chiếu mô hình Nhanh") — **có trước N116**, không do lượt sửa này (P3 #7) |
| Đối chiếu | `PRC-001-arch.md` §3.1/§3.3/§5.2/§8.2 · `D-321`/`D-322`/`D-323`/`D-324` · as-built `CostFeedLookupAdapter.cs`, `CostPriceAdminAppService.cs`, `CostEngineAppService.cs`, `DoiSoatTonKho.cs`, `PricingErrorCodes.cs`, `PricingPermissions.cs` |

## Đối chiếu as-built (graph — bằng chứng lệnh)

- `graph: codegraph_explore "ICostPriceLookup CostPriceLookupDto CostPriceAdminAppService CostEngineAppService ChayAsync CostEngineService AnhXa CostFeedLookupAdapter"` → `ICostPriceLookup` `CostPriceContracts.cs:108` (**2** method `:111`/`:114`, **không** `IApplicationService`/`[Authorize]`); `CostPriceAdminAppService` `[Authorize]` cấp class `:23`, `CreateAsync` `.CostPrice.Manage` `:34`, `ApproveAsync` `.CostPrice.Approve` `:73`; `CostEngineAppService` `[Authorize(CostPriceManage)]` cấp class `:13`; `CostFeedLookupAdapter.AnhXa` `:119-206`.
- `graph: codegraph_explore "CostFeedLookupAdapter.AnhXa …"` → switch ánh xạ `:132-199` khớp **từng loại** với bảng §20.4: `Receipt` có `UnitPrice` ⇒ `NhapCoGia`; 5 loại `NhapKhongGia` + `Receipt` không giá; 8 loại `Xuat`; **bỏ** 5 loại + `Transfer`/`TransferOut`/`TransferIn` (`:190-199`); `TienTeNhap = null` `:148`; loại lạ/lệch dấu ⇒ `0014` `:201-215`.
- `graph: codegraph_explore "DoiSoatTonKho Dau AnhHuongOnHand"` → `Dau()` `DoiSoatTonKho.cs:31-64`: `Transfer=+1` **:36**, `TransferIn=+1` **:38**, `TransferOut=-1` **:42** ⇒ dẫn dòng mới ở §20.4 :1269-1270 **đúng** và ngoại lệ cụm chuyển kho **khớp** `D-321` ②(4)(5).
- `graph: codegraph_explore "PricingErrorCodes … PricingPermissions …"` → `0012` `PricingErrorCodes.cs:65`, `0013` `:71`, `0014` `:79`, `0015` `:86`; `CostPriceManage` `PricingPermissions.cs:45`, `CostPriceApprove` `:52` (khai ở provider `eCommercePermissionDefinitionProvider.cs:69-70` — kiểm bằng `grep -n`, chỉ là tra chuỗi).
- `graph: codegraph_explore "CostEngineService ChayAsync …"` → `AnhXa` của Pricing ném `0014` khi `TienTeNhap` khác `Currency.Vnd` (`CostEngineService.cs:345-353`) ⇒ ca §12 :727 + §20.4 :1275-1276 **khớp as-built**; sổ run 3 pha UoW `requiresNew` `:140/184/202`.
- `psql` (đo as-built, `BEGIN … ROLLBACK`): `__EFMigrationsHistory` có `20261128052137_Added_Pricing_CostEngine`; `pg_indexes` có 2 unique `NULLS NOT DISTINCT`; `PrcCostPrices` có 4 cột nullable `NguonTinh`/`RunId`/`ComputedAtUtc`/`IdempotencyKey` ⇒ mục "Trạng thái triển khai" :1310-1312 vẫn **đo được**.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc lại `Q9` (:906) ↔ bảng §20.4 (:1260-1265) ↔ `D-321` ②(1)-(5); bullet fail-closed (:1267-1274) ↔ chính bảng ↔ `DoiSoatTonKho.Dau()` (graph); `Q4`/`AQ2` (:901, :910) ↔ §20.4 mục 1 (:1236-1240); `Q2` (:1191 vs :1195-1196) | 0 P1/P2; **1 P3** (#1, câu "Hiện … chỉ đọc" còn ở thì hiện tại dù đã có ⚠️ ngay sau) |
| D2 chéo tài liệu / code / `D-xx` | `Q9` ↔ `D-321` **khớp đủ 4 nhóm**; §20.4 ngoại lệ chuyển kho ↔ `D-321` (5) + `DoiSoatTonKho.Dau()` **khớp**; §12 :727 + §20.4 tiền tệ ↔ `D-324` ③ + `CostEngineService.AnhXa` **khớp**; §20/§20.3/§20.4 ⚠️ W17-2 ↔ `CostPriceAdminAppService`/`PricingPermissions` **khớp**; §9 :645 ↔ code (thiếu method `ChayAsync` — P3 #3); §6.1 bảng method (P3 #4) | **1 P2 cũ đã đóng** (#3); 0 P1/P2 mới; 2 P3 |
| D3 cơ chế thực thi | "NÉM `0014`" có thật (adapter :201-215, Domain `TinhBinhQuanGiaQuyenDiDong.cs:198-256`, `CostEngineService.AnhXa` :345-353); "NÉM `0015`" có thật (`:330`, `CostPrice.cs:179`); "KHÔNG INSERT kỳ thiếu giá" = nhánh `AvgCost == null`; `RunKey` unique NND **đo trên DB**; quyền mới có method dùng | 0 |
| D4 SQL chạy được | `grep '^```sql|^```powershell|^```bash'` = **0** ⇒ không có khối lệnh phải chạy; `npm --prefix tools run check:mermaid` → **214 khối · 0 lỗi** | 0 |
| D5 test/probe/tệp có thật | Dẫn dòng mới: `DoiSoatTonKho.cs:36,38,42` **đúng**; `CostFeedLookupAdapter.cs:190-199` **đúng**; `CostPrice.cs:103-105` **đúng** (guard "KHÔNG đòi > 0"); `PricingErrorCodes.cs:65/71` **đúng**; `StockDocument.cs:361-367` **đúng**; `Enums.Inventory.cs:4-7` **đúng**; `PricingErrorCodesTests:47-57` **có thật**. Câu "bỏ trước khi kiểm dấu" chưa đúng thứ tự code (P3 #2) | 1 P3 |
| D6 nhãn tin cậy | ✅ trong "Trạng thái triển khai" đo lại được (psql + graph); `Q9`–`Q11` ✅ `Closed` có `D-321/322/323`; các mục còn kế hoạch không dán ✅; không có số liệu mới không nguồn | 0 |
| D7 14 quy tắc + đối chiếu Nhanh | Tiền `decimal`/`Money` (không `==`), enum mới là dải mới (D-17), khoá phân tán không lặp tiền tố, NFR-006 `[Authorize]` cấp class giữ ở 2 app service mới; Mermaid 214/0; **thiếu** mục "Đối chiếu mô hình Nhanh" + header `v0.1` + §20.5 dừng v0.6 (P3 #5, #7) | 0 trong phạm vi |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §20.4 mục 1 dòng 1237-1238 · §20.3 #3/#4 dòng 1230-1231 | Câu cũ vẫn ở **thì hiện tại** ("Hiện bảng `PrcCostPrices` **chỉ đọc**", "**KHÔNG** có `ApproveCostAsync`", "`.Approve` … **chưa cấp**") dù ngay sau đó đã có ⚠️ W17-2 nói ngược lại (đã có `ICostPriceAdminAppService.CreateAsync`/`ApproveAsync`, quyền `.Manage`/`.Approve`). ⚠️ cùng ô/đoạn nên **không còn gây hiểu nhầm** — chỉ còn tự mâu thuẫn về câu chữ. | Đổi "Hiện" → "Tại `S12`" (và cột "Code làm (S12)") để bảng "ghi để không ai sửa nhầm" không tự phủ định. |
| 2 | P3 | §20.4 dòng 1271 | "adapter **KHÔNG** xét dấu nhóm `Transfer` (**bỏ trước khi kiểm dấu** — `CostFeedLookupAdapter.cs:190-199`)" — as-built kiểm `QuantityDelta <= 0` ⇒ NÉM `0014` **trước** switch (`CostFeedLookupAdapter.cs:125-128`), nên bút toán `Transfer*` có delta ≤ 0 vẫn bị kiểm dấu, không "bỏ trước". Hành vi **chặt hơn** (fail-closed), không phải bỏ qua — nhưng câu chữ chưa đúng thứ tự. | Ghi: "adapter kiểm **độ lớn dương** cho mọi bút toán rồi mới bỏ cụm chuyển kho ở `switch`; **không** đối chiếu chiều của `Transfer*` với `Dau()`". |
| 3 | P3 | §9 dòng 645 | `.CostPrice.Manage` mới gán `ICostPriceAdminAppService.CreateAsync`; as-built **còn** gate `CostEngineAppService.ChayAsync` (`[Authorize(PricingPermissions.CostPriceManage)]`, `CostEngineAppService.cs:13`). Người kiểm quyền không thấy quyền này mở endpoint chạy động cơ. | Thêm `CostEngineAppService.ChayAsync` vào cột "Method được gán". |
| 4 | P3 | §6.1 dòng 423 · bảng method 427-441 | Danh sách interface đã thêm `ICostPriceAdminAppService`/`ICostEngineAppService` nhưng bảng method **chưa** có dòng cho `ChayAsync`/`CreateAsync`/`ApproveAsync`, vẫn liệt kê `GetCostAsync`/`ApproveCostAsync` theo `S12` (§20.3 #3 đã ghi sai lệch; OpenAPI/schema.d.ts đã ghi nợ `W17-3`/`W17-5`). | Lượt chạm toàn tài liệu: thêm 3 dòng method mới + đánh dấu `GetCostAsync`/`ApproveCostAsync` là tên đặc tả. |
| 5 | P3 | §15.4 dòng 939 · §20.5 dòng 1290 · header dòng 11 | `v0.8` chỉ ở §15.4; §20.5 dừng ở **v0.6**; header vẫn `` `v0.1 — 13/10/2026` ``. (Nợ từ lượt N113, chưa sửa.) | Thêm v0.7/v0.8 vào §20.5; đồng bộ header. |
| 6 | P3 | §15.1 `Q8` dòng 905 | `Q8` vẫn không nêu ánh xạ as-built `UnitCost` (tài liệu) ↔ `UnitPrice` (`StockDocument.cs:367`); nay §20.4 :1274 đã nêu nên rủi ro giảm. (Nợ từ lượt N113.) | Thêm một câu vào `Q8` như `rep-003` `Q11`. |
| 7 | P3 | toàn tài liệu (máy) | `check-feature-doc -WP14` **exit 1**, 2 lỗi: `BR-PRC-001-008` chưa có test §14; thiếu mục "Đối chiếu mô hình Nhanh". **Có trước N116**. | Đóng ở lượt WP-14 của `PRC-001` (thêm 1 TC unique `PriceListItems`/`ChannelPrices` + mục đối chiếu). |

## Không kiểm được

- **D4**: tài liệu **0** khối `sql`/`powershell`/`bash` ⇒ không có cam kết lệnh nào phải chạy (psql chỉ dùng **đo** as-built của mục Trạng thái).
- **Test/`dotnet test`**: phần giá vốn GĐ3 **chưa có test** (0 tệp test nhắc `CostEngine`) ⇒ không có gì để chạy; danh sách K06 giao `qa` (`W17-4`).
- **Ngoài phạm vi lượt sửa**: không rà lại D1/D7 toàn văn §0–§19; 2 lỗi máy và nợ N113 giữ nguyên mức P3.

## Vòng trước

### Vòng 1 — N116 F1b (28/11/2026)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (`Q9` "còn lại `NhapKhongGia`") | **đã đóng** — `Q9` nay nêu đủ 4 nhóm đúng `D-321` | P2 |
| 2 (§20.4 fail-closed thiếu ngoại lệ cụm chuyển kho) | **đã đóng** — có ngoại lệ + dẫn `DoiSoatTonKho.cs:36,38,42` (đã xác nhận) | P2 |
| 3 (§20/§20.3/§20.4 còn nói "chỉ đọc"/".Approve chưa cấp") | **đã đóng** — 3 chỗ có ⚠️ W17-2 nêu as-built; còn câu chữ thì hiện tại → P3 #1 | P2 |
| 4 (tiền tệ "cơ sở tenant") | **đã đóng** — §20.4 = "v1 VND literal", §12 :727 thêm ca "≠ VND ⇒ `0014`" | P3 |
| 5 (dẫn `CostPrice.cs:85-87`) | **đã đóng** — nay `CostPrice.cs:103-105` | P3 |
| 6 (lịch sử/phiên bản) | **còn** — chép lên P3 #5 | P3 |
| 7 (`Q8` thiếu ánh xạ `UnitPrice`) | **còn** — chép lên P3 #6 | P3 |
| 8 (§9/§6.1 thiếu `.Manage` + 2 app service) | **đã đóng một phần** — §9 có `.Manage`, §6.1 có 2 interface; phần còn lại → P3 #3, #4 | P3 |
| 9 (2 lỗi `check-feature-doc`) | **còn** — chép lên P3 #7 | P3 |

### Lượt N113/WP-17 (tham chiếu)

| # vòng N113 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (`Q2` chưa chốt cạnh `Q8` đã chốt) | **đã đóng** — nay ghi rõ "`Q2` (nguồn giá vốn) đã chốt `D-186`/`D-321`" | P3 |
| 2 (v0.7 ở §15.4, v0.6 ở §20.5, header `v0.1`) | **còn** — gộp vào P3 #5 | P3 |
| 3 (`Q8` thiếu ánh xạ `UnitCost` ↔ `UnitPrice`) | **còn** — gộp vào P3 #6 | P3 |

```json
{
  "agent": "DOCREV",
  "doc": "docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md",
  "artifact": "docs/reviews/PRC-001-docreview.md",
  "round": 2,
  "verdict": "DOC-OK",
  "p1": 0,
  "p2": 0,
  "p3": 7,
  "sqlRun": "không có khối sql (psql BEGIN…ROLLBACK chỉ dùng để đo as-built: 2 truy vấn)",
  "readRanges": ["1-16", "417-442", "631-660", "701-736", "892-940", "1187-1319"],
  "mustFix": []
}
```

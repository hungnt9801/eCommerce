# Doc review — «PRC-001» phần `arch` (WP-17, hàng W17-1)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/pricing/PRC-001-arch.md` (vòng 3) |
| Ngày · vòng | 28/11/2026 · vòng 3 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N114) |
| Đã đọc | vòng 1: 1–508; vòng 2: 1–552; vòng 3: đọc lại §2.1 (55–84), §4.1 S1 (270–289), §5.2 (325–364), §5.6 (406–420), §7 (475–488), §9 (525–532) |
| Máy | `check-feature-doc.ps1 -WP14` → exit **1** (3 `LOI`) — **đúng chủng loại `-arch`** (tiền lệ `PRC-002-arch.md`; `kiem-tat-ca.ps1` không gọi). `kiem-link-tai-lieu.ps1` → **PASS** (0 chết). |

## Đã kiểm (vòng 3)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | §5.2 bảng `TrangThai` ↔ §5.6 ↔ K06-08/08b; §2.1 dòng 75 ↔ `Pricing.Application.Contracts.csproj:9` | **0 P1/P2**; 1 P3 (#9) |
| D2 chéo tài liệu / code / D-xx | `Reporting.Application.csproj` reference; `PrcCostEngineRuns.Attempt`; `0014`/`0015`; `OtherOut=21`; `CostPrice` ctor | **0 P1/P2**; 3 P3 (#3, #8, #9) |
| D3 cơ chế thực thi | `ThanhCong`/`DangChay`/`Loi`; `Attempt++`; reference Reporting→Pricing; BR-013 storefront | **0 P1/P2**; 2 P3 (#7, #10) |
| D4 SQL / lệnh chạy được | không có khối ```` ```sql ````/`bash`/`pwsh`; lệnh §4.2–§4.3 là kế hoạch W17-2 | 0; **chưa chạy** |
| D5 tên test/probe/tệp có thật | §5.2/§5.6/K06-08b; §9 dòng 531; `PricingErrorCodes.cs:58`; `Enums.cs:118` | **0 P1/P2**; 1 P3 (#8) |
| D6 nhãn tin cậy | "**có thể** có dữ liệu"; không có ✅ chưa đo | 0 |
| D7 14 quy tắc + `-arch` | L5 idempotency (đã đủ 3 trạng thái); L6 khoá; L7 `Money`; L10 `file:dòng` | **0 P1/P2**; 2 P3 (#4, #5) |

## Xác nhận 2 P2 vòng 2 — đã đóng

| # vòng 2 | Trạng thái | Bằng chứng vòng 3 |
|---|---|---|
| 1 — thiếu `ProjectReference` Reporting→Pricing | **đã đóng** | §2.1 dòng 74–76: "`Reporting.Application.csproj` **thêm `ProjectReference` → `eCommerce.Pricing.Application.Contracts`** (khuôn ProjectReference, **KHÔNG** `[DependsOn]`)" + đường lùi "gỡ ProjectReference + bỏ lời gọi ... revert commit, 0 migration"; §9 dòng 531 lặp lại đúng. |
| 2 — "trả run cũ" bỏ qua `TrangThai` | **đã đóng** | §5.2 dòng 342–346: `ThanhCong` ⇒ trả run cũ; `DangChay` ⇒ **NÉM 409/bận**; `Loi` ⇒ **retry attempt MỚI** (UPDATE `Attempt++`, `StartedAtUtc` mới, `TrangThai=DangChay`, xoá `ErrorMessage`); `chưa có` ⇒ INSERT `Attempt=1`. §5.6 dòng 415 khớp; K06-08 (ThanhCong) + **K06-08b** (`DangChay`/`Loi`) khớp. |

- **`Attempt int` (mới):** §4.1 S1 dòng 278 — bảng `PrcCostEngineRuns` là **bảng mới**; `Attempt int` NOT NULL (chưa ghi default) là **chấp nhận được với C4**; luồng INSERT đặt `Attempt=1` (§5.2 dòng 346). **Không phải P2.**
- **C4 vẫn 7/7** (additive; cột `PrcCostPrices` nullable; enum mới; 0 `XC-001`/`RoundingPolicy`; `Money`; 13 ca K-06; bảng schema + revert). `Attempt` nằm trên **bảng mới**, không phải cột mới trên bảng có dữ liệu.

## Phát hiện (vòng 3 — còn mở, đều P3)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §4.1 S1 dòng 278 vs dòng 303 | `TenantId uuid` (không `?`) **mâu thuẫn** với lý do `NULLS NOT DISTINCT` ("`TenantId = NULL` (1 tenant)") và `PrcCostPrices.TenantId` nullable. | Ghi `TenantId uuid?` (hoặc bỏ lý do NULL nếu chọn NOT NULL). |
| 2 | P3 | §4.1 S1 dòng 278; §5.2 dòng 345/354 | `TrangThai int` chưa định nghĩa enum/tên giá trị; luồng dùng `DangChay`/`ThanhCong`/`Loi`. | Thêm `enum CostEngineRunStatus { DangChay=0, ThanhCong=1, Loi=2 }` (append, `D-17`). |
| 3 | P3 | §5.1 dòng ~321, ~327 | Pseudo-code không khớp chữ ký: `CostPrice(Sku, Money.Cua(...), ...)` thiếu `Guid id` (`CostPrice.cs:72-77`); `Duyet(CurrentUser.Id, ...)` truyền `Guid?` vào `Guid`. | `CostPrice(GuidGenerator.Create(), ...)`; `Duyet(CurrentUser.Id ?? Guid.Empty, ...)` để guard `0009` ném. |
| 4 | P3 | §1 dòng 37; §3.1 dòng 131–138 | §1 nói "một method" (đúng `D-186` ②) nhưng port có **2** (single + many). | Ghi method lô là biến thể tối ưu của cùng hợp đồng, hoặc cập nhật `D-186` ②. |
| 5 | P3 | §3.4 dòng ~269 | "`pnpm gen:api` không đổi `schema.d.ts`" chỉ đúng cho DTO báo giá bán; `ICostPriceAdminAppService` mới (2 endpoint + DTO) **sẽ làm đổi** `schema.d.ts`. | Thêm bước `pnpm gen:api` + commit `schema.d.ts` trong PR W17-2. |
| 6 | P3 | §2.1 dòng 64 + 145 | Sơ đồ ghi "Reporting → ... (`ICostPriceLookup`; `ICostPriceAppService` cho HTTP/UI)" dễ đọc thành Reporting dùng app service — tái mở đường P1. | Vẽ rõ Reporting **chỉ** dùng `ICostPriceLookup`; app service là HTTP/UI. |
| 7 | P3 | §3.1 dòng 146 | Port trần không `[Authorize]`; `StorefrontCongKhaiTests` chỉ quét chuỗi `CostPrice` ở 2 tệp. | Thêm scan storefront cho `ICostPriceLookup`/`GetApprovedCost` (BR-013). |
| 8 | P3 | §7 K06-08 dòng 480 | Dẫn "`D-176` ② tái lập"; tính tái lập ở `D-176` ①(2). | Sửa thành `D-176` ①(2). |
| 9 | P3 | §2.1 dòng 75 | Câu "Reporting chỉ thấy port/DTO, **không kéo `Pricing.Domain`**" **mâu thuẫn trực tiếp** với `eCommerce.Pricing.Application.Contracts.csproj:9` (Contracts có `ProjectReference` → `Pricing.Domain`) ⇒ Domain Pricing *có mặt bắc cầu*. | Sửa thành "có mặt bắc cầu; **CẤM** `using eCommerce.Pricing`/`IRepository<CostPrice>` trong `modules/Reporting/**`"; thêm test (khuôn Sales). |
| 10 | P3 | §5.2 dòng 345 | Retry `Loi` UPDATE `Attempt++`, `StartedAtUtc` mới, xoá `ErrorMessage` nhưng **không reset `FinishedAtUtc`/`Rows*`** ⇒ run `DangChay` có thể còn `FinishedAtUtc` của lần lỗi trước. | Thêm `FinishedAtUtc = null` + reset `RowsUpserted/SkippedApproved/FlaggedMissing`. |

## Vòng 2 (P2 đã đóng, P3 còn — đã chép lên bảng trên)

| # vòng 2 | Trạng thái | Mức lúc đó | Ghi chú |
|---|---|---|---|
| 1 | đã đóng | P2 | Reporting ProjectReference + đường lùi (§2.1 74–76; §9 531). |
| 2 | đã đóng | P2 | `ThanhCong`/`DangChay`/`Loi` + `Attempt++` (§5.2 342–346; §5.6; K06-08b). |
| 3 | còn — đã chép lên #1 | P3 | `TenantId uuid` vs NND. |
| 4 | còn — đã chép lên #2 | P3 | `TrangThai` enum. |
| 5 | còn — đã chép lên #3 | P3 | Pseudo-code chữ ký. |
| 6 | còn — đã chép lên #4 | P3 | "một method" vs 2. |
| 7 | còn — đã chép lên #5 | P3 | `schema.d.ts`. |
| 8 | còn — đã chép lên #6 | P3 | Sơ đồ Reporting/app service. |
| 9 | còn — đã chép lên #7 | P3 | BR-013 storefront. |
| 10 | còn — đã chép lên #8 | P3 | `D-176` ②. |
| 11 | còn — đã chép lên #9 (cập nhật) | P3 | "không kéo `Pricing.Domain`" trái `Contracts.csproj:9`; thêm #10 mới. |

## Vòng 1 (20 phát hiện — đã đóng)

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đã sửa |
|---|---|---|---|
| 1 | đã đóng | P1 | Port trần `ICostPriceLookup` + adapter; `ICostPriceAppService` giữ `[Authorize]` cho HTTP. |
| 2 | đã đóng | P2 | `0014`/`0015` append sau `0011`, không dùng `0012`/`0013`. |
| 3 | đã đóng | P2 | `avg=null` ⇒ không INSERT kỳ (`AmountValue NOT NULL`). |
| 4 | đã đóng | P2 | Bỏ revision; `true` ⇒ NÉM `0015`. |
| 5 | đã đóng | P2 | `IdempotencyKey` bắt buộc; thiếu ⇒ `400`. |
| 6 | đã đóng | P2 | Approve lặp ⇒ NÉM `0009`. |
| 7 | đã đóng | P2 | Tìm `RunKey` trước — vòng 2 bổ sung `TrangThai`/`Attempt`. |
| 8 | đã đóng | P2 | Thêm `OtherOut=21`; loại lạ ⇒ `0014`. |
| 9 | đã đóng | P2 | Nhánh "nhập có giá SAU `costMissing`". |
| 10 | đã đóng | P2 | `Range` theo `numeric(18,4)`. |
| 11 | đã đóng | P2 | DTO thay tuple; port trần. |
| 12 | đã đóng | P3 | "sẽ thêm" ProjectReference. |
| 13 | đã đóng | P3 | "có thể có dữ liệu"; migration không seed. |
| 14 | đã đóng | P3 | Nêu nợ bắc cầu Inventory.Domain + test (Pricing.Domain còn ở P3 #9). |
| 15 | đã đóng | P3 | `20261013024226…:66-68`. |
| 16 | đã đóng | P3 | `StockDocument.cs:366`/`:361-367`. |
| 17 | đã đóng | P3 | Bỏ `D-04`, dùng `XC-002`. |
| 18 | đã đóng | P3 | Hàm mới + chuỗi khoá; `§15.2 F5`. |
| 19 | đã đóng | P3 | ACC-001 không tiêu thụ GĐ3. |
| 20 | đã đóng | P3 | 4 `SettingDefinition` + default. |

## Không kiểm được

- **D4:** không có khối ```` ```sql ````/`bash`/`pwsh`; lệnh `pg_dump`/`dotnet ef` trong §4.2–§4.3 là **kế hoạch W17-2**, chưa áp dụng.
- **P2 #2 hành vi runtime:** chưa có code `PrcCostEngineRuns`/job; kết luận từ luồng §5.2 + `TrangThai`.
- **Reference Reporting→Pricing:** tài liệu đã ghi "thêm"; csproj hiện chưa có (đúng là việc W17-2).
- **K-06:** 13 ca là thiết kế, chưa có test.
- **`check:mermaid`:** không có sơ đồ Mermaid.
- **`check-feature-doc` exit 1:** 3 `LOI` — **đúng chủng loại `-arch`** (blockquote 14–16; tiền lệ `PRC-002-arch.md`), không tính phát hiện.

## Ghi chú tích cực (không phải phát hiện)

- **2/2 P2 vòng 2 đã đóng đúng:** Reporting ProjectReference + đường lùi; `TrangThai` 3 nhánh + `Attempt++` + K06-08b.
- **11/11 mustFix vòng 1** và **9 P3 vòng 1** đã đóng; C4 vẫn **7/7**; Q4 vẫn `Open`.
- `Attempt int` trên **bảng mới** là C4-OK; luồng INSERT đặt `Attempt=1`; không có P2.
- Các `file:dòng` mới đã đúng: `StockDocument.cs:366`, `20261013024226…:66-68`, `PricingErrorCodes.cs:58`, `Enums.cs:118`.

# Doc review — «PRC-002» phần `arch` (WP-16, hàng W16-1)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/pricing/PRC-002-arch.md` (466 dòng — vòng 2) |
| Ngày · vòng | 25/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N71) |
| Đã đọc | vòng 1: dòng 1–448 (toàn văn bản cũ). Vòng 2: đọc lại các mục đã sửa — dòng 1–70, 168–260, 355–374, 382–466 — đối chiếu baseline vòng 1 cho phần còn lại |
| Máy | `check-feature-doc.ps1 -Path …-arch.md -WP14` → exit **1** (3 `LOI`) — **đúng chủng loại**: `-arch` là tài liệu bổ trợ, ngoài mẫu 16 mục/BR/"Đối chiếu mô hình Nhanh" (blockquote dòng 14–16, ORCH chốt 25/11/2026) và `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó (đã grep). Không còn là phát hiện. |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | mở lại §2.2 (dòng 64–119) ↔ §3.1 (dòng 194–199) ↔ §6.3 hàng 3d (dòng 394): test đã chuyển sang bề mặt công khai của 4 kiểu hàm thuần, bỏ câu khẳng định cả assembly | 0 phát hiện mở (#1 đã đóng) |
| D2 chéo tài liệu / code / D-xx | mở lại R1 (dòng 429), R2 (430), R11 (439), AQ2 (448); đối chiếu SRS `prc-002` §9 (dòng 716/718), §12 (dòng 787/792); `D-218`/`D-219` | 0 phát hiện mở (#2, #3 đã đóng) |
| D3 cơ chế thực thi | test 3d mới (dòng 394) kiểm được bề mặt 4 kiểu hàm thuần; 4 hạng mục vẫn đủ ca + negative control §6.3 | 0 phát hiện mở |
| D4 SQL / migration chạy được | tài liệu vẫn không có khối ```` ```sql ````/`bash`/`pwsh`; "0 migration" đối chiếu bằng đọc migration `AbpSettings` (`20261009102627`:226, index `:535`) + `eCommerceDbContext.cs:369` | không có khối sql; 0 phát hiện |
| D5 tên test/probe/tệp có thật | mở lại 4 chỗ `file:dòng` đã sửa: `…md:708` (R2/AQ2), `eCommerceHttpApiHostModule.cs:73` (§3.3/§6.1), `IPriceAppServices.cs:81-99` (§3.5), `PriceCalculationDtos.cs:103-122` (§3.2) — tất cả khớp | 0 phát hiện mở (#5, #6 đã đóng) |
| D6 nhãn tin cậy | §9 (dòng 456–462) nay nêu danh sách truy vấn đồ thị + tệp đã đọc thay cho "khối JSON cuối" không tồn tại | 0 phát hiện mở (#7 đã đóng) |
| D7 14 quy tắc + đối chiếu Nhanh | §3.1 dòng 190 đã sửa "NFR-PRC-002-002: ≤ 4 lệnh/giỏ"; blockquote dòng 14–16 nêu việc ngoài mẫu 16 mục là đúng chủng loại; không có Mermaid | 0 phát hiện mở (#4, #8 đã đóng) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| — | — | — | **Không còn phát hiện mở sau vòng 2** (1 P1 + 3 P2 + 4 P3 của vòng 1 đều đã đóng — xem bảng dưới) | — |

## Vòng trước (đã đóng ở vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đã sửa |
|---|---|---|---|
| 1 | đã đóng | P1 | §3.1 dòng 194–199 + §6.3 3d dòng 394: test nay soi **bề mặt công khai của `TinhTienDon`/`ChonBacThang`/`GiaCoSoResolver`/`PricingChinhSach`** (không tham số/field/property kiểu `ISettingProvider`/`ISettingManager`), **bỏ** câu "assembly không tham chiếu `Volo.Abp.Settings`" và giải thích vì sao khẳng định đó sai/rỗng. Không còn mâu thuẫn với việc provider nằm trong `Pricing.Domain`. |
| 2 | đã đóng | P2 | R11 dòng 439: nêu đúng lệch SRS §9 (`…md:716`/`:718`, `eCommerce.Money.RoundingPolicy`, `eCommerce.Pricing.MissingPricePolicy = Throw \| FallbackToBase`) và giao BA sửa cùng PR; nhãn SRS dòng 9 nay có §9, §13. |
| 3 | đã đóng | P2 | R1 dòng 429: "chồng khung" dùng **`0013`** (sau `0011` và sau `0012` đã bị "Σ dòng ≠ tổng" chiếm), cấm cấp `0012`. |
| 4 | đã đóng | P2 | Blockquote dòng 14–16: ghi `-arch` ngoài mẫu 16 mục/BR/"Đối chiếu mô hình Nhanh" của `-WP14`, `kiem-tat-ca.ps1` không gọi công cụ đó, ORCH chốt 25/11/2026. Máy vẫn exit 1 nhưng đúng chủng loại. |
| 5 | đã đóng | P3 | §6.1 dòng 363: thay liệt kê thiếu bằng "`apps/admin-web` **không có route Pricing/tier-price** (grep = 0)" — khớp thực tế. |
| 6 | đã đóng | P3 | Căn lại 4 chỗ: R2/AQ2 `…md:708` (bỏ `:704`); `eCommerceHttpApiHostModule.cs:73`; `IPriceAppServices.cs:81-99`; `PriceCalculationDtos.cs:103-122`. |
| 7 | đã đóng | P3 | §9 dòng 456–462: bỏ "khối JSON cuối", thay bằng danh sách 3 truy vấn đồ thị + các tệp đã đọc. |
| 8 | đã đóng | P3 | §3.1 dòng 190: "NFR-PRC-002-002: **≤ 4 lệnh/giỏ**" (khớp SRS §13). |

## Không kiểm được

- **D4:** tài liệu không có khối `sql`/`bash`/`pwsh` nào ⇒ không có lệnh để chạy. Kết luận "0 migration" được đối chiếu bằng **đọc** migration `20261009102627_Added_Phase0_AuthModules` (`AbpSettings` :226, index :535) + `eCommerceDbContext.cs:369` (`ConfigureSettingManagement()`) — **không** chạy migration trên PostgreSQL.
- **Test thực thi:** chưa có code (đây là thiết kế) ⇒ các ca §6.3 và R8 chỉ kiểm được ở mức thiết kế, chưa chạy.
- **`check:mermaid`:** tài liệu không có sơ đồ Mermaid ⇒ không áp dụng.

## Ghi chú tích cực (không phải phát hiện)

- Vòng 2 sửa đúng và đủ 8 mục; đặc biệt §3.1 dòng 197–199 nay chủ động giải thích **vì sao** không được khẳng định "cả assembly không tham chiếu `Volo.Abp.Settings`" — thể hiện hiểu ràng buộc DI, không chỉ vá câu chữ.
- `D-218` ① (4) ghi 4 "tên index" bằng tên bảng; `-arch` §6.4 vẫn giữ đúng ánh xạ sang tên đầy đủ `IX_…` (khớp 100% migration thật) — tránh test `phaiCo` xanh vô nghĩa.
- Các mốc code đã kiểm trong vòng 1 và vòng 2 đều đúng: `TinhTienDon.cs:73-78,112,114-118,139-146`, `TinhTienDonTests.cs:179-204`, `PricingErrorCodes.cs:46`, `RoundingPolicy.cs:39-42`, `PricingPermissions` (không có `RoundingPolicy.Manage`), `PostgreSqlSchemaTests.cs:208-209,325-328,394-400`.

# Doc review — PRO-001 Voucher / Coupon: phát hành, giới hạn lượt & điều kiện áp dụng

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-001-voucher-coupon.md` (v0.10, 27/11/2026; 1195 dòng) |
| Ngày · vòng | 27/11/2026 · vòng 5 — kiểm nhanh theo `D-169` (b): **chỉ** các mục BA đã đổi ở v0.10 (`D-296`: P1 #26 + P2 #27 + P3 #28 + §8.1 #3) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, không đọc giải thích của BA) |
| Đã đọc | `Read` nguyên văn 137–141, 405, 482–485, 641–662, 884–891, 906–908, 1039–1046, 1166–1195; `git diff` vùng đổi v0.10. Đối chiếu: code `modules/Promotions/eCommerce.Promotions.Domain/Promotion.cs:57–157` (`TaoMoi`), `PromotionErrorCodes.cs` (toàn bộ), `eCommerce.Promotions.Application.Contracts/IPromotionAppService.cs:12–30`; `decisions.md` `D-296` (`:310`, toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → **DAT** (16/16 heading, 36/36 BR có test, mục đối chiếu dòng 1171, 7 câu `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → **0 lỗi** · `kiem-link-tai-lieu.ps1` → **PASS**, 0 tham chiếu chết (1791 đường dẫn) · `check-agent-artifact -Kind docreview` → DAT |

Kết quả: **0 P1 · 1 P2 · 1 P3**. Cả **3 mustFix vòng 4 đã đóng đúng**: #26 (`0109` → `AbpValidationException` 400 không `Code`, giữ số `0109` + "v1 chưa có đường ném" ở E10/BR-19/CR-1/§6.2/§12/`TC-20`/`TC-21`); #27 (`pro-002:511`/`510-517` thay bằng §6.2 `IPromotionAppService` `IPromotionAppService.cs:12-30` đủ 10 method ở BR-18/`TC-40`/Đối chiếu); #28 (heading "cập nhật 27/11/2026" + dòng `D-296`). §8.1 #3 có nhánh `NULL`, §14.3 = 66 TC. Lượt sửa sinh **#29** (BR-19 còn `0 <` mâu thuẫn E10/`TC-21b` về `DiscountValue = 0`) và **#30** (§1 Post-Condition chưa có nhánh `NULL`).

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep toàn tệp `0109`, `pro-002:510-517`, `pro-002:511`, `AbpValidationException`, `DiscountValue = 0`, `[1, MaxUsagePerCustomer]`. `0109` **không còn** chỗ nào nói "`CreateAsync` ném `0109`" (mọi chỗ: `AbpValidationException` + "0109 giữ số/v1 chưa có đường ném"); `pro-002:510-517`/`:511` = **0 lần**; `DiscountValue = 0` mâu thuẫn BR-19 (141) ↔ E10 (140)/`TC-21b` (889) → #29; §1 Post-Condition (137) `[1, MaxUsagePerCustomer]` thiếu nhánh NULL → #30 | **2 phát hiện (#29, #30)** |
| D2 chéo tài liệu / code / `D-xx` | graph: `codegraph_explore "Promotion TaoMoi percentage MaxDiscountAmount AbpValidationException PromotionErrorCodes IPromotionAppService CreateAsync"` → flow `CreateAsync` (`IPromotionAppService.cs:16`) → `PromotionAppService.cs:152` → `Promotion.TaoMoi` (`Promotion.cs:61`); `TaoMoi` `:107` chỉ chặn `discountValue < 0`, `:112-116` chặn `> 100 || maxDiscountAmount == null` (⇒ `0` **lọt**), `:130` `AbpValidationException`; `PromotionErrorCodes` **không có `0109`**; `IPromotionAppService` 10 method. Khớp `D-296` (a) doc-only: đúng 7 chỗ, giữ số `0109`, `TC-21` v1 `{-1, 100.01}`, `TC-21b` = 0 lọt | 0 phát hiện (khẳng định as-built của v0.10 **đúng**; mâu thuẫn BR-19↔E10 ghi ở #29 là nội tại tài liệu, D1) |
| D3 cơ chế thực thi | `TC-20`/`TC-21` đòi `AbpValidationException` = đúng cái `TaoMoi` ném (`Promotion.cs:130`) ⇒ test xanh được. `TC-21b` khoá as-built "0 chấp nhận" (D-296 ⑥(iii): đỏ nếu ai đổi as-built) | 0 phát hiện |
| D4 SQL chạy được | **Không có khối** ```` ```sql ```` (không đổi từ vòng 4). Câu `UPDATE`/`CHECK` inline ở bước 5/7/10 không đổi | 0 phát hiện do chạy |
| D5 test / probe / tệp có thật | `Promotion.cs:112-116`/`:130` **đúng** (graph + đọc); `IPromotionAppService.cs:12-30` đủ 10 method **đúng**; `PromotionErrorCodes` không có `0109` **đúng**. `TC-21b` mới; §14.1 = **66** dòng `| **TC-`; §14.3 dòng 1040 = "66" | 0 phát hiện |
| D6 nhãn tin cậy | As-built ref đều mang `file:dòng` đúng; "v1 chưa có đường ném `0109`" ghi rõ ở §6.2/BR-19/CR-1/§12 — không còn nhãn ✅ sai | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | Mục "Đối chiếu mô hình Nhanh" ở cuối, heading "(cập nhật 27/11/2026)" (1171), có dòng `D-150` (sửa ref) + `D-296` mới; #11–#14 máy PASS; 66 TC phủ 26 BR (máy 36/36 mục BR có test) | 0 phát hiện |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 29 | P2 | BR-19 (dòng 141) ↔ E10 (140) / `TC-21` (888) / `TC-21b` (889) | BR-19 phát biểu luật `DiscountKind = Percentage` ⇒ **`0 < DiscountValue ≤ 100`** và "vi phạm ⇒ `AbpValidationException`" ⇒ coi `DiscountValue = 0` là **vi phạm** (⇒ ném). Nhưng E10 (sửa ở v0.10) ghi tập vi phạm là **"`DiscountValue < 0` hoặc `> 100`"** (⇒ `0` hợp lệ), và `TC-21b` ghi "v1 as-built **chấp nhận** `0`" (đúng `Promotion.cs:107` chỉ chặn `< 0`, `:112-116` chặn `> 100`/thiếu trần). Vậy **BR-19 mâu thuẫn E10 + `TC-21`/`TC-21b`** đúng ở ranh giới `0`: người đọc BR-19 sẽ viết test "`0` ⇒ ném", trái `TC-21b`. `D-296` ①(4) cố ý "BR-19 giữ `0 <`" (luật đích, nợ `PRO-002`) nhưng thân BR-19 **không** ghi nhánh v1 "0 lọt", nên tự nó đọc thành "0 bị ném" | Thêm **một câu ngoại lệ v1** trong BR-19: "*(v1 as-built: chỉ chặn `< 0` và `> 100`; `DiscountValue = 0` **chưa** bị chặn — nợ có chủ `PRO-002`, xem `TC-21b`)*" — **không** nới luật `0 <` (giữ nguyên ý `D-296`), chỉ để BR-19 hết mâu thuẫn với E10/`TC-21b` |
| 30 | P3 | §1 Post-Condition, dòng 137 | Post-Condition viết "`UsageSequence` ∈ `[1, MaxUsagePerCustomer]` do ứng dụng cấp" nhưng **thiếu** nhánh `MaxUsagePerCustomer = NULL` (khi đó cấp số trong `[1, ∞)`, không trần) — trong khi §8.1 #3 (645), BR-04 (141), §5.2 (386) đã có nhánh này. Đây là chỗ **duy nhất** còn sót sau khi v0.10 vá §8.1 #3 | Thêm ngoặc "*(khi `MaxUsagePerCustomer = NULL` ⇒ `[1, ∞)`, không trần)*" vào Post-Condition dòng 137 cho đồng bộ §8.1 #3/BR-04/§5.2 |

## Không kiểm được

- D4: không có `psql` client (`which psql` → no; `docker` có); không phát sinh khối `sql` mới. Câu `UPDATE … WHERE` bước 5/7/10, `CHECK` CR-11/`CHECK >= 1`, index trần-theo-khách — **chưa chạy** trên PostgreSQL (kế thừa vòng 4).
- Vòng kiểm nhanh: không đọc lại §0.0 (16–122), §2 (146–199), §9–§11 (688–773), §13 (822–861) — hunk v0.10 không chạm.
- Ghi chú (không tính mức, kế thừa): §14.5 (dòng 1073) còn ghi "Module `Promotions` — Chưa có" (nay đã có code `modules/Promotions/`, PRO-002) — cần cập nhật ở lượt toàn văn.

## Vòng trước (vòng 4)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 4 | Trạng thái | Mức lúc đó |
|---|---|---|
| 26 | **đã đóng** — E10 (140)/BR-19 (141)/CR-1 (405)/§6.2 hàng `0109` (544)/§12 (796)/`TC-20` (887)/`TC-21` (888) nay ghi `AbpValidationException` (400, không `Code`) trong `CreateAsync`/`Promotion.TaoMoi` (`Promotion.cs:112-116`, ném `:130`); giữ số `0109` + "v1 chưa có đường ném". As-built xác nhận bằng graph | P1 |
| 27 | **đã đóng** — BR-18 (141)/`TC-40` (908)/Đối chiếu `D-150` (1187) thay `pro-002:511`/`510-517` bằng §6.2 `IPromotionAppService` (`IPromotionAppService.cs:12-30`, đủ 10 method); grep `pro-002:510-517`/`:511` = 0 | P2 |
| 28 | **đã đóng** — heading "Đối chiếu mô hình Nhanh (cập nhật 27/11/2026)" (1171) + dòng `D-296` (1191) | P3 |

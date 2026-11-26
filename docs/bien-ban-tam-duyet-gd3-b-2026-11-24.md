# TẠM DUYỆT (DEC, uỷ quyền D-115) — chưa phải biên bản duyệt của người; không merge `main`

> **Đợt B của WP-14 (GĐ3)** · ngày 24/11/2026 · phiên `N55` · hàng `P-13` (`duyet-dec`) · quyết định [D-169](nhanh/decisions.md).
> Căn cứ: [hồ sơ uỷ quyền](agents/uy-quyen-chu-du-an.md) §2 M2 + M4 + §2.1 (danh sách đỏ), `D-115`, `D-71`. Đầu vào: [nháp biên bản đợt B](agents/nhap-bien-ban-gd3-b.md) (N54).
>
> - "Tạm duyệt" chỉ cho phép **code trên nhánh WP**, và chỉ khi các điều kiện "trước WP code" của dòng đó đã đạt.
> - Tạm duyệt áp cho bản đầu tiên **sau khi sửa** mà DOCREV kiểm nhanh ra `DOC-OK` và đóng đúng các P2 đã nêu.
> - DOCREV ra `DOC-CHANGES`, hoặc tài liệu đổi mà không qua DOCREV ⇒ tạm duyệt của dòng đó **tự mất hiệu lực**.
> - Merge `main` vẫn cần biên bản **người ký** `docs/bien-ban-duyet-gd3-b-<ngày>.md` (`implementation-plan.md` §A.5).
> - Cổng máy "merge `main` khi biên bản chưa ký ⇒ đỏ" (`D-115` ⑤) **chưa có** — OPS phải dựng trước khi code dựa trên tạm duyệt chạm `main`.
> - Kế toán (= chủ dự án, `R3-3`) **duyệt lô M4 trước release** cho mọi phần chạm tiền/VAT — yêu cầu này **không** biến tài liệu thành "chưa".
> - Xem lại từng dòng ở [hàng đợi xem lại](agents/hang-doi-xem-lai.md) (D-169).

| # | MÃ | Tài liệu | DOCREV | Kết luận DEC | Lý do | Điều kiện (trước WP code / trước release) | Người review bắt buộc còn thiếu |
|---|---|---|---|---|---|---|---|
| 1 | PRO-001 | [pro-001](features/promotions/pro-001-voucher-coupon.md) | [DOC-OK vòng 3](reviews/PRO-001-docreview.md) · 0 P1 · **1 P2 (#20)** · 6 P3 | **Tạm duyệt có điều kiện** | Ngoài danh sách đỏ (§2.1 "tạm duyệt được"); `DOC-OK` 0 P1 | Trước WP code: (a) BA sửa **P2 #20** đúng hướng DEC chốt — bước 7 / `BR-04` / §5.2 (:385) ghi "`MaxUsagePerCustomer = NULL` ⇒ **không có trần theo khách**; vẫn cấp `UsageSequence` = số nhỏ nhất chưa bị lượt còn hiệu lực giữ trong `[1, ∞)` (lượt `Released` nhả số), giữ index `(TenantId, PromotionId, CustomerId, UsageSequence)` làm lưới, `23505` do đua ⇒ thử lại số kế tiếp có trần; **cấm** gán `1` cho mọi lượt"; thêm TC "cùng khách, `MaxUsagePerCustomer = NULL`, giữ 3 lượt ⇒ cả 3 thành công, 0 `0104`". Hướng khác (bỏ cấp số / cột nullable / đổi `CHECK`) ⇒ phải có `D-xx` thay thế. (b) DOCREV kiểm nhanh bản sau sửa ⇒ `DOC-OK` (0 P1, đóng #20). P3 (#21–#25, #12) sửa **cùng WP code**. M4 **trước release**: `D-143`, `D-148`, `D-149`, `D-152`, `D-158`. P10: **không** `[AllowAnonymous]` (`D-146`) — phát sinh sau ⇒ ngoài tạm duyệt. Q2/Q6/Q7/Q9/Q11 theo mốc "trước WP code / trước release" (nháp §3.1) | **Kiến trúc trưởng** · **kế toán** · BA |
| 2 | PRO-002 | [pro-002](features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md) | [DOC-OK vòng 4](reviews/PRO-002-docreview.md) · 0 P1 · 0 P2 · 2 P3 | **Tạm duyệt có điều kiện** | Ngoài danh sách đỏ; điều kiện §2.1 "(sau P3)" **đã đạt** nhờ `D-152`/`D-155`/`D-158`/`D-160`; **kiến trúc engine được DEC xác nhận ở mức thiết kế** (xem §2) | 2 P3 (#1 DoD 54⇒55; #2 trích số dòng cũ ở delta `pro-001` :467/:469) sửa **cùng WP code**. **Q8** chỉ chặn nhánh code điều kiện địa chỉ — GHTK chưa xác minh 2 cấp (R3-1) ⇒ **không** code bằng suy đoán. Q1/Q2/Q5/Q9/Q26/Q28/Q29 theo mốc "trước WP code / `migrations add` / release" (nháp §3.1). M4 **trước release**: `D-158`, `D-160`, `D-161`, `D-162`, `D-163` + phần flash `D-156` | **Kiến trúc trưởng** · BA · **kế toán** |
| 3 | LOY-001 | [loy-001](features/loyalty/loy-001-diem-thuong.md) | [DOC-OK vòng 4](reviews/LOY-001-docreview.md) · 0 P1 · **1 P2 (§11)** · 2 P3 | **Chưa** | **Đỏ có điều kiện** (§2.1): "tới khi chốt P13 được người duyệt" — điều kiện **còn**. Chọn P13 phương án (a) ở `U-02` (`D-114` ①) là chốt **chính sách**, nhưng hàng P13 ghi "**chủ dự án duyệt một lần**" và `D-114` ⑤ còn liệt "duyệt P13" là **cổng release**; `D-167` ⑤ / `D-168` ⑤ cũng còn ghi điều kiện mở ⇒ DEC **không** tự rút gọn danh sách đỏ | **Đường mở khoá:** người ghi một hành vi "P13 đã duyệt" (biên bản đợt B hoặc một dòng `D-xx` của người) ⇒ BA sửa **P2 §11** (thêm `LoyRedemptions(TenantId, RetailBillId)` + `(TenantId, OrderId)`, `NULLS NOT DISTINCT`, `WHERE … IS NOT NULL`) ⇒ DOCREV kiểm nhanh `DOC-OK` ⇒ **dòng DEC sau** tạm duyệt được. **Mốc cứng bất kể tạm duyệt:** P2 §11 sửa **trước `migrations add` bảng `Loy*`** (thiếu unique ⇒ trùng lô tiêu POS im lặng). `D-164…D-168` giữ nguyên hiệu lực (chốt **thiết kế**, không phải duyệt tài liệu). M4 **trước release**: `D-165`, `D-166` | **chủ dự án (P13)** · Kiến trúc trưởng · **kế toán** |

## 1. Tóm tắt

- Tạm duyệt có điều kiện: **2/3** (`PRO-001`, `PRO-002`). Chưa: **1/3** — `LOY-001` (đỏ có điều kiện, chờ người duyệt P13).
- **0/3** có chữ ký người. Lùi: xoá tệp này + một dòng `D-xx (thay thế D-169)`.
- Không dòng nào của 3 tài liệu thuộc **vùng cấm §6** (không `[AllowAnonymous]`, không secret, không NCC/API production, không dữ liệu thật).

## 2. Xác nhận kiến trúc engine điều kiện (`PRO-002`) — DEC, phương án (a)

WP-14 bước ④ đòi "review kiến trúc cho engine điều kiện". DEC **không** dựng bước review riêng chặn tạm duyệt; xác nhận kiến trúc **ở mức thiết kế** vì engine **chưa có trong code** (`codegraph_explore` không thấy `PromotionEvaluationService`/`EvaluateAsync`/`PromotionRuleContext`), việc DOCREV D1–D7 đã soát thiết kế (0 P1 / 0 P2) và `D-151…D-163` đã chốt từng cơ chế. Các vế đã đối chiếu:

| # | Vế kiến trúc | Căn cứ |
|---|---|---|
| i | Cây điều kiện bất biến từ `Approved`; `SaveTreeAsync` chỉ `Draft`; `TreeVersion` đóng băng; unique một gốc/promotion; không trigger DB v1 | `D-159`, Q2 |
| ii | `evaluate` **chỉ đọc** — không `IIdempotencyGuard`, không khoá; mọi **ghi** qua `ReserveAsync`/`CommitAsync` | `D-159` (đính chính CR-7 của `D-151`) |
| iii | Ghép tham lam: mã trước rồi `Priority DESC, Id`; nhận khi cả cái đã nhận `IsCumulative = true`; lý do loại `SupersededByVoucher = 8` / `ExcludedByNonCumulative = 10` (cuối enum); tiền → trần | `D-160`, `D-158` |
| iv | Ngân sách `MaxNodes = 64`, `MaxDepth = 8`, `LeafVisitBudget = 512`; vi phạm ⇒ `0001`; giá trị giữ đề xuất, đo lại khi có dữ liệu | Q1 (người kiến trúc) |
| v | Unique một lượt voucher/đơn + một lượt tự động/đơn, `NULLS NOT DISTINCT` | `D-157` |
| vi | Mọi phép tiền qua `Money`, phân bổ qua `MoneyAllocator.PhanBo` (9 caller ở `Pricing/TinhTienDon.cs`) | `D-155` |
| vii | Fail-closed: `CustomerId` NULL ⇒ lá `CustomerGroup`/`FirstOrderOnly` = `false`; tự động có trần/khách + vãng lai ⇒ bỏ qua `CustomerRequired = 11`; "đơn đầu" đếm cả đơn huỷ/trả | `D-162`, `D-161`, `D-163` |

Bản review kiến trúc do **người** viết (nếu chủ dự án muốn) là phần của **biên bản người ký** trước merge `main` — **không** phải điều kiện của tạm duyệt.

## 3. Tạm duyệt tài liệu ARCH của `PRO-002` — hàng `W24-1` (phiên `N93`, 27/11/2026) · [D-277](nhanh/decisions.md)

| # | MÃ | Tài liệu | DOCREV | Kết luận DEC | Lý do | Điều kiện (trước release) | Người review bắt buộc còn thiếu |
|---|---|---|---|---|---|---|---|
| 1 | PRO-002 (ARCH) | [PRO-002-arch](features/promotions/PRO-002-arch.md) v0.3 | [DOC-OK vòng 3](reviews/PRO-002-arch-docreview.md) · 0 P1 · 0 P2 · 1 P3 | **Tạm duyệt (M2)** — chỉ để code **F3 trên nhánh WP** | Ngoài danh sách đỏ §2.1; điều kiện "(sau P3)" của dòng `PRO-002` **đã đạt** ở `D-169` (3) | P3 còn lại (thu hẹp allowlist test `eCommerce.Messaging.EventCoverage` + nêu tên test) sửa **cùng `W24-2`**; **5 `docDeviation` D1–D5** giữ theo SRS/ARCH, BA ghi rõ ở `W24-2`; **D6** guard `CheckoutInput.OrderDiscount ≠ 0 ⇒ NÉM` (`D-143` (2)) **bắt buộc** `W24-2`; **Q8/AQ10** (`D-282`) vẫn **chuyển người** (R3-1, GHTK) — **không** chặn WP. M4 **trước release**: `D-158`, `D-160`, `D-161`, `D-162`, `D-163` + phần flash `D-156` | **Kiến trúc trưởng** · BA · **kế toán** (trước release) |

> Đây **không** phải biên bản duyệt của người: `main` vẫn cần biên bản **người ký**. Tạm duyệt mất hiệu lực nếu DOCREV ra `DOC-CHANGES` hoặc tài liệu đổi mà không qua DOCREV (`D-169` (1)).

# Nháp biên bản duyệt GĐ3 — đợt B (WP-14)

> ⚠️ **NHÁP — chưa có hiệu lực duyệt.** Tệp này do orchestrator ghi trong `/doc-gate` để chủ dự án tham khảo.
> Biên bản thật (`docs/bien-ban-duyet-gd3-b-<ngày>.md`) do **chủ dự án** lập khi duyệt (`07` §WP-14 ④).
>
> Cập nhật **N54 (24/11/2026)** — hàng `P-12` (`tong-hop`): đủ **3/3** dòng tài liệu của đợt B, cột người review bắt buộc
> theo `design-doc-guidelines.md` §6, thêm **bảng câu còn chờ người** (§3). Không lập biên bản thật.
>
> Cập nhật **N99 (27/11/2026)** — hàng `W25-0` (chuỗi `gd3-1`): `PRO-001` lên **v0.10**, `DOC-OK` **vòng 5** (0 P1),
> **đóng P2 #20** theo `D-169`; áp `D-295` (trần thử `UsageSequence` = 5) và `D-296` (`0109` theo as-built). Điều kiện tạm duyệt của `D-169` **đã đạt**.

## 1. Bảng tài liệu đợt B

| MÃ | Tệp | DOCREV | §15 còn Open | Chờ người | Người review bắt buộc (`design-doc-guidelines.md` §6) | Đề xuất |
|---|---|---|---|---|---|---|
| PRO-001 | [pro-001-voucher-coupon.md](../features/promotions/pro-001-voucher-coupon.md) | `DOC-OK` **vòng 5** kiểm nhanh v0.10 (N99/W25-0) · 0 P1 · 1 P2 (#29: BR-19 còn câu `0 < DiscountValue` trong khi as-built `Promotion.TaoMoi` chấp nhận `0` — xem `TC-21b`, `D-296`) · 1 P3 (#30: §1 Post-Condition thiếu nhánh `MaxUsagePerCustomer = NULL`) ([docreview](../reviews/PRO-001-docreview.md)). **P2 #20 đã đóng** (bước 7 / BR-04 / §5.2 / skeleton `TC08` + `TC-68`); P3 #12/#21–#25 đã đóng | 7 dòng `Open` (script) — **Q2** TTL giữ lượt (BA), **Q6** `MaxBatchSize`+rate limit (Kiến trúc+Vận hành), **Q7** API liệt kê lượt giữ (Vận hành), **Q9** dry-run (BA), **Q11** kiểm chứng tiền tố khoá bằng `MONITOR` (hạ mức); **Q3** `Deferred` (`D-149`, gắn `CR-P1`); **Q4** `Deferred` (`D-158`, `CR-S2`) | 0 — mọi câu thiết kế đã vào `D-146…D-150` (TẠM CHỐT, uỷ quyền `D-114`); CR chờ chủ: CR-11 → `PRO-002` (đã nhận, `D-151`), CR-12 → `SAL-004`, CR-P1 → `PRC-002`, CR-S1 → `SAL-002`, CR-S2 → `Sales`+kế toán (chưa gửi). `D-295`/`D-296` chờ duyệt lô | **Kiến trúc trưởng** (máy trạng thái lượt voucher `Reserved → Applied → Released/Reversed`, bộ đếm, khoá phân tán) + **BA** (state machine + luật ghép) + **kế toán** (chạm tiền/VAT: `D-143`, `D-149`, Q4 `FreeShipping` hạch toán) | **Tạm duyệt** — điều kiện (a)(b) của `D-169` **đã đạt** 27/11/2026 (P2 #20 đóng, `DOC-OK` vòng 5 0 P1); `D-295` trần thử 5, `D-296` `0109` as-built; trình người sau khi xem `D-142…D-150`/`D-295`/`D-296`. SQL (`UPDATE` bước 5, `CHECK` CR-11, index) chưa đo — không có `psql`/docker; `TC-59`/`TC-61` sẽ đo |
| PRO-002 | [pro-002-cay-dieu-kien-engine-khuyen-mai.md](../features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md) | `DOC-OK` vòng 4 kiểm nhanh v0.9 (N51) · 0 P1 · 0 P2 · 2 P3 (#1 §15.4 :1213 ghi "DoD 54 test case" trong khi §14.1/§14.3 = 55; #2 delta `pro-001` §6.1 :467/:469 còn trích số dòng cũ của `pro-002` — P3 vòng 3 #9 chưa sửa, ngoài vế đã soát) ([docreview](../reviews/PRO-002-docreview.md)) | 9 dòng `Open` (script) — **Q1** trần `MaxNodes`/`MaxDepth`… (Kiến trúc+BA), **Q2** trigger DB chặn trần (Kiến trúc+DBA), **Q5** tách nhiệm vụ người dựng ≠ người duyệt (BA+Kiểm soát nội bộ), **Q8 🔴 CHẶN** điều kiện theo địa chỉ/vùng giao (GHTK chưa xác minh 2 cấp), **Q9** phân biệt KM của ta vs của sàn (BA+Kế toán), **Q10** dry-run (BA), **Q26** lỗi nạp context khách (BA+Kiến trúc `Sales`), **Q28** lỗi cấu hình + candidate tự động (BA+Kiến trúc), **Q29** gộp hai tên context (Kiến trúc) | 0 — câu thiết kế đã vào `D-151…D-163` (TẠM CHỐT, uỷ quyền `D-114`); `Q26`/`Q28`/`Q29` còn `Open` nhưng **không chặn DoD** (v1 giữ nguyên trạng, đổi là câu `DEC` mới) | **Kiến trúc trưởng** (engine cây điều kiện, ghép tham lam, idempotency, ngân sách) + **BA** (state machine `Draft → Approved`, luật ghép) + **kế toán** (tiền giảm cấp đơn/VAT, `D-158`/`D-160`/`D-163` trước release — M4) | **Tạm duyệt được** theo hồ sơ uỷ quyền (M2) — `DOC-OK` 0 P1/0 P2; kế toán duyệt "đơn đầu"/tiền giảm trước release (M4, `D-163`). 2 P3 sửa cùng WP code. SQL §5.3 chưa đo (không `psql`/docker). **Q8** chỉ chặn nhánh code điều kiện địa chỉ, không chặn duyệt |
| LOY-001 | [loy-001-diem-thuong.md](../features/loyalty/loy-001-diem-thuong.md) | `DOC-OK` **vòng 4 kiểm nhanh** v0.8 (N53) · 0 P1 · **1 P2** (§11 danh sách unique index thiếu `LoyRedemptions(TenantId, RetailBillId)` và `(TenantId, OrderId)` — nên sửa trước WP code) · 2 P3 (#1 §7.1 "6 biên vào" ⇒ thật 7 hàng; #2 trích `PosDtos.cs:33,35,119,122`) ([docreview](../reviews/LOY-001-docreview.md)) | 0 dòng `Open` (Q1–Q12 `Closed`; Q3/Q4/Q5/Q6/Q9 là `TIEN_LE`) | 0 câu chờ người; kế toán duyệt lô M4 (`D-165`/`D-166`) trước release; **CR POS** (`D-168`) do chủ `arch Pos` — chặn bật Loyalty ở môi trường có POS bán | **Kiến trúc trưởng** (engine luật + sổ lô FIFO, cổng `ILoyaltyEarnGateway`, append-only `LoyLedgerEntries`, cổng POS) + **BA** (state machine lô tiêu/điều chỉnh) + **kế toán** (hạch toán điểm `D-166`, ngưỡng duyệt `D-114` P1) | **Tạm duyệt được** theo hồ sơ uỷ quyền (M2) — `DOC-OK` 0 P1; P2/P3 để người duyệt sửa cùng WP code. CR chéo bắt buộc: **POS** (`D-168`), **`SAL-006`** BR-023 (`D-167`), **`Customers`** `KetQuaDaoKhach` (G8), **`ACC-001`** `LoyaltyDiscount` (`D-166`). SQL/lô chưa chạy (module chưa có trong code) |

## 2. Tóm tắt đợt B (N54, 24/11/2026 — chốt hồ sơ)

- **3/3 tài liệu** có `DOC-OK` và `check-feature-doc.ps1 -WP14` exit 0: `PRO-001` (v0.6) + `PRO-002` (v0.9, vòng 4 kiểm nhanh) + `LOY-001` (v0.8, vòng 4 kiểm nhanh — N53 đóng P1 chéo POS bằng `D-168`, P2 chữ ký bằng cập nhật `D-167`). **0 P1** trên cả 3; P2 mở: `PRO-001` 1 · `PRO-002` 0 · `LOY-001` 1 (2 P2 — BA sửa trước hoặc cùng WP code, không có P1 chặn).
- **Cột "Người review bắt buộc"** theo `design-doc-guidelines.md` §6: cả 3 tài liệu có **state machine** ⇒ Kiến trúc trưởng + BA; cả 3 **chạm tiền/thuế** ⇒ thêm **kế toán** (M4). Không tài liệu nào có tích hợp NCC ngoài trực tiếp ⇒ không cần "người đã liên hệ NCC" trong cột này (`PRO-002` Q8 chỉ là điều kiện *dùng* dữ liệu địa chỉ của GHTK, đã ghi ở §3).
- `PRO-001` v0.6: 64 test case · 36 BR có test · `check-feature-doc.ps1 -WP14` exit 0.
- `PRO-002` v0.9: 55 test case · 20 BR có test · `check-feature-doc.ps1 -WP14` exit 0; vòng 3 → vòng 4 đóng 1 P1 + 3 P2; `D-163` (định nghĩa "đơn đầu", khác khuyến nghị BA vì vòng lách huỷ-đơn-hoàn-lượt `D-148`).
- `LOY-001` v0.8: 53 test case (thêm `TC-52`/`TC-53`, mở rộng `TC-49`) · 24 BR có test · 0 câu `Open`; 5 câu theo **tiền lệ** (`Q3/Q4/Q5` = `D-114` P13; `Q6` = `D-114` P1; `Q9` = `D-45` ①/`D-133`), 5 quyết định **tạm chốt** `D-164…D-168`; `DOC-OK` 0 P1 · 1 P2 · 2 P3.
- **Phân loại theo hồ sơ uỷ quyền (M2/M4):** cả **3** tài liệu **tạm duyệt được** theo M2 sau khi đóng P2/`TC-59` ghi ở §3.3; **kế toán duyệt lô M4** (R3-3) trước release cho phần chạm tiền/VAT (`PRO-001` `D-143`/`D-149`; `PRO-002` `D-158`/`D-160`/`D-163`; `LOY-001` `D-165`/`D-166`).
- Nợ chéo cần BA/arch làm cùng PR WP code: CR-11 → `PRO-002` (đã nhận, `D-151`); CR-12 → `SAL-004`; CR-P1 → `PRC-002`; CR-S1 → `SAL-002`; CR-S2 → `Sales`+kế toán; **`SAL-006`** BR-023 (gọi cổng `D-167`); **`Customers`** `ICustomerLedger.GhiNhanTraHangAsync` trả `Task<KetQuaDaoKhach>` (G8); **`ACC-001`** `LoyaltyDiscount` (`D-166`); **POS** 3 đường gọi (`D-168`, phải merge trước/cùng release với migration Loyalty đầu tiên).

## 3. Bảng câu còn chờ người của đợt B

Nguồn: §15.1 của 3 tài liệu · `docs/agents/hang-doi-xem-lai.md` (dòng `TẠM CHỐT` của đợt B) · `docs/agents/uy-quyen-chu-du-an.md` §5 (R3).
**Không dòng nào chặn DoD `P-12`** — đây là việc của người duyệt, gom để trình cùng biên bản.

### 3.1 Câu §15 còn mở / chưa chốt (thuộc đợt B)

| # | Câu hỏi | Tài liệu | Loại / cần ai | Khuyến nghị (đứng đầu) | Khi nào |
|---|---|---|---|---|---|
| 1 | `PRO-002` **Q8**: có điều kiện khuyến mãi theo **địa chỉ/vùng giao** không | PRO-002 | 🔴 **CHẶN nhánh code** — BA + người đã liên hệ GHTK (R3-1 chưa xác minh địa chỉ 2 cấp của GHTK) | **Không** làm ở v1; chỉ mở khi GHTK xác nhận 2 cấp | Trước khi có rule địa chỉ; không chặn duyệt tài liệu |
| 2 | `PRO-001` **Q2**: TTL giữ lượt mặc định (đề xuất 900 s) + có gia hạn | PRO-001 | BA (không chặn DoD) | 900 s, không gia hạn ở v1; so `INV-002`/`SAL-002` Q4 để nhất quán | Trước WP code `PRO-001` |
| 3 | `PRO-001` **Q6**: trần `MaxBatchSize` (đề xuất 10.000) + rate limit endpoint sinh lô | PRO-001 | Kiến trúc trưởng + Vận hành | 10.000/lô + rate limit theo tenant | Trước WP code `PRO-001` |
| 4 | `PRO-001` **Q7**: API/CLI liệt kê **lượt đang giữ** để CSKH gỡ tay | PRO-001 | Vận hành | Có, kèm permission riêng + audit (đường thoát khi job chết) | Trước WP code `PRO-001` |
| 5 | `PRO-001` **Q9** / `PRO-002` **Q10**: dry-run mã/cây trên giỏ mẫu trước phát hành | PRO-001, PRO-002 | BA | Nên có, nhưng là endpoint + màn hình mới ⇒ hoãn sau v1 | Trước release nếu làm |
| 6 | `PRO-001` **Q11**: kiểm chứng tiền tố khoá phân tán thật bằng `redis-cli MONITOR` | PRO-001 | Kiến trúc trưởng (hạ mức) | Giữ quy ước không tiền tố (`D-82`); đóng khi có Redis | Khi có Redis |
| 7 | `PRO-001` **Q3** `Deferred`: `ConfigJson` vs `ProPromotionMappings` | PRO-001 | Kiến trúc trưởng — gắn `CR-P1` | Mở lại cùng `CR-P1`; ưu tiên `ProPromotionMappings` (một nguồn điều kiện) | Cùng `CR-P1` |
| 8 | `PRO-001` **Q4** `Deferred`: hạch toán `FreeShipping` | PRO-001 | Kế toán + Kiến trúc + `SAL-*` — `CR-S2` | v1 **chặn** `FreeShipping` (`D-158`); thêm cột `ShippingDiscountTotal` ở `Sales` khi mở | Trước release phần free-ship |
| 9 | `PRO-002` **Q1**: giá trị trần cụ thể (`MaxNodes=64`, `MaxDepth=8`, `LeafVisitBudget=512`…) | PRO-002 | Kiến trúc trưởng + BA | Giữ giá trị đề xuất, đo lại khi có dữ liệu thật | Trước WP code `PRO-002` |
| 10 | `PRO-002` **Q2**: trigger DB chặn trần node/lá | PRO-002 | Kiến trúc trưởng + DBA | Không trigger ở v1 (validator + guard đủ; engine vẫn ném `0001`) | Trước WP code `PRO-002` |
| 11 | `PRO-002` **Q5**: tách nhiệm vụ người dựng ≠ người duyệt (4 mắt) | PRO-002 | BA + Kiểm soát nội bộ | Chưa bắt buộc v1; thêm permission `Approve` riêng khi cần | Trước khi bật duyệt 4 mắt |
| 12 | `PRO-002` **Q9**: phân biệt KM **của ta** vs **của sàn** trong cùng `Breakdown` | PRO-002 | BA + Kế toán | Tách trường nguồn trước khi bật báo cáo lãi gộp | Trước release báo cáo (chạm `REP-002`/`REP-003`) |
| 13 | `PRO-002` **Q26/Q28/Q29**: lỗi nạp context khách · lỗi cấu hình + candidate tự động · gộp hai tên context | PRO-002 | BA + Kiến trúc | Q26 = (b) fail-closed, không ném; Q28 giữ (a) v1; Q29 gộp là refactor sau — **đều câu `DEC` mới nếu đổi** | Sau v1 (không chặn) |

### 3.2 Quyết định `TẠM CHỐT` của đợt B đang chờ duyệt lô

26 dòng `D-xx` (uỷ quyền `D-114`; **không** gồm `D-145` — quyết định hệ agent, ngoài đợt). Duyệt lô theo `docs/agents/hang-doi-xem-lai.md`.

| D-xx | Nội dung ngắn | Xem lại trước |
|---|---|---|
| `D-142` | Voucher **không** áp cho bán quầy `RetailBill`; chỉ kênh `web` (BR-17) | WP code đầu tiên `PRO-001`; xác nhận phạm vi "coupon" GĐ3 trước khi đóng GĐ3 |
| `D-143` | `CheckoutInput.OrderDiscount ≠ 0` ⇒ **NÉM** (giữ trường DTO, không bỏ qua im lặng) | WP code `PRO-001`; **release** (chạm tiền) |
| `D-144` | Dải mã `PRO-001` = `01xx` (`0101–0112`), `FreeShipping` = `0113`; `PRO-002` giữ `00xx` | WP code `PRO-001` |
| `D-146` | Khách vãng lai **không** áp voucher ⇒ `0118`; trần theo `CustomerId` | WP code `PRO-001`; trước khi mở checkout vãng lai (`SAL-002` Q2) |
| `D-147` | `MaxUsageTotal` cấp promotion: `UsedCount`/`ReservedCount` + `CHECK` + `UPDATE` điều kiện thứ hai | WP code `PRO-001` (trước `Add_Promotion_Module`); **release** nếu `TC-59` chưa xanh |
| `D-148` | Huỷ đơn: cạnh `Applied → Released`, hoàn `UsedCount` (chỉ `OrderCancelledEto`) | WP code `PRO-001`; **chặn release** tới khi chủ dự án duyệt lô |
| `D-149` | Voucher chỉ giảm **cấp đơn**; 3 rule theo SKU/danh mục ra ngoài v1 (`CR-P1`) | WP code `PRO-001`; **release** (thuế GTGT sau giảm — M4) |
| `D-150` | `0004` = rule chưa từng đăng ký, `0010` = đã gỡ; `TC-59` còn lại `0007`/`0119`; `0106` không có đường sửa | WP code `PRO-001`; `TC-59` vẫn chặn release |
| `D-151` | Nhận `CR-1…CR-10` của `PRO-001`; `PRO-002` là nguồn schema `Pro*` duy nhất; `EvaluateAsync` chỉ đọc | WP code `PRO-001`/`PRO-002`; **chặn release** tới khi duyệt lô |
| `D-152` | Giảm theo SKU/danh mục ngoài v1 (`PerLine` hoãn theo `CR-P1`) | WP code; **release** (thuế sau giảm — M4) |
| `D-153` | Khuyến mãi tự động tại quầy `RetailBill` ngoài v1 | WP code (cùng lô `D-142`/`D-151`/`D-152`) |
| `D-154` | Phân biệt `0004`/`0010` bằng cột `RetiredAtUtc`, seeder không xoá dòng | WP code (cùng lô) |
| `D-155` | `Q3` đóng: `Money`/`MoneyAllocator` đã có; mọi phép tiền qua `Money` | WP code `PRO-002` (cùng lô) |
| `D-156` | Giá flash sale đi **đơn giá** pipeline `PRC-002`, engine không trả giá; `CHECK ("Kind" <> 'FlashSale')` | WP code `PRO-002`; bật `CR-P2` ⇒ câu `DEC` mới, **chặn release** phần flash |
| `D-157` | Unique một lượt **voucher**/đơn + một lượt **KM tự động**/đơn | WP code `PRO-002` (cùng lô) |
| `D-158` | Mã khách nhập thắng KM tự động không ghép được; trần `DiscountTotal ≤ Subtotal`; `FreeShipping` chặn | **Release** (tiền/VAT, kế toán duyệt lô M4) |
| `D-159` | Cây bất biến từ `Approved`; `evaluate` không bọc guard; unique một gốc | WP code `PRO-002` (cùng lô) |
| `D-160` | `Percentage` trên `Subtotal` gốc; một luật ghép tham lam + `ExcludedByNonCumulative = 10` | **Release** (M4, tiền) |
| `D-161` | KM tự động có trần/khách + vãng lai ⇒ bỏ qua candidate, lý do `CustomerRequired = 11` | **Release** (M4, tiền) |
| `D-162` | Lá `CustomerGroup`/`FirstOrderOnly` + `CustomerId` NULL ⇒ `false` (fail-closed) | **Release** (M4, tiền) |
| `D-163` | "Đơn đầu" đếm **cả** đơn đã huỷ/đã trả (chống vòng lách huỷ-đơn-hoàn-lượt) | **Release** (M4, tiền) |
| `D-164` | Lô tiêu mồ côi: `void` trong `catch` + job chỉ void khi `IOrderExistenceProbe` chứng minh; auto-void **TẮT** | **Release** (điểm khách = nợ phải trả; bật auto-void cần duyệt lô) |
| `D-165` | Thu hồi điểm khi trả một phần = luỹ kế `Floor2(E×V/T) − Floor2(E×V_trước/T)` | **Release** (kế toán duyệt lô M4) |
| `D-166` | Điểm đã tiêu = **giảm trừ doanh thu** (không phải chi phí), kỳ `Delivered`, cấm cộng vào `DiscountTotal` | **Release** (kế toán duyệt lô M4/R3-3) |
| `D-167` | Trả hàng sau giao đi **cổng** `GhiNhanDaoDiemTraHangAsync`, giảm lô đúng `DiemDaDao` (8 tham số có `noGiam`) | **Release** (điểm khách = nợ phải trả) |
| `D-168` | Cả 3 đường POS (bán/huỷ/trả) đi **cổng** `ILoyaltyEarnGateway` cùng UoW; `modules/Pos/**` 0 gọi thẳng; 1 cột nullable `RetailBillId` | **Release**; mốc sớm: WP code đầu tiên `LOY-001` (trước `migrations add` `Loy*` và trước merge CR POS) |

### 3.3 Việc người duyệt sửa **cùng WP code** (không chặn tạm duyệt)

| # | Việc | Tài liệu | Ghi chú |
|---|---|---|---|
| 1 | ~~**P2 #20**~~ ✅ **đã đóng ở N99/W25-0** (`MaxUsagePerCustomer = NULL` ⇒ không trần, cấp `UsageSequence` trong `[1, ∞)`, **CẤM** gán `1`; `TC-68`; `D-295` trần thử 5) | PRO-001 | Xong — không còn việc người duyệt |
| 2 | **P2 §11**: danh sách unique index thiếu `LoyRedemptions(TenantId, RetailBillId)` và `(TenantId, OrderId)` | LOY-001 | Sửa **trước** khi viết migration Loyalty (kẻo bỏ sót unique chống trùng lô tiêu POS) |
| 3 | **P3**: `PRO-002` §15.4 "DoD 54 test case" ⇒ 55; trích dòng `pro-001` §6.1 :467/:469 | PRO-002 | Sửa cùng WP code |
| 4 | **P3**: `LOY-001` §7.1 "6 biên vào" ⇒ 7 hàng; trích `PosDtos.cs:33,35,119,122` | LOY-001 | Sửa cùng WP code |
| 5 | **CR chéo** (`D-168` POS · `D-167` `SAL-006` · G8 `Customers` · `D-166` `ACC-001` · `CR-11` `PRO-002` · `CR-12` `SAL-004` · `CR-P1` `PRC-002` · `CR-S1` `SAL-002` · `CR-S2` `Sales`+kế toán) | 3 tài liệu | Việc **chủ tài liệu/module khác**, không sửa trong WP-14 |
| 6 | **CR POS** bắt buộc merge trước/cùng release với migration Loyalty đầu tiên | LOY-001 (`D-168`) | Chủ: arch `Pos` + BA `POS`; chưa có ⇒ **không** bật Loyalty ở môi trường có POS bán |

> Dòng 1–4 **không cần người trả lời** — ghi ở đây để chủ dự án biết "tạm duyệt" của 3 tài liệu còn điều kiện sửa tài liệu; dòng 5–6 là nợ chéo ngoài phạm vi WP-14.

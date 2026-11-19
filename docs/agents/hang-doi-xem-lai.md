# Hàng đợi xem lại — quyết định do agent `decider` chốt tạm

> Agent `decider` (`.claude/agents/decider.md`) ghi **một dòng** ở đây cho mỗi quyết định `TẠM CHỐT (DEC)` hoặc
> `ĐỀ XUẤT (DEC)` mà nó thêm vào `docs/nhanh/decisions.md`. Chủ dự án duyệt **theo lô**, không cần dừng phiên.
>
> **Cách duyệt một dòng:**
> - Đồng ý ⇒ sửa trạng thái của dòng `D-xx` trong `docs/nhanh/decisions.md` thành `ĐÃ CHỐT`, chuyển dòng ở đây sang "Đã xử lý".
> - Không đồng ý ⇒ thêm dòng `D-yy (thay thế D-xx)` với giá trị đúng; agent phiên sau làm theo `revert` đã ghi.
>
> 🚨 **Cổng release:** không phát hành khi còn dòng có `Xem lại trước = release` ở bảng "Đang chờ".

## Đang chờ

| D-xx | Ngày | Phiên / feature | Câu hỏi | Đã chọn (tạm) | Tin cậy | Chi phí lùi | Xem lại trước |
|---|---|---|---|---|---|---|---|
| D-101 | 18/11/2026 | N23 / ADDR-003 Q15 | Ledger append-only (BR-08) mang `InputSnapshot` có chuỗi địa chỉ (PII): ẩn danh khi khách yêu cầu xoá thế nào? (1) nợ GĐ2 cùng `ADDR-004` ADR-11 · (2) thiết kế ngay ẩn danh một phần | (1) + tối thiểu hoá: BR-08 giữ nguyên, 0 đường sửa/xoá ledger; `InputSnapshot` serialize từ record riêng `AddressInputSnapshot` khoá cố định, PII chỉ ở khoá con `freeText` {`rawText`,`streetLine`}; cột khác (`Candidates`/`CorrelationId`/`IdempotencyKey`/ETO/log) không PII, `Note` có nhãn cảnh báo; 🚨 **chặn release** tới khi người (pháp chế) chọn hướng GĐ2: (i) một cửa ẩn danh `freeText` (ngoại lệ duy nhất BR-08) · (ii) tách `freeText` ra bảng xoá được — rẻ nhất nếu chọn trước WP code ADDR-003 · (iii) giữ có thời hạn theo căn cứ pháp lý; ❓ `freeText` có vào bản xuất PII không; xác minh Luật 91/2025/QH15 + NĐ 356/2025 thay NĐ 13/2023 (chưa xác minh điều khoản) | VỪA | THẤP lúc này (chỉ tài liệu, bảng chưa tạo); thành CAO sau dòng production đầu tiên | release |
| D-116 | 19/11/2026 | N31 / SHP-001 Q7b · Q5b · Q9-TTL (uỷ quyền `D-114`) | (Q7b) `IShipmentAppService` dùng chung `SHP-001`/`SHP-002` hay tách? · (Q5b) làm tròn `Σ` cân SKU × SL lên bậc nào? · (Q9) mặc định `FeeEstimateCacheTtlSeconds`: 60 / 300 / 900 s? | (Q7b) một interface + một class (được `partial`), `SHP-001` là chủ, UoW không transaction chỉ trên method cần · (Q5b) `ceil` lên gram nguyên **một lần trên tổng**, kiểm tràn `int`, không làm tròn theo bậc hãng; "bậc cân" của khoá cache = `TotalWeightGrams` · (Q9) `300` s, `0` = tắt, khoá cache có `TenantId` + `ProviderCode` + toàn bộ yêu cầu phí, `CreateShipmentAsync` không đọc cache | VỪA (Q7b, Q5b) · THẤP (Q9 — chưa đo) | THẤP (0 tệp `.cs`, 0 migration; Q9 đổi Setting không build) | release (Q7b thêm mốc: WP code đầu tiên của `SHP-001`) |
| D-117 | 19/11/2026 | N30 / SHP-003 Q10 (uỷ quyền D-114) | Khi đổi COD sang Confirmed, Order.CodAmount có cập nhật theo không? | (a) Sales tiêu thụ CodAdjustmentConfirmedEto, so-và-đặt, không ghi OrderStatusHistory | VỪA | THẤP | release |
| D-118 | 20/11/2026 | N31 / SHP-001 DOCREV #37 (uỷ quyền `D-114`; thay thế phần ⑥ Q7b của `D-116`) | `TC-53` "đúng 1 `IApplicationService` tên chứa `Shipment`" va `IShipmentCodAppService` (`SHP-003` §6.1) cùng project: đổi điều kiện thế nào? | Danh sách đóng có tên {`IShipmentAppService`, `IShipmentCodAppService`} (⊆ + chứa `IShipmentAppService`) + neo `Create/CancelShipmentAsync`, `EstimateOrder/CartFeeAsync` chỉ khai trên `IShipmentAppService`; `TC-19` quét mọi `IApplicationService` ShippingHub trừ đúng `IShipmentCodAppService` | VỪA | THẤP (chỉ tài liệu; sau code = một hằng trong test) | WP code đầu tiên của `SHP-001` |
| D-119 | 20/11/2026 | N30 / SHP-002 DOCREV #1 (uỷ quyền `D-114`) | Máy trạng thái vận đơn chỉ có cạnh từng bước; hãng mất webhook trung gian ⇒ đối soát không áp được `Delivered`/`Returned`, vận đơn kẹt. Cho nhảy bước (a), sinh sự kiện trung gian (b) hay chờ người (c)? | (a) thu hẹp: hàng §3.2 #16 — `S` chưa hấp thụ ⇒ `T` đến được trên đồ thị §3.1 (không theo số enum), guard của đích (`Delivered`/`Returned` chỉ đối soát; webhook ⇒ `PendingConfirmStatus`); `Cancelled` chỉ từ `Created`; 1 dòng sự kiện + 1 ETO `From` thật; log + metric `shipping_status_skipped_total`; nợ `SAL-003` đi qua cạnh Sales có sẵn | VỪA | THẤP (chỉ tài liệu; sau code = bảng hằng cạnh + 4 TC, 0 migration) | release (mốc sớm: WP code đầu tiên của `SHP-002`) |
| D-120 | 20/11/2026 | N30 / SHP-003 Q12 · Q13 · Q14 (uỷ quyền `D-114`) | (Q12) Nguồn khoá chống gửi trùng đổi COD: `RequestKey` client / khoá theo nội dung / server `AttemptNo++`? · (Q13) `PendingApproval`/`Unknown` chiếm chỗ: không tự hết hạn / TTL tự `Rejected` / thêm CSKH tự huỷ? · (Q14) "không tìm thấy vận đơn": giữ `0301` hay gộp `0204`? | (Q12) (a): client sinh `RequestKey` một lần khi mở form; nguồn = trường bắt buộc trong body `RequestCodAdjustmentDto` (khuôn `D-42`/`PosController`), header `Idempotency-Key` chỉ bản sao, thiếu hoặc lệch ⇒ `0315`; giữ UNIQUE khoá + partial UNIQUE một yêu cầu chưa hấp thụ/vận đơn · (Q13) (a): chỉ duyệt/từ chối/đối soát giải phóng, không TTL; CSKH tự huỷ = nợ có chủ BA (chỉ `PendingApproval` nếu làm) · (Q14) (a): giữ `0301` (400), `0204` khác nghĩa + khác HTTP | VỪA | THẤP (chỉ tài liệu; sau code = 1 DTO/controller, 0 migration mới) | release (mốc sớm: WP code đầu tiên của `SHP-003`) |

## Chuyển người (agent không được quyết)

| D-xx | Ngày | Câu hỏi | Khuyến nghị | Đang làm tiếp trong lúc chờ |
|---|---|---|---|---|

## Đã xử lý

| D-xx | Kết quả (ĐÃ CHỐT / thay thế bằng D-yy) | Ngày |
|---|---|---|
| H1–H4 ⇒ `D-70` | ĐÃ CHỐT theo khuyến nghị: `SAL-006` + `NOT-*` tối thiểu vào GĐ3 · `REV-001` bỏ · storefront còn lại đóng băng | 14/11/2026 |
| D-72 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-73 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-74 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-75 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-76 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-77 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-78 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-79 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-80 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-81 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-82 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-83 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-84 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-85 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-86 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-87 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-88 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-89 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-90 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-91 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-92 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-95 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-96 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-97 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-98 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-99 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-100 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-102 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-103 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-104 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-108 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-109 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-110 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-111 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-112 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-113 | ĐÃ CHỐT (người, phiên uỷ quyền `U-02`, theo `D-114`) | 19/11/2026 |
| D-114, D-115 | ĐÃ CHỐT (người) — hồ sơ uỷ quyền hiệu lực + bổ sung `D-71` (M2); không qua hàng đợi | 19/11/2026 |


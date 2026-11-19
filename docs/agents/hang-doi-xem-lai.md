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


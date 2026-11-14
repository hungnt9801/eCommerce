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

## Chuyển người (agent không được quyết)

| D-xx | Ngày | Câu hỏi | Khuyến nghị | Đang làm tiếp trong lúc chờ |
|---|---|---|---|---|

## Đã xử lý

| D-xx | Kết quả (ĐÃ CHỐT / thay thế bằng D-yy) | Ngày |
|---|---|---|
| H1–H4 ⇒ `D-70` | ĐÃ CHỐT theo khuyến nghị: `SAL-006` + `NOT-*` tối thiểu vào GĐ3 · `REV-001` bỏ · storefront còn lại đóng băng | 14/11/2026 |

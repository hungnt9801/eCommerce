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
| D-72 | 17/11/2026 | dựng hệ B1–B2 | Model nào cho từng vai? | Opus: docrev, decider, (arch, rev); Sonnet: ba, ORCH, (be, fe, qa, ops) | VỪA | THẤP (sửa frontmatter) | B4 |
| D-73 | 17/11/2026 | dựng hệ B1 | Hàng rào hook dựng thế nào? | Node exec form, scopes.json một nguồn, fail-closed, lưới git status | CAO | THẤP | B3 |
| D-74 | 17/11/2026 | dựng hệ B2 | Quy ước tệp + đo phủ BR | docs/reviews, docs/inbox, mẫu trong skill, phủ BR theo §14 | CAO | THẤP | B3 |
| D-75 | 17/11/2026 | dựng hệ B1 (Q15) | Cấm agent chạy `dotnet ef database update`? | Cấm (đã áp tạm) — **cần chủ dự án xác nhận** | VỪA | THẤP | B5 |
| D-76 | 17/11/2026 | dựng hệ B2 | `/doc-gate` gửi decider câu nào, bao nhiêu tài liệu/phiên? | Chỉ câu chặn DoD; 2 tài liệu/phiên; SAL-006 riêng | VỪA | THẤP | B4 |

## Chuyển người (agent không được quyết)

| D-xx | Ngày | Câu hỏi | Khuyến nghị | Đang làm tiếp trong lúc chờ |
|---|---|---|---|---|

## Đã xử lý

| D-xx | Kết quả (ĐÃ CHỐT / thay thế bằng D-yy) | Ngày |
|---|---|---|
| H1–H4 ⇒ `D-70` | ĐÃ CHỐT theo khuyến nghị: `SAL-006` + `NOT-*` tối thiểu vào GĐ3 · `REV-001` bỏ · storefront còn lại đóng băng | 14/11/2026 |

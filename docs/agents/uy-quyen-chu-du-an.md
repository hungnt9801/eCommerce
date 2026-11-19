# Hồ sơ uỷ quyền của chủ dự án cho `decider`

> **Trạng thái: CHƯA HIỆU LỰC** — hồ sơ sẽ được điền và có hiệu lực ở phiên uỷ quyền `U-02` ([bảng phiên](bang-phien-wp14.md)).
> Khi chưa có hiệu lực, `decider` làm đúng như cũ: ma trận quyền + vùng cấm của `.claude/skills/decision-policy/SKILL.md`.
>
> **Chỉ chủ dự án** đổi tệp này, trong phiên `U-02` hoặc khi tự yêu cầu rõ ràng. Không vai nào có vùng ghi tệp này
> (`.claude/scopes.json`); ORCH chỉ ghi khi chủ dự án đang ngồi cùng và đã xác nhận nội dung.
> Thiết kế: [de-xuat-uy-quyen-va-chay-tu-dong.md](de-xuat-uy-quyen-va-chay-tu-dong.md) §4 (K1), §5 (K2), §11 (M1–M6).

<!-- Phiên U-02 thay dòng "Trạng thái" ở trên bằng: **Trạng thái: HIỆU LỰC** từ <ngày> · `D-xx` · xem lại: <ngày / mốc> -->

## 1. Mục tiêu và thứ tự ưu tiên

_(U-02 điền: khi trễ thì cắt gì trước; đúng-chắc hay nhanh khi hai phương án ngang nhau; "chuẩn" khi tài liệu mơ hồ.)_

## 2. Luật chơi M1–M6

| # | Câu | Chủ dự án chốt | `D-xx` |
|---|---|---|---|
| M1 | Phiên uỷ quyền một lần + duyệt lô khi rảnh | | |
| M2 | `DOC-OK` ngoài danh sách đỏ ⇒ decider **tạm duyệt** để code trên nhánh (không merge `main`) | | |
| M3 | Agent merge vào nhánh tích hợp khi cổng máy xanh | | |
| M4 | Câu kế toán: ai trả lời | | |
| M5 | Câu NCC: ai liên hệ; agent gọi sandbox? | | |
| M6 | Ngân sách / giờ chạy | | |

## 3. Chính sách theo nhóm câu (R1)

| Nhóm | Chính sách | Áp cho (mã câu / tài liệu) | `D-xx` |
|---|---|---|---|

## 4. Ngoại lệ đã chốt riêng (R2)

| Tài liệu | Câu | Chủ dự án chọn (khác khuyến nghị) | `D-xx` |
|---|---|---|---|

## 5. Việc không ai quyết được ngay (R3) — ai lo, khi nào

| Câu | Phụ thuộc | Ai lo | Khi nào | Dây chuyền làm gì trong lúc chờ |
|---|---|---|---|---|

## 6. Những điều decider KHÔNG BAO GIỜ quyết, dù có hồ sơ

`CLAUDE.md` §5 + `decision-policy` §3 mục 1–3: merge vào `main` · đổi branch protection · sửa/xoá dữ liệu thật · gọi API production
(thanh toán, HĐĐT, SMS, Zalo) · tạo/đổi secret · chọn NCC có phí / ký hợp đồng · điền dữ liệu bên ngoài (trường XML thuế, endpoint hãng) bằng suy đoán.

## 7. Câu treo, xem lại, thu hồi

- Câu chuyển người quá hạn: _(U-02 điền)_
- Đo tỉ lệ quyết định bị chủ dự án đảo khi duyệt lô; vượt 20 % ở một nhóm ⇒ nhóm đó về "chỉ `TIEN_LE`" (ghi `D-xx`).
- Thu hồi toàn bộ: đổi dòng "Trạng thái" thành **THU HỒI** — decider trở về chính sách cũ ngay phiên sau.

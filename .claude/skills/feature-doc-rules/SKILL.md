---
name: feature-doc-rules
description: Luật viết và đối chiếu tài liệu feature eCommerce (mẫu 16 mục §0–§15) — đọc theo mục để tiết kiệm ngữ cảnh, BR phải có test ở §14, NFR có ngưỡng số, danh sách quyết định D-xx bắt buộc đối chiếu (mô hình Nhanh), cách viết mục "Đối chiếu mô hình Nhanh", cách ghi câu hỏi mở. Nạp sẵn cho vai ba và docrev; dùng khi viết, sửa hoặc kiểm một tệp docs/features/<module>/<mã>-<slug>.md.
user-invocable: false
---

# Luật tài liệu feature — eCommerce

Nguồn sự thật: `docs/templates/feature-design-template.md` (mẫu 16 mục) · `docs/design-doc-guidelines.md` (§4 vòng đời,
§5 14 quy tắc chất lượng, §6 người review) · `docs/nhanh/decisions.md`. Skill này **dẫn**, không chép lại; mâu thuẫn ⇒ tệp nguồn thắng.

## 1. Đọc theo mục (bắt buộc — gói Pro có hạn mức)

Tài liệu thật dài 86–338 KB (gấp 5–16 lần mẫu). Đọc nguyên văn hai lượt (BA + DOCREV) một tài liệu lớn có thể ăn phần lớn một cửa sổ 5 giờ.

1. Lấy mục lục: `Grep` mẫu `^## ` (kèm số dòng) trên tệp.
2. Chỉ `Read` đoạn cần (`offset` = dòng heading, `limit` = tới heading kế).
3. Ghi lại các đoạn đã đọc vào `readRanges` của JSON trả về.
4. **Ngoại lệ:** DOCREV đọc toàn văn tài liệu đang kiểm (đó là giá trị của vai).
5. Không đọc tệp nhãn **LỊCH SỬ** làm nguồn (CLAUDE.md §2), không đọc `docs/nhanh/handover-n*.md` cũ.

## 2. Quyết định phải đối chiếu (chế độ `doi-chieu`, WP-14 ①)

Tài liệu GĐ3 viết **trước** các quyết định dưới đây (mô hình website). Với mỗi dòng, tìm chỗ tài liệu nói khác và **sửa thân**:

| D-xx | Quyết định (tóm tắt — đọc dòng gốc trong `docs/nhanh/decisions.md` trước khi sửa) | Dấu hiệu lệch thường gặp |
|---|---|---|
| `D-07` | Bỏ `ProductVariant` giữ tồn/giá → `ParentId` + `ProductUnit` + `Type` (10 loại) | Nhắc `ProductVariant`, `VariantId` làm khoá tồn/giá |
| `D-08` | Đơn giữ **4 trục**, `FulfillmentStatus` mở rộng **18 giá trị** (append); UI hiện 1 nhãn suy ra | "7 trạng thái đơn", một trục trạng thái |
| `D-17` | Enum lưu DB chỉ **thêm ở cuối** dải | Chèn giá trị giữa enum, đánh số lại |
| `D-18` / `D-42` | POS là aggregate riêng `RetailBill` + `CashSession`; tiền do **hàm thuần**; idempotent theo `ClientBillId` | Coi bán quầy là `Order`; client gửi tổng tiền |
| `D-27` | Tồn kho **vector 9 chiều**; `Available = OnHand − Reserved − Transferring − Holding − Damaged` | Tồn một số, "stock quantity" |
| `D-31` | Số lượng `decimal(18,3)` (thay `int`) | Cột/DTO số lượng `int` |
| `D-45` | **Một cửa ghi** điểm / tiền tích luỹ / công nợ khách: `Customer.GhiNhanMuaHang` qua `ICustomerLedger`, cùng UoW | Module khác tự cộng điểm/công nợ |
| `D-47` / `D-48` | Báo cáo đọc **read-model** (grain = dòng chứng từ, thay thế theo `(ngày, nguồn)`); doanh thu = `Σ thành tiền dòng − chiết khấu chứng từ`, loại chứng từ huỷ, VAT tách | Báo cáo query bảng nghiệp vụ trực tiếp; dùng `TotalPayment` |
| `D-03` | Kế toán GĐ0–GĐ3 = công nợ + xuất Excel/MISA; bút toán kép ở GĐ4 | Thiết kế sổ cái kép ngay GĐ3 |
| `D-06` / `D-70` | Storefront **không** làm lại; `REV-001` bỏ; `SAL-006` (RMA đơn giao vận) + `NOT-*` tối thiểu vào GĐ3 | Luồng storefront, đánh giá sản phẩm |

Căn cứ pháp lý: `docs/business-modules-proposal.md` §6.7 (đã có ghi chú đổi căn cứ — vd HĐĐT theo NĐ 254/2026). Không tự kết luận tuân thủ.
Gặp `D-xx` mới hơn tài liệu mà không có trong bảng ⇒ grep theo mã module trong `decisions.md` và đối chiếu thêm.

## 3. Luật nội dung (rút từ mẫu + `design-doc-guidelines.md` §5)

| # | Luật | Máy kiểm? |
|---|---|---|
| L1 | Đủ 16 mục `## 0.` … `## 15.`; mục không áp dụng ghi "Không áp dụng + lý do", không xoá | `check-feature-doc.ps1` |
| L2 | BR ở §1 dạng `BR-<MÃ>-NNN` (vd `BR-SHP-003-001`) hoặc `**BR-NN**`; mỗi BR được phủ ở cột "Phủ" của bảng §14 (mã đầy đủ hoặc viết tắt `BR-NN`) | `check-feature-doc.ps1` |
| L3 | NFR có **ngưỡng số** + cách đo ("nhanh", "ổn định" không hợp lệ) | DOCREV D7 |
| L4 | Mỗi nhánh state machine ghi **guard sai thì làm gì**; "bỏ qua" phải giải thích | DOCREV D7 |
| L5 | Mọi cửa vào từ ngoài (webhook, IPN, job retry) có **chống trùng** + nêu **index** idempotency | DOCREV D3/D7 |
| L6 | Khoá phân tán ghi **tên chính xác** (quy ước `XC-003`) | DOCREV D7 |
| L7 | Tiền `decimal` qua `Money`, cấm so `==`; số lượng `decimal(18,3)` | DOCREV D2/D7 |
| L8 | Không hard-code endpoint NCC; endpoint vào cấu hình, tài liệu ghi **nguồn** để xác minh | DOCREV D7 |
| L9 | Mỗi bảng ghi **module chủ**; lời gọi chéo module qua `*.Application.Contracts` / ETO + outbox (`XC-002`) | DOCREV D2 |
| L10 | Code thật mô tả kèm `file:dòng`; phần chưa có ghi "kế hoạch" (`docs/coding-conventions.md` §12) | DOCREV D2/D5 |
| L11 | Mermaid: không `;` trong text, không `<...>` làm placeholder (dùng `«...»`); chạy `npm --prefix tools run check:mermaid` | cổng mermaid |
| L12 | Tuyên bố mang nhãn ✅ đã kiểm · 🔶 một phần/chưa đo · ⬜ chưa có | DOCREV D6 |

## 4. Mục "Đối chiếu mô hình Nhanh (<ngày>)" — WP-14 ①

Đặt **cuối** tài liệu, heading `## Đối chiếu mô hình Nhanh (<dd/mm/yyyy>)` (không đánh số, để không đụng 16 mục của mẫu):

```text
| D-xx | Tài liệu cũ nói (§mục) | Đã sửa thành (§mục) | Code thật (file:dòng) / kế hoạch |
|---|---|---|---|
| D-08 | §3: 7 trạng thái đơn | §3: 4 trục, FulfillmentStatus 18 giá trị | modules/Sales/...:42 |
```

Không có chỗ lệch ⇒ một câu: "Đã đối chiếu với D-07, D-08, …, không lệch." — **không** để trống (script coi mục rỗng là lỗi).

## 5. Câu hỏi mở (§15) và `openQuestions`

- Câu đã có `D-xx` trả lời ⇒ đổi trạng thái thành `Đã chốt — D-xx`, sửa thân theo đó.
- Câu chưa có ⇒ **không tự chọn**; đưa vào `openQuestions` dạng **chọn-một**, có `recommend` + `why` (nguồn) + `blocks` (mục bị chặn)
  + `needs`: `DEC` (kỹ thuật/nghiệp vụ đảo ngược được) · `NCC` (chỉ hãng trả lời được — vd endpoint nào bắt OTP) · `ke-toan` · `nguoi` (vùng cấm).
- Câu `NCC`: thiết kế theo hướng **trung lập** (adapter + cấu hình + fail-closed) và ghi "chưa xác minh"; không chặn các mục khác.

## 6. Duyệt — ai, ở đâu

- Trạng thái duyệt: **biên bản duyệt** (`docs/bien-ban-duyet-gd1-2026-10-29.md` và biên bản sau cùng khuôn) → `docs/features/README.md` §2 → header.
  Header `Draft` **không** có nghĩa chưa duyệt. Agent **không** đổi header, **không** ghi biên bản.
- Người review bắt buộc theo loại (`design-doc-guidelines.md` §6): state machine ⇒ + kiến trúc; tích hợp NCC ⇒ + người đã liên hệ NCC;
  pháp lý/kế toán ⇒ + kế toán. Orchestrator in danh sách này vào nháp biên bản.

## 7. Quy tắc vàng

*"Code buộc lệch tài liệu ⇒ sửa tài liệu trước, trong cùng PR"* (`design-doc-guidelines.md` §4). BE không tự sửa hợp đồng: trả
`docDeviation` ⇒ orchestrator gọi BA sửa ⇒ DOCREV kiểm phần đổi.

## 8. Lệnh kiểm

```powershell
pwsh -File tools/agents/check-feature-doc.ps1 -Path <tệp> -WP14   # 16 mục, BR có test, mục đối chiếu Nhanh
pwsh -File tools/kiem-link-tai-lieu.ps1                          # link + đường dẫn backtick có thật
npm --prefix tools run check:mermaid                             # sơ đồ Mermaid hợp lệ
```

## 9. Khi bị chặn

Ghi `docs/inbox/<MÃ>-blocked.md`: chặn ở đâu (§mục), đã đọc gì (đoạn dòng), cần gì (câu hỏi chọn-một + khuyến nghị). Không tự mở rộng phạm vi.

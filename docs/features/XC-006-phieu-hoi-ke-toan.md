# PHIẾU HỎI KẾ TOÁN — Mẫu 01 giảm thuế GTGT 8%
### (kèm theo tài liệu thiết kế `XC-006`)

| | |
|---|---|
| **Gửi** | Kế toán trưởng |
| **Ngày** | 12/10/2026 |
| **Cần trả lời** | **2 câu** — ước tính 10–15 phút |
| **Vì sao cần** | Phần mềm phải **xuất Mẫu 01 đúng số**. Hiểu sai ⇒ **kê khai sai, phải làm lại tờ khai** |
| **Căn cứ** | **Mẫu số 01, Phụ lục III — Nghị định 174/2025/NĐ-CP** ngày 30/6/2025 (giảm thuế GTGT theo NQ 204/2025/QH15) |

---

## BỐI CẢNH

Mẫu 01 có **3 phần**. Phần mềm bán hàng của công ty **chỉ xuất được Phần II** (hàng hoá bán ra) —
vì hệ thống **không có phân hệ mua vào**. Phần I (mua vào) và Phần III (chênh lệch) kế toán làm trên phần mềm kế toán.

**Phần II — Hàng hoá, dịch vụ bán ra trong kỳ** — biểu mẫu có 6 cột:

| Cột | Nội dung **nguyên văn trong biểu mẫu** |
|---|---|
| (1) | STT |
| (2) | Tên hàng hóa, dịch vụ |
| (3) | Giá trị hàng hóa, dịch vụ chưa có thuế GTGT/Doanh thu hàng hóa, dịch vụ chịu thuế |
| (4) | Thuế suất/Tỷ lệ tính thuế GTGT **theo quy định** |
| (5) | Thuế suất/Tỷ lệ tính thuế GTGT **sau giảm** |
| (6) | Thuế GTGT của hàng hóa, dịch vụ bán ra **được giảm** |

**Công thức in sẵn ngay trong biểu mẫu:** `(5) = (4) × 80%` và `(6) = (3) × [(4) − (5)]`

---

# ❓ CÂU HỎI 1 — Cột (6) là con số nào?

**Ví dụ:** một mặt hàng có doanh thu chưa thuế = **100.000.000 đồng**; thuế suất theo quy định (4) = **10%**;
thuế suất sau giảm (5) = **8%**.

| Cách hiểu | Phép tính | Cột (6) bằng | Ý nghĩa |
|---|---|---|---|
| **A. Tính theo ĐÚNG CÔNG THỨC in trong mẫu** | `100.000.000 × (10% − 8%)` | **2.000.000 đ** | Số thuế **được giảm** (phần ngân sách giảm do chính sách) |
| **B. Tính theo TÊN CỦA CỘT** (*"thuế GTGT của hàng bán ra"*) | `100.000.000 × 8%` | **8.000.000 đ** | Số thuế **ở mức 8%** của hàng bán ra |

> ⚠️ **Tên cột và công thức trong biểu mẫu KHÔNG khớp nhau.** Hai cách hiểu lệch nhau **6.000.000 đồng cho mỗi 100 triệu doanh thu.**

### ✅ Cách trả lời dễ nhất

Mở phần mềm **HTKK**, nhập thử **1 dòng** dữ liệu như ví dụ trên, rồi xem:

1. Phần mềm tự tính **cột (6)** ra **2.000.000** hay **8.000.000**?
2. Chỉ tiêu **[08]** (tổng cột 6) và **[09]** mà HTKK sinh ra là bao nhiêu?

*(Chỉ cần chụp màn hình hoặc ghi lại 2 con số.)*

### ❓ Câu hỏi 1b

Phần II của Mẫu 01 liệt kê **những hàng nào**?

- ☐ Chỉ hàng hoá/dịch vụ **thuộc diện được giảm (8%)**
- ☐ **Tất cả** hàng bán ra trong kỳ (cả loại 10% và 5%)

---

# ❓ CÂU HỎI 2 — Cần xuất file dạng nào?

| | Phương án | Nghĩa là | Cần gì thêm |
|---|---|---|---|
| ☐ | **A. File nộp được luôn** | Xuất file **import vào HTKK / nộp qua eTax** là dùng được ngay | ⚠️ Cần **1 file mẫu** (hoặc file do HTKK xuất ra) để khớp **chính xác** định dạng ô/cột |
| ☐ | **B. Bảng số liệu** | Chỉ cần bảng đúng cột để kế toán **nhập lại** vào HTKK | Không cần gì thêm — bên lập trình tự thiết kế bảng Excel |

**Lưu ý để chọn:**

- Chọn **A**: tiện cho kế toán, **nhưng** định dạng do **HTKK** quyết định và **thay đổi theo phiên bản phần mềm**
  ⇒ mỗi lần HTKK nâng cấp có thể phải sửa lại, chi phí bảo trì cao hơn.
- Chọn **B**: rủi ro nhập tay sai thấp hơn, nhưng **tốn công kế toán** mỗi kỳ.

---

## PHẦN TRẢ LỜI

**Câu 1 — Cột (6) = ?**
✅ **B** — tính theo TÊN CỘT: `(3) × 8%` ⇒ ví dụ 100 triệu cho ra **8.000.000 đ**

**Câu 1b — Phần II liệt kê hàng nào?**
✅ **A** — CHỈ hàng hoá/dịch vụ **thuộc diện được giảm (8%)**

**Câu 2 — Chọn phương án (A hay B):**
✅ **A** — cần **file nộp được luôn** (import HTKK/eTax)

**Ghi chú thêm (nếu có):**
⚠️ Phiếu hỏi đã đề nghị phép thử HTKK 5 phút (nhập 1 dòng: 100.000.000 · (4)=10% · (5)=8%) và ghi lại
cột (6) + chỉ tiêu [08]/[09]. **Kế toán chưa ghi 2 con số này.** Với mức lệch **6 triệu/100 triệu doanh thu**,
đề nghị bổ sung phép thử này TRƯỚC khi lập trình xuất file.
🚧 Phương án A của Câu 2 cần **1 file mẫu do HTKK xuất ra** — hiện CHƯA có ⇒ chưa thể code phần xuất file.

| | |
|---|---|
| Ngày trả lời | **13 / 10 / 2026** |
| Người trả lời | **Kế toán trưởng** |
| Chức vụ | ……………………………………… |

---

### 📎 Phụ lục đính kèm (nếu cần đối chiếu)

- **Mẫu 01** (Phụ lục III NĐ 174/2025) — 3 phần: I. mua vào · II. bán ra · III. chênh lệch `[09] = [08] − [06]`
- Bảng mức thuế suất mà hệ thống đang dùng: `VAT0` (0%) · `VAT5` (5%) · **`VAT8` (8%, chỉ đến hết 31/12/2026)** · `VAT10` (10%)
- Hệ thống **tự chuyển về 10%** từ 00:00 ngày **01/01/2027** nếu không có văn bản gia hạn

namespace eCommerce.Tax.Mau01;

/// <summary>
/// Cách hiểu cột **(6)** của Mẫu 01 (Phụ lục III) — **điểm mâu thuẫn giữa TÊN CỘT và CÔNG THỨC IN TRONG MẪU**.
///
/// 🚩 VẤN ĐỀ (đã kiểm tra file gốc biểu mẫu):
///   • **Tên cột (6)**: *"Thuế GTGT của hàng hoá, dịch vụ bán ra **được giảm**"* ⇒ gợi ý số thuế **được giảm**
///   • **Công thức in trong mẫu**: `(6) = (3) × [(4) − (5)]` = `(3) × 2%` ⇒ cũng là số thuế **được giảm**
///   • **NHƯNG Phần III** lại tính `[09] = [08] − [06]`. Nếu (6) **đã là** số được giảm thì `[08] − [06]`
///     **trừ hai lần** ⇒ **vô lý**.
///   ⇒ Biểu mẫu **tự mâu thuẫn**. Hai cách hiểu lệch nhau **6.000.000 đ cho mỗi 100 triệu doanh thu**.
///
/// ✅ QUYẾT ĐỊNH (kế toán trưởng chốt 13/10/2026 — "Câu 1 = B"): dùng <see cref="ReducedRateTax"/>,
///    tức `(6) = (3) × (5)` = **số thuế ở mức SAU GIẢM (8%)**. Cách này làm **Phần III nhất quán**
///    (`[09] = [08] − [06]` = đúng số thuế được giảm).
///
/// 🔒 VÌ SAO VẪN ĐỂ DẠNG LỰA CHỌN (enum) MÀ KHÔNG HARD-CODE:
///    kế toán **chưa chạy phép thử HTKK** (nhập 1 dòng dữ liệu) để xác nhận con số. Với mức lệch
///    **6 triệu/100 triệu doanh thu**, nếu phép thử cho kết quả khác thì đổi lại chỉ là **đổi cấu hình**,
///    **không phải sửa lại công thức trong code**. Đây là cách "chỉnh sửa lại sau" rẻ nhất.
/// </summary>
public enum Mau01Column6Mode
{
    /// <summary>
    /// `(6) = (3) × [(4) − (5)]` — số thuế **ĐƯỢC GIẢM** (đúng công thức in trong mẫu).
    /// ⚠️ Dùng cách này thì Phần III **không được** tính `[09] = [08] − [06]` nữa (sẽ trừ hai lần).
    /// </summary>
    ReductionAmount = 0,

    /// <summary>
    /// `(6) = (3) × (5)` — số thuế ở mức **SAU GIẢM** (đúng TÊN CỘT).
    /// ✅ **ĐANG DÙNG** — kế toán trưởng chốt 13/10/2026; làm Phần III `[09] = [08] − [06]` nhất quán.
    /// </summary>
    ReducedRateTax = 1
}

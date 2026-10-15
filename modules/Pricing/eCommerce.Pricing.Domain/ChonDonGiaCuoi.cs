using eCommerce.Kernel.Money;

namespace eCommerce.Pricing;

/// <summary>
/// **Snapshot chính sách cho MỘT lần tính giá** (`PRC-002` **B1**/ADR-08).
/// 📌 Đọc **MỘT LẦN** ở đầu request rồi truyền xuyên suốt ⇒ request đang chạy dùng chính sách **cố định**
/// (`XC-001` §8.2 #1). Đọc lại giữa pipeline ⇒ hai dòng trong **cùng một** hoá đơn có thể dùng **hai chính sách
/// khác nhau** nếu cấu hình đổi đúng lúc ⇒ **tổng không khớp Σ dòng**.
/// </summary>
public sealed record ChinhSachSnapshot(
    string MaChinhSachLamTron,
    IRoundingPolicy ChinhSachLamTron,
    ChinhSachThieuGia ChinhSachThieuGia,
    DateTime At);

/// <summary>
/// 🚨 **CHỌN ĐƠN GIÁ CUỐI** (`PRC-002` **B5**) — hàm **THUẦN**.
///
/// **LUẬT (một câu, tường minh):** nếu có **bậc thang** hiệu lực cho số lượng này ⇒ **bậc thang THẮNG**;
/// ngược lại dùng **giá cơ sở** đã chọn ở B2 (`PRC-001` D7 — 4 cấp).
///
/// 📌 **VÌ SAO KHÔNG nhét bậc thang vào `GiaCoSoResolver` như một "cấp" thứ 5:** `GiaCoSoResolver` **NÉM**
/// khi hai ứng viên **cùng cấp + cùng `Priority`** (đúng luật của nó). Nếu bậc thang nằm cùng cấp với giá
/// bảng giá mà `Priority` bằng nhau thì hệ thống **NÉM `Pricing:0001` ⇒ checkout CHẾT** — biến một tính năng
/// (giảm giá theo số lượng) thành **sự cố**. Tách thành **một bước riêng sau B2** giữ được:
///   • luật 4 cấp của B2 **không đổi** (đã kiểm chứng ở `PRC-001`),
///   • bậc thang là **quyết định tường minh** "theo số lượng thì giá này",
///   • và **vẫn giải trình được**: `AllCandidates` giữ **cả** giá cơ sở lẫn bậc thang.
///
/// ⚠️ **Cái giá của lựa chọn này (ghi rõ):** bậc thang **luôn thắng** giá cơ sở khi hiệu lực — kể cả giá cơ sở
/// đến từ **cấp shop**. Nếu nghiệp vụ cần "giá shop thắng bậc thang" thì **phải** bổ sung luật ưu tiên giữa
/// hai nguồn ⇒ **cần người quyết** (xem `prc-002` §18.3).
/// </summary>
public static class ChonDonGiaCuoi
{
    /// <summary>Kết quả chọn: đơn giá + **nguồn** + **cấp** (để ghi log/sổ giải trình).</summary>
    public sealed record KetQua(Money DonGia, string Nguon, string Cap);

    public static KetQua Chon(
        Money giaCoSo,
        string nguonCoSo,
        string capCoSo,
        ChonBacThang.Bac? bacThang)
    {
        if (bacThang is null)
        {
            return new KetQua(giaCoSo, nguonCoSo, capCoSo);
        }

        // Kiểm tra loại tiền trước khi thay thế — cấm trộn tiền tệ (`PRC-001` BR-010).
        if (bacThang.DonGia.Currency != giaCoSo.Currency)
        {
            throw new ArgumentException(
                $"Bậc thang lệch loại tiền ({bacThang.DonGia.Currency.Code} ≠ {giaCoSo.Currency.Code}) — " +
                "CẤM tự quy đổi (PRC-001 BR-010).", nameof(bacThang));
        }

        return new KetQua(bacThang.DonGia, bacThang.Nguon, "BacThang");
    }
}

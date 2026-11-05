namespace eCommerce.Inventory;

/// <summary>
/// **HÀM THUẦN: tiền của phiếu kho** (đơn giá dòng → thành tiền → tổng phiếu) — `WP-04` việc 8.
///
/// 📌 **VÌ SAO TÁCH RA HÀM THUẦN:** tiền là chỗ **hai nơi cùng tính một công thức** dễ lệch nhất (API tính
/// một kiểu, UI/in phiếu tính kiểu khác ⇒ hoá đơn và sổ sách không khớp **mà không có lỗi nào**). Luật nằm
/// **một chỗ**, test được **không cần DB**.
///
/// 🚨 **QUYẾT ĐỊNH VỀ "THIẾU GIÁ" (D-37):** dòng phiếu **có thể không khai giá** (dữ liệu cũ, hàng biếu tặng,
/// kiểm kê). Khi đó **KHÔNG** cộng dòng đó như `0` — vì `0` nghĩa là *"miễn phí"*, còn *"chưa khai"* nghĩa là
/// *"không biết"*. Nếu **bất kỳ** dòng nào thiếu giá thì **tổng = `null`** (UI hiện `—`), thay vì một con số
/// **nhỏ hơn sự thật** mà người dùng tin là đúng.
///
/// ⚠️ **VAT ở đây là THÔNG TIN, không phải thuế phải nộp:** `VatPercent` của dòng được lưu từ `WP-04`; cách
/// tính *"giá đã gồm VAT hay chưa"* phụ thuộc `Product.VatMode` (`Included`/`Excluded`) — **WP-05/POS** mới có
/// ngữ cảnh đó. Ở đây `TongVat` chỉ là `Σ(thành tiền × %VAT)` để **in/hiển thị**, và được **cộng vào**
/// `TongThanhToan` theo giả định *"đơn giá chưa gồm VAT"* — giả định này ghi rõ ở `D-37`.
///
/// 📌 **Độ chính xác:** làm tròn **2 số lẻ** (`MidpointRounding.AwayFromZero`) ở **từng dòng**, rồi cộng dòng
/// đã làm tròn ⇒ tổng luôn bằng **tổng các số hiển thị** (nếu làm tròn sau khi cộng, cột Thành tiền và dòng
/// Tổng có thể lệch 1 đồng — lỗi nhìn thấy được trên hoá đơn).
/// </summary>
public static class TinhTienChungTu
{
    /// <summary>Số lẻ của **tiền** (VND dùng 0 khi hiển thị, nhưng vẫn giữ 2 để chịu giá chia nhỏ).</summary>
    public const int SoLeTien = 2;

    public static decimal LamTronTien(decimal tien) => Math.Round(tien, SoLeTien, MidpointRounding.AwayFromZero);

    /// <summary>
    /// **Thành tiền một dòng** = số lượng × đơn giá. `null` khi dòng **không khai đơn giá**.
    /// 📌 Số lượng có thể lẻ 3 chữ số (kg/ml) ⇒ tích có thể lẻ nhiều hơn; làm tròn về 2.
    /// </summary>
    public static decimal? ThanhTien(decimal soLuong, decimal? donGia)
    {
        if (donGia is null)
        {
            return null;
        }

        if (donGia < 0m)
        {
            // 🚨 Đơn giá âm không có nghĩa nghiệp vụ nào (chiết khấu đi đường riêng, D-15/`DatTien`).
            throw new ArgumentOutOfRangeException(nameof(donGia), donGia, "Đơn giá không được âm.");
        }

        return LamTronTien(soLuong * donGia.Value);
    }

    /// <summary>**Tiền VAT một dòng** = thành tiền × %VAT / 100. `null` khi thiếu thành tiền **hoặc** thiếu %VAT.</summary>
    public static decimal? TienVat(decimal? thanhTien, decimal? vatPercent)
    {
        if (thanhTien is null || vatPercent is null)
        {
            return null;
        }

        if (vatPercent is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(vatPercent), vatPercent, "% VAT phải trong 0..100.");
        }

        return LamTronTien(thanhTien.Value * vatPercent.Value / 100m);
    }

    /// <summary>
    /// **Tổng tiền hàng** = Σ thành tiền dòng. `null` khi: danh sách **rỗng**, hoặc **có dòng thiếu giá**
    /// (xem quyết định ở đầu lớp — `0` sẽ là lời nói dối).
    /// </summary>
    public static decimal? TongTienHang(IEnumerable<(decimal SoLuong, decimal? DonGia)> dong)
    {
        var tong = 0m;
        var coDong = false;

        foreach (var (soLuong, donGia) in dong)
        {
            coDong = true;

            var thanhTien = ThanhTien(soLuong, donGia);

            if (thanhTien is null)
            {
                return null;
            }

            tong += thanhTien.Value;
        }

        return coDong ? LamTronTien(tong) : null;
    }

    /// <summary>
    /// **Tổng VAT** = Σ tiền VAT dòng. `null` khi **không dòng nào** khai %VAT (không phải `0`: chưa khai ≠ miễn thuế).
    /// 📌 Dòng **có** %VAT nhưng thiếu đơn giá cũng làm tổng `null` — cùng lý do "không biết".
    /// </summary>
    public static decimal? TongVat(IEnumerable<(decimal SoLuong, decimal? DonGia, decimal? VatPercent)> dong)
    {
        var tong = 0m;
        var coVat = false;

        foreach (var (soLuong, donGia, vatPercent) in dong)
        {
            if (vatPercent is null)
            {
                continue;
            }

            var thanhTien = ThanhTien(soLuong, donGia);

            if (thanhTien is null)
            {
                return null;
            }

            tong += TienVat(thanhTien, vatPercent) ?? 0m;
            coVat = true;
        }

        return coVat ? LamTronTien(tong) : null;
    }

    /// <summary>
    /// **Tổng thanh toán** = tổng tiền hàng + tổng VAT − chiết khấu.
    /// `null` nếu không xác định được tổng tiền hàng (thiếu giá). 📌 Chiết khấu âm ⇒ NÉM (dùng `DatTien`).
    /// </summary>
    public static decimal? TongThanhToan(decimal? tongTienHang, decimal? tongVat, decimal? chietKhau)
    {
        if (tongTienHang is null)
        {
            return null;
        }

        if (chietKhau is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(chietKhau), chietKhau, "Chiết khấu không được âm.");
        }

        return LamTronTien(tongTienHang.Value + (tongVat ?? 0m) - (chietKhau ?? 0m));
    }
}

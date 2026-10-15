namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MODULE PRICING — NGUỒN SỰ THẬT DUY NHẤT** (`PRC-001` §12 + `PRC-002`).
///
/// 📌 **VÌ SAO Ở TRONG MODULE, KHÔNG Ở `eCommerceConsts`:** quy ước dự án (*"module sở hữu hằng số của mình"*)
/// — đã có tiền lệ và **đã có bài học**: mã lỗi miền **kênh** từng tồn tại ở **cả hai** nơi với **hai định dạng
/// khác nhau** (`eCommerce:Channel:*` vs `ChannelHub:*`) ⇒ **hai nguồn sự thật cho cùng một mã**
/// (`XC-003` F2). Pricing nay là **module thật** ⇒ khai ở đây, **không** khai lại ở `eCommerceConsts`.
/// </summary>
public static class PricingErrorCodes
{
    /// <summary>Nhiều ứng viên **cùng cấp** và **cùng `Priority`** ⇒ dữ liệu mâu thuẫn (**NÉM**, không chọn tuỳ tiện).</summary>
    public const string UngVienCungCapXungDot = "eCommerce:Pricing:0001";

    /// <summary>Phát hành bảng giá **thiếu dòng** (`PRC-001` E1).</summary>
    public const string BangGiaThieuDong = "eCommerce:Pricing:0002";

    /// <summary>Sửa/xoá bảng giá đang **`Active`** ⇒ append-only (`PRC-001` E2/D4).</summary>
    public const string BangGiaActiveKhongSuaDuoc = "eCommerce:Pricing:0003";

    /// <summary>Không tìm thấy giá mà chính sách = `Throw` (`PRC-001` E3/D14) — **kèm `Sku` + kênh + ngày**.</summary>
    public const string KhongTimThayGia = "eCommerce:Pricing:0004";

    /// <summary>`Currency` không khớp ⇒ **cấm tự quy đổi** (`PRC-001` E4/BR-010).</summary>
    public const string LechLoaiTien = "eCommerce:Pricing:0005";

    /// <summary>Import thiếu khoá idempotency (`PRC-001` E5/D11).</summary>
    public const string ThieuKhoaIdempotency = "eCommerce:Pricing:0006";

    /// <summary>`RowVersion` lệch khi hai người sửa cùng bảng giá (`PRC-001` E6/D9).</summary>
    public const string XungDotPhienBan = "eCommerce:Pricing:0007";

    /// <summary>Hai bậc thang **cùng `MinQuantity`** trên cùng SKU/khung (`PRC-002` ADR-02) — dữ liệu mâu thuẫn.</summary>
    public const string BacThangTrungMinQuantity = "eCommerce:Pricing:0008";
}

/// <summary>
/// 🚨 **TÊN KHOÁ PHÂN TÁN CỦA PRICING** — **KHÔNG** chứa tiền tố `ecommerce:`.
///
/// ABP ghép `KeyPrefix + tên khoá` **KHÔNG có dấu phân cách**, và `KeyPrefix` **đã là** `"ecommerce:"`
/// (`XC-003` **§5.4 hệ quả #2**; `PRC-002` **ADR-12**). Tự thêm tiền tố ⇒ khoá thật
/// `ecommerce:ecommerce:pricing:…` ⇒ **khoá không bảo vệ gì** (đúng lỗi C13/F1 đã gặp).
///
/// 📌 **LƯU Ý:** `PRC-002` §15.2 **F3** ghi rằng `PRC-001` §8.2 nói **ngược lại** quy ước này — tài liệu
/// `PRC-001` cần được sửa cho khớp; ở **code** tôi theo `XC-003` (nguồn sự thật về khoá).
/// </summary>
public static class PricingLockKeys
{
    /// <summary>Khoá khi **phát hành/kích hoạt** bảng giá theo `(tenant, scope, kênh)` — chống hai bảng giá cùng `Active`.</summary>
    public static string BangGiaTheoPhamVi(Guid? tenantId, string scope, string? channelCode)
        => $"pricing:pricelist:{tenantId?.ToString() ?? "null"}:{scope}:{channelCode ?? "all"}";
}

namespace eCommerce.Signing;

/// <summary>
/// Mã lỗi miền **Signing** (`SIGN-001`), quy ước `eCommerce:Sign:nnnn`.
/// 🔒 Do CHÍNH module sở hữu.
/// </summary>
public static class SigningErrorCodes
{
    /// <summary>Chuyển trạng thái không hợp lệ (state machine ném lỗi).</summary>
    public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Sign:0001";

    /// <summary>
    /// 🚨 **Hash tài liệu KHÔNG khớp** giữa bước 1 (chuẩn bị) và bước 2 (ký xong).
    ///
    /// ĐÂY LÀ LỖI IM LẶNG NGUY HIỂM NHẤT của ký 2 bước: người ký ký một tệp **khác** với tệp hệ thống
    /// đã chuẩn bị (tệp bị thay, hoặc ký nhầm tệp của chứng từ khác). Nếu không kiểm hash thì hệ thống
    /// **nhận chữ ký của tài liệu A và gắn vào tài liệu B** — chữ ký số vẫn "hợp lệ" về mặt kỹ thuật
    /// nhưng **vô hiệu về pháp lý**, và không có cảnh báo nào.
    /// </summary>
    public const string HashKhongKhop = "eCommerce:Sign:0002";

    /// <summary>Chứng thư số không hợp lệ tại thời điểm ký (hết hạn, chưa hiệu lực, hoặc đã bị thu hồi).</summary>
    public const string ChungThuKhongHopLe = "eCommerce:Sign:0003";

    /// <summary>Thiếu lý do khi huỷ yêu cầu ký.</summary>
    public const string ThieuLyDo = "eCommerce:Sign:0004";

    /// <summary>Thiếu thông tin bắt buộc (mã tài liệu, hash, chữ ký, người ký…).</summary>
    public const string ThieuThongTinBatBuoc = "eCommerce:Sign:0005";

    /// <summary>Yêu cầu ký đã ở trạng thái kết thúc (đã ký hoặc đã huỷ) — không thao tác được nữa.</summary>
    public const string DaKetThuc = "eCommerce:Sign:0006";

    /// <summary>Hash sai định dạng (ví dụ SHA-256 hex phải đúng 64 ký tự).</summary>
    public const string HashKhongHopLe = "eCommerce:Sign:0007";
}

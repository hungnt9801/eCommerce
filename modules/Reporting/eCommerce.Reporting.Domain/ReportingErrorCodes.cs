namespace eCommerce.Reporting;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN BÁO CÁO** (`eCommerce:Reports:xxxx`) — `WP-08`.
///
/// 📌 Mỗi mã là **một chỉ dẫn sửa** cho người dùng, không phải mô tả kỹ thuật (`D6`):
/// *"khoảng ngày không hợp lệ"* khác *"báo cáo chưa được tổng hợp"* — gộp lại thì người dùng không biết
/// phải bấm gì. Mọi mã **phải** được map sang HTTP status ở Host (`AbpExceptionHttpStatusCodeOptions`),
/// nếu không ABP trả **`403`** và client đọc thành *"tôi không có quyền"* (bài học `INV-003` `0042`).
/// </summary>
public static class ReportingErrorCodes
{
    /// <summary>Nguồn báo cáo không hợp lệ (ví dụ dùng `TonKho` cho sổ doanh thu bán hàng).</summary>
    public const string NguonKhongHopLe = "eCommerce:Reports:0001";

    /// <summary>Khoảng ngày không hợp lệ (`từ` &gt; `đến`).</summary>
    public const string KhoangKhongHopLe = "eCommerce:Reports:0002";

    /// <summary>Khoảng ngày vượt trần một lượt tổng hợp (366 ngày) — chia nhỏ để chạy.</summary>
    public const string KhoangQuaDai = "eCommerce:Reports:0003";

    /// <summary>Chiết khấu chứng từ LỚN HƠN tổng tiền dòng ⇒ doanh thu âm (dữ liệu chứng từ sai).</summary>
    public const string DoanhThuAm = "eCommerce:Reports:0004";

    /// <summary>Chứng từ có dòng nhưng dòng không thuộc chứng từ nào đang xét (lỗi ghép dữ liệu).</summary>
    public const string DongMoCoi = "eCommerce:Reports:0005";

    /// <summary>Số lượng tồn âm khi chụp ảnh kho (dữ liệu kho đã sai từ trước — không được ghi ảnh xấu).</summary>
    public const string TonKhoAm = "eCommerce:Reports:0006";

    /// <summary>Chưa có lượt tổng hợp nào cho khoảng ngày được hỏi (báo cáo **rỗng**, không phải 0 đồng).</summary>
    public const string ChuaTongHop = "eCommerce:Reports:0007";

    /// <summary>Một dòng nguồn xuất hiện HAI lần trong cùng lượt ⇒ doanh thu sẽ bị nhân đôi.</summary>
    public const string DongTrung = "eCommerce:Reports:0008";

    /// <summary>Chứng từ có chiết khấu nhưng KHÔNG có dòng nào ⇒ không tính được doanh thu.</summary>
    public const string ChungTuKhongCoDong = "eCommerce:Reports:0009";
}

namespace eCommerce.EInvoice;

/// <summary>
/// **Trục 1 — trạng thái PHÁT HÀNH** của hoá đơn điện tử.
///
/// 🎯 VÌ SAO TÁCH LÀM **2 TRỤC** (đây là điểm dễ hiểu sai nhất của hoá đơn điện tử VN):
///   "đã phát hành" (trục nghiệp vụ, với khách) và "đã gửi/nhận kết quả cơ quan thuế" (trục tuân thủ,
///   với CQT) là **HAI chuyện khác nhau**. Gộp làm một trường trạng thái sẽ dẫn tới **mâu thuẫn không
///   biểu diễn được**: hoá đơn đã phát hành cho khách nhưng CQT **từ chối** — phải biết cả hai để xử lý.
/// </summary>
public enum EInvoiceStatus
{
    /// <summary>Nháp — chưa ký, chưa có giá trị.</summary>
    Nhap = 0,

    /// <summary>Đã ký số (bước 2 của quy trình ký) nhưng **chưa** phát hành.</summary>
    DaKy = 1,

    /// <summary>Đã phát hành cho khách.</summary>
    DaPhatHanh = 2,

    /// <summary>Đã bị **điều chỉnh** bởi một hoá đơn khác (vẫn tồn tại, không xoá).</summary>
    DaDieuChinh = 3,

    /// <summary>Đã bị **thay thế** bởi một hoá đơn khác.</summary>
    DaThayThe = 4,

    /// <summary>Đã **huỷ** — trạng thái KẾT THÚC, không thao tác được nữa.</summary>
    DaHuy = 5
}

/// <summary>
/// **Trục 2 — trạng thái GỬI CƠ QUAN THUẾ**. Độc lập với <see cref="EInvoiceStatus"/>.
/// </summary>
public enum TaxAuthorityStatus
{
    /// <summary>Chưa gửi.</summary>
    ChuaGui = 0,

    /// <summary>Đã gửi, đang chờ kết quả.</summary>
    DaGui = 1,

    /// <summary>CQT đã **nhận** (có mã giao dịch/mã cấp).</summary>
    DaNhan = 2,

    /// <summary>CQT **từ chối** (chuỗi `ERR:` — xem `EINV-002`). Vẫn phải xử lý tiếp.</summary>
    BiTuChoi = 3
}

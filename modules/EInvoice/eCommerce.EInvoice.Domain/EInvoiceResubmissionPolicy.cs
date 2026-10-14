namespace eCommerce.EInvoice;

/// <summary>
/// **Chính sách gửi lại cơ quan thuế** cho hoá đơn đã bị từ chối (`EINV-002` + mục (3)).
///
/// 🎯 VÌ SAO CẦN CHÍNH SÁCH TƯỜNG MINH: nếu job cứ gửi lại vô điều kiện thì (a) hoá đơn lỗi **vĩnh viễn**
/// (ví dụ sai định dạng XML) sẽ bị gửi **mãi mãi**, làm rác log và có thể bị CQT **chặn tần suất**;
/// (b) ngược lại, nếu không gửi lại thì hoá đơn **hợp lệ nhưng lỗi tạm thời** sẽ **mắc kẹt vĩnh viễn**.
///
/// 🔒 Hàm THUẦN (không DI, không DB, không thời gian hệ thống) ⇒ test được toàn bộ, và dùng chung cho
/// mọi đường gửi lại (job định kỳ và API thủ công của kế toán).
///
/// 📌 Ngưỡng thử **5** theo chốt `XC-002` Q3.
/// </summary>
public static class EInvoiceResubmissionPolicy
{
    /// <summary>Số lần thử tối đa cho một hoá đơn (chốt `XC-002` Q3).</summary>
    public const int SoLanThuToiDa = 5;

    /// <summary>Khoảng cách tối thiểu giữa hai lần gửi — tránh dồn dập làm CQT chặn tần suất.</summary>
    public static readonly TimeSpan KhoangCachToiThieu = TimeSpan.FromMinutes(15);

    /// <summary>Lý do quyết định — để ghi log/metric và giải thích cho kế toán.</summary>
    public enum LyDo
    {
        /// <summary>Đủ điều kiện gửi lại.</summary>
        DuDieuKien = 0,

        /// <summary>Không ở trạng thái bị từ chối ⇒ không có gì để gửi lại.</summary>
        KhongPhaiDangBiTuChoi = 1,

        /// <summary>Lỗi **vĩnh viễn** (không phải lỗi tạm thời) ⇒ phải có người sửa, không gửi lại tự động.</summary>
        LoiVinhVien = 2,

        /// <summary>Hết lượt thử.</summary>
        HetLuotThu = 3,

        /// <summary>Chưa đủ khoảng cách tối thiểu kể từ lần gửi trước.</summary>
        ChuaDuKhoangCach = 4
    }

    /// <summary>Kết quả quyết định gửi lại.</summary>
    public sealed record KetQua(bool NenGuiLai, LyDo LyDo, string ThongDiep);

    /// <summary>Quyết định có nên gửi lại hay không.</summary>
    /// <param name="trangThaiCqt">Trạng thái CQT hiện tại của hoá đơn.</param>
    /// <param name="maLoiCqt">Mã lỗi CQT đã nhận (chuỗi sau `ERR:`).</param>
    /// <param name="soLanDaThu">Số lần đã gửi (kể cả lần thất bại gần nhất).</param>
    /// <param name="lanGuiCuoiUtc">Thời điểm gửi gần nhất (UTC); `null` nếu chưa từng gửi.</param>
    /// <param name="nowUtc">Thời điểm hiện tại (UTC) — truyền vào để hàm THUẦN và test được.</param>
    public static KetQua DanhGia(
        TaxAuthorityStatus trangThaiCqt,
        string? maLoiCqt,
        int soLanDaThu,
        DateTime? lanGuiCuoiUtc,
        DateTime nowUtc)
    {
        if (trangThaiCqt != TaxAuthorityStatus.BiTuChoi)
        {
            return new KetQua(false, LyDo.KhongPhaiDangBiTuChoi,
                $"Hoá đơn không ở trạng thái bị từ chối (đang là {trangThaiCqt}).");
        }

        if (soLanDaThu >= SoLanThuToiDa)
        {
            return new KetQua(false, LyDo.HetLuotThu,
                $"Đã thử {soLanDaThu}/{SoLanThuToiDa} lần ⇒ DỪNG tự động, cần người xử lý.");
        }

        // 🚨 Lỗi VĨNH VIỄN: gửi lại y nguyên nội dung sẽ hỏng y nguyên ⇒ không lãng phí lượt gửi.
        if (!EInvoiceResponseParser.LaLoiTamThoi(maLoiCqt))
        {
            return new KetQua(false, LyDo.LoiVinhVien,
                $"Lỗi '{maLoiCqt}' không phải lỗi tạm thời ⇒ phải sửa nội dung trước khi gửi lại.");
        }

        if (lanGuiCuoiUtc is { } lanCuoi && nowUtc - lanCuoi < KhoangCachToiThieu)
        {
            return new KetQua(false, LyDo.ChuaDuKhoangCach,
                $"Mới gửi cách đây {nowUtc - lanCuoi:g}, cần chờ tối thiểu {KhoangCachToiThieu:g}.");
        }

        return new KetQua(true, LyDo.DuDieuKien,
            $"Đủ điều kiện gửi lại (lần {soLanDaThu + 1}/{SoLanThuToiDa}, lỗi tạm thời '{maLoiCqt}').");
    }
}

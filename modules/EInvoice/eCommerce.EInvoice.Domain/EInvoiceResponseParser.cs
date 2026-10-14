namespace eCommerce.EInvoice;

/// <summary>Mã lỗi cho việc **gửi cơ quan thuế** (`EINV-002`) — dải `eCommerce:EInvoice:03xx`.</summary>
public static class EInvoiceSubmissionErrorCodes
{
    /// <summary>
    /// 🚨 Phản hồi của cơ quan thuế **KHÔNG nhận dạng được**.
    ///
    /// Đây là lỗi IM LẶNG nguy hiểm nhất khi tích hợp CQT: nếu coi "không nhận dạng được" là **thành công**
    /// thì hệ thống sẽ đánh dấu hoá đơn **đã gửi thành công** trong khi thực tế **CQT chưa nhận** ⇒
    /// hoá đơn **không được cấp mã** mà kế toán tưởng là xong.
    /// </summary>
    public const string PhanHoiKhongNhanDangDuoc = "eCommerce:EInvoice:0301";

    /// <summary>Phản hồi `OK:` nhưng **thiếu mã cơ quan thuế** ⇒ không thể đối chiếu, phải coi là lỗi.</summary>
    public const string OkThieuMaCQT = "eCommerce:EInvoice:0302";

    /// <summary>Phản hồi rỗng.</summary>
    public const string PhanHoiRong = "eCommerce:EInvoice:0303";
}

/// <summary>Kết quả gửi/đối chiếu với cơ quan thuế.</summary>
/// <param name="ThanhCong">CQT đã nhận hay chưa.</param>
/// <param name="MaCQT">Mã cơ quan thuế cấp (chỉ có khi thành công).</param>
/// <param name="MaLoi">Mã lỗi CQT trả về (chỉ có khi thất bại).</param>
/// <param name="MoTa">Mô tả thêm nếu phản hồi có kèm (phần sau dấu hai chấm thứ hai).</param>
public sealed record KetQuaGuiCQT(
    bool ThanhCong,
    string? MaCQT = null,
    string? MaLoi = null,
    string? MoTa = null);

/// <summary>
/// Phân tích **phản hồi của cơ quan thuế** theo quy ước `OK:` / `ERR:`.
///
/// 🔒 NGUYÊN TẮC **FAIL-CLOSED** — đây là điểm quan trọng nhất của lớp này:
///   Chỉ **`OK:` có mã** mới được coi là thành công. Mọi thứ khác — kể cả chuỗi lạ, chuỗi rỗng,
///   hay `OK:` **thiếu mã** — đều **NÉM LỖI**. Tuyệt đối **không** mặc định là thành công, vì hậu quả là
///   **hoá đơn chưa được CQT nhận mà hệ thống tưởng đã xong** (không có cảnh báo nào để phát hiện).
///
/// 📌 Phạm vi: lớp này **chỉ** phân tích chuỗi. Việc gọi HTTP tới CQT là việc của tầng hạ tầng
/// (`IHttpClientFactory` — hiện **chưa có** trong repo, xem `EINV-002` phát hiện F3).
/// </summary>
public static class EInvoiceResponseParser
{
    public const string TienToThanhCong = "OK:";
    public const string TienToLoi = "ERR:";

    /// <summary>Phân tích phản hồi. Ném <see cref="Volo.Abp.BusinessException"/> khi không nhận dạng được.</summary>
    public static KetQuaGuiCQT PhanTich(string? phanHoi)
    {
        if (string.IsNullOrWhiteSpace(phanHoi))
        {
            // Rỗng KHÔNG phải thành công: không có mã nghĩa là không có bằng chứng CQT đã nhận.
            throw new Volo.Abp.BusinessException(EInvoiceSubmissionErrorCodes.PhanHoiRong);
        }

        var s = phanHoi.Trim();

        if (s.StartsWith(TienToThanhCong, StringComparison.OrdinalIgnoreCase))
        {
            var phan = s[TienToThanhCong.Length..].Trim();

            if (phan.Length == 0)
            {
                // ⚠️ `OK:` mà không có mã ⇒ KHÔNG thể đối chiếu về sau ⇒ coi là lỗi, không phải thành công.
                throw new Volo.Abp.BusinessException(EInvoiceSubmissionErrorCodes.OkThieuMaCQT)
                    .WithData("phanHoi", s);
            }

            // Cho phép dạng `OK:<mã>` và `OK:<mã>:<mô tả>`.
            var phanTach = phan.Split(':', 2, StringSplitOptions.TrimEntries);

            return new KetQuaGuiCQT(
                ThanhCong: true,
                MaCQT: phanTach[0],
                MoTa: phanTach.Length > 1 ? phanTach[1] : null);
        }

        if (s.StartsWith(TienToLoi, StringComparison.OrdinalIgnoreCase))
        {
            var phan = s[TienToLoi.Length..].Trim();
            var phanTach = phan.Split(':', 2, StringSplitOptions.TrimEntries);

            return new KetQuaGuiCQT(
                ThanhCong: false,
                // `ERR:` thiếu mã vẫn coi là lỗi (an toàn), nhưng ghi lại toàn bộ phần còn lại để đối chiếu.
                MaLoi: phanTach.Length > 0 && phanTach[0].Length > 0 ? phanTach[0] : s,
                MoTa: phanTach.Length > 1 ? phanTach[1] : null);
        }

        // 🚨 KHÔNG nhận dạng được ⇒ NÉM LỖI. Đây là nhánh chống lỗi im lặng quan trọng nhất.
        throw new Volo.Abp.BusinessException(EInvoiceSubmissionErrorCodes.PhanHoiKhongNhanDangDuoc)
            .WithData("phanHoi", s);
    }

    /// <summary>
    /// Danh sách mã lỗi CQT mà ta **biết là có thể thử lại** (lỗi tạm thời: quá tải, timeout phía CQT).
    /// Mọi mã khác ⇒ **KHÔNG** tự động thử lại (phải có người xử lý), tránh gửi lặp vô ích.
    /// ⚠️ Danh sách này cần **đối chiếu với tài liệu CQT thật** — hiện là **đề xuất**, xem `EINV-002` Q.
    /// </summary>
    public static bool LaLoiTamThoi(string? maLoi)
        => maLoi is not null && (maLoi == "5" || maLoi.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase));
}

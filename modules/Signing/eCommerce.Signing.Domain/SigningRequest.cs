using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Signing;

/// <summary>
/// **Yêu cầu ký 2 bước** (`SIGN-001`) — mô hình hoá đúng quy trình **ký thủ công 2 bước**:
///
///   • **Bước 1** (`TaoYeuCau`): hệ thống chuẩn bị tài liệu và **tính hash**, ghi lại hash đó.
///   • **Bước 2** (`HoanTatKy`): người dùng ký **bên ngoài hệ thống** (USB token / HSM) rồi đưa kết quả
///     trở lại. Hệ thống **kiểm hash phải khớp** với hash đã ghi ở bước 1, và **kiểm hiệu lực chứng thư
///     tại đúng thời điểm ký**.
///
/// 🚨 VÌ SAO BẮT BUỘC KIỂM HASH: nếu bỏ qua, hệ thống sẽ **nhận chữ ký của tài liệu A và gắn vào tài
/// liệu B** (tệp bị thay giữa hai bước, hoặc người ký ký nhầm tệp). Chữ ký vẫn hợp lệ về mặt kỹ thuật
/// nhưng **vô hiệu về pháp lý**, và **không có cảnh báo nào**. Đây là lỗi im lặng tốn kém nhất của
/// quy trình ký thủ công.
///
/// 📌 GHI CHÚ PHẠM VI (trung thực): hàm này **KHÔNG** tự kiểm tra chữ ký bằng khoá công khai của chứng
/// thư — việc đó cần **chứng thư/khoá công khai thật** và còn là **câu hỏi mở Q6** của tài liệu
/// (bước 2 là **tệp hash đã ký** hay **XML đã ký**). Điều kiểm được **ngay bây giờ** và **bắt buộc**
/// là: **hash khớp** + **chứng thư còn hiệu lực tại thời điểm ký**.
/// </summary>
public class SigningRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã tài liệu cần ký (ví dụ mã/số hoá đơn) — để truy vết ký cho cái gì.</summary>
    public string MaTaiLieu { get; protected set; } = default!;

    /// <summary>Hash tài liệu đã chuẩn bị ở **bước 1**. Bất biến sau khi tạo.</summary>
    public string HashTaiLieu { get; protected set; } = default!;

    /// <summary>Thuật toán băm (mặc định `SHA256`).</summary>
    public string ThuatToanBam { get; protected set; } = "SHA256";

    public SigningRequestStatus TrangThai { get; protected set; }

    public Guid? ChungThuSoId { get; protected set; }

    /// <summary>Chữ ký nhận ở **bước 2** (base64).</summary>
    public string? ChuKyDaKy { get; protected set; }

    public DateTime? ThoiDiemKy { get; protected set; }

    /// <summary>Người thực hiện ký (truy vết trách nhiệm).</summary>
    public string? NguoiKy { get; protected set; }

    public string? LyDoHuy { get; protected set; }

    /// <summary>Độ dài hash SHA-256 dạng hex.</summary>
    public const int DoDaiHashSha256 = 64;

    protected SigningRequest()
    {
    }

    /// <summary>**BƯỚC 1** — chuẩn bị tài liệu và ghi lại hash để đối chiếu ở bước 2.</summary>
    public static SigningRequest TaoYeuCau(
        string maTaiLieu,
        string hashTaiLieu,
        string thuatToanBam = "SHA256",
        Guid? tenantId = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(maTaiLieu))
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc).WithData("truong", "maTaiLieu");
        }

        var hash = KiemHash(hashTaiLieu, thuatToanBam);

        return new SigningRequest
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            MaTaiLieu = maTaiLieu.Trim(),
            HashTaiLieu = hash,
            ThuatToanBam = thuatToanBam.Trim().ToUpperInvariant(),
            TrangThai = SigningRequestStatus.ChoKy
        };
    }

    private static string KiemHash(string? hash, string? thuatToan)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc).WithData("truong", "hashTaiLieu");
        }

        var h = hash.Trim();

        if (string.Equals(thuatToan, "SHA256", StringComparison.OrdinalIgnoreCase)
            && (h.Length != DoDaiHashSha256 || !h.All(char.IsAsciiHexDigit)))
        {
            throw new BusinessException(SigningErrorCodes.HashKhongHopLe)
                .WithData("hash", h)
                .WithData("doDai", h.Length)
                .WithData("doDaiYeuCau", DoDaiHashSha256);
        }

        return h;
    }

    private void YeuCauDangChoKy(string hanhDong)
    {
        if (TrangThai != SigningRequestStatus.ChoKy)
        {
            throw new BusinessException(
                    TrangThai == SigningRequestStatus.DaHuy
                        ? SigningErrorCodes.DaKetThuc
                        : SigningErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("hanhDong", hanhDong)
                .WithData("trangThaiHienTai", TrangThai.ToString());
        }
    }

    /// <summary>
    /// **BƯỚC 2** — nhận kết quả ký từ người dùng.
    /// </summary>
    /// <param name="hashDaKy">
    /// Hash của **tệp mà người dùng thực sự đã ký**. 🚨 Phải **khớp** <see cref="HashTaiLieu"/>.
    /// </param>
    /// <param name="chungThu">Chứng thư dùng để ký — phải **còn hiệu lực tại <paramref name="thoiDiemKy"/>**.</param>
    /// <param name="chuKy">Chữ ký nhận được (base64).</param>
    /// <param name="nguoiKy">Người thực hiện ký.</param>
    /// <param name="thoiDiemKy">Thời điểm ký (UTC) — dùng để kiểm hiệu lực chứng thư.</param>
    public void HoanTatKy(
        string hashDaKy,
        SigningCertificate chungThu,
        string chuKy,
        string nguoiKy,
        DateTime thoiDiemKy)
    {
        ArgumentNullException.ThrowIfNull(chungThu);

        YeuCauDangChoKy(nameof(HoanTatKy));

        // ① 🚨 HASH PHẢI KHỚP — chống "ký nhầm tài liệu" (xem ghi chú đầu class).
        var hash = KiemHash(hashDaKy, ThuatToanBam);

        if (!string.Equals(HashTaiLieu, hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(SigningErrorCodes.HashKhongKhop)
                .WithData("maTaiLieu", MaTaiLieu)
                .WithData("hashDaChuanBi", HashTaiLieu)
                .WithData("hashDaKy", hash);
        }

        // ② Chứng thư phải còn hiệu lực TẠI THỜI ĐIỂM KÝ (không phải lúc tạo yêu cầu).
        if (!chungThu.ConHieuLuc(thoiDiemKy))
        {
            throw new BusinessException(SigningErrorCodes.ChungThuKhongHopLe)
                .WithData("soSerial", chungThu.SoSerial)
                .WithData("trangThai", chungThu.TrangThai.ToString())
                .WithData("hieuLucTu", chungThu.HieuLucTu)
                .WithData("hieuLucDen", chungThu.HieuLucDen)
                .WithData("thoiDiemKy", thoiDiemKy);
        }

        if (string.IsNullOrWhiteSpace(chuKy))
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc).WithData("truong", "chuKy");
        }

        if (string.IsNullOrWhiteSpace(nguoiKy))
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc).WithData("truong", "nguoiKy");
        }

        TrangThai = SigningRequestStatus.DaKy;
        ChungThuSoId = chungThu.Id;
        ChuKyDaKy = chuKy.Trim();
        NguoiKy = nguoiKy.Trim();
        ThoiDiemKy = thoiDiemKy;
    }

    /// <summary>Huỷ yêu cầu ký (bắt buộc có lý do) — chỉ khi đang chờ ký.</summary>
    public void Huy(string lyDo)
    {
        YeuCauDangChoKy(nameof(Huy));

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(SigningErrorCodes.ThieuLyDo).WithData("maTaiLieu", MaTaiLieu);
        }

        TrangThai = SigningRequestStatus.DaHuy;
        LyDoHuy = lyDo.Trim();
    }

    public bool DaKyXong => TrangThai == SigningRequestStatus.DaKy;

    public override string ToString() => $"{MaTaiLieu} · {TrangThai} · {ThuatToanBam}:{HashTaiLieu[..8]}…";
}

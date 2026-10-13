using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Signing;

/// <summary>Trạng thái một **yêu cầu ký 2 bước**.</summary>
public enum SigningRequestStatus
{
    /// <summary>Bước 1 xong: đã chuẩn bị tài liệu + hash, **chờ người ký ký bên ngoài hệ thống**.</summary>
    ChoKy = 0,

    /// <summary>Bước 2 xong: đã nhận chữ ký **và hash khớp**.</summary>
    DaKy = 1,

    /// <summary>Đã huỷ (không ký nữa). Trạng thái KẾT THÚC.</summary>
    DaHuy = 2
}

/// <summary>Trạng thái chứng thư số.</summary>
public enum CertificateStatus
{
    HoatDong = 0,
    ThuHoi = 1
}

/// <summary>
/// **Chứng thư số** dùng để ký.
///
/// 🔒 BẤT BIẾN: hiệu lực của chứng thư phải kiểm **tại ĐÚNG thời điểm ký** — không phải tại thời điểm
/// tạo yêu cầu. Ký bằng chứng thư đã hết hạn/đã thu hồi ⇒ chữ ký **vô hiệu về pháp lý**, dù phần mềm
/// vẫn tạo ra được chuỗi chữ ký.
/// </summary>
public class SigningCertificate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Số serial của chứng thư (do nhà cung cấp CA cấp).</summary>
    public string SoSerial { get; protected set; } = default!;

    /// <summary>Chủ thể (tên tổ chức/cá nhân được cấp).</summary>
    public string ChuSoHuu { get; protected set; } = default!;

    /// <summary>Đơn vị phát hành (CA).</summary>
    public string DonViPhatHanh { get; protected set; } = default!;

    public DateTime HieuLucTu { get; protected set; }

    public DateTime HieuLucDen { get; protected set; }

    public CertificateStatus TrangThai { get; protected set; }

    protected SigningCertificate()
    {
    }

    public static SigningCertificate TaoMoi(
        string soSerial,
        string chuSoHuu,
        string donViPhatHanh,
        DateTime hieuLucTu,
        DateTime hieuLucDen,
        Guid? tenantId = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(soSerial) || string.IsNullOrWhiteSpace(chuSoHuu))
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", "soSerial/chuSoHuu");
        }

        if (hieuLucDen <= hieuLucTu)
        {
            throw new BusinessException(SigningErrorCodes.ThieuThongTinBatBuoc)
                .WithData("lyDo", "HieuLucDen phải sau HieuLucTu");
        }

        return new SigningCertificate
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SoSerial = soSerial.Trim(),
            ChuSoHuu = chuSoHuu.Trim(),
            DonViPhatHanh = donViPhatHanh?.Trim() ?? string.Empty,
            HieuLucTu = hieuLucTu,
            HieuLucDen = hieuLucDen,
            TrangThai = CertificateStatus.HoatDong
        };
    }

    /// <summary>Chứng thư còn hiệu lực **tại thời điểm</summary> cho trước** hay không.</summary>
    public bool ConHieuLuc(DateTime thoiDiem)
        => TrangThai == CertificateStatus.HoatDong
           && thoiDiem >= HieuLucTu
           && thoiDiem <= HieuLucDen;

    /// <summary>Thu hồi chứng thư. Không cho thu hồi hai lần (tránh ghi đè mốc thời gian).</summary>
    public void ThuHoi()
    {
        if (TrangThai == CertificateStatus.ThuHoi)
        {
            throw new BusinessException(SigningErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", TrangThai.ToString())
                .WithData("den", nameof(CertificateStatus.ThuHoi));
        }

        TrangThai = CertificateStatus.ThuHoi;
    }
}

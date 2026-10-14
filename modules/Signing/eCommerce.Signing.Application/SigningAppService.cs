using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;

namespace eCommerce.Signing.Application;

// ─────────────────────────── DTO ───────────────────────────

public sealed record SigningRequestDto(
    Guid Id,
    string MaTaiLieu,
    string HashTaiLieu,
    string ThuatToanBam,
    SigningRequestStatus TrangThai,
    string? NguoiKy,
    DateTime? ThoiDiemKy)
{
    public static SigningRequestDto Tu(SigningRequest r)
        => new(r.Id, r.MaTaiLieu, r.HashTaiLieu, r.ThuatToanBam, r.TrangThai, r.NguoiKy, r.ThoiDiemKy);
}

public sealed record SigningCertificateDto(
    Guid Id,
    string SoSerial,
    string ChuSoHuu,
    string DonViPhatHanh,
    DateTime HieuLucTu,
    DateTime HieuLucDen,
    CertificateStatus TrangThai);

/// <summary>**BƯỚC 1** — yêu cầu ký: hệ thống đã chuẩn bị tài liệu và ghi lại hash.</summary>
public sealed record TaoYeuCauKyRequest(string MaTaiLieu, string HashTaiLieu, string? ThuatToanBam);

/// <summary>**BƯỚC 2** — kết quả ký do người dùng đưa vào (ký bên ngoài hệ thống).</summary>
public sealed record HoanTatKyRequest(
    string HashDaKy,
    Guid ChungThuSoId,
    string ChuKy,
    string NguoiKy,
    DateTime ThoiDiemKy);

public sealed record TaoChungThuRequest(
    string SoSerial,
    string ChuSoHuu,
    string DonViPhatHanh,
    DateTime HieuLucTu,
    DateTime HieuLucDen);

// ─────────────────────────── Module ABP ───────────────────────────

/// <summary>
/// Module tầng Application của `Signing`.
/// 🚨 BẮT BUỘC có class này VÀ phải khai trong `[DependsOn]` của Host (bài học C8/C16).
/// </summary>
[DependsOn(
    typeof(SigningDomainModule),
    typeof(AbpDddApplicationModule)
)]
public class SigningApplicationModule : AbpModule
{
}

// ─────────────────────────── App service ───────────────────────────

/// <summary>
/// API **ký 2 bước thủ công** (`SIGN-001`). `[Authorize]` ở CẤP CLASS (quy ước #6).
/// 🔒 Hai bất biến được cưỡng chế ở tầng Domain (đã có test): **hash phải khớp** và **chứng thư phải
/// còn hiệu lực tại đúng thời điểm ký**. App service chỉ chuyển tiếp — không nới lỏng gì.
/// </summary>
[Authorize]
public class SigningAppService : ApplicationService
{
    private readonly IRepository<SigningRequest, Guid> _yeuCauKy;
    private readonly IRepository<SigningCertificate, Guid> _chungThu;

    public SigningAppService(
        IRepository<SigningRequest, Guid> yeuCauKy,
        IRepository<SigningCertificate, Guid> chungThu)
    {
        _yeuCauKy = yeuCauKy;
        _chungThu = chungThu;
    }

    /// <summary>**BƯỚC 1** — tạo yêu cầu ký và ghi lại hash tài liệu.</summary>
    public async Task<SigningRequestDto> TaoYeuCauKyAsync(TaoYeuCauKyRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var yeuCauKy = SigningRequest.TaoYeuCau(
            yeuCau.MaTaiLieu,
            yeuCau.HashTaiLieu,
            string.IsNullOrWhiteSpace(yeuCau.ThuatToanBam) ? "SHA256" : yeuCau.ThuatToanBam);

        await _yeuCauKy.InsertAsync(yeuCauKy, autoSave: true);

        return SigningRequestDto.Tu(yeuCauKy);
    }

    /// <summary>
    /// **BƯỚC 2** — nhận kết quả ký.
    /// 🚨 Ném `eCommerce:Sign:0002` nếu hash không khớp (ký nhầm tài liệu) và `0003` nếu chứng thư
    /// không còn hiệu lực **tại thời điểm ký**.
    /// </summary>
    public async Task<SigningRequestDto> HoanTatKyAsync(Guid id, HoanTatKyRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var yeuCauKy = await _yeuCauKy.GetAsync(id);
        var chungThu = await _chungThu.GetAsync(yeuCau.ChungThuSoId);

        yeuCauKy.HoanTatKy(
            yeuCau.HashDaKy, chungThu, yeuCau.ChuKy, yeuCau.NguoiKy, yeuCau.ThoiDiemKy);

        await _yeuCauKy.UpdateAsync(yeuCauKy, autoSave: true);

        return SigningRequestDto.Tu(yeuCauKy);
    }

    public async Task<SigningRequestDto> HuyYeuCauKyAsync(Guid id, string lyDo)
    {
        var yeuCauKy = await _yeuCauKy.GetAsync(id);
        yeuCauKy.Huy(lyDo);
        await _yeuCauKy.UpdateAsync(yeuCauKy, autoSave: true);

        return SigningRequestDto.Tu(yeuCauKy);
    }

    public async Task<SigningRequestDto> LayYeuCauKyAsync(Guid id)
        => SigningRequestDto.Tu(await _yeuCauKy.GetAsync(id));

    /// <summary>Danh sách chứng thư (để chọn khi ký; chỉ hiển thị cho người dùng biết cái nào còn hạn).</summary>
    public async Task<IReadOnlyList<SigningCertificateDto>> LayChungThuAsync()
    {
        var ds = await _chungThu.GetListAsync();

        return ds.OrderBy(x => x.SoSerial, StringComparer.Ordinal)
            .Select(c => new SigningCertificateDto(
                c.Id, c.SoSerial, c.ChuSoHuu, c.DonViPhatHanh, c.HieuLucTu, c.HieuLucDen, c.TrangThai))
            .ToList();
    }

    public async Task<SigningCertificateDto> ThemChungThuAsync(TaoChungThuRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var cert = SigningCertificate.TaoMoi(
            yeuCau.SoSerial, yeuCau.ChuSoHuu, yeuCau.DonViPhatHanh, yeuCau.HieuLucTu, yeuCau.HieuLucDen);

        await _chungThu.InsertAsync(cert, autoSave: true);

        return new SigningCertificateDto(
            cert.Id, cert.SoSerial, cert.ChuSoHuu, cert.DonViPhatHanh,
            cert.HieuLucTu, cert.HieuLucDen, cert.TrangThai);
    }

    public async Task ThuHoiChungThuAsync(Guid id)
    {
        var cert = await _chungThu.GetAsync(id);
        cert.ThuHoi();
        await _chungThu.UpdateAsync(cert, autoSave: true);
    }
}

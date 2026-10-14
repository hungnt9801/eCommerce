using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Address;

/// <summary>
/// **API tra cứu &amp; xác minh địa chỉ** (tầng Application của `ADDR-001`).
///
/// 🔒 QUY ƯỚC BẮT BUỘC: `[Authorize]` ở **CẤP CLASS** (quy ước #6, đã bị test reflection `TC-01` của
/// `XC-004` chặn nếu thiếu).
///
/// 🎯 VÌ SAO ĐÁNG LÀM ĐẦU TIÊN TRONG TẦNG APPLICATION: đây là **điểm nghẽn dữ liệu đầu vào** — mọi đơn
/// hàng, vận đơn và hoá đơn đều đi qua việc xác minh địa chỉ. Nếu tầng này trả `null`/mặc định khi
/// không tìm thấy thì **địa chỉ sai sẽ lan ra toàn hệ thống**. Vì vậy mọi nhánh đều **fail-closed** và
/// **không bao giờ** trả về đơn vị "gần đúng".
/// </summary>
[Authorize]
public class AdministrativeUnitAppService : ApplicationService
{
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;

    public AdministrativeUnitAppService(IRepository<AdministrativeUnit, Guid> donVi)
    {
        _donVi = donVi;
    }

    /// <summary>Tra một đơn vị theo (phiên bản, cấp, mã). Ném lỗi nếu không tìm thấy.</summary>
    public async Task<AdministrativeUnitDto> TraDonViAsync(TraDonViRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var tatCa = await _donVi.GetListAsync();

        // Dùng lại ĐÚNG logic đã kiểm chứng ở tầng Domain (kèm phiên bản ⇒ không trả sai đơn vị).
        var donVi = AdministrativeUnitResolver.TraTheoMa(
            tatCa, yeuCau.SchemaVersion, yeuCau.Level, yeuCau.Code);

        return Chuyen(donVi);
    }

    /// <summary>
    /// Xác minh địa chỉ 2 cấp cho đơn **MỚI**.
    /// 🔒 **FAIL-CLOSED**: mọi nhánh không chắc chắn đều trả `HopLe = false` (không ném lỗi, để giao diện
    /// hiển thị lý do cho người dùng).
    /// </summary>
    public async Task<XacMinhDiaChiResult> XacMinhDiaChiAsync(XacMinhDiaChiRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var tatCa = await _donVi.GetListAsync();

        var kq = AdministrativeUnitResolver.KiemTraHaiCap(
            tatCa, yeuCau.SchemaVersion, yeuCau.TinhCode, yeuCau.XaCode);

        return new XacMinhDiaChiResult(
            kq.HopLe,
            kq.ErrorCode,
            kq.ThongDiep,
            kq.Tinh is null ? null : Chuyen(kq.Tinh),
            kq.Xa is null ? null : Chuyen(kq.Xa));
    }

    /// <summary>Lấy danh sách xã/phường của một tỉnh (cho dropdown chọn địa chỉ).</summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> LayXaTheoTinhAsync(int schemaVersion, string tinhCode)
    {
        AdministrativeSchemaVersion.TheoSo(schemaVersion);
        var maTinh = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Tinh, tinhCode);

        var ds = await _donVi.GetListAsync(x =>
            x.SchemaVersion == schemaVersion
            && x.Level == AdministrativeLevel.Xa
            && x.ParentCode == maTinh
            && x.IsActive);

        return ds.OrderBy(x => x.Code, StringComparer.Ordinal).Select(Chuyen).ToList();
    }

    /// <summary>
    /// 🚩 **Soi bẫy mã trùng giữa hai phiên bản** — dùng cho báo cáo kiểm tra dữ liệu.
    /// Trả về đơn vị xã tương ứng ở **cả hai** phiên bản, để thấy rõ chúng có thể **KHÁC NHAU**.
    /// </summary>
    public async Task<IReadOnlyList<MaXaHaiPhienBanDto>> SoSanhMaXaAsync(string maXa)
    {
        var tatCa = await _donVi.GetListAsync();

        var soSanh = AdministrativeUnitResolver.SoSanhMaXaGiuaCacPhienBan(tatCa, maXa);

        return soSanh
            .OrderBy(kv => kv.Key)
            .Select(kv => new MaXaHaiPhienBanDto(kv.Key, kv.Value is null ? null : Chuyen(kv.Value)))
            .ToList();
    }

    private static AdministrativeUnitDto Chuyen(AdministrativeUnit u)
        => new(u.Id, u.SchemaVersion, u.Level, u.Code, u.Name, u.ParentCode, u.IsActive);
}

using eCommerce.Address.Events;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Address;

/// <summary>
/// **API miền địa chỉ** (`ADDR-004` §6.1) — tạo/đọc/chuẩn hoá/xác minh `Address`.
///
/// 🔒 `[Authorize]` ở **CẤP CLASS** + permission cụ thể ở từng method (quy ước #6). 🚨 Không lặp lại lỗi
/// `F8` của `AdministrativeUnitAppService` (`[Authorize]` trần ⇒ **mọi** user đã đăng nhập gọi được).
///
/// ⚠️ **TÊN METHOD QUYẾT ĐỊNH HTTP VERB:** ABP chỉ sinh `GET` cho method có tên bắt đầu bằng `Get…`.
/// 📌 Đây đúng là bài học `CUS-001` §16.2 (`TimTheoIdAsync` bị sinh thành **`POST`** — sai ngữ nghĩa HTTP).
/// ⇒ `GetAsync` / `GetHangDoiChuanHoaAsync` **cố ý** đặt tên như vậy; route thật phải in bằng
/// `tools/route-check.ps1` trước khi tin.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Addresses.Default)]
public class AddressAppService : ApplicationService, IAddressAppService
{
    private readonly AddressDirectory _directory;
    private readonly IRepository<Address, Guid> _diaChiRepository;
    private readonly IRepository<AdministrativeUnit, Guid> _donViRepository;
    private readonly IDistributedEventBus _eventBus;

    public AddressAppService(
        AddressDirectory directory,
        IRepository<Address, Guid> diaChiRepository,
        IRepository<AdministrativeUnit, Guid> donViRepository,
        IDistributedEventBus eventBus)
    {
        _directory = directory;
        _diaChiRepository = diaChiRepository;
        _donViRepository = donViRepository;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Tạo **hoặc** lấy `Address` theo nội dung (`ADR-04`) — **idempotent**: gửi lại **không** tạo bản thứ hai.
    /// 🔑 Phát `AddressChangedEto` **chỉ khi** vừa tạo thật (không phát lại mỗi lần gọi).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.Manage)]
    public async Task<DiaChiDto> TaoHoacLayDiaChiAsync(TaoDiaChiInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var (snapshot, vuaTao) = await _directory.TaoHoacLayVaBietAsync(input);

        var thucThe = await _diaChiRepository.FindAsync(snapshot.AddressId);

        if (vuaTao && thucThe is not null)
        {
            await PhatSuKienThayDoiAsync(thucThe);
        }

        return ChuyenDto(snapshot, thucThe);
    }

    /// <summary>
    /// Đọc một địa chỉ. ⚠️ Tên `Get…` ⇒ ABP sinh **`GET`** (xem chú thích ở đầu lớp).
    /// 🔒 Không tìm thấy / khác tenant ⇒ **`0101`** (`404`) — **không** tiết lộ sự tồn tại (`XC-004` BR-008).
    /// </summary>
    public async Task<DiaChiDto> GetAsync(Guid addressId)
    {
        var snapshot = await _directory.TimTheoIdAsync(addressId)
            ?? throw new BusinessException(AddressErrorCodes.KhongTimThayDiaChi)
                .WithData("addressId", addressId);

        var thucThe = await _diaChiRepository.FindAsync(addressId);

        return ChuyenDto(snapshot, thucThe);
    }

    /// <summary>
    /// Chuẩn hoá địa chỉ **thô** ⇒ `DaChuanHoa`. 🔒 Chỉ chạy **MỘT lần** (`ADR-05`): gọi lần hai ⇒ `0102`.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.Manage)]
    public async Task<DiaChiDto> ChuanHoaLaiAsync(Guid addressId, ChuanHoaInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var diaChi = await _diaChiRepository.FindAsync(addressId)
            ?? throw new BusinessException(AddressErrorCodes.KhongTimThayDiaChi).WithData("addressId", addressId);

        var (tinh, huyen, xa, diaChiDayDu) = await KiemVaDungChuAsync(
            input.SchemaVersion, input.TinhCode, input.HuyenCode, input.XaCode, input.SoNhaDuong);

        // 🚨 Guard `0102` nằm TRONG entity (`ChuanHoa`) — không nuốt lỗi ở đây (quy ước #7).
        diaChi.ChuanHoa(
            AdministrativeSchemaVersion.TheoSo(input.SchemaVersion),
            tinh, huyen, xa, input.SoNhaDuong, diaChiDayDu, DateTime.UtcNow);

        await _diaChiRepository.UpdateAsync(diaChi, autoSave: true);

        await PhatSuKienThayDoiAsync(diaChi);

        return ChuyenDto(await _directory.TimTheoIdAsync(addressId)!, diaChi);
    }

    /// <summary>
    /// **Xác minh** địa chỉ — 🔒 **bắt buộc căn cứ** (`0106`): *"vì sao tin địa chỉ này đúng"* là một phần
    /// của bằng chứng, không phải tuỳ chọn. Quyền **riêng** (`Verify`): người xác minh ≠ người nhập.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.Verify)]
    public async Task<DiaChiDto> DanhDauDaXacMinhAsync(Guid addressId, string canCu)
    {
        var diaChi = await _diaChiRepository.FindAsync(addressId)
            ?? throw new BusinessException(AddressErrorCodes.KhongTimThayDiaChi).WithData("addressId", addressId);

        // Guard `0106` nằm trong entity (`XacMinh`) — không kiểm lại ở đây để khỏi **hai nguồn sự thật**.
        diaChi.XacMinh(canCu, DateTime.UtcNow);

        await _diaChiRepository.UpdateAsync(diaChi, autoSave: true);

        await PhatSuKienThayDoiAsync(diaChi);

        return ChuyenDto(await _directory.TimTheoIdAsync(addressId)!, diaChi);
    }

    /// <summary>Hàng đợi vận hành: địa chỉ **thô** + **nghi ngờ**, cũ nhất trước. ⚠️ Tên `Get…` ⇒ **`GET`**.</summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.Manage)]
    public async Task<List<DiaChiChoDuyetDto>> GetHangDoiChuanHoaAsync(int toiDa = 50)
    {
        var tran = Math.Clamp(toiDa, 1, 200);

        var truyVan = await _diaChiRepository.GetQueryableAsync();

        // ⚠️ Lấy ENTITY rồi map trong bộ nhớ: `TrangThai.ToString()` **không** dịch được sang SQL
        // (nếu để trong `Select` thì EF ném lúc chạy, không phải lúc build).
        var dong = await AsyncExecuter.ToListAsync(
            truyVan
                .Where(x => x.TrangThai == TrangThaiDiaChi.ChuaChuanHoa || x.TrangThai == TrangThaiDiaChi.NghiNgo)
                .OrderBy(x => x.CreationTime)
                .Take(tran));

        return dong
            .Select(x => new DiaChiChoDuyetDto(
                x.Id, x.TrangThai.ToString(), x.SchemaVersion, x.DiaChiDayDu, x.CreationTime))
            .ToList();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // NỘI BỘ
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task<(string tinh, string? huyen, string xa, string diaChiDayDu)> KiemVaDungChuAsync(
        int schemaVersion, string? tinhCode, string? huyenCode, string? xaCode, string? soNhaDuong)
    {
        if (string.IsNullOrWhiteSpace(tinhCode) || string.IsNullOrWhiteSpace(xaCode))
        {
            throw new BusinessException(AddressErrorCodes.ThieuNoiDung)
                .WithData("lyDo", "Chuẩn hoá cần cả `tinhCode` và `xaCode`.");
        }

        var ma = new[] { tinhCode, xaCode, huyenCode }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var donVi = await _donViRepository.GetListAsync(x => ma.Contains(x.Code));

        var ketQua = schemaVersion == 1
            ? AdministrativeUnitResolver.KiemTraBaCap(donVi, 1, tinhCode, huyenCode!, xaCode)
            : AdministrativeUnitResolver.KiemTraHaiCap(donVi, schemaVersion, tinhCode, xaCode);

        if (!ketQua.HopLe)
        {
            throw new BusinessException(ketQua.ErrorCode!)
                .WithData("tinhCode", tinhCode)
                .WithData("xaCode", xaCode)
                .WithData("lyDo", ketQua.ThongDiep);
        }

        return (
            tinhCode.Trim(),
            string.IsNullOrWhiteSpace(huyenCode) ? null : huyenCode.Trim(),
            xaCode.Trim(),
            AddressDirectory.DungDiaChiDayDu(soNhaDuong, ketQua.Xa?.Name, ketQua.Huyen?.Name, ketQua.Tinh?.Name));
    }

    /// <summary>
    /// Phát `AddressChangedEto` — 🚨 payload **KHÔNG** chứa chữ địa chỉ (`XC-002` **D11**): consumer cần nội
    /// dung thì gọi `IAddressDirectory` (một nguồn sự thật).
    /// 📌 Phát trong **cùng UoW** với thay đổi ⇒ hiện tại là event bus **in-process** (chưa có broker) —
    /// chính là nợ `XC-002` đã ghi ở `CAT-001` **F8**; khi có broker thì bus đã hỗ trợ outbox.
    /// </summary>
    private async Task PhatSuKienThayDoiAsync(Address diaChi)
    {
        await _eventBus.PublishAsync(new AddressChangedEto
        {
            TenantId = CurrentTenant.Id,
            AddressId = diaChi.Id,
            AggregateId = diaChi.Id,
            AggregateVersion = diaChi.ConcurrencyStamp,
            TrangThai = diaChi.TrangThai.ToString(),
            SchemaVersion = diaChi.SchemaVersion,
            TinhCode = diaChi.TinhCode,
            HuyenCode = diaChi.HuyenCode,
            XaCode = diaChi.XaCode,
            CorrelationId = LấyCorrelationId()
        });
    }

    private string? LấyCorrelationId()
        => System.Diagnostics.Activity.Current?.Id;

    /// <summary>
    /// Ghép DTO đầy đủ từ **snapshot** (đã có tên ĐVHC) + **entity** (các trường kỹ thuật).
    /// 📌 Tách khỏi snapshot để **một chỗ** quyết định trường nào ra API — thêm trường là thêm **một** chỗ.
    /// </summary>
    private static DiaChiDto ChuyenDto(DiaChiSnapshotDto s, Address? thucThe) => new()
    {
        AddressId = s.AddressId,
        TrangThai = s.TrangThai,
        SchemaVersion = s.SchemaVersion,
        TinhCode = s.TinhCode,
        TenTinh = s.TenTinh,
        HuyenCode = s.HuyenCode,
        TenHuyen = s.TenHuyen,
        XaCode = s.XaCode,
        TenXa = s.TenXa,
        SoNhaDuong = s.SoNhaDuong,
        DiaChiDayDu = s.DiaChiDayDu,
        ContentHash = thucThe?.ContentHash ?? "(không đọc được)",
        NguonTao = thucThe?.NguonTao.ToString() ?? "(không đọc được)",
        NormalizedAtUtc = s.NormalizedAtUtc,
        XacMinhLucUtc = thucThe?.XacMinhLucUtc,
        CreationTime = thucThe?.CreationTime ?? default,
        CoDungDuoc = s.CoDungDuoc
    };
}

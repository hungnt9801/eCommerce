using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// **HÀM THUẦN** quyết định bảng giá nào phải bị **thay thế** khi kích hoạt một bảng giá mới
/// (`PRC-001` **BR-007**). Hàm thuần ⇒ test được **không cần DB**.
///
/// 🚨 **BẤT BIẾN `BR-007`: tại một thời điểm, mỗi `(TenantId, Scope, ChannelCode)` chỉ có MỘT bảng giá `Active`.**
/// 📌 **Tài liệu `PRC-001` D8 ghi rõ bất biến này KHÔNG có hàng rào DB** (khác `TaxClassification` — nơi có
/// `EXCLUDE USING gist`). Lý do: `EXCLUDE` chỉ chặn được chồng khung **trong cùng `Code`**; hai `Code` khác nhau
/// cùng phạm vi vẫn chồng ⇒ **cảm giác an toàn giả**. Vì vậy phải khoá + kiểm **trong transaction**
/// (xem <see cref="PriceListActivationService"/>).
///
/// **Nếu bỏ kiểm này:** hai bảng giá cùng `Active` cho cùng kênh ⇒ tầng tra giá nhận **hai ứng viên cùng cấp,
/// cùng `Priority`** ⇒ ném `Pricing:0001` ⇒ **checkout CHẾT** cho mọi SKU của kênh đó. Tệ hơn: nếu `Priority`
/// khác nhau thì hệ thống **âm thầm chọn một cái** mà không ai biết cái còn lại cũng đang "hiệu lực".
/// </summary>
public static class QuyetDinhKichHoatBangGia
{
    /// <summary>Một bảng giá đang `Active` (đầu vào của quyết định).</summary>
    public sealed record BangGiaDangHieuLuc(Guid Id, PriceListScope Scope, string? ChannelCode, int Priority);

    /// <summary>
    /// Trả về **danh sách `Id` cần bị thay thế** (chuyển `Active → Superseded`).
    /// ⚠️ **KHÔNG** bao giờ trả về <paramref name="sapKichHoat"/> (idempotent nếu nó đang active).
    /// </summary>
    public static IReadOnlyList<Guid> CanThayThe(
        Guid sapKichHoat,
        PriceListScope scopeMoi,
        string? channelMoi,
        IEnumerable<BangGiaDangHieuLuc> dangHieuLuc)
    {
        ArgumentNullException.ThrowIfNull(dangHieuLuc);

        var kenhMoi = ChuanHoaKenh(channelMoi);

        return dangHieuLuc
            .Where(x => x.Id != sapKichHoat)
            .Where(x => x.Scope == scopeMoi)
            // So kênh đã CHUẨN HOÁ: `null` và `""` phải được coi là **cùng** phạm vi, nếu không thì
            // `Scope=All` với `ChannelCode = null` và `Scope=All` với `ChannelCode = ""` sẽ **cùng Active**
            // ⇒ đúng cái mà BR-007 cấm, mà **không có lỗi nào**.
            .Where(x => ChuanHoaKenh(x.ChannelCode) == kenhMoi)
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>`null`/rỗng/khoảng trắng ⇒ **một giá trị chuẩn duy nhất** (`null`) để so sánh an toàn.
    /// ⚠️ Không dùng `Trim()` trần: `" "` và `null` phải là **cùng** phạm vi.</summary>
    private static string? ChuanHoaKenh(string? channelCode)
        => string.IsNullOrWhiteSpace(channelCode) ? null : channelCode.Trim().ToLowerInvariant();
}

/// <summary>
/// Dịch vụ miền **kích hoạt bảng giá** — nơi **DUY NHẤT** cưỡng chế `BR-007`.
///
/// 🚨 **TRÌNH TỰ BẮT BUỘC:**
///   1. **Giành khoá phân tán theo PHẠM VI** (`pricing:pricelist:{tenant}:{scope}:{kênh}`, ADR-12: **không**
///      tiền tố `ecommerce:`). Khoá theo **phạm vi** (không theo toàn tenant) để hai kênh khác nhau
///      vẫn kích hoạt **song song** — chỉ hai bảng giá **cùng phạm vi** mới phải tuần tự.
///   2. Đọc các bảng giá **đang `Active`** cùng phạm vi **SAU KHI** đã có khoá.
///   3. **Thay thế** chúng (`Active → Superseded` — **KHÔNG** xoá: append-only, `BR-004`) rồi kích hoạt cái mới,
///      **trong cùng một transaction**.
///
/// 📌 **Vì sao "thay thế" chứ không "từ chối":** bảng giá cũ **đã từng** là nguồn sự thật của những đơn đã đặt
/// ⇒ phải **giữ lại** để tra cứu "vì sao đơn hôm đó giá đó" (`BR-009`). `Superseded` là trạng thái đúng
/// cho việc đó, và `HetHanNeuQuaNgay`/`CoHieuLucTai` đã loại nó khỏi mọi truy vấn giá.
/// </summary>
public class PriceListActivationService : DomainService
{
    /// <summary>TTL khoá (giây). Kích hoạt bảng giá là thao tác **ghi thưa** ⇒ 30 s là dư.</summary>
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<PriceList, Guid> _bangGiaRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public PriceListActivationService(
        IRepository<PriceList, Guid> bangGiaRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _bangGiaRepository = bangGiaRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Kích hoạt bảng giá và **thay thế** bảng giá đang hiệu lực cùng phạm vi.
    /// Trả về **(đã kích hoạt?, số bảng giá bị thay thế)**.
    /// ⚠️ Chưa tới ngày ⇒ **`false`** và **không** thay thế gì (`PRC-002` **ADR-11**: "chưa tới ngày"
    /// **không phải** vi phạm guard).
    /// </summary>
    public async Task<(bool DaKichHoat, int SoBiThayThe)> KichHoatAsync(Guid priceListId, DateTime at)
    {
        var bangGia = await _bangGiaRepository.FindAsync(priceListId)
            ?? throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Không tìm thấy bảng giá cần kích hoạt.")
                .WithData("priceListId", priceListId);

        var tenKhoa = PricingLockKeys.BangGiaTheoPhamVi(_currentTenant.Id, bangGia.Scope.ToString(), bangGia.ChannelCode);

        await using var khoa = await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerce.Kernel.Locking.LockErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa)
                .WithData("choGiay", ChoKhoaToiDaGiay);

        // ⚠️ Nạp LẠI **sau khi** có khoá (dữ liệu có thể đã đổi giữa 2 lần đọc).
        var dangHieuLuc = (await _bangGiaRepository.GetListAsync(x => x.Status == PriceListStatus.Active))
            .Select(x => new QuyetDinhKichHoatBangGia.BangGiaDangHieuLuc(x.Id, x.Scope, x.ChannelCode, x.Priority))
            .ToList();

        bangGia = await _bangGiaRepository.GetAsync(priceListId);

        // ADR-11: chưa tới ngày ⇒ trả `false`, KHÔNG ném, KHÔNG thay thế gì.
        if (!bangGia.KichHoatNeuToiNgay(at))
        {
            return (false, 0);
        }

        var canThayThe = QuyetDinhKichHoatBangGia.CanThayThe(
            bangGia.Id, bangGia.Scope, bangGia.ChannelCode, dangHieuLuc);

        foreach (var idCu in canThayThe)
        {
            var cu = await _bangGiaRepository.FindAsync(idCu);

            if (cu is null || cu.Status != PriceListStatus.Active)
            {
                continue;   // ai đó vừa đổi — bỏ qua an toàn
            }

            cu.BiThayThe();
            await _bangGiaRepository.UpdateAsync(cu, autoSave: false);
        }

        await _bangGiaRepository.UpdateAsync(bangGia, autoSave: true);

        Logger.LogInformation(
            "Đã KÍCH HOẠT bảng giá {Code} ({Scope}/{Kenh}) tại {At} — thay thế {SoCu} bảng giá cùng phạm vi.",
            bangGia.Code, bangGia.Scope, bangGia.ChannelCode ?? "(mọi kênh)", at, canThayThe.Count);

        return (true, canThayThe.Count);
    }
}

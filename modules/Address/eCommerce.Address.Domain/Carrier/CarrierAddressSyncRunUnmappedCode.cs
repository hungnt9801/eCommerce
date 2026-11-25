using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address.Carrier;

/// <summary>
/// **MỘT MÃ ĐỊA CHỈ HÃNG CHƯA ĐƯỢC ÁNH XẠ của một lượt đồng bộ** (`ADDR-002` §4.2, `D-88`) — APPEND-ONLY.
///
/// 🎯 VÌ SAO CÓ BẢNG NÀY: danh mục hãng **chỉ sống trong bộ nhớ worker một lượt**
/// (`CarrierAddressSyncWorker`) — `PhatHienSaiLechAsync` đòi caller tự truyền danh mục vào, nên **không**
/// thể là nguồn "danh sách đầy đủ mã chưa ánh xạ". Bảng con lưu **đủ** cặp (cấp, mã, tên) của mọi việc
/// `CanNguoiAnhXa` của lượt; mẫu tối đa 20 mã chỉ nằm ở dòng lô (`MauCanNguoiAnhXa`, `D-234`).
///
/// 🔒 BẤT BIẾN:
///   • **0** public setter, **0** method đổi, **0** `UPDATE`/`DELETE` — dòng con là **sự kiện bất biến**
///     ("lượt R thấy mã X chưa có ánh xạ"), **không** bao giờ đóng/mở;
///   • **không** FK tới `AddrCarrierMappings` (mã đã ánh xạ chưa chỉ ở bảng ánh xạ, tại lúc đọc);
///   • UNIQUE `(TenantId, SyncRunId, CarrierAddressCode)` `NULLS NOT DISTINCT` là hàng rào DB cho
///     "đúng 1 dòng con mỗi việc" (`D-88`, NFR-003).
/// </summary>
public class CarrierAddressSyncRunUnmappedCode : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Lượt nguồn — FK trong **cùng module** (`onDelete: Restrict`, sổ bất biến).</summary>
    public Guid SyncRunId { get; private set; }

    public string CarrierCode { get; private set; } = default!;

    /// <summary>Cấp phía hãng (chuỗi, `Trim().ToUpperInvariant()` — ngoài BR-013).</summary>
    public string CarrierLevel { get; private set; } = default!;

    /// <summary>Mã hãng **đã `Trim()`**, so `Ordinal` (`D-84`); giữ hoa–thường/số 0 đầu/khoảng trắng giữa.</summary>
    public string CarrierAddressCode { get; private set; } = default!;

    public string CarrierAddressName { get; private set; } = default!;

    protected CarrierAddressSyncRunUnmappedCode()
    {
    }

    public static CarrierAddressSyncRunUnmappedCode Ghi(
        Guid syncRunId,
        string carrierCode,
        string? carrierLevel,
        string carrierAddressCode,
        string? carrierAddressName,
        Guid? tenantId = null,
        Guid? id = null)
    {
        if (syncRunId == Guid.Empty)
        {
            throw new ArgumentException("SyncRunId không được rỗng.", nameof(syncRunId));
        }

        return new CarrierAddressSyncRunUnmappedCode
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SyncRunId = syncRunId,
            CarrierCode = ChuanHoaMaCuaTa(carrierCode, nameof(carrierCode)),
            CarrierLevel = ChuanHoaMaCuaTa(carrierLevel, nameof(carrierLevel)),
            // 🔒 Mã hãng: CHỈ `Trim()` — không đổi hoa–thường/số 0 đầu/khoảng trắng giữa (BR-013, `D-84`).
            CarrierAddressCode = ChuanHoaNgan(
                carrierAddressCode, nameof(carrierAddressCode), maxLength: 64),
            // 📌 Tên `null` vẫn hợp lệ ở cửa đồng bộ (`D-91`) ⇒ lưu chuỗi rỗng, KHÔNG ném.
            CarrierAddressName = ChuanHoaNgan(carrierAddressName, nameof(carrierAddressName), maxLength: 256)
        };
    }

    /// <summary>Mã/cấp của **ta**: `Trim().ToUpperInvariant()` — ngoài BR-013.</summary>
    private static string ChuanHoaMaCuaTa(string? giaTri, string tenThamSo)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
        {
            return string.Empty;
        }

        var sach = giaTri.Trim().ToUpperInvariant();

        return sach.Length > 32
            ? throw new ArgumentException("Giá trị vượt độ dài tối đa 32 ký tự.", tenThamSo)
            : sach;
    }

    private static string ChuanHoaNgan(string? giaTri, string tenThamSo, int maxLength)
    {
        var sach = giaTri?.Trim() ?? string.Empty;

        return sach.Length > maxLength
            ? throw new ArgumentException($"Giá trị vượt độ dài tối đa {maxLength} ký tự.", tenThamSo)
            : sach;
    }
}

using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address.Carrier;

/// <summary>Mã lỗi của miền **mã địa chỉ theo hãng vận chuyển** (`ADDR-002`) — dải `eCommerce:Address:05xx`.</summary>
public static class CarrierAddressErrorCodes
{
    /// <summary>Không tìm thấy mã địa chỉ của hãng vận chuyển.</summary>
    public const string KhongTimThayMaHang = "eCommerce:Address:0501";

    /// <summary>
    /// 🚨 Ánh xạ **CHƯA được xác minh** ⇒ **KHÔNG** được dùng để tạo vận đơn.
    /// Dùng nó nghĩa là gửi hàng tới địa chỉ do **suy đoán** (khớp tên gần đúng) — sai địa chỉ
    /// mà **không có cảnh báo nào**, và hàng đã đi rồi thì không sửa được.
    /// </summary>
    public const string MaChuaXacMinh = "eCommerce:Address:0502";

    /// <summary>Hai ánh xạ cùng (hãng, mã hãng) — vi phạm bất biến "một mã hãng chỉ ánh xạ một lần".</summary>
    public const string AnhXaTrungLap = "eCommerce:Address:0503";

    /// <summary>Đơn vị hành chính đích đã bị đóng (sáp nhập) ⇒ không dùng cho vận đơn MỚI.</summary>
    public const string DonViDichDaDong = "eCommerce:Address:0504";

    /// <summary>Thiếu thông tin bắt buộc (mã hãng, mã địa chỉ hãng, đơn vị đích…).</summary>
    public const string ThieuThongTinBatBuoc = "eCommerce:Address:0505";
    // ── Đồng bộ danh mục mã hãng (ADDR-002) — dải 05xx vì cùng miền nghiệp vụ ──

    /// <summary>Danh mục hãng RỖNG (hoặc không khớp mã hãng) — từ chối.</summary>
    public const string DanhMucHangRong = "eCommerce:Address:0506";

    /// <summary>Danh mục hãng quá ÍT đơn vị — nghi hãng trả về một phần.</summary>
    public const string DanhMucHangQuaIt = "eCommerce:Address:0507";

    /// <summary>
    /// Số ánh xạ bị ĐÁNH DẤU NGHI NGỜ vượt ngưỡng — từ chối.
    /// Đánh dấu nghi ngờ làm ánh xạ **không dùng được** ⇒ nếu áp dụng hàng loạt thì **mọi vận đơn mới
    /// fail-closed** ⇒ sập luồng giao hàng.
    /// </summary>
    public const string BienMatHangLoat = "eCommerce:Address:0508";
}

/// <summary>Cách một ánh xạ được tạo ra — quyết định **mức tin cậy** của nó.</summary>
public enum CarrierMatchMethod
{
    /// <summary>Khớp theo **mã** (hai bên cùng hệ mã) — tin cậy nhất.</summary>
    TheoMa = 0,

    /// <summary>Khớp theo **tên** (đã chuẩn hoá). ⚠️ Rủi ro: dấu tiếng Việt, "Phường" vs "Xã", tỉnh sáp nhập.</summary>
    TheoTen = 1,

    /// <summary>Do con người xác nhận thủ công.</summary>
    ThuCong = 2
}

/// <summary>Trạng thái xác minh của một ánh xạ.</summary>
public enum CarrierMappingStatus
{
    /// <summary>Mới đồng bộ, CHƯA ai xác minh — **không** dùng cho vận đơn thật.</summary>
    ChuaXacMinh = 0,

    /// <summary>Đã xác minh — được dùng để tạo vận đơn.</summary>
    DaXacMinh = 1,

    /// <summary>Nghi ngờ (đồng bộ sau thấy tên đổi / mã biến mất) — cần xem lại.</summary>
    NghiNgo = 2
}

/// <summary>
/// **Ánh xạ mã địa chỉ của một hãng vận chuyển** sang đơn vị hành chính của TA (`ADDR-002`).
///
/// 🔒 BẤT BIẾN:
///   1. **Mã hãng KHÔNG phải master** — nó là **dữ liệu đồng bộ**, có thể đổi tên, biến mất, hoặc
///      **được tái sử dụng cho đơn vị khác** sau khi hãng sáp nhập. Vì vậy ánh xạ phải lưu
///      **nguồn + thời điểm đồng bộ + cách khớp + trạng thái xác minh**.
///   2. **Khoá duy nhất là (TenantId, CarrierCode, CarrierAddressCode)** — cùng một mã có thể là
///      địa chỉ khác nhau ở **hai hãng khác nhau**. Không scope theo hãng ⇒ gửi sai địa chỉ.
///      (Ràng buộc DB cần `NULLS NOT DISTINCT` — bài học C4/F1.)
///   3. **Chưa xác minh ⇒ KHÔNG dùng được** (fail-closed). Khớp theo tên chỉ là **gợi ý**.
/// </summary>
public class CarrierAddressMapping : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã hãng vận chuyển (ví dụ `GHN`, `GHTK`).</summary>
    public string CarrierCode { get; protected set; } = default!;

    /// <summary>Cấp địa chỉ phía hãng (theo quy ước của hãng, không nhất thiết trùng cấp của ta).</summary>
    public string CarrierLevel { get; protected set; } = default!;

    /// <summary>Mã địa chỉ **của hãng**. Đây là thứ ta gửi trong payload tạo vận đơn.</summary>
    public string CarrierAddressCode { get; protected set; } = default!;

    /// <summary>Tên địa chỉ phía hãng (lưu lại để phát hiện đổi tên khi đồng bộ lần sau).</summary>
    public string CarrierAddressName { get; protected set; } = default!;

    /// <summary>Đơn vị hành chính của TA mà mã này trỏ tới.</summary>
    public Guid OurAdministrativeUnitId { get; protected set; }

    public CarrierMatchMethod MatchMethod { get; protected set; }

    public CarrierMappingStatus Status { get; protected set; }

    /// <summary>Nguồn dữ liệu (endpoint/tệp đồng bộ) — để truy vết khi có tranh chấp.</summary>
    public string? Source { get; protected set; }

    public DateTime SyncedAtUtc { get; protected set; }

    protected CarrierAddressMapping()
    {
    }

    public static CarrierAddressMapping TaoMoi(
        string carrierCode,
        string carrierLevel,
        string carrierAddressCode,
        string carrierAddressName,
        Guid ourAdministrativeUnitId,
        CarrierMatchMethod matchMethod,
        string? source,
        DateTime syncedAtUtc,
        Guid? tenantId = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(carrierCode)
            || string.IsNullOrWhiteSpace(carrierLevel)
            || string.IsNullOrWhiteSpace(carrierAddressCode)
            || ourAdministrativeUnitId == Guid.Empty)
        {
            throw new BusinessException(CarrierAddressErrorCodes.ThieuThongTinBatBuoc)
                .WithData("carrierCode", carrierCode)
                .WithData("carrierAddressCode", carrierAddressCode);
        }

        return new CarrierAddressMapping
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CarrierCode = carrierCode.Trim().ToUpperInvariant(),
            CarrierLevel = carrierLevel.Trim().ToUpperInvariant(),
            CarrierAddressCode = carrierAddressCode.Trim(),
            CarrierAddressName = carrierAddressName?.Trim() ?? string.Empty,
            OurAdministrativeUnitId = ourAdministrativeUnitId,
            MatchMethod = matchMethod,

            // 🔒 Mặc định là CHƯA XÁC MINH: khớp theo tên chỉ là gợi ý, cần người xác nhận.
            Status = matchMethod == CarrierMatchMethod.ThuCong
                ? CarrierMappingStatus.DaXacMinh
                : CarrierMappingStatus.ChuaXacMinh,

            Source = source,
            SyncedAtUtc = syncedAtUtc
        };
    }

    /// <summary>Xác minh ánh xạ (con người xác nhận) — chỉ sau đó mới dùng cho vận đơn thật.</summary>
    public void XacMinh(CarrierMatchMethod? cachKhớp = null)
    {
        if (cachKhớp is not null)
        {
            MatchMethod = cachKhớp.Value;
        }

        Status = CarrierMappingStatus.DaXacMinh;
    }

    /// <summary>Đánh dấu nghi ngờ (đồng bộ thấy tên đổi / mã biến mất).</summary>
    public void DanhDauNghiNgo()
    {
        // Không hạ cấp ánh xạ do con người xác minh thành "nghi ngờ"? VẪN hạ — nếu tên phía hãng đổi
        // thì người xác minh trước đó đã xác minh trên thông tin CŨ ⇒ phải xem lại.
        Status = CarrierMappingStatus.NghiNgo;
    }

    /// <summary>Cập nhật tên/tthời điểm đồng bộ khi đồng bộ lại (giữ nguyên đơn vị đích đã ánh xạ).</summary>
    public void CapNhatTuDongBo(string tenMoi, DateTime syncedAtUtc)
    {
        var tenCu = CarrierAddressName;
        CarrierAddressName = tenMoi?.Trim() ?? string.Empty;
        SyncedAtUtc = syncedAtUtc;

        if (!string.Equals(tenCu, CarrierAddressName, StringComparison.Ordinal))
        {
            // Tên đổi ⇒ mọi xác minh trước đó dựa trên tên CŨ ⇒ cần xem lại.
            DanhDauNghiNgo();
        }
    }
}

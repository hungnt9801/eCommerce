using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address;

/// <summary>
/// Một **đơn vị hành chính** trong một phiên bản schema.
///
/// 🔒 BẤT BIẾN:
///   1. Danh mục ĐVHC là **dữ liệu**, không hard-code (`ADDR-001` ADR). Nguồn: `provinces.open-api.vn`
///      (v1/v2) **và** văn bản pháp lý chính thức.
///   2. Khoá duy nhất là **(Phiên bản, Cấp, Mã)** — **KHÔNG** phải mã trần. Ràng buộc unique ở DB
///      phải là `(TenantId, SchemaVersion, Level, Code)` và **cần `NULLS NOT DISTINCT`** (bài học C4/F1).
///   3. **Mã đã đóng** (`IsActive = false`, ví dụ tỉnh bị sáp nhập) vẫn phải **tra được** để hiển thị
///      dữ liệu lịch sử, nhưng **KHÔNG** được dùng cho địa chỉ MỚI.
/// </summary>
public class AdministrativeUnit : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Phiên bản schema (1 = 3 cấp, 2 = 2 cấp).</summary>
    public int SchemaVersion { get; protected set; }

    public AdministrativeLevel Level { get; protected set; }

    /// <summary>Mã ĐVHC: tỉnh 2 chữ số, huyện 3 chữ số, xã 5 chữ số.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Mã đơn vị cấp trên (xã ⇒ tỉnh ở v2; xã ⇒ huyện ở v1; huyện ⇒ tỉnh ở v1).</summary>
    public string? ParentCode { get; protected set; }

    /// <summary>
    /// Còn hiệu lực để dùng cho địa chỉ MỚI hay không.
    /// `false` = mã **đã đóng** do sáp nhập — theo `QĐ 19/2025/QĐ-TTg`, mã của tỉnh bị nhập **bị đóng và
    /// không cấp lại**.
    /// </summary>
    public bool IsActive { get; protected set; }

    /// <summary>Căn cứ pháp lý (số quyết định/nghị quyết) — để truy vết vì sao có bản ghi này.</summary>
    public string? LegalBasis { get; protected set; }

    protected AdministrativeUnit()
    {
    }

    public static AdministrativeUnit TaoMoi(
        AdministrativeSchemaVersion phienBan,
        AdministrativeLevel level,
        string code,
        string name,
        string? parentCode = null,
        bool isActive = true,
        string? legalBasis = null,
        Guid? tenantId = null,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(phienBan);

        // 🔒 Cấp huyện KHÔNG tồn tại ở schema 2 cấp ⇒ ném lỗi ngay khi TẠO, không để lọt vào DB.
        if (level == AdministrativeLevel.QuanHuyen && !phienBan.ChoPhepCapHuyen)
        {
            throw new BusinessException(AddressErrorCodes.HuyenKhongDuocPhepOCap2)
                .WithData("phienBan", phienBan.Version);
        }

        var ma = ChuanHoaMa(level, code);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe).WithData("lyDo", "tên rỗng");
        }

        // Cấp dưới BẮT BUỘC có cấp trên (xã ⇒ tỉnh/huyện; huyện ⇒ tỉnh).
        if (level != AdministrativeLevel.Tinh && string.IsNullOrWhiteSpace(parentCode))
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe)
                .WithData("lyDo", $"cấp {level} bắt buộc có ParentCode");
        }

        return new AdministrativeUnit
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SchemaVersion = phienBan.Version,
            Level = level,
            Code = ma,
            Name = name.Trim(),
            ParentCode = parentCode?.Trim(),
            IsActive = isActive,
            LegalBasis = legalBasis
        };
    }

    /// <summary>Chuẩn hoá + kiểm định dạng mã theo cấp (ném lỗi, không im lặng cắt/nối).</summary>
    public static string ChuanHoaMa(AdministrativeLevel level, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe).WithData("lyDo", "mã rỗng");
        }

        var ma = code.Trim();
        var doDai = AdministrativeSchemaVersion.DoDaiMa(level);

        if (ma.Length != doDai || !ma.All(char.IsAsciiDigit))
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe)
                .WithData("level", level.ToString())
                .WithData("ma", ma)
                .WithData("doDaiYeuCau", doDai);
        }

        return ma;
    }

    /// <summary>Đóng mã (sáp nhập). Bản ghi vẫn tra được nhưng không dùng cho địa chỉ mới.</summary>
    public void Dong(string? legalBasis = null)
    {
        IsActive = false;

        if (!string.IsNullOrWhiteSpace(legalBasis))
        {
            LegalBasis = legalBasis;
        }
    }

    /// <summary>
    /// **Đổi tên** đơn vị theo danh mục chính thức.
    ///
    /// 🎯 VÌ SAO ĐỔI TÊN TẠI CHỖ (không tạo bản ghi mới): hoá đơn, vận đơn và địa chỉ khách hàng
    /// đang tham chiếu tới **Id** này. Nếu tạo bản ghi mới rồi đóng bản cũ thì các tham chiếu cũ
    /// **trỏ vào một đơn vị đã đóng** ⇒ tra cứu địa chỉ cũ sẽ **fail-closed** và không sửa được.
    /// Tên là **thuộc tính mô tả**, không phải danh tính — danh tính là (phiên bản, cấp, mã).
    /// </summary>
    public void DoiTen(string tenMoi, string? legalBasis = null)
    {
        Name = Volo.Abp.Check.NotNullOrWhiteSpace(tenMoi, nameof(tenMoi), maxLength: 256);

        if (!string.IsNullOrWhiteSpace(legalBasis))
        {
            LegalBasis = legalBasis;
        }
    }

    /// <summary>
    /// **Mở lại** đơn vị từng bị đóng, khi mã xuất hiện lại trong danh mục chính thức.
    /// Không ném lỗi nếu đang mở (để lần đồng bộ lặp lại vẫn an toàn).
    /// </summary>
    public void MoLai(string? legalBasis = null)
    {
        IsActive = true;

        if (!string.IsNullOrWhiteSpace(legalBasis))
        {
            LegalBasis = legalBasis;
        }
    }
    public override string ToString() => $"v{SchemaVersion}/{Level}/{Code} {Name}";
}

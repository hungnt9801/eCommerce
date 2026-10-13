using Volo.Abp;

namespace eCommerce.Address;

/// <summary>Cấp đơn vị hành chính.</summary>
public enum AdministrativeLevel
{
    /// <summary>Tỉnh / thành phố trực thuộc trung ương. Mã **2 chữ số**.</summary>
    Tinh = 1,

    /// <summary>Xã / phường / đặc khu (cấp cơ sở). Mã **5 chữ số**.</summary>
    Xa = 2,

    /// <summary>
    /// Quận / huyện / thị xã — 🚩 **CHỈ tồn tại ở schema 3 cấp (cũ)**. Mã **3 chữ số**.
    /// Từ 01/7/2025 (`Luật 72/2025/QH15`) cấp huyện **đã bị bỏ** ⇒ không dùng cho địa chỉ MỚI.
    /// </summary>
    QuanHuyen = 3
}

/// <summary>
/// **Phiên bản schema đơn vị hành chính** — 🚩 đây là khái niệm quan trọng nhất của module này.
///
/// VÌ SAO PHẢI CÓ: mã ĐVHC **KHÔNG phải khoá toàn cục**. Cùng một mã có thể là **hai đơn vị khác nhau**
/// ở hai phiên bản (đã kiểm chứng bằng gọi API thật ngày 13/10/2026):
///   • mã `00004` = **Phường Ba Đình** ở v2 nhưng = **Phường Trúc Bạch** ở v1;
///   • mã `001` = **Quận Ba Đình** ở v1 (cấp huyện), không tồn tại ở v2.
/// ⇒ Tra mã **trần** (không kèm phiên bản) sẽ **im lặng trả về SAI đơn vị** — và địa chỉ sai sẽ đi thẳng
/// vào vận đơn và hoá đơn.
/// </summary>
public sealed record AdministrativeSchemaVersion
{
    /// <summary>Schema **3 cấp** cũ: 63 tỉnh, có quận/huyện. Dùng cho dữ liệu LỊCH SỬ.</summary>
    public static readonly AdministrativeSchemaVersion V1 = new(
        version: 1,
        moTa: "3 cấp (63 tỉnh, có quận/huyện) — dữ liệu lịch sử",
        soCap: 3,
        choPhepCapHuyen: true,
        soTinh: 63);

    /// <summary>
    /// Schema **2 cấp** hiện hành: 34 tỉnh, **không có quận/huyện**; 3.321 xã/phường.
    /// Căn cứ: `Luật 72/2025/QH15` (hiệu lực 01/7/2025) + `NQ 60-NQ/TW` + `QĐ 19/2025/QĐ-TTg`.
    /// </summary>
    public static readonly AdministrativeSchemaVersion V2 = new(
        version: 2,
        moTa: "2 cấp (34 tỉnh, bỏ quận/huyện) — hiện hành",
        soCap: 2,
        choPhepCapHuyen: false,
        soTinh: 34);

    public int Version { get; }

    public string MoTa { get; }

    /// <summary>Số cấp của schema (3 hoặc 2).</summary>
    public int SoCap { get; }

    /// <summary>Có cho phép khai báo cấp huyện hay không.</summary>
    public bool ChoPhepCapHuyen { get; }

    /// <summary>Số tỉnh/thành theo schema (v1 = 63, v2 = 34).</summary>
    public int SoTinh { get; }

    private AdministrativeSchemaVersion(int version, string moTa, int soCap, bool choPhepCapHuyen, int soTinh)
    {
        Version = version;
        MoTa = moTa;
        SoCap = soCap;
        ChoPhepCapHuyen = choPhepCapHuyen;
        SoTinh = soTinh;
    }

    /// <summary>Lấy phiên bản theo số. Ném lỗi nếu chưa hỗ trợ (KHÔNG im lặng mặc định).</summary>
    public static AdministrativeSchemaVersion TheoSo(int version) => version switch
    {
        1 => V1,
        2 => V2,
        _ => throw new BusinessException(AddressErrorCodes.PhienBanKhongHopLe).WithData("version", version)
    };

    /// <summary>Phiên bản dùng cho địa chỉ MỚI (hiện hành = 2 cấp).</summary>
    public static AdministrativeSchemaVersion HienHanh => V2;

    /// <summary>Độ dài mã theo cấp.</summary>
    public static int DoDaiMa(AdministrativeLevel level) => level switch
    {
        AdministrativeLevel.Tinh => 2,
        AdministrativeLevel.QuanHuyen => 3,
        AdministrativeLevel.Xa => 5,
        _ => throw new BusinessException(AddressErrorCodes.MaKhongHopLe).WithData("level", level)
    };

    public override string ToString() => $"v{Version} ({MoTa})";
}

/// <summary>Kết quả kiểm tra một địa chỉ (tỉnh [+ huyện] + xã) theo một phiên bản schema.</summary>
/// <param name="HopLe">Địa chỉ có hợp lệ để dùng cho đơn MỚI hay không.</param>
/// <param name="ErrorCode">Mã lỗi khi không hợp lệ.</param>
/// <param name="ThongDiep">Giải thích ngắn cho người dùng.</param>
/// <param name="Tinh">Đơn vị tỉnh tra được (nếu có).</param>
/// <param name="Huyen">Đơn vị huyện tra được (chỉ schema v1).</param>
/// <param name="Xa">Đơn vị xã tra được (nếu có).</param>
public sealed record KetQuaKiemTraDiaChi(
    bool HopLe,
    string? ErrorCode = null,
    string? ThongDiep = null,
    AdministrativeUnit? Tinh = null,
    AdministrativeUnit? Huyen = null,
    AdministrativeUnit? Xa = null)
{
    public static KetQuaKiemTraDiaChi Ok(AdministrativeUnit tinh, AdministrativeUnit? huyen, AdministrativeUnit xa)
        => new(true, Tinh: tinh, Huyen: huyen, Xa: xa);

    public static KetQuaKiemTraDiaChi Loi(string errorCode, string thongDiep, AdministrativeUnit? tinh = null)
        => new(false, errorCode, thongDiep, tinh);
}

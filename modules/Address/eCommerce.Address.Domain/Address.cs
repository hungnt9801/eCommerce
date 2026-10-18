using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address;

/// <summary>
/// **Địa chỉ VẬT LÝ**, dùng chung trong tenant — `ADDR-004` (`AddrAddresses`).
///
/// 🔒 BẤT BIẾN (mỗi điều có hàng rào ở tầng DB, không chỉ ở code):
///   1. **Một NỘI DUNG ⇒ một bản ghi**: `(TenantId, ContentHash)` là khoá tự nhiên (`ADR-04`).
///      `ContentHash` **chỉ** được tính bởi <see cref="DiaChiCanonical"/> — cấm tính ở nơi khác.
///   2. **Nội dung địa lý BẤT BIẾN sau khi `DaChuanHoa`** (`ADR-05`): muốn đổi thì tạo **bản ghi mới**
///      rồi tạo **liên kết mới** (sổ địa chỉ append-only). Vì hoá đơn/vận đơn đã in từ snapshot chữ.
///   3. **`DiaChiDayDu` là SNAPSHOT CHỮ** lúc tạo — chứng từ in từ cột này, **CẤM** join
///      `AddrAdministrativeUnits` để in (`ADR-10`), vì `DoiTen` sửa **tại chỗ**
///      (`.../AdministrativeUnit.cs:138`) ⇒ join sẽ **đổi** hoá đơn cũ.
///   4. **Fail-closed**: đã `DaChuanHoa` thì **bắt buộc** có `TinhCode` + `XaCode` + `NormalizedAtUtc`;
///      địa chỉ thô thì **bắt buộc KHÔNG** có mã (CHECK ở DB — hàng rào cuối, xem `ADDR-004` §5.2).
///
/// 🚨 PHẠM VI `S1`: entity + migration. Đường API, `IAddressDirectory`, ETO và **xoá mềm/ẩn danh**
///    (`ADR-11`, cần `IAddressUsageProbe`) là việc của `S2` — **chưa** có ở đây.
/// </summary>
public class Address : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa `SoNhaDuong` — khớp `HasMaxLength` + CHECK ở DB.</summary>
    public const int DoDaiSoNhaDuong = 256;

    /// <summary>Độ dài tối đa `DiaChiDayDu` (snapshot chữ để in).</summary>
    public const int DoDaiDiaChiDayDu = 512;

    public Guid? TenantId { get; protected set; }

    /// <summary>Trạng thái trong vòng đời chuẩn hoá (`ADDR-004` §3.1).</summary>
    public TrangThaiDiaChi TrangThai { get; protected set; }

    /// <summary>Nguồn tạo — để truy vết *"địa chỉ này từ đâu ra"*.</summary>
    public NguonTaoDiaChi NguonTao { get; protected set; }

    /// <summary>
    /// Phiên bản schema ĐVHC của địa chỉ này: **1** = 3 cấp (lịch sử), **2** = 2 cấp (hiện hành).
    ///
    /// 📌 Địa chỉ **thô** (`ChuaChuanHoa`) chưa biết schema nên tạm ghi phiên bản **hiện hành**;
    /// khi <see cref="ChuanHoa"/> thành công thì ghi đè bằng phiên bản thật của lần kiểm.
    /// (Canonical của địa chỉ thô **không** chứa `SchemaVersion` — xem `DiaChiCanonical.DungChuoiCanonicalTho`.)
    /// </summary>
    public int SchemaVersion { get; protected set; }

    /// <summary>Mã tỉnh/thành (2 chữ số). `NULL` ⇔ địa chỉ thô.</summary>
    public string? TinhCode { get; protected set; }

    /// <summary>Mã quận/huyện (3 chữ số) — **CHỈ** ở `SchemaVersion = 1`. `NULL` ở v2 (quy tắc pháp lý).</summary>
    public string? HuyenCode { get; protected set; }

    /// <summary>Mã xã/phường/đặc khu (5 chữ số). `NULL` ⇔ địa chỉ thô.</summary>
    public string? XaCode { get; protected set; }

    /// <summary>Số nhà, đường, ngõ, thôn… (chữ tự do). `NULL` = địa chỉ chỉ có phần hành chính (hợp lệ).</summary>
    public string? SoNhaDuong { get; protected set; }

    /// <summary>Snapshot chữ để **in chứng từ**: <c>SoNhaDuong, Xa, Huyen, Tinh</c>. Bất biến sau `DaChuanHoa`.</summary>
    public string DiaChiDayDu { get; protected set; } = default!;

    /// <summary>SHA-256 hex (64 ký tự, chữ thường) của chuỗi canonical — khoá tự nhiên (`ADR-04`).</summary>
    public string ContentHash { get; protected set; } = default!;

    /// <summary>Thời điểm **đã** kiểm ĐVHC thành công — **bằng chứng** của fail-closed. `NULL` ⇔ địa chỉ thô.</summary>
    public DateTime? NormalizedAtUtc { get; protected set; }

    /// <summary>Thời điểm xác minh. Giữ lại khi chuyển sang `NghiNgo` (để không mất lịch sử).</summary>
    public DateTime? XacMinhLucUtc { get; protected set; }

    protected Address()
    {
    }

    /// <summary>
    /// Tạo **địa chỉ THÔ** (`ChuaChuanHoa`) — dùng khi chưa xác định được đơn vị hành chính
    /// (webhook sàn, chữ tự do của khách). Guard sai ⇒ **NÉM** `eCommerce:Address:0105`.
    /// </summary>
    public static Address TaoTuDiaChiTho(
        string diaChiDayDu,
        NguonTaoDiaChi nguonTao,
        Guid? tenantId = null,
        Guid? id = null)
    {
        var text = KiemTraNoiDung(diaChiDayDu);

        return new Address
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            TrangThai = TrangThaiDiaChi.ChuaChuanHoa,
            NguonTao = nguonTao,
            SchemaVersion = AdministrativeSchemaVersion.HienHanh.Version,
            DiaChiDayDu = text,
            ContentHash = DiaChiCanonical.TinhHashTho(text),
            NormalizedAtUtc = null,
            XacMinhLucUtc = null
        };
    }

    /// <summary>
    /// Tạo địa chỉ **đã chuẩn hoá** (`DaChuanHoa`) — dùng khi mã ĐVHC **đã** qua resolver fail-closed.
    /// Guard sai ⇒ **NÉM** (fail-closed, **0 dòng** ghi): `0001`/`0003`/`0006`/`0004`/`0005` hoặc `0105`.
    /// </summary>
    /// <param name="diaChiDayDu">
    /// Snapshot chữ đã dựng sẵn từ tên ĐVHC (<c>SoNhaDuong, Xa, Huyen, Tinh</c>) — entity **không**
    /// tự tra tên (nó chỉ có mã), nên nơi gọi **bắt buộc** dựng đúng thứ tự này.
    /// </param>
    public static Address TaoDaChuanHoa(
        AdministrativeSchemaVersion phienBan,
        string tinhCode,
        string? huyenCode,
        string xaCode,
        string? soNhaDuong,
        string diaChiDayDu,
        NguonTaoDiaChi nguonTao,
        DateTime normalizedAtUtc,
        Guid? tenantId = null,
        Guid? id = null)
    {
        var a = new Address
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            NguonTao = nguonTao
        };

        a.GanNoiDungDiaLy(phienBan, tinhCode, huyenCode, xaCode, soNhaDuong, diaChiDayDu, normalizedAtUtc);
        a.TrangThai = TrangThaiDiaChi.DaChuanHoa;
        return a;
    }

    /// <summary>
    /// **Chuẩn hoá** địa chỉ thô ⇒ `DaChuanHoa`. 🔒 Chỉ được chạy **MỘT LẦN** trên mỗi bản ghi.
    ///
    /// Guard sai ⇒ **NÉM**:
    ///   • trạng thái **không phải** `ChuaChuanHoa` ⇒ `eCommerce:Address:0102`
    ///     (*"cố đổi nội dung địa lý đã chuẩn hoá"* — `ADR-05`: nội dung bất biến, phải tạo bản ghi mới);
    ///   • mã/phiên bản sai ⇒ `0004`/`0005`/`0001`/`0003`/`0006` (fail-closed).
    /// </summary>
    public void ChuanHoa(
        AdministrativeSchemaVersion phienBan,
        string tinhCode,
        string? huyenCode,
        string xaCode,
        string? soNhaDuong,
        string diaChiDayDu,
        DateTime normalizedAtUtc)
    {
        // 🚨 Không im lặng bỏ qua: nội dung địa lý đã chốt thì KHÔNG được đổi.
        if (TrangThai != TrangThaiDiaChi.ChuaChuanHoa)
        {
            throw new BusinessException(AddressErrorCodes.DaCoNoiDungDiaLy)
                .WithData("id", Id)
                .WithData("trangThai", TrangThai.ToString());
        }

        GanNoiDungDiaLy(phienBan, tinhCode, huyenCode, xaCode, soNhaDuong, diaChiDayDu, normalizedAtUtc);
        TrangThai = TrangThaiDiaChi.DaChuanHoa;
    }

    /// <summary>
    /// **Xác minh** địa chỉ — cần **căn cứ** (không rỗng) để trả lời *"vì sao tin địa chỉ này đúng"*.
    ///
    /// Guard sai ⇒ **NÉM**: thiếu căn cứ ⇒ `eCommerce:Address:0106`; trạng thái nguồn không hợp lệ
    /// (chỉ `DaChuanHoa` hoặc `NghiNgo`) ⇒ `eCommerce:Address:0103`.
    /// </summary>
    public void XacMinh(string canCu, DateTime lucUtc)
    {
        if (string.IsNullOrWhiteSpace(canCu))
        {
            throw new BusinessException(AddressErrorCodes.ThieuCanCuXacMinh).WithData("id", Id);
        }

        if (TrangThai is not (TrangThaiDiaChi.DaChuanHoa or TrangThaiDiaChi.NghiNgo))
        {
            throw new BusinessException(AddressErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("id", Id)
                .WithData("tu", TrangThai.ToString())
                .WithData("sang", TrangThaiDiaChi.DaXacMinh.ToString());
        }

        TrangThai = TrangThaiDiaChi.DaXacMinh;
        XacMinhLucUtc = lucUtc;
    }

    /// <summary>
    /// Đưa địa chỉ vào **hàng đợi vận hành** khi job đối chiếu thấy mã ĐVHC đã đóng / dữ liệu lệch.
    /// Giữ **nguyên** nội dung và `XacMinhLucUtc` (lịch sử).
    ///
    /// Guard sai ⇒ **NÉM** `eCommerce:Address:0103` (chỉ chuyển được từ `DaChuanHoa`/`DaXacMinh`).
    /// </summary>
    public void DanhDauNghiNgo(string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(AddressErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("id", Id)
                .WithData("lyDo", "thiếu lý do nghi ngờ");
        }

        if (TrangThai is not (TrangThaiDiaChi.DaChuanHoa or TrangThaiDiaChi.DaXacMinh))
        {
            throw new BusinessException(AddressErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("id", Id)
                .WithData("tu", TrangThai.ToString())
                .WithData("sang", TrangThaiDiaChi.NghiNgo.ToString());
        }

        TrangThai = TrangThaiDiaChi.NghiNgo;
    }

    /// <summary>
    /// Gán toàn bộ nội dung địa lý + **tính lại** `ContentHash` trong cùng một chỗ.
    /// 🔒 Dùng chung cho <see cref="TaoDaChuanHoa"/> và <see cref="ChuanHoa"/> để **không** có hai
    /// đường tính hash khác nhau (canonical phải là **một** nguồn sự thật).
    /// </summary>
    private void GanNoiDungDiaLy(
        AdministrativeSchemaVersion phienBan,
        string tinhCode,
        string? huyenCode,
        string xaCode,
        string? soNhaDuong,
        string diaChiDayDu,
        DateTime normalizedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(phienBan);

        var text = KiemTraNoiDung(diaChiDayDu);
        var tinh = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Tinh, tinhCode);
        var xa = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Xa, xaCode);

        string? huyen = null;
        if (!string.IsNullOrWhiteSpace(huyenCode))
        {
            // 🚨 Quy tắc PHÁP LÝ (Luật 72/2025/QH15): schema 2 cấp KHÔNG có cấp huyện.
            if (!phienBan.ChoPhepCapHuyen)
            {
                throw new BusinessException(AddressErrorCodes.HuyenKhongDuocPhepOCap2)
                    .WithData("phienBan", phienBan.Version)
                    .WithData("huyenCode", huyenCode);
            }

            huyen = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.QuanHuyen, huyenCode);
        }

        if (normalizedAtUtc == default)
        {
            throw new BusinessException(AddressErrorCodes.ThieuCanCuXacMinh)
                .WithData("lyDo", "thiếu thời điểm chuẩn hoá");
        }

        var soNha = string.IsNullOrWhiteSpace(soNhaDuong) ? null : soNhaDuong!.Trim();
        if (soNha is not null && soNha.Length > DoDaiSoNhaDuong)
        {
            throw new BusinessException(AddressErrorCodes.ThieuNoiDung)
                .WithData("lyDo", "SoNhaDuong quá dài")
                .WithData("doDaiToiDa", DoDaiSoNhaDuong);
        }

        SchemaVersion = phienBan.Version;
        TinhCode = tinh;
        HuyenCode = huyen;
        XaCode = xa;
        SoNhaDuong = soNha;
        DiaChiDayDu = text;
        NormalizedAtUtc = normalizedAtUtc;
        ContentHash = DiaChiCanonical.TinhHashDaChuanHoa(phienBan.Version, tinh, huyen, xa, soNha);
    }

    /// <summary>Kiểm nội dung chữ bắt buộc — ném `0105` kèm lý do, **không** im lặng cắt bớt.</summary>
    private static string KiemTraNoiDung(string? diaChiDayDu)
    {
        if (string.IsNullOrWhiteSpace(diaChiDayDu))
        {
            throw new BusinessException(AddressErrorCodes.ThieuNoiDung).WithData("lyDo", "DiaChiDayDu rỗng");
        }

        var text = diaChiDayDu.Trim();
        if (text.Length > DoDaiDiaChiDayDu)
        {
            throw new BusinessException(AddressErrorCodes.ThieuNoiDung)
                .WithData("lyDo", "DiaChiDayDu quá dài")
                .WithData("doDaiToiDa", DoDaiDiaChiDayDu);
        }

        return text;
    }

    public override string ToString() => $"{TrangThai}/v{SchemaVersion}/{TinhCode}-{HuyenCode ?? "-"}-{XaCode} {DiaChiDayDu}";
}

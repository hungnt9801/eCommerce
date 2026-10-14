using Volo.Abp;

namespace eCommerce.Address.Sync;

/// <summary>Mã lỗi của miền **đồng bộ danh mục ĐVHC** — dải `eCommerce:Address:06xx`.</summary>
public static class AdministrativeSyncErrorCodes
{
    /// <summary>Danh mục nguồn RỖNG — từ chối áp dụng (sẽ đóng toàn bộ đơn vị đang dùng).</summary>
    public const string DanhMucRong = "eCommerce:Address:0601";

    /// <summary>Danh mục nguồn quá ÍT đơn vị — nghi nguồn bị lỗi/trả về một phần.</summary>
    public const string DanhMucQuaIt = "eCommerce:Address:0602";

    /// <summary>Danh mục nguồn THIẾU cả một cấp (ví dụ không có tỉnh nào) — không thể là dữ liệu thật.</summary>
    public const string ThieuCapBatBuoc = "eCommerce:Address:0603";

    /// <summary>Số đơn vị bị ĐÓNG vượt ngưỡng — từ chối để tránh phá hoại danh mục đang chạy.</summary>
    public const string DongHangLoatVuotNguong = "eCommerce:Address:0604";
}

/// <summary>Một đơn vị hành chính trong **danh mục nguồn** (dữ liệu tải về, chưa phải entity).</summary>
public sealed record DonViNguon(
    int SchemaVersion,
    AdministrativeLevel Level,
    string Code,
    string Name,
    string? ParentCode = null);

/// <summary>Danh mục nguồn của một lần tải — kèm NGUỒN và THỜI ĐIỂM để truy vết.</summary>
public sealed record DanhMucNguon(
    IReadOnlyList<DonViNguon> DonVis,
    string Nguon,
    DateTime TaiLucUtc);

/// <summary>
/// Nguồn danh mục ĐVHC (ví dụ `provinces.open-api.vn`).
/// Tách thành interface để: (a) thay nguồn không phải sửa luật đồng bộ, (b) **test được** bằng nguồn giả.
/// </summary>
public interface IKhungDonViHanhChinhSource
{
    Task<DanhMucNguon> TaiDanhMucAsync(int schemaVersion, CancellationToken cancellationToken = default);
}

/// <summary>Loại thay đổi cần áp dụng cho một đơn vị.</summary>
public enum LoaiThayDoi
{
    /// <summary>Mã mới hoàn toàn ⇒ tạo bản ghi.</summary>
    ThemMoi = 0,

    /// <summary>Tên đổi ⇒ cập nhật tên (KHÔNG tạo bản ghi mới, để hoá đơn cũ vẫn tham chiếu đúng).</summary>
    DoiTen = 1,

    /// <summary>Mã từng bị đóng nay có lại trong danh mục ⇒ mở lại.</summary>
    MoLai = 2,

    /// <summary>Mã biến mất khỏi danh mục ⇒ ĐÓNG (không xoá — hoá đơn cũ còn tham chiếu).</summary>
    Dong = 3
}

/// <summary>Một thay đổi cụ thể trong kế hoạch đồng bộ.</summary>
public sealed record ThayDoiDonVi(
    LoaiThayDoi Loai,
    DonViNguon? Nguon,
    AdministrativeUnit? HienCo,
    string? LyDo);

/// <summary>Kế hoạch đồng bộ — TÍNH TRƯỚC, chưa áp dụng gì.</summary>
public sealed record KeHoachDongBo(IReadOnlyList<ThayDoiDonVi> ThayDois)
{
    public int SoThemMoi => ThayDois.Count(x => x.Loai == LoaiThayDoi.ThemMoi);
    public int SoDoiTen => ThayDois.Count(x => x.Loai == LoaiThayDoi.DoiTen);
    public int SoMoLai => ThayDois.Count(x => x.Loai == LoaiThayDoi.MoLai);
    public int SoDong => ThayDois.Count(x => x.Loai == LoaiThayDoi.Dong);

    public bool CoThayDoi => ThayDois.Count > 0;

}

/// <summary>
/// **LÕI THUẦN đồng bộ danh mục ĐVHC** — không I/O, không đồng hồ hệ thống ⇒ test được toàn bộ.
///
/// 🚨 BỐN HÀNG RÀO CHỐNG "ĐỒNG BỘ PHÁ HOẠI" — đây là phần quan trọng nhất của lớp này:
///   Đồng bộ danh mục là thao tác **CÓ TÍNH PHÁ HOẠI**: nó **đóng** những mã không còn trong danh mục.
///   Nếu nguồn trả về **một phần** (API lỗi, timeout giữa chừng, đổi định dạng, tải phải trang rỗng)
///   thì "đóng những mã không còn" sẽ **đóng gần hết địa chỉ đang dùng** — và mọi vận đơn mới sẽ
///   **fail-closed** (không tra được mã, `eCommerce:Address:0001/0006`), tức **sập luồng bán hàng**
///   chỉ vì một lần gọi API hỏng.
///   Vì vậy: danh mục rỗng / quá ít / thiếu cấp / đóng quá nhiều ⇒ **TỪ CHỐI ÁP DỤNG** và ném lỗi rõ ràng.
///
/// 🔒 BẤT BIẾN KHÁC: **KHÔNG BAO GIỜ XOÁ** — mã biến mất chỉ bị **ĐÓNG** (`IsActive = false`), vì
///   hoá đơn và vận đơn cũ còn tham chiếu tới nó. Xoá sẽ làm mất khả năng tra cứu lịch sử.
/// </summary>
public static class AdministrativeUnitSynchronizer
{
    /// <summary>Tỉ lệ đơn vị bị đóng tối đa cho phép trong một lần đồng bộ (mặc định 20%).</summary>
    public const double NguongDongMacDinh = 0.20;

    /// <summary>Số đơn vị tối thiểu của một danh mục được coi là hợp lệ.</summary>
    public const int SoDonViToiThieuMacDinh = 10;

    /// <summary>
    /// Tính kế hoạch đồng bộ (KHÔNG thay đổi gì). Ném <see cref="BusinessException"/> nếu danh mục nguồn
    /// không đáng tin (xem bốn hàng rào ở ghi chú lớp).
    /// </summary>
    public static KeHoachDongBo LapKeHoach(
        int schemaVersion,
        IReadOnlyList<AdministrativeUnit> hienCo,
        DanhMucNguon danhMuc,
        double nguongDong = NguongDongMacDinh,
        int soDonViToiThieu = SoDonViToiThieuMacDinh)
    {
        ArgumentNullException.ThrowIfNull(hienCo);
        ArgumentNullException.ThrowIfNull(danhMuc);

        // Chỉ xét các đơn vị THUỘC PHIÊN BẢN đang đồng bộ — v1 và v2 có danh mục song song,
        // và mã ở hai phiên bản KHÔNG có cùng ý nghĩa (bài học ADDR-001: mã 00004 là hai xã khác nhau).
        var cuaPhienBan = hienCo.Where(x => x.SchemaVersion == schemaVersion).ToList();
        var nguon = danhMuc.DonVis.Where(x => x.SchemaVersion == schemaVersion).ToList();

        // ── HÀNG RÀO 1: danh mục rỗng ──
        if (nguon.Count == 0)
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucRong)
                .WithData("nguon", danhMuc.Nguon)
                .WithData("schemaVersion", schemaVersion);
        }

        // ── HÀNG RÀO 2: quá ít đơn vị ──
        if (nguon.Count < soDonViToiThieu)
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucQuaIt)
                .WithData("soLuong", nguon.Count)
                .WithData("toiThieu", soDonViToiThieu)
                .WithData("nguon", danhMuc.Nguon);
        }

        // ── HÀNG RÀO 3: thiếu cấp bắt buộc ──
        // Danh mục ĐVHC thật LUÔN có cấp tỉnh. Không có tỉnh nào ⇒ chắc chắn dữ liệu sai/thiếu.
        if (!nguon.Any(x => x.Level == AdministrativeLevel.Tinh))
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.ThieuCapBatBuoc)
                .WithData("capThieu", nameof(AdministrativeLevel.Tinh))
                .WithData("nguon", danhMuc.Nguon);
        }

        var theoKhoa = cuaPhienBan.ToDictionary(x => Khoa(x.Level, x.Code), x => x);
        var thayDois = new List<ThayDoiDonVi>();
        var maNguon = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dv in nguon)
        {
            var khoa = Khoa(dv.Level, dv.Code);
            maNguon.Add(khoa);

            if (!theoKhoa.TryGetValue(khoa, out var cu))
            {
                thayDois.Add(new ThayDoiDonVi(LoaiThayDoi.ThemMoi, dv, null, "Mã mới trong danh mục nguồn."));
                continue;
            }

            if (!cu.IsActive)
            {
                thayDois.Add(new ThayDoiDonVi(LoaiThayDoi.MoLai, dv, cu, "Mã có lại trong danh mục nguồn."));
                continue;   // mở lại rồi thì lần đồng bộ sau mới xét đổi tên
            }

            if (!string.Equals(cu.Name, dv.Name, StringComparison.Ordinal))
            {
                thayDois.Add(new ThayDoiDonVi(LoaiThayDoi.DoiTen, dv, cu,
                    $"Tên đổi: '{cu.Name}' → '{dv.Name}'."));
            }
        }

        // Mã biến mất khỏi danh mục ⇒ ĐÓNG (không xoá).
        foreach (var cu in cuaPhienBan.Where(x => x.IsActive && !maNguon.Contains(Khoa(x.Level, x.Code))))
        {
            thayDois.Add(new ThayDoiDonVi(LoaiThayDoi.Dong, null, cu, "Mã không còn trong danh mục nguồn."));
        }

        var soDong = thayDois.Count(x => x.Loai == LoaiThayDoi.Dong);
        var tongDangHoatDong = cuaPhienBan.Count(x => x.IsActive);

        // ── HÀNG RÀO 4: đóng quá nhiều ──
        // Đây là hàng rào bắt đúng ca NGUY HIỂM NHẤT: nguồn trả về một phần (thiếu trang, timeout
        // giữa chừng) ⇒ nếu áp dụng thì sẽ đóng hàng loạt và làm sập luồng tra cứu địa chỉ.
        if (tongDangHoatDong > 0 && soDong > 0)
        {
            var tiLe = (double)soDong / tongDangHoatDong;

            if (tiLe > nguongDong)
            {
                throw new BusinessException(AdministrativeSyncErrorCodes.DongHangLoatVuotNguong)
                    .WithData("soDong", soDong)
                    .WithData("tongDangHoatDong", tongDangHoatDong)
                    .WithData("tiLe", Math.Round(tiLe, 4))
                    .WithData("nguong", nguongDong)
                    .WithData("nguon", danhMuc.Nguon);
            }
        }

        return new KeHoachDongBo(thayDois);
    }

    /// <summary>
    /// **Áp dụng kế hoạch** lên các entity (vẫn THUẦN: không I/O). Trả về các đơn vị **cần chèn mới**;
    /// các thay đổi còn lại được sửa **trực tiếp** trên entity hiện có (nơi gọi chỉ cần `UpdateAsync`).
    /// </summary>
    public static IReadOnlyList<AdministrativeUnit> ApDung(KeHoachDongBo keHoach, Guid? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(keHoach);

        var deThem = new List<AdministrativeUnit>();

        foreach (var td in keHoach.ThayDois)
        {
            switch (td.Loai)
            {
                case LoaiThayDoi.ThemMoi:
                    deThem.Add(AdministrativeUnit.TaoMoi(
                        AdministrativeSchemaVersion.TheoSo(td.Nguon!.SchemaVersion),
                        td.Nguon.Level,
                        td.Nguon.Code,
                        td.Nguon.Name,
                        parentCode: td.Nguon.ParentCode,
                        tenantId: tenantId));
                    break;

                case LoaiThayDoi.DoiTen:
                    td.HienCo!.DoiTen(td.Nguon!.Name);
                    break;

                case LoaiThayDoi.MoLai:
                    td.HienCo!.MoLai();
                    break;

                case LoaiThayDoi.Dong:
                    td.HienCo!.Dong(td.LyDo);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(keHoach), td.Loai, "Loại thay đổi không xử lý được.");
            }
        }

        return deThem;
    }

    /// <summary>Khoá so khớp: **cấp + mã** (trong cùng một phiên bản schema).</summary>
    private static string Khoa(AdministrativeLevel level, string code)
        => $"{level}|{code.Trim()}";
}

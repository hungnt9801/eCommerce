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

    /// <summary>
    /// Dữ liệu nguồn SAI ĐỊNH DẠNG (thiếu trường, sai kiểu, mã dài hơn quy ước).
    /// 🚨 Ném lỗi thay vì bỏ qua: bỏ qua sẽ tạo **danh mục thiếu** ⇒ lần đồng bộ sau sẽ **ĐÓNG** những
    /// đơn vị bị thiếu đó. Sai định dạng phải dừng, không được đoán.
    /// </summary>
    public const string DanhMucSaiDinhDang = "eCommerce:Address:0605";
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
    /// **Bộ mã "hàng rào chặn"** — dùng chung cho **job** và **`ImportDatasetAsync`** để phân loại
    /// *"bị từ chối có lý do"* (dòng lô `BiChan`) khác với *"lỗi không mong đợi"* (dòng lô `Loi`).
    /// Gồm 4 hàng rào cũ (`0601`…`0604`), hàng rào 0b (`0605`) và hàng rào 5 (`0008`).
    /// </summary>
    public static readonly IReadOnlyList<string> HangRaoChan =
    [
        AdministrativeSyncErrorCodes.DanhMucRong,
        AdministrativeSyncErrorCodes.DanhMucQuaIt,
        AdministrativeSyncErrorCodes.ThieuCapBatBuoc,
        AdministrativeSyncErrorCodes.DongHangLoatVuotNguong,
        AdministrativeSyncErrorCodes.DanhMucSaiDinhDang,
        AddressErrorCodes.NguonDoiDonViCapTren
    ];

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

        // ── HÀNG RÀO 0b (mới, D-81): TÍNH TOÀN VẸN CỦA NGUỒN ──
        // Kiểm TRƯỚC khi dựng `theoKhoa`/chạm entity: mã sai định dạng/tên rỗng/thiếu ParentCode/trùng khoá
        // ⇒ 0605 (KHÔNG để `TaoMoi` ném 0002 ở bước áp dụng — khi đó lỗi thành "không mong đợi" và dòng lô
        // bị ghi `Loi` thay vì `BiChan`).
        KiemTraToanVenNguon(nguon, danhMuc.Nguon, schemaVersion);

        var theoKhoa = cuaPhienBan.ToDictionary(x => Khoa(x.Level, x.Code), x => x);
        var thayDois = new List<ThayDoiDonVi>();
        var maNguon = new HashSet<string>(StringComparer.Ordinal);
        var doiCha = new List<string>();

        foreach (var dv in nguon)
        {
            var khoa = Khoa(dv.Level, dv.Code ?? string.Empty);
            maNguon.Add(khoa);

            if (!theoKhoa.TryGetValue(khoa, out var cu))
            {
                thayDois.Add(new ThayDoiDonVi(LoaiThayDoi.ThemMoi, dv, null, "Mã mới trong danh mục nguồn."));
                continue;
            }

            // ── HÀNG RÀO 5 (D-81): nguồn đổi `ParentCode` của mã ĐÃ CÓ ⇒ từ chối CẢ LÔ (0008) ──
            // Kiểm TRƯỚC nhánh `!cu.IsActive` (bản ghi đang đóng vẫn phải kiểm). Chuẩn hoá: Trim,
            // `null` ≡ rỗng, Ordinal ⇒ chỉ khác khoảng trắng/null↔rỗng thì KHÔNG ném.
            var chaCu = ChuanHoaCha(cu.ParentCode);
            var chaMoi = ChuanHoaCha(dv.ParentCode);

            if (!string.Equals(chaCu, chaMoi, StringComparison.Ordinal))
            {
                doiCha.Add($"{dv.Code?.Trim()}: {chaCu} → {chaMoi}");
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

        // Hàng rào 5 phải được kiểm TRƯỚC hàng rào 4 (D-81, NC-5): lô vừa đổi cha vừa vượt ngưỡng đóng
        // phải trả `0008` (danh tính đổi) chứ không phải `0604` (đóng quá nhiều).
        if (doiCha.Count > 0)
        {
            throw new BusinessException(AddressErrorCodes.NguonDoiDonViCapTren)
                .WithData("soDoiCha", doiCha.Count)
                .WithData("nguon", danhMuc.Nguon)
                .WithData("schemaVersion", schemaVersion)
                .WithData("muc", string.Join("; ", doiCha.Take(20)));
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
    ///
    /// 🔒 `legalBasis` (căn cứ pháp lý của lượt nạp, = `danhMuc.Nguon`) **BẮT BUỘC** và được truyền vào
    /// `DoiTen` (`D-239`): thiếu căn cứ ⇒ `DoiTen` ném `0009`, không đổi tên mù.
    /// </summary>
    public static IReadOnlyList<AdministrativeUnit> ApDung(
        KeHoachDongBo keHoach,
        string legalBasis,
        Guid? tenantId = null)
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
                        legalBasis: legalBasis,
                        tenantId: tenantId));
                    break;

                case LoaiThayDoi.DoiTen:
                    td.HienCo!.DoiTen(td.Nguon!.Name, legalBasis);
                    break;

                case LoaiThayDoi.MoLai:
                    td.HienCo!.MoLai(legalBasis);
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

    /// <summary>
    /// ⚠️ **Overload TƯƠNG THÍCH NGƯỢC** cho call site cũ chưa truyền căn cứ (test của `WP-14`).
    ///
    /// 🔒 **KHÔNG dùng cho code mới**: code sản phẩm (worker, `ImportDatasetAsync`) **PHẢI** gọi overload có
    /// `legalBasis = danhMuc.Nguon` để `DoiTen` ghi đúng căn cứ pháp lý (`D-239`). Overload này chỉ tồn tại
    /// để không phá build khi vùng ghi của vai `be` không bao gồm `test/**`; QA thay call site cũ rồi xoá nó.
    /// </summary>
    public static IReadOnlyList<AdministrativeUnit> ApDung(KeHoachDongBo keHoach, Guid? tenantId = null)
        => ApDung(keHoach, "Tương thích ngược: kế hoạch đã có lý do thay đổi", tenantId);

    /// <summary>Khoá so khớp: **cấp + mã** (trong cùng một phiên bản schema).</summary>
    private static string Khoa(AdministrativeLevel level, string code)
        => $"{level}|{code.Trim()}";

    /// <summary>Chuẩn hoá `ParentCode` để so: `Trim`, `null` ≡ rỗng (cấp tỉnh).</summary>
    private static string ChuanHoaCha(string? parentCode)
        => string.IsNullOrWhiteSpace(parentCode) ? string.Empty : parentCode.Trim();

    /// <summary>
    /// **Hàng rào 0b** (`D-81`): kiểm tính toàn vẹn nguồn **một lượt** trước khi dựng kế hoạch.
    /// Sai ⇒ `0605` (không phải `0002` của `TaoMoi`) để worker/import ghi dòng lô **`BiChan`**, không phải `Loi`.
    /// </summary>
    private static void KiemTraToanVenNguon(
        IReadOnlyList<DonViNguon> nguon,
        string nguonTen,
        int schemaVersion)
    {
        var daGap = new HashSet<string>(StringComparer.Ordinal);
        var trung = new List<string>();

        foreach (var dv in nguon)
        {
            var ma = (dv.Code ?? string.Empty).Trim();
            var ten = dv.Name?.Trim();
            var cha = dv.ParentCode?.Trim();

            var doDai = AdministrativeSchemaVersion.DoDaiMa(dv.Level);

            if (ma.Length != doDai || !ma.All(char.IsAsciiDigit))
            {
                throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                    .WithData("lyDo", $"mã '{ma}' sai định dạng cho cấp {dv.Level} (cần {doDai} chữ số)")
                    .WithData("ma", ma)
                    .WithData("nguon", nguonTen)
                    .WithData("schemaVersion", schemaVersion);
            }

            if (string.IsNullOrWhiteSpace(ten))
            {
                throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                    .WithData("lyDo", $"mã '{ma}' có tên rỗng")
                    .WithData("ma", ma)
                    .WithData("nguon", nguonTen)
                    .WithData("schemaVersion", schemaVersion);
            }

            if (dv.Level != AdministrativeLevel.Tinh && string.IsNullOrWhiteSpace(cha))
            {
                throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                    .WithData("lyDo", $"mã '{ma}' ở cấp {dv.Level} thiếu ParentCode")
                    .WithData("ma", ma)
                    .WithData("nguon", nguonTen)
                    .WithData("schemaVersion", schemaVersion);
            }

            var khoa = Khoa(dv.Level, ma);
            if (!daGap.Add(khoa))
            {
                trung.Add($"{dv.Level}:{ma}");
            }
        }

        if (trung.Count > 0)
        {
            var maTrung = trung.Distinct(StringComparer.Ordinal).Take(20).ToList();

            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "danh mục nguồn có khoá (Level, Code) TRÙNG")
                .WithData("soMaTrung", trung.Count)
                .WithData("maTrung", string.Join(", ", maTrung))
                .WithData("nguon", nguonTen)
                .WithData("schemaVersion", schemaVersion);
        }
    }
}

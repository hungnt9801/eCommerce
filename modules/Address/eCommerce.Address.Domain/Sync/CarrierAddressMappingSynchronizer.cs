using eCommerce.Address.Carrier;
using Volo.Abp;

namespace eCommerce.Address.Sync;

/// <summary>Một đơn vị địa chỉ **phía hãng vận chuyển** (dữ liệu tải về, chưa phải entity).</summary>
public sealed record DonViDiaChiHang(
    string CarrierCode,
    string CarrierLevel,
    string CarrierAddressCode,
    string CarrierAddressName);

/// <summary>Danh mục địa chỉ của một hãng trong một lần tải.</summary>
public sealed record DanhMucDiaChiHang(
    string CarrierCode,
    IReadOnlyList<DonViDiaChiHang> DonVis,
    string Nguon,
    DateTime TaiLucUtc);

/// <summary>
/// **Adapter cho MỘT hãng vận chuyển** — cắm được, mỗi hãng một hiện thực.
///
/// 🚧 VÌ SAO CHỈ CÓ INTERFACE + CHƯA CÓ HIỆN THỰC THẬT (trung thực, không giấu):
///   `ADDR-002` cần **endpoint + tài liệu danh mục của GHN và GHTK**, hiện **chưa có**. Viết hiện thực
///   theo suy đoán sẽ tạo ra adapter **gọi sai** và **trông như đã xong** — đúng loại lỗi tệ nhất.
///   Vì vậy: **toàn bộ phần luật** (kế hoạch, hàng rào, áp dụng) đã xong và **có test**; phần còn lại
///   là **một lớp mỏng** đọc JSON của từng hãng. Khi có tài liệu ⇒ chỉ viết `IKhungDiaChiHangSource`
///   cho hãng đó, **không phải sửa luật**.
/// </summary>
public interface IKhungDiaChiHangSource
{
    /// <summary>Mã hãng mà adapter này phục vụ (khớp `CarrierAddressMapping.CarrierCode`).</summary>
    string CarrierCode { get; }

    Task<DanhMucDiaChiHang> TaiDanhMucAsync(CancellationToken cancellationToken = default);
}

/// <summary>Loại việc cần làm với một mã địa chỉ của hãng.</summary>
public enum LoaiViecMaHang
{
    /// <summary>Tên phía hãng đổi ⇒ cập nhật tên (entity tự hạ mức tin cậy).</summary>
    CapNhatTen = 0,

    /// <summary>Mã biến mất khỏi danh mục hãng ⇒ đánh dấu NGHI NGỜ (không xoá).</summary>
    DanhDauNghiNgo = 1,

    /// <summary>Mã MỚI chưa có ánh xạ ⇒ **cần NGƯỜI ánh xạ** (hệ thống KHÔNG tự đoán).</summary>
    CanNguoiAnhXa = 2
}

/// <summary>Một việc cụ thể trong kế hoạch đồng bộ mã hãng.</summary>
/// <param name="CarrierLevel">
/// Cấp phía hãng của đơn vị trong danh mục — cần để ghi **đủ** cấp/mã/tên vào bảng con
/// `AddrCarrierSyncRunUnmappedCodes` (`ADDR-002` §4.2, `D-88`). `null` cho việc `DanhDauNghiNgo`
/// (đơn vị đã biến mất khỏi danh mục nên không có cấp mới); additive trên record **nội bộ**.
/// </param>
public sealed record ViecMaHang(
    LoaiViecMaHang Loai,
    string CarrierAddressCode,
    CarrierAddressMapping? HienCo,
    string? TenMoi,
    string? LyDo,
    string? CarrierLevel = null);

/// <summary>Kế hoạch đồng bộ mã hãng — TÍNH TRƯỚC, chưa áp dụng gì.</summary>
public sealed record KeHoachDongBoMaHang(IReadOnlyList<ViecMaHang> Viec)
{
    public int SoCapNhatTen => Viec.Count(x => x.Loai == LoaiViecMaHang.CapNhatTen);
    public int SoDanhDauNghiNgo => Viec.Count(x => x.Loai == LoaiViecMaHang.DanhDauNghiNgo);
    public int SoCanNguoiAnhXa => Viec.Count(x => x.Loai == LoaiViecMaHang.CanNguoiAnhXa);
    public bool CoViec => Viec.Count > 0;
}

/// <summary>
/// **LÕI THUẦN đồng bộ danh mục mã địa chỉ hãng vận chuyển** (`ADDR-002`) — không I/O.
///
/// 🚨 GIỚI HẠN CỐT LÕI (đây là điều quan trọng nhất cần hiểu về lớp này):
///   Danh mục của hãng **CHỈ** cho biết **mã và tên phía hãng**. Nó **KHÔNG** cho biết mã đó ứng với
///   **đơn vị hành chính nào của ta**. Mà `CarrierAddressMapping` **bắt buộc** có đơn vị đích.
///   ⇒ Hệ thống **KHÔNG THỂ** tự tạo ánh xạ mới. Muốn tạo thì phải **khớp theo tên** — và khớp theo tên
///   chỉ là **gợi ý** (dấu tiếng Việt, "Phường" vs "Xã", tỉnh sáp nhập) ⇒ **sai địa chỉ giao hàng,
///   không thu hồi được**.
///   Vì vậy lớp này **KHÔNG BAO GIỜ tạo ánh xạ mới**. Nó chỉ:
///     • cập nhật **tên** khi hãng đổi tên (đối tượng đích đã có, không phải đoán),
///     • đánh dấu **NGHI NGỜ** những mã đã biến mất (để chúng **không dùng được** cho vận đơn mới),
///     • và **báo** danh sách mã mới **cần người ánh xạ**.
///
/// 🔒 BỐN HÀNG RÀO chống "đồng bộ phá hoại" (cùng cấu trúc như đồng bộ ĐVHC):
///   danh mục rỗng · quá ít đơn vị · **đánh dấu nghi ngờ vượt 20%**. Lý do: đánh dấu nghi ngờ làm ánh xạ
///   **không dùng được** ⇒ nếu hãng trả về **một phần** mà ta áp dụng thì **toàn bộ vận đơn mới sẽ
///   fail-closed** (`eCommerce:Address:0502`) ⇒ **sập luồng giao hàng** chỉ vì một lần gọi API hỏng.
/// </summary>
public static class CarrierAddressMappingSynchronizer
{
    /// <summary>Tỉ lệ ánh xạ bị đánh dấu nghi ngờ tối đa cho phép (mặc định 20%).</summary>
    public const double NguongNghiNgoMacDinh = 0.20;

    /// <summary>Số đơn vị tối thiểu của một danh mục hãng được coi là hợp lệ.</summary>
    public const int SoDonViToiThieuMacDinh = 5;

    /// <summary>
    /// **MỘT vòng quét chuẩn hoá + kiểm mã hãng** (`D-84`/`D-91`) — dùng chung bởi **worker** (trước khi băm
    /// `SourceHash`) và <see cref="LapKeHoach"/> (ở đầu hàm) ⇒ **một** hiện thực, không hai logic.
    ///
    /// 🔒 Luật:
    ///   • phần tử `null` **hoặc** `CarrierAddressCode` `null`/rỗng/chỉ khoảng trắng ⇒ đếm `soMaRong`
    ///     (**không** bỏ qua im lặng, **không** ném `0505` ở cửa đồng bộ — `D-91`);
    ///   • mã còn lại **chỉ** `Trim()` (`D-84`) ⇒ gom trùng `Ordinal` (giữ hoa–thường, số 0 đầu, khoảng trắng giữa);
    ///   • `soMaRong > 0` **hoặc** có mã trùng ⇒ NÉM **một** `BusinessException`
    ///     <see cref="CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe"/> (0509) mang cả hai số + tối đa 20 mã
    ///     trùng sắp `Ordinal` — **0** việc, từ chối cả lô.
    ///
    /// 📌 Trả danh sách **đã sạch**: không phần tử `null`, mã đã `Trim`, mỗi mã xuất hiện **một** lần
    /// (giữ bản ghi **đầu tiên** khi trùng — nhưng trùng thì đã ném trước khi trả).
    /// </summary>
    public static IReadOnlyList<DonViDiaChiHang> ChuanHoaVaKiemMa(DanhMucDiaChiHang danhMuc)
    {
        ArgumentNullException.ThrowIfNull(danhMuc);

        var soLanTheoMa = new Dictionary<string, int>(StringComparer.Ordinal);
        var daChuanHoa = new List<DonViDiaChiHang>(danhMuc.DonVis?.Count ?? 0);
        var soMaRong = 0;

        foreach (var dv in danhMuc.DonVis ?? [])
        {
            if (dv is null || string.IsNullOrWhiteSpace(dv.CarrierAddressCode))
            {
                soMaRong++;
                continue;
            }

            var ma = dv.CarrierAddressCode.Trim();

            soLanTheoMa[ma] = soLanTheoMa.TryGetValue(ma, out var soLan) ? soLan + 1 : 1;

            if (soLanTheoMa[ma] == 1)
            {
                daChuanHoa.Add(dv with { CarrierAddressCode = ma });
            }
        }

        var maTrung = soLanTheoMa
            .Where(x => x.Value > 1)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // 📌 `soMaTrung` = số **đơn vị** nằm trong nhóm trùng (hai dòng `" P1 "` + `"P1"` ⇒ 2 — TC-10).
        var soMaTrung = soLanTheoMa.Where(x => x.Value > 1).Sum(x => x.Value);

        if (soMaRong > 0 || maTrung.Count > 0)
        {
            throw new BusinessException(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe)
                .WithData("carrierCode", (danhMuc.CarrierCode ?? string.Empty).Trim().ToUpperInvariant())
                .WithData("soMaRong", soMaRong)
                .WithData("soMaTrung", soMaTrung)
                .WithData("maTrung", maTrung.Take(20).ToList())
                .WithData("nguon", danhMuc.Nguon);
        }

        return daChuanHoa;
    }

    public static KeHoachDongBoMaHang LapKeHoach(
        string carrierCode,
        IReadOnlyList<CarrierAddressMapping> anhXaHienCo,
        DanhMucDiaChiHang danhMuc,
        double nguongNghiNgo = NguongNghiNgoMacDinh,
        int soDonViToiThieu = SoDonViToiThieuMacDinh)
    {
        ArgumentNullException.ThrowIfNull(anhXaHienCo);
        ArgumentNullException.ThrowIfNull(danhMuc);

        var hang = (carrierCode ?? string.Empty).Trim().ToUpperInvariant();

        if (!string.Equals(hang, (danhMuc.CarrierCode ?? string.Empty).Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            // 🚨 Không được đồng bộ danh mục của hãng A lên ánh xạ của hãng B: mã ở hai hãng KHÔNG
            // cùng ý nghĩa. Nếu lọt, toàn bộ ánh xạ của hãng B sẽ bị đánh dấu nghi ngờ OAN.
            throw new BusinessException(CarrierAddressErrorCodes.DanhMucHangRong)
                .WithData("lyDo", "Mã hãng của danh mục KHÔNG khớp mã hãng đang đồng bộ.")
                .WithData("carrierCodeDoiSync", hang)
                .WithData("carrierCodeCuaDanhMuc", danhMuc.CarrierCode ?? string.Empty);
        }

        var cuaTa = anhXaHienCo
            .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // ── VÒNG QUÉT `D-84`/`D-91` — TRƯỚC hàng rào 1: mã rỗng/`null`/phần tử `null`/trùng sau `Trim`
        //    ⇒ NÉM **một** `0509` (từ chối cả lô). `theoMa` dựng **sau** khi đã kiểm trùng (không ghi đè
        //    im lặng như code cũ `theoMa[...] = dv`).
        var daChuanHoa = ChuanHoaVaKiemMa(danhMuc);

        var theoMa = daChuanHoa.ToDictionary(
            x => x.CarrierAddressCode, x => x, StringComparer.Ordinal);

        // ── HÀNG RÀO 1: danh mục rỗng ──
        if (theoMa.Count == 0)
        {
            throw new BusinessException(CarrierAddressErrorCodes.DanhMucHangRong)
                .WithData("carrierCode", hang)
                .WithData("nguon", danhMuc.Nguon);
        }

        // ── HÀNG RÀO 2: quá ít đơn vị ──
        if (theoMa.Count < soDonViToiThieu)
        {
            throw new BusinessException(CarrierAddressErrorCodes.DanhMucHangQuaIt)
                .WithData("soLuong", theoMa.Count)
                .WithData("toiThieu", soDonViToiThieu)
                .WithData("nguon", danhMuc.Nguon);
        }

        var viec = new List<ViecMaHang>();

        // ① Tên đổi ⇒ cập nhật tên (đối tượng đích đã có sẵn, không phải đoán gì).
        // ② Mã mới ⇒ CẦN NGƯỜI ÁNH XẠ (hệ thống không tự tạo ánh xạ — xem ghi chú đầu lớp).
        foreach (var anhXa in cuaTa)
        {
            if (!theoMa.TryGetValue(anhXa.CarrierAddressCode, out var moi))
            {
                continue;   // mã biến mất — xử ở bước ③
            }

            if (!string.Equals(anhXa.CarrierAddressName, moi.CarrierAddressName?.Trim(), StringComparison.Ordinal))
            {
                // Giữ cho MỌI ánh xạ kể cả `NghiNgo` (tên mới giúp người xác minh — `D-87` ①(2)).
                viec.Add(new ViecMaHang(LoaiViecMaHang.CapNhatTen, anhXa.CarrierAddressCode, anhXa,
                    moi.CarrierAddressName, $"Tên hãng đổi: '{anhXa.CarrierAddressName}' → '{moi.CarrierAddressName}'.",
                    moi.CarrierLevel));
            }
        }

        var maDaCoAnhXa = cuaTa.Select(x => x.CarrierAddressCode).ToHashSet(StringComparer.Ordinal);

        foreach (var ma in theoMa.Keys.Where(k => !maDaCoAnhXa.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            viec.Add(new ViecMaHang(LoaiViecMaHang.CanNguoiAnhXa, ma, null, theoMa[ma].CarrierAddressName,
                "Mã mới chưa có ánh xạ — cần người ánh xạ sang đơn vị hành chính. Hệ thống KHÔNG tự đoán theo tên.",
                theoMa[ma].CarrierLevel));
        }

        // ③ Mã biến mất ⇒ đánh dấu NGHI NGỜ (KHÔNG xoá: vận đơn cũ còn tham chiếu).
        //    `D-87` ①(1): ánh xạ ĐÃ `NghiNgo` mà vẫn vắng mặt ⇒ **không** sinh lại việc, **không** đếm
        //    (vẫn còn bản ghi, vẫn tra được — NFR-009) ⇒ không đếm lặp gây `0508` vĩnh viễn.
        foreach (var anhXa in cuaTa.Where(x =>
                     !theoMa.ContainsKey(x.CarrierAddressCode)
                     && x.Status != CarrierMappingStatus.NghiNgo))
        {
            viec.Add(new ViecMaHang(LoaiViecMaHang.DanhDauNghiNgo, anhXa.CarrierAddressCode, anhXa, null,
                "Mã không còn trong danh mục hãng.", anhXa.CarrierLevel));
        }

        // ── HÀNG RÀO 3 (`D-87`): CHỈ đếm **chuyển trạng thái MỚI** sang `NghiNgo` trong lượt.
        //    Tử số = số việc `CapNhatTen`/`DanhDauNghiNgo` trên ánh xạ **chưa** `NghiNgo`;
        //    mẫu số = số ánh xạ của hãng **chưa** `NghiNgo`. So với mẫu số TOÀN BỘ sẽ chặn oan (TC-25).
        var soNghiNgo = viec.Count(x =>
            (x.Loai == LoaiViecMaHang.DanhDauNghiNgo || x.Loai == LoaiViecMaHang.CapNhatTen)
            && x.HienCo!.Status != CarrierMappingStatus.NghiNgo);

        var tongDangDung = cuaTa.Count(x => x.Status != CarrierMappingStatus.NghiNgo);
        var soDaNghiNgoTruoc = cuaTa.Count(x => x.Status == CarrierMappingStatus.NghiNgo);

        if (tongDangDung > 0 && soNghiNgo > 0)
        {
            var tiLe = (double)soNghiNgo / tongDangDung;

            if (tiLe > nguongNghiNgo)
            {
                throw new BusinessException(CarrierAddressErrorCodes.BienMatHangLoat)
                    .WithData("soNghiNgo", soNghiNgo)
                    .WithData("tongDangDung", tongDangDung)
                    .WithData("soDaNghiNgoTruoc", soDaNghiNgoTruoc)
                    .WithData("tiLe", Math.Round(tiLe, 4))
                    .WithData("nguong", nguongNghiNgo)
                    .WithData("carrierCode", hang)
                    .WithData("nguon", danhMuc.Nguon);
            }
        }

        return new KeHoachDongBoMaHang(viec);
    }

    /// <summary>
    /// **Áp dụng kế hoạch** (vẫn THUẦN: không I/O). Trả về các ánh xạ **cần cập nhật**;
    /// việc <see cref="LoaiViecMaHang.CanNguoiAnhXa"/> **không** tạo bản ghi nào.
    /// </summary>
    public static IReadOnlyList<CarrierAddressMapping> ApDung(KeHoachDongBoMaHang keHoach, DateTime taiLucUtc)
    {
        ArgumentNullException.ThrowIfNull(keHoach);

        var canCapNhat = new List<CarrierAddressMapping>();

        foreach (var v in keHoach.Viec)
        {
            switch (v.Loai)
            {
                case LoaiViecMaHang.CapNhatTen:
                    // Entity TỰ hạ mức tin cậy xuống NghiNgo khi tên đổi (cơ sở xác minh cũ đã thay đổi).
                    v.HienCo!.CapNhatTuDongBo(v.TenMoi!, taiLucUtc);
                    canCapNhat.Add(v.HienCo);
                    break;

                case LoaiViecMaHang.DanhDauNghiNgo:
                    v.HienCo!.DanhDauNghiNgo();
                    canCapNhat.Add(v.HienCo);
                    break;

                case LoaiViecMaHang.CanNguoiAnhXa:
                    // KHÔNG tạo bản ghi: thiếu đơn vị đích ⇒ tạo bản ghi sẽ là ĐOÁN.
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(keHoach), v.Loai, "Loại việc không xử lý được.");
            }
        }

        return canCapNhat;
    }
}

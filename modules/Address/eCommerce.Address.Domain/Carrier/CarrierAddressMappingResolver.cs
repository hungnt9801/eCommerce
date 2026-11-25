using Volo.Abp;

namespace eCommerce.Address.Carrier;

/// <summary>Kết quả tra một mã địa chỉ hãng vận chuyển.</summary>
public sealed record KetQuaTraMaHang(CarrierAddressMapping AnhXa, AdministrativeUnit DonVi);

/// <summary>Kết quả tra **ngược** đơn vị của ta → mã địa chỉ hãng (`D-89`).</summary>
public sealed record KetQuaTraNguoc(CarrierAddressMapping AnhXa, AdministrativeUnit DonVi);

/// <summary>Loại sai lệch phát hiện khi đồng bộ lại danh mục của hãng.</summary>
public enum LoaiSaiLech
{
    /// <summary>Mã cũ **biến mất** khỏi danh mục hãng — ánh xạ cũ có thể trỏ sai.</summary>
    MaBienMat = 0,

    /// <summary>Tên của cùng mã **đã đổi** — cần xem lại ánh xạ.</summary>
    TenDoi = 1,

    /// <summary>Mã **mới** chưa có ánh xạ nào.</summary>
    MaMoiChuaAnhXa = 2
}

/// <summary>Một sai lệch cụ thể.</summary>
public sealed record SaiLechDiaChiHang(LoaiSaiLech Loai, string CarrierAddressCode, string? MoTa);

/// <summary>
/// **Tra &amp; kiểm tra ánh xạ mã địa chỉ hãng vận chuyển** — **hàm THUẦN** (không DI, không DB).
///
/// 🎯 NGUYÊN TẮC XUYÊN SUỐT: **FAIL-CLOSED**.
///   Không tra được, hoặc ánh xạ chưa xác minh ⇒ **KHÔNG** được tạo vận đơn. Tuyệt đối **không**
///   "chọn gần đúng theo tên", vì giao hàng tới địa chỉ **suy đoán** là lỗi **không thể thu hồi**:
///   hàng đã đi, và **không có cảnh báo nào** để biết là đã sai.
/// </summary>
public static class CarrierAddressMappingResolver
{
    /// <summary>
    /// Tra mã địa chỉ của hãng → đơn vị hành chính của ta.
    /// 🔒 Chỉ nhận ánh xạ **`DaXacMinh`** và đơn vị đích **còn hiệu lực**.
    /// </summary>
    public static KetQuaTraMaHang Tra(
        IEnumerable<CarrierAddressMapping> anhXa,
        IEnumerable<AdministrativeUnit> donVi,
        string carrierCode,
        string carrierAddressCode)
    {
        ArgumentNullException.ThrowIfNull(anhXa);
        ArgumentNullException.ThrowIfNull(donVi);

        if (string.IsNullOrWhiteSpace(carrierCode) || string.IsNullOrWhiteSpace(carrierAddressCode))
        {
            throw new BusinessException(CarrierAddressErrorCodes.ThieuThongTinBatBuoc);
        }

        var hang = carrierCode.Trim().ToUpperInvariant();
        var ma = carrierAddressCode.Trim();

        // ⚠️ Phải scope theo HÃNG: cùng một mã có thể là địa chỉ khác nhau ở hai hãng.
        var ungVien = anhXa
            .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.CarrierAddressCode, ma, StringComparison.Ordinal))
            .ToList();

        if (ungVien.Count == 0)
        {
            throw new BusinessException(CarrierAddressErrorCodes.KhongTimThayMaHang)
                .WithData("carrierCode", hang)
                .WithData("carrierAddressCode", ma);
        }

        if (ungVien.Count > 1)
        {
            // Bất biến: một mã hãng chỉ được ánh xạ MỘT lần. Nhiều ánh xạ ⇒ không biết chọn cái nào
            // ⇒ phải ném lỗi, KHÔNG chọn bừa cái đầu tiên.
            throw new BusinessException(CarrierAddressErrorCodes.AnhXaTrungLap)
                .WithData("carrierCode", hang)
                .WithData("carrierAddressCode", ma)
                .WithData("soAnhXa", ungVien.Count);
        }

        var mapping = ungVien[0];

        if (mapping.Status != CarrierMappingStatus.DaXacMinh)
        {
            throw new BusinessException(CarrierAddressErrorCodes.MaChuaXacMinh)
                .WithData("carrierCode", hang)
                .WithData("carrierAddressCode", ma)
                .WithData("trangThai", mapping.Status.ToString())
                .WithData("cachKhop", mapping.MatchMethod.ToString());
        }

        var dich = donVi.FirstOrDefault(u => u.Id == mapping.OurAdministrativeUnitId)
            ?? throw new BusinessException(CarrierAddressErrorCodes.KhongTimThayMaHang)
                .WithData("lyDo", "không tìm thấy đơn vị hành chính đích theo Id")
                .WithData("ourAdministrativeUnitId", mapping.OurAdministrativeUnitId);

        if (!dich.IsActive)
        {
            // Đơn vị đích đã bị sáp nhập ⇒ dùng nó cho vận đơn MỚI là gửi tới đơn vị không còn tồn tại.
            throw new BusinessException(CarrierAddressErrorCodes.DonViDichDaDong)
                .WithData("ourCode", dich.Code)
                .WithData("ourName", dich.Name);
        }

        return new KetQuaTraMaHang(mapping, dich);
    }

    /// <summary>
    /// **Tra NGƯỢC** đơn vị của ta → mã địa chỉ hãng, cho `SHP-001`/`SHP-004` tạo vận đơn (`D-89`).
    /// 🔒 **FAIL-CLOSED**: không trả `null`, **không** chọn bừa "cái đầu/mới nhất".
    ///
    /// Thứ tự luật:
    ///   1. `carrierCode` rỗng **hoặc** `Guid.Empty` ⇒ NÉM `0505`;
    ///   2. không thấy đơn vị đích ⇒ NÉM `0501`;
    ///   3. đơn vị đích `IsActive = false` ⇒ NÉM `0504`;
    ///   4. không có ánh xạ nào cùng hãng + cùng đích ⇒ NÉM `0501`;
    ///   5. **&gt; 1** ánh xạ `DaXacMinh` cùng đích ⇒ NÉM <see cref="CarrierAddressErrorCodes.AnhXaNguocKhongDuyNhat"/> (`0510`);
    ///   6. đúng **0** `DaXacMinh` (chỉ `ChuaXacMinh`/`NghiNgo`) ⇒ NÉM `0502`;
    ///   7. đúng **1** `DaXacMinh` ⇒ trả nó + đơn vị đích.
    /// </summary>
    public static KetQuaTraNguoc TraNguoc(
        IEnumerable<CarrierAddressMapping> anhXa,
        IEnumerable<AdministrativeUnit> donVi,
        string carrierCode,
        Guid ourAdministrativeUnitId)
    {
        ArgumentNullException.ThrowIfNull(anhXa);
        ArgumentNullException.ThrowIfNull(donVi);

        if (string.IsNullOrWhiteSpace(carrierCode) || ourAdministrativeUnitId == Guid.Empty)
        {
            throw new BusinessException(CarrierAddressErrorCodes.ThieuThongTinBatBuoc)
                .WithData("carrierCode", carrierCode)
                .WithData("ourAdministrativeUnitId", ourAdministrativeUnitId);
        }

        var hang = carrierCode.Trim().ToUpperInvariant();

        var dich = donVi.FirstOrDefault(u => u.Id == ourAdministrativeUnitId)
            ?? throw new BusinessException(CarrierAddressErrorCodes.KhongTimThayMaHang)
                .WithData("lyDo", "không tìm thấy đơn vị đích theo Id")
                .WithData("ourAdministrativeUnitId", ourAdministrativeUnitId);

        if (!dich.IsActive)
        {
            throw new BusinessException(CarrierAddressErrorCodes.DonViDichDaDong)
                .WithData("ourCode", dich.Code)
                .WithData("ourName", dich.Name);
        }

        // Mọi `CarrierLevel` đều tính — khoá tra ngược là (hãng, đơn vị đích), không phải cấp.
        var ungVien = anhXa
            .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase)
                        && x.OurAdministrativeUnitId == ourAdministrativeUnitId)
            .ToList();

        if (ungVien.Count == 0)
        {
            throw new BusinessException(CarrierAddressErrorCodes.KhongTimThayMaHang)
                .WithData("carrierCode", hang)
                .WithData("ourAdministrativeUnitId", ourAdministrativeUnitId)
                .WithData("soAnhXa", 0);
        }

        var daXacMinh = ungVien.Where(x => x.Status == CarrierMappingStatus.DaXacMinh).ToList();

        if (daXacMinh.Count > 1)
        {
            // 🚨 Nhiều mã hãng cùng trỏ một đơn vị của ta là dữ liệu **hợp lệ** để đọc xuôi, nhưng để
            // TẠO VẬN ĐƠN thì không biết chọn mã nào ⇒ phải NÉM, KHÔNG chọn bừa (D-89).
            throw new BusinessException(CarrierAddressErrorCodes.AnhXaNguocKhongDuyNhat)
                .WithData("carrierCode", hang)
                .WithData("ourAdministrativeUnitId", ourAdministrativeUnitId)
                .WithData("soAnhXa", daXacMinh.Count)
                .WithData("carrierAddressCode", daXacMinh
                    .Select(x => x.CarrierAddressCode)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Take(20)
                    .ToList());
        }

        if (daXacMinh.Count == 0)
        {
            // Ứng viên `ChuaXacMinh`/`NghiNgo` (cùng hãng + đích) **không** dùng được cho vận đơn — cùng
            // luật "chỉ `DaXacMinh`" của `Tra` (fail-closed).
            throw new BusinessException(CarrierAddressErrorCodes.MaChuaXacMinh)
                .WithData("carrierCode", hang)
                .WithData("ourAdministrativeUnitId", ourAdministrativeUnitId)
                .WithData("soAnhXa", ungVien.Count)
                .WithData("trangThai", string.Join(
                    ", ",
                    ungVien.Select(x => x.Status.ToString()).Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)));
        }

        return new KetQuaTraNguoc(daXacMinh[0], dich);
    }

    /// <summary>Tra **mềm** — trả `null` thay vì ném lỗi. Dùng cho báo cáo/đối chiếu, KHÔNG cho vận đơn.</summary>
    public static CarrierAddressMapping? ThuTra(
        IEnumerable<CarrierAddressMapping> anhXa, string carrierCode, string carrierAddressCode)
    {
        ArgumentNullException.ThrowIfNull(anhXa);

        var hang = carrierCode?.Trim().ToUpperInvariant();
        var ma = carrierAddressCode?.Trim();

        return anhXa.FirstOrDefault(x =>
            string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.CarrierAddressCode, ma, StringComparison.Ordinal));
    }

    /// <summary>
    /// **Phát hiện sai lệch** khi hãng đồng bộ lại danh mục: mã biến mất · tên đổi · mã mới.
    /// 🎯 Dùng cho báo cáo vận hành: ánh xạ cũ có thể trỏ **sai** sau khi hãng sáp nhập.
    /// </summary>
    /// <param name="anhXaHienCo">Ánh xạ ta đang giữ.</param>
    /// <param name="danhMucMoi">Danh mục mới nhất từ hãng: (mã, tên).</param>
    public static IReadOnlyList<SaiLechDiaChiHang> PhatHienSaiLech(
        IEnumerable<CarrierAddressMapping> anhXaHienCo,
        string carrierCode,
        IReadOnlyDictionary<string, string> danhMucMoi)
    {
        ArgumentNullException.ThrowIfNull(anhXaHienCo);
        ArgumentNullException.ThrowIfNull(danhMucMoi);

        var hang = (carrierCode ?? string.Empty).Trim().ToUpperInvariant();
        var hienCo = anhXaHienCo
            .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ketQua = new List<SaiLechDiaChiHang>();

        // ① Mã cũ biến mất / ② tên đổi
        foreach (var m in hienCo)
        {
            if (!danhMucMoi.TryGetValue(m.CarrierAddressCode, out var tenMoi))
            {
                ketQua.Add(new SaiLechDiaChiHang(LoaiSaiLech.MaBienMat, m.CarrierAddressCode,
                    $"Mã '{m.CarrierAddressCode}' ({m.CarrierAddressName}) không còn trong danh mục hãng."));
                continue;
            }

            if (!string.Equals(m.CarrierAddressName, tenMoi?.Trim(), StringComparison.Ordinal))
            {
                ketQua.Add(new SaiLechDiaChiHang(LoaiSaiLech.TenDoi, m.CarrierAddressCode,
                    $"Mã '{m.CarrierAddressCode}': tên cũ '{m.CarrierAddressName}' → tên mới '{tenMoi}'."));
            }
        }

        // ③ Mã mới chưa ánh xạ
        var daCo = hienCo.Select(x => x.CarrierAddressCode).ToHashSet(StringComparer.Ordinal);

        foreach (var maMoi in danhMucMoi.Keys.Where(k => !daCo.Contains(k)))
        {
            ketQua.Add(new SaiLechDiaChiHang(LoaiSaiLech.MaMoiChuaAnhXa, maMoi,
                $"Mã mới '{maMoi}' ({danhMucMoi[maMoi]}) chưa có ánh xạ."));
        }

        return ketQua;
    }

    /// <summary>Danh sách ánh xạ **chưa xác minh** — việc cần người xử lý trước khi dùng được.</summary>
    public static IReadOnlyList<CarrierAddressMapping> LayChuaXacMinh(
        IEnumerable<CarrierAddressMapping> anhXa, string? carrierCode = null)
    {
        ArgumentNullException.ThrowIfNull(anhXa);

        var hang = carrierCode?.Trim().ToUpperInvariant();

        return anhXa
            .Where(x => x.Status != CarrierMappingStatus.DaXacMinh)
            .Where(x => hang is null || string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CarrierCode, StringComparer.Ordinal)
            .ThenBy(x => x.CarrierAddressCode, StringComparer.Ordinal)
            .ToList();
    }
}

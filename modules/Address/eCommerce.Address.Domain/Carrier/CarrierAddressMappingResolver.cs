using Volo.Abp;

namespace eCommerce.Address.Carrier;

/// <summary>Kết quả tra một mã địa chỉ hãng vận chuyển.</summary>
public sealed record KetQuaTraMaHang(CarrierAddressMapping AnhXa, AdministrativeUnit DonVi);

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

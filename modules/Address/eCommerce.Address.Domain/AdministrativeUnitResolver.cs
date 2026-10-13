using Volo.Abp;

namespace eCommerce.Address;

/// <summary>
/// **Tra cứu &amp; xác minh địa chỉ** theo phiên bản schema — **hàm thuần**, test được không cần DB.
///
/// 🎯 ĐÂY LÀ CHỖ CHẶN LỖI IM LẶNG QUAN TRỌNG NHẤT của module:
///   • tra mã **kèm phiên bản** (cùng mã khác phiên bản là **hai đơn vị khác nhau**);
///   • xã **phải thuộc** tỉnh đã khai báo;
///   • **FAIL-CLOSED**: không tra được hoặc không khớp ⇒ **KHÔNG hợp lệ**, tuyệt đối không "đoán" và
///     cũng không trả về đơn vị gần đúng. Địa chỉ sai đi thẳng vào **vận đơn** và **hoá đơn**.
/// </summary>
public static class AdministrativeUnitResolver
{
    /// <summary>
    /// Tra một đơn vị theo **(phiên bản, cấp, mã)**.
    /// 🚨 Ném lỗi khi không tìm thấy — KHÔNG trả `null` để nơi gọi không thể vô tình bỏ qua.
    /// </summary>
    public static AdministrativeUnit TraTheoMa(
        IEnumerable<AdministrativeUnit> donVi,
        int schemaVersion,
        AdministrativeLevel level,
        string code)
    {
        ArgumentNullException.ThrowIfNull(donVi);
        AdministrativeSchemaVersion.TheoSo(schemaVersion);   // xác thực phiên bản

        var ma = AdministrativeUnit.ChuanHoaMa(level, code);

        var timThay = donVi.FirstOrDefault(u =>
            u.SchemaVersion == schemaVersion && u.Level == level && u.Code == ma);

        if (timThay is null)
        {
            throw new BusinessException(AddressErrorCodes.KhongTimThayDonVi)
                .WithData("schemaVersion", schemaVersion)
                .WithData("level", level.ToString())
                .WithData("ma", ma);
        }

        return timThay;
    }

    /// <summary>
    /// Tra **mềm**: trả `null` khi không thấy. Dùng khi chỉ cần HIỂN THỊ dữ liệu lịch sử.
    /// </summary>
    public static AdministrativeUnit? ThuTra(
        IEnumerable<AdministrativeUnit> donVi,
        int schemaVersion,
        AdministrativeLevel level,
        string code)
    {
        ArgumentNullException.ThrowIfNull(donVi);

        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var ma = code.Trim();

        return donVi.FirstOrDefault(u =>
            u.SchemaVersion == schemaVersion && u.Level == level && u.Code == ma);
    }

    /// <summary>
    /// 🚩 **Vì sao phải tra kèm phiên bản** — trả về đơn vị xã theo mã ở **cả hai** phiên bản để thấy rõ
    /// chúng có thể **KHÁC NHAU**. Dùng cho báo cáo/kiểm tra dữ liệu, không dùng cho luồng đặt hàng.
    /// </summary>
    public static IReadOnlyDictionary<int, AdministrativeUnit?> SoSanhMaXaGiuaCacPhienBan(
        IEnumerable<AdministrativeUnit> donVi, string codeXa)
    {
        ArgumentNullException.ThrowIfNull(donVi);
        var ds = donVi.ToList();

        return new Dictionary<int, AdministrativeUnit?>
        {
            [AdministrativeSchemaVersion.V1.Version] = ThuTra(ds, 1, AdministrativeLevel.Xa, codeXa),
            [AdministrativeSchemaVersion.V2.Version] = ThuTra(ds, 2, AdministrativeLevel.Xa, codeXa)
        };
    }

    /// <summary>
    /// Xác minh địa chỉ dùng cho đơn **MỚI**: `tinhCode` + `xaCode` (schema 2 cấp).
    ///
    /// 🔒 **FAIL-CLOSED** — mọi nhánh không chắc chắn đều trả **KHÔNG hợp lệ**:
    ///   • phiên bản không hỗ trợ · mã sai định dạng · không tra được ·
    ///   • **xã không thuộc tỉnh** đã khai báo ·
    ///   • **mã đã đóng** (sáp nhập) — không dùng cho địa chỉ mới.
    /// </summary>
    public static KetQuaKiemTraDiaChi KiemTraHaiCap(
        IEnumerable<AdministrativeUnit> donVi,
        int schemaVersion,
        string tinhCode,
        string xaCode)
    {
        ArgumentNullException.ThrowIfNull(donVi);

        var phienBan = AdministrativeSchemaVersion.TheoSo(schemaVersion);

        if (phienBan.ChoPhepCapHuyen)
        {
            // Schema 3 cấp: địa chỉ mới nên dùng KiemTraBaCap để kiểm cả huyện.
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.PhienBanKhongHopLe,
                $"Phiên bản {schemaVersion} là 3 cấp — dùng KiemTraBaCap để kiểm đủ cấp huyện.");
        }

        var ds = donVi.ToList();

        var tinh = ThuTra(ds, schemaVersion, AdministrativeLevel.Tinh, tinhCode);

        if (tinh is null)
        {
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.KhongTimThayDonVi,
                $"Không tìm thấy tỉnh '{tinhCode}' ở phiên bản {schemaVersion}.");
        }

        var xa = ThuTra(ds, schemaVersion, AdministrativeLevel.Xa, xaCode);

        if (xa is null)
        {
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.KhongTimThayDonVi,
                $"Không tìm thấy xã '{xaCode}' ở phiên bản {schemaVersion}.",
                tinh);
        }

        // 🚩 Xã phải thuộc ĐÚNG tỉnh đã khai báo. Sai ⇒ từ chối, KHÔNG tự sửa.
        if (!string.Equals(xa.ParentCode, tinh.Code, StringComparison.Ordinal))
        {
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.XaKhongThuocTinh,
                $"Xã '{xa.Code}' ({xa.Name}) thuộc tỉnh '{xa.ParentCode}', không thuộc '{tinh.Code}' ({tinh.Name}).",
                tinh);
        }

        if (!tinh.IsActive || !xa.IsActive)
        {
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.MaDaDong,
                $"Mã đã đóng (sáp nhập) nên không dùng được cho địa chỉ MỚI: " +
                $"tỉnh={tinh.Code}/{tinh.IsActive}, xã={xa.Code}/{xa.IsActive}.",
                tinh);
        }

        return KetQuaKiemTraDiaChi.Ok(tinh, huyen: null, xa);
    }

    /// <summary>
    /// Xác minh địa chỉ **3 cấp cũ** (tỉnh + huyện + xã) — chỉ hợp lệ ở schema **v1**.
    /// 🔒 Dùng cho việc **đọc/đối chiếu dữ liệu lịch sử**, KHÔNG dùng để nhận địa chỉ mới.
    /// </summary>
    public static KetQuaKiemTraDiaChi KiemTraBaCap(
        IEnumerable<AdministrativeUnit> donVi,
        int schemaVersion,
        string tinhCode,
        string huyenCode,
        string xaCode)
    {
        ArgumentNullException.ThrowIfNull(donVi);

        var phienBan = AdministrativeSchemaVersion.TheoSo(schemaVersion);

        if (!phienBan.ChoPhepCapHuyen)
        {
            // 🚩 Đây là quy tắc pháp lý, không phải lựa chọn kỹ thuật: từ 01/7/2025 không còn cấp huyện.
            return KetQuaKiemTraDiaChi.Loi(
                AddressErrorCodes.HuyenKhongDuocPhepOCap2,
                $"Phiên bản {schemaVersion} là 2 cấp — KHÔNG được khai báo cấp huyện.");
        }

        var ds = donVi.ToList();

        var tinh = ThuTra(ds, schemaVersion, AdministrativeLevel.Tinh, tinhCode);
        if (tinh is null)
        {
            return KetQuaKiemTraDiaChi.Loi(AddressErrorCodes.KhongTimThayDonVi,
                $"Không tìm thấy tỉnh '{tinhCode}' ở phiên bản {schemaVersion}.");
        }

        var huyen = ThuTra(ds, schemaVersion, AdministrativeLevel.QuanHuyen, huyenCode);
        if (huyen is null)
        {
            return KetQuaKiemTraDiaChi.Loi(AddressErrorCodes.KhongTimThayDonVi,
                $"Không tìm thấy huyện '{huyenCode}' ở phiên bản {schemaVersion}.", tinh);
        }

        if (!string.Equals(huyen.ParentCode, tinh.Code, StringComparison.Ordinal))
        {
            return KetQuaKiemTraDiaChi.Loi(AddressErrorCodes.HuyenKhongThuocTinh,
                $"Huyện '{huyen.Code}' thuộc tỉnh '{huyen.ParentCode}', không thuộc '{tinh.Code}'.", tinh);
        }

        var xa = ThuTra(ds, schemaVersion, AdministrativeLevel.Xa, xaCode);
        if (xa is null)
        {
            return KetQuaKiemTraDiaChi.Loi(AddressErrorCodes.KhongTimThayDonVi,
                $"Không tìm thấy xã '{xaCode}' ở phiên bản {schemaVersion}.", tinh);
        }

        if (!string.Equals(xa.ParentCode, huyen.Code, StringComparison.Ordinal))
        {
            return KetQuaKiemTraDiaChi.Loi(AddressErrorCodes.XaKhongThuocTinh,
                $"Xã '{xa.Code}' thuộc '{xa.ParentCode}', không thuộc huyện '{huyen.Code}'.", tinh);
        }

        return KetQuaKiemTraDiaChi.Ok(tinh, huyen, xa);
    }
}

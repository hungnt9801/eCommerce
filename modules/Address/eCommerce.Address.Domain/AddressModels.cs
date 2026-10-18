namespace eCommerce.Address;

/// <summary>
/// Trạng thái của một <see cref="Address"/> trong vòng đời chuẩn hoá.
///
/// 🔒 VÌ SAO LƯU SỐ NGUYÊN (không lưu chuỗi): cột `TrangThai` là **int** có `CHECK in (0,1,2,3)` ở DB
/// (`ADDR-004` §5.2) — cùng khuôn `CarrierMappingStatus`
/// (`modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressMapping.cs:58-68`).
///
/// 🚨 `ChuaChuanHoa` **KHÔNG** phải "trạng thái thứ hai của cùng nội dung": nó là **bước** trước khi
/// biết đơn vị hành chính. Sau khi `DaChuanHoa`, nội dung địa lý là **BẤT BIẾN** (`ADR-05`).
/// </summary>
public enum TrangThaiDiaChi
{
    /// <summary>Địa chỉ **THÔ** (chữ tự do), chưa xác định được tỉnh/xã. Chỉ có `DiaChiDayDu`.</summary>
    ChuaChuanHoa = 0,

    /// <summary>Đã gán được **tỉnh + xã** và đã kiểm ĐVHC thành công (fail-closed).</summary>
    DaChuanHoa = 1,

    /// <summary>Đã được người có quyền **xác minh** (có căn cứ).</summary>
    DaXacMinh = 2,

    /// <summary>Job đối chiếu thấy **mã ĐVHC đã đóng** hoặc dữ liệu lệch ⇒ đưa vào hàng đợi vận hành.</summary>
    NghiNgo = 3
}

/// <summary>Nguồn tạo địa chỉ — để truy vết *"địa chỉ này từ đâu ra"* (`ADDR-004` §5.2).</summary>
public enum NguonTaoDiaChi
{
    /// <summary>Khách nhập trên website.</summary>
    Web = 0,

    /// <summary>Webhook đơn từ sàn (dữ liệu thô của sàn).</summary>
    San = 1,

    /// <summary>Nhân viên CSKH nhập.</summary>
    NhanVien = 2,

    /// <summary>Import từ tệp.</summary>
    Import = 3
}

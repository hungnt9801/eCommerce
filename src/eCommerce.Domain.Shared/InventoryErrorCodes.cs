namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN KHO** (`INV-001`) — dải riêng `eCommerce:Inventory:0xxx`.
///
/// 📌 **VÌ SAO LÀ DẢI RIÊNG, KHÔNG DÙNG LẠI MÃ CỦA MIỀN KHÁC:** client/CSKH phân loại sự cố theo mã
/// (bài học `XC-003` **F2** — *"hai nguồn sự thật cho cùng một mã"* đã gây nhầm ở miền kênh).
/// Mã kho phải nói được **việc kho**, không phải việc giá/đơn.
/// </summary>
public static class InventoryErrorCodes
{
    private const string Nhom = "eCommerce:Inventory:";

    /// <summary>🚨 **KHÔNG đủ tồn kho** để đáp ứng yêu cầu (đã có từ GĐ0 — nay là **bản chuẩn DUY NHẤT**).</summary>
    public const string KhongDuTon = Nhom + "0001";

    /// <summary>Kho không tồn tại hoặc **đã ngừng dùng** (`INV-001` E1).</summary>
    public const string KhoNgungHoatDong = Nhom + "0002";

    /// <summary>Chứng từ **không ở trạng thái `Draft`** ⇒ không sửa/ghi sổ/huỷ được.</summary>
    public const string ChungTuKhongOTrangThaiNhap = Nhom + "0010";

    /// <summary>Chứng từ **chưa có dòng** nào ⇒ ghi sổ vô nghĩa (không sinh bút toán nào).</summary>
    public const string ChungTuThieuDong = Nhom + "0011";

    /// <summary>Chứng từ **đã ghi sổ** ⇒ **KHÔNG** sửa nội dung (append-only).</summary>
    public const string ChungTuDaGhiSoKhongSuaDuoc = Nhom + "0012";

    /// <summary>Chứng từ **đã ghi sổ** ⇒ **KHÔNG** huỷ (muốn đảo phải tạo chứng từ ĐẢO).</summary>
    public const string ChungTuDaGhiSoKhongHuyDuoc = Nhom + "0013";

    // ---- `INV-002` — giữ chỗ (reservation) ----

    /// <summary>Lượt giữ chỗ **không còn `Held`** ⇒ không nhả/chốt được lần nữa.</summary>
    public const string GiuChoKhongConGiu = Nhom + "0030";

    /// <summary>Giữ chỗ **đã hết hạn** mà vẫn cố chốt ⇒ nguy cơ **bán hai lần cùng một món**.</summary>
    public const string GiuChoDaHetHan = Nhom + "0031";

    /// <summary>**Không đủ hàng** để giữ chỗ (dùng chung mã `0001` với "không đủ tồn" — cùng một nguyên nhân).</summary>
    public const string KhongDuHangDeGiu = KhongDuTon;

    /// <summary>Mã kho **đã tồn tại** trong tenant (`INV-001` ADR-01).</summary>
    public const string MaKhoDaTonTai = Nhom + "0003";

    /// <summary>Số chứng từ kho **đã tồn tại** trong tenant.</summary>
    public const string SoChungTuDaTonTai = Nhom + "0014";

    /// <summary>SKU **đã có dòng** trong cùng chứng từ (phải gộp số lượng vào MỘT dòng).</summary>
    public const string SkuDaCoTrongChungTu = Nhom + "0015";

    /// <summary>Nguồn (giỏ/đơn) **đã giữ chỗ** cho SKU này rồi (`INV-002`).</summary>
    public const string DaGiuChoNguonNay = Nhom + "0032";

    /// <summary>Phiếu **chuyển kho** mà **thiếu kho đích**.</summary>
    public const string ChuyenKhoThieuKhoDich = Nhom + "0020";

    /// <summary>Phiếu **chuyển kho** mà kho nguồn **trùng** kho đích.</summary>
    public const string ChuyenKhoCungMotKho = Nhom + "0021";

    /// <summary>Loại chứng từ **không phải chuyển kho** mà lại có kho đích.</summary>
    public const string ChuyenKhoThuaKhoDich = Nhom + "0022";

    /// <summary>Số lượng dòng chứng từ phải **≥ 1** (0 hoặc âm sẽ tạo bút toán vô nghĩa/đảo chiều ngầm).</summary>
    public const string SoLuongPhaiDuong = Nhom + "0023";

    /// <summary>
    /// 🚨 **Khách trả NHIỀU HƠN số đã mua** (tính **luỹ kế** các lần trả trước) — `SAL-004`.
    /// 📌 **VÌ SAO CẦN MÃ RIÊNG (không mượn `0001`/`KhongDuTon`):** khi ca này trả `0001` (*"không đủ tồn"*),
    /// client hiển thị **SAI thông điệp** — *"hết hàng, chờ nhập"* — trong khi **tồn kho KHÔNG hề thiếu**:
    /// vấn đề là **dữ liệu đầu vào** (khách trả quá số đã mua).
    /// 📌 Mã **đúng kỹ thuật** nhưng **sai nghiệp vụ** ⇒ người dùng nhận **chỉ dẫn sai**, và **không test nào
    /// phát hiện** vì HTTP vẫn `403` (`inv-001` §34.2).
    /// </summary>
    public const string TraVuotSoDaMua = Nhom + "0040";

    /// <summary>
    /// **SKU trả hàng KHÔNG thuộc đơn** (`SAL-004`). 📌 Nếu **im lặng bỏ qua** thì khách **tưởng đã trả**
    /// mà hệ thống **không ghi gì** ⇒ mất hàng, mất tiền, không dấu vết.
    /// </summary>
    public const string SkuKhongThuocDon = Nhom + "0041";

}

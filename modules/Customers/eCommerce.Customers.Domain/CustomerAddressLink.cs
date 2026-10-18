using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

/// <summary>Loại địa chỉ trong sổ — bất biến *"một mặc định"* áp **theo từng loại** (`BR-ADDR-004-006`).</summary>
public enum LoaiDiaChi
{
    /// <summary>Địa chỉ giao hàng.</summary>
    Shipping = 0,

    /// <summary>Địa chỉ xuất hoá đơn.</summary>
    Billing = 1
}

/// <summary>
/// 🚨 **LIÊN KẾT SỔ ĐỊA CHỈ CỦA KHÁCH** (bảng `CusAddressLinks`) — `ADDR-004` phần B.
///
/// **BA BẤT BIẾN (mỗi cái có hàng rào ở DB, không chỉ ở code):**
/// <list type="number">
///   <item>🚨 **`AddressId` là THAM CHIẾU MỀM, KHÔNG FK** (`ADDR-004` §5.1): `Address` thuộc module
///     `Address`, và FK cứng giữa hai module sẽ khoá chéo vòng đời của chúng.</item>
///   <item>🚨 **Mỗi `(CustomerId, AddressType)` có TỐI ĐA MỘT mặc định**: partial unique index
///     `(TenantId, CustomerId, AddressType) WHERE "IsDefault" AND NOT "IsDeleted"` — `NULLS NOT DISTINCT`
///     (SQL thô trong migration). ⚠️ Thiếu index ⇒ hai mặc định **lọt**, không lỗi nào nổi lên (`ADR-06`).</item>
///   <item>🔒 **APPEND-ONLY**: gỡ = **xoá mềm**; `AddressId`/`AddressType` **KHÔNG** được sửa (`BR-008`).
///     Muốn đổi địa chỉ ⇒ thêm **liên kết mới** (hoặc dùng lại liên kết **đã gỡ** bằng cách thêm mới).</item>
/// </list>
///
/// 📌 **KHÔNG** có cột nội dung địa chỉ nào ở đây: nội dung là của `Address` (`ADR-01`) — chép vào đây là
/// mở lại đúng anti-pattern *"hai nguồn sự thật về địa chỉ"* mà dự án đã loại bỏ.
/// </summary>
public class CustomerAddressLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của nhãn — khớp `AddressConsts.DoDaiNhanToiDa` + CHECK ở DB.</summary>
    public const int DoDaiNhanToiDa = 128;

    public Guid? TenantId { get; protected set; }

    /// <summary>Khách sở hữu — **FK cứng** (cùng module `Customers`).</summary>
    public Guid CustomerId { get; protected set; }

    /// <summary>🚨 **tham chiếu MỀM** sang `AddrAddresses` — **KHÔNG** FK (§5.1).</summary>
    public Guid AddressId { get; protected set; }

    /// <summary>Loại — **bất biến** sau khi tạo (`0013` nếu cố đổi).</summary>
    public LoaiDiaChi AddressType { get; protected set; }

    /// <summary>Nhãn hiển thị (*"Nhà riêng"*, *"Văn phòng"*) — chỉ để đọc.</summary>
    public string? Label { get; protected set; }

    /// <summary>Mặc định cho `AddressType` này — xem partial unique index.</summary>
    public bool IsDefault { get; protected set; }

    /// <summary>
    /// Lần dùng gần nhất trên đơn. ⚠️ Cập nhật qua **consumer ETO** `OrderPlacedEto`, **không** qua gọi
    /// chéo module (một module không ghi vào sổ của module khác).
    /// </summary>
    public DateTime? LastUsedAtUtc { get; protected set; }

    protected CustomerAddressLink()
    {
    }

    /// <summary>
    /// Tạo liên kết mới. Guard sai ⇒ **NÉM**:
    /// `eCommerce:Customers:0006` (thiếu `CustomerId`/`AddressId`/nhãn quá dài).
    /// </summary>
    public CustomerAddressLink(
        Guid id,
        Guid customerId,
        Guid addressId,
        LoaiDiaChi loai,
        string? nhan = null,
        bool laMacDinh = false) : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(customerId));
        }

        if (addressId == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(addressId));
        }

        if (!Enum.IsDefined(loai))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(loai))
                .WithData("giaTri", loai);
        }

        CustomerId = customerId;
        AddressId = addressId;
        AddressType = loai;
        Label = ChuanNhan(nhan);
        IsDefault = laMacDinh;
    }

    /// <summary>
    /// Đặt làm **mặc định** cho loại của mục này.
    /// 📌 Idempotent khi đã mặc định (gọi lại **không** ném — retry/double-click là vô hại).
    /// 🚨 **NÉM `0014`** nếu mục **đã gỡ**: *"bỏ xoá"* một liên kết đã gỡ là đường vòng qua `BR-008`
    /// ⇒ phải thêm **mục mới**.
    /// </summary>
    public void DatMacDinh()
    {
        KiemConHieuLuc(nameof(DatMacDinh));
        IsDefault = true;
    }

    /// <summary>Bỏ cờ mặc định (dùng khi có mục khác thay thế — `ADR-06`: bỏ-trước-đặt-sau).</summary>
    public void BoMacDinh()
    {
        if (IsDeleted)
        {
            return;   // mục đã gỡ: cờ mặc định không còn ý nghĩa, không coi là chuyển sai
        }

        IsDefault = false;
    }

    /// <summary>Ghi nhận đã dùng cho một đơn (qua consumer ETO) — 🔑 dữ liệu cho **A4** (chọn mặc định thay thế).</summary>
    public void GhiNhanDaDung(DateTime lucUtc)
    {
        if (lucUtc == default)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lucUtc));
        }

        LastUsedAtUtc = lucUtc;
    }

    /// <summary>
    /// 🚨 **BẤT BIẾN APPEND-ONLY** — cố đổi địa chỉ của một mục **đã có** ⇒ **NÉM `0013`**
    /// (`ADDR-004` §12). 🔒 Nội dung địa lý bất biến (`ADR-05`) ⇒ đổi địa chỉ **luôn** là *"thêm mục mới"*.
    /// 📌 Phương thức này **cố ý luôn ném**: nó tồn tại để bất biến có **một chỗ phát ngôn** (và có test),
    /// thay vì chỉ dựa vào *"không có setter"* — hai thứ đó không tương đương khi ai đó thêm setter sau này.
    /// </summary>
    public void DoiDiaChi(Guid addressIdMoi)
    {
        throw new BusinessException(CustomerErrorCodes.KhongSuaDuocLienKet)
            .WithData("linkId", Id)
            .WithData("addressIdHienTai", AddressId)
            .WithData("addressIdMoi", addressIdMoi)
            .WithData("lyDo", "Liên kết là APPEND-ONLY: hãy thêm mục mới thay vì sửa địa chỉ của mục đã có.");
    }

    /// <summary>
    /// 🚨 **CẤM "bỏ xoá"** (`0014`): liên kết đã gỡ **không** phục hồi được — phải thêm **mục mới**.
    /// 🔒 Nếu cho phục hồi: một liên kết "sống lại" sẽ đụng partial unique index *"một mặc định"* theo cách
    /// **không đoán trước được** (cờ mặc định cũ vẫn còn) ⇒ lịch sử sổ không còn tuyến tính.
    /// </summary>
    public void PhucHoi()
    {
        throw new BusinessException(CustomerErrorCodes.KhongPhucHoiDuocLienKet)
            .WithData("linkId", Id)
            .WithData("lyDo", "Mục đã gỡ KHÔNG phục hồi được (append-only) — hãy thêm mục mới.");
    }

    /// <summary>Đang dùng được: còn hiệu lực **và** có cờ mặc định hay không (chỉ đọc, cho DTO).</summary>
    public bool ConHieuLuc => !IsDeleted;

    private void KiemConHieuLuc(string thaoTac)
    {
        if (IsDeleted)
        {
            throw new BusinessException(CustomerErrorCodes.KhongPhucHoiDuocLienKet)
                .WithData("linkId", Id)
                .WithData("thaoTac", thaoTac)
                .WithData("lyDo", "Mục đã gỡ không nhận thao tác này — hãy thêm mục mới.");
        }
    }

    private static string? ChuanNhan(string? nhan)
    {
        if (string.IsNullOrWhiteSpace(nhan))
        {
            return null;
        }

        var sach = nhan.Trim();

        if (sach.Length > DoDaiNhanToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(nhan))
                .WithData("toiDa", DoDaiNhanToiDa);
        }

        return sach;
    }
}

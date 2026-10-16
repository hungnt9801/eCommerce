using eCommerce.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **HẠN MỨC PHÂN BỔ **RIÊNG CHO TỪNG KÊNH** (`INV-003` — bảng `InvChannelLimits`).
///
/// **VÌ SAO CẦN BẢNG NÀY (khoảng trống thật đã ghi ở `docs/tiep-tuc-phien-moi.md` §5 việc 1):**
/// trước lát cắt này hạn mức chỉ có **MỘT** giá trị **dùng chung** (`Inv.ChannelLimit.DefaultValue`)
/// ⇒ **mọi** kênh (Shopee · Lazada · TikTok · Web) dùng **cùng một** hạn mức. Nghiệp vụ thật **không** như vậy:
/// mỗi kênh có hợp đồng/chiến lược phân bổ **khác nhau**. Hệ quả của "một giá trị chung":
/// muốn siết kênh A thì **siết luôn** kênh B ⇒ người vận hành **không dám** bật chính sách
/// ⇒ hạn mức **không bao giờ** được dùng (một dạng "tắt im lặng" — xem `HanMucVoNghia`).
///
/// 🔑 **KHOÁ NGHIỆP VỤ = `(TenantId, ChannelCode, WarehouseId)`:**
/// <list type="bullet">
///   <item><see cref="WarehouseId"/> = <c>null</c> ⇒ hạn mức **cả kênh** (mọi kho cộng lại) — <see cref="PhamViHanMuc.Channel"/>;</item>
///   <item><see cref="WarehouseId"/> có giá trị ⇒ hạn mức **của kênh TRONG kho đó** (chặt hơn) — <see cref="PhamViHanMuc.ChannelWarehouse"/>.</item>
/// </list>
/// 📌 Vì <c>WarehouseId</c> **nullable**, unique index của bảng này **BẮT BUỘC** phải là
/// <c>NULLS NOT DISTINCT</c> (PostgreSQL coi `NULL` là **khác nhau** ⇒ thiếu nó thì *"hạn mức cả kênh"*
/// có thể bị **khai hai lần** và **không có gì chặn** — đúng lỗi C4/F1 mà `PostgreSqlSchemaTests` canh).
/// ⚠️ EF Core **không** sinh được mệnh đề đó ⇒ migration có **SQL thô** (mẫu: `Inv001_Fix_SoCai_Unique_ThemMovementType`).
///
/// ⚠️ **PHẠM VI v2 (ghi rõ, không giấu lời hứa):**
///   • **`DonVi` (`Percent`/`Absolute`) vẫn là setting DÙNG CHUNG** (`Inv.ChannelLimit.Unit`) — **không** đặt
///     ở từng dòng, vì trộn hai đơn vị trong cùng một báo cáo là nguồn sai số khó thấy; đây là **nợ đã ghi**
///     (`inv-003` §16.5), không phải tính năng đã có;
///   • **`Percent` vẫn tính trên tồn khả dụng của TỪNG `(kho, SKU)`** — tồn kho **chưa** mô hình hoá theo kênh
///     (`docs/tiep-tuc-phien-moi.md` §5 việc 3);
///   • bảng này **KHÔNG** phải "ngân sách đã cấp" (`StockAllotment` của `inv-003` §5.2) — nó là **HẠN MỨC**
///     (trần), đọc **lúc chạy**, không trừ dần.
///
/// 🔒 **Kế thừa `FullAuditedAggregateRoot`** (không phải `FullAuditedEntity`): đây là **dữ liệu cấu hình do
/// người vận hành sửa** ⇒ phải có **vết ai sửa/khi nào** (`LastModifierId`/`LastModificationTime`), và xoá
/// là **xoá mềm** (`ISoftDelete`) để *"hạn mức cũ đã tắt"* vẫn **giải thích được** lịch sử chặn đơn.
/// </summary>
public class InvChannelLimit : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của mã kênh — **đồng bộ** với `StockLedgerEntry.ChannelCode` và `Order.ChannelCode` (32).
    /// 🔑 Nguồn DUY NHẤT là <see cref="eCommerceConsts.InventoryLimits.MaKenhToiDa"/> (ở `Domain.Shared`) vì
    /// DTO ở `Application.Contracts` **không** được tham chiếu `Domain` (layering) — xem chú thích ở đó.</summary>
    public const int DoDaiMaKenhToiDa = eCommerceConsts.InventoryLimits.MaKenhToiDa;

    public Guid? TenantId { get; protected set; }

    /// <summary>Mã kênh bán (`shopee` · `lazada` · `tiktok` · `web`…) — **BẮT BUỘC**, so khớp **không phân biệt hoa/thường**.</summary>
    public string ChannelCode { get; protected set; } = default!;

    /// <summary>
    /// Kho áp hạn mức. <c>null</c> = **hạn mức cả kênh** (mọi kho);
    /// có giá trị = **hạn mức của kênh trong kho đó** (chỉ dùng khi <see cref="PhamViHanMuc.ChannelWarehouse"/>).
    /// </summary>
    public Guid? WarehouseId { get; protected set; }

    /// <summary>Giá trị hạn mức (diễn giải theo đơn vị **dùng chung** `Inv.ChannelLimit.Unit`). `0` = **TẮT** (quy ước toàn hệ thống).</summary>
    public decimal GiaTri { get; protected set; }

    /// <summary>
    /// Việc cần làm khi **vượt** hạn mức này: <see cref="HanhDongHanMuc.Block"/> (chặn — mặc định) hay
    /// <see cref="HanhDongHanMuc.Warn"/> (cho qua + cảnh báo).
    /// 📌 **VÌ SAO ĐỂ Ở TỪNG DÒNG (không chỉ ở setting):** một kênh mới/đang thử nghiệm cần **theo dõi** mà
    /// **chưa** muốn chặn đơn thật; nếu `Action` chỉ có ở setting thì bật cho kênh mới là **bật cho mọi kênh**.
    /// </summary>
    public HanhDongHanMuc HanhDong { get; protected set; } = HanhDongHanMuc.Block;

    protected InvChannelLimit() { }

    public InvChannelLimit(
        Guid id,
        string channelCode,
        decimal giaTri,
        Guid? warehouseId = null,
        HanhDongHanMuc hanhDong = HanhDongHanMuc.Block) : base(id)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(InventoryErrorCodes.HanMucKenhThieuMaKenh)
                .WithData("truong", nameof(channelCode))
                .WithData("lyDo", "Hạn mức theo kênh PHẢI có mã kênh — hạn mức không gắn kênh là hạn mức dùng chung (setting), không phải dòng trong bảng này.");
        }

        if (giaTri < 0)
        {
            throw new BusinessException(InventoryErrorCodes.HanMucKenhGiaTriAm)
                .WithData("channelCode", channelCode)
                .WithData("giaTri", giaTri)
                .WithData("lyDo", "Giá trị hạn mức KHÔNG được âm. Muốn TẮT hạn mức thì đặt `0`, không đặt số âm (số âm sẽ bị hiểu là TẮT ở chỗ đọc ⇒ hai cách diễn đạt cùng một ý, dễ sai).");
        }

        ChannelCode = ChuẩnHoáKenh(channelCode);
        WarehouseId = warehouseId;
        GiaTri = giaTri;
        HanhDong = hanhDong;
    }

    /// <summary>Đổi hạn mức (kèm hành động). 📌 Dùng chung một cửa vào để **không** có đường ghi tắt bỏ qua kiểm.</summary>
    public void DatHanMuc(decimal giaTri, HanhDongHanMuc hanhDong)
    {
        if (giaTri < 0)
        {
            throw new BusinessException(InventoryErrorCodes.HanMucKenhGiaTriAm)
                .WithData("channelCode", ChannelCode)
                .WithData("giaTri", giaTri)
                .WithData("lyDo", "Giá trị hạn mức KHÔNG được âm (xem hàm tạo).");
        }

        GiaTri = giaTri;
        HanhDong = hanhDong;
    }

    /// <summary>Tắt hạn mức của dòng này (giữ dòng lại để còn **vết cấu hình** + xoá mềm được).</summary>
    public void Tat() => GiaTri = 0m;

    /// <summary>
    /// Chuẩn hoá mã kênh: **trim** + **chữ thường**. 🔒 Cố ý **không** đổi dấu/ký tự khác — mã kênh là
    /// **khoá nghiệp vụ** đã dùng ở `Order.ChannelCode`; đổi mạnh tay ở đây sẽ tạo **hai** cách viết cho
    /// cùng một kênh và **hạn mức sẽ không khớp** lúc chạy (lỗi im lặng, không ném).
    /// </summary>
    public static string ChuẩnHoáKenh(string channelCode) => channelCode.Trim().ToLowerInvariant();
}

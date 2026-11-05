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
///   • ✅ **`DonVi` ĐÃ CÓ Ở TỪNG DÒNG** (`DonVi`, nullable) — nợ `inv-003` §16.8 #1 **ĐÃ ĐÓNG**.
///     📌 `null` = **kế thừa** setting `Inv.ChannelLimit.Unit`; có giá trị = **override** cho riêng dòng này.
///     🔑 **VÌ SAO KHÔNG CÒN LÀ NỢ:** nghiệp vụ thật dùng **hai đơn vị cho hai kênh khác nhau** (kênh A theo
///     `%` tồn khả dụng, kênh B theo số lượng tuyệt đối). ⚠️ Với **một** đơn vị dùng chung thì kênh còn lại
///     bị **diễn giải sai** — 🚨 và sai **im lặng**: `100` đọc là `100%` thay vì `100 chiếc` thì hạn mức
///     **không chặn gì cả**, **không có lỗi nào**. 📌 Lo ngại cũ (*"trộn hai đơn vị là nguồn sai số"*) được
///     xử lý bằng **hiển thị đơn vị ở MỌI chỗ đọc** (`KetQuaHanMuc.DonVi` + cột `Đơn vị` trên màn hình),
///   • **`Percent` vẫn tính trên tồn khả dụng của TỪNG `(kho, SKU)`** — tồn kho **chưa** mô hình hoá theo kênh
///     (`docs/tiep-tuc-phien-moi.md` §5 việc 3);
///   • bảng này **KHÔNG** phải "ngân sách đã cấp" (`StockAllotment` của `inv-003` §5.2) — nó là **HẠN MỨC**
///     (trần), đọc **lúc chạy**, không trừ dần.
///
/// 🔒 **Kế thừa `FullAuditedAggregateRoot`** (không phải `FullAuditedEntity`): đây là **dữ liệu cấu hình do
/// người vận hành sửa** ⇒ phải có **vết ai sửa/khi nào** (`LastModifierId`/`LastModificationTime`), và xoá
/// là **xoá mềm** (`ISoftDelete`) để *"hạn mức cũ đã tắt"* vẫn **giải thích được** lịch sử chặn đơn.
/// </summary>
public class InvChannelLimit : FullAuditedAggregateRoot<Guid>, IMultiTenant, IDongHanMucNguon
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

    /// <summary>
    /// 🚨 **ĐƠN VỊ CỦA RIÊNG DÒNG NÀY** (`INV-003` mục 5 việc 2) — `null` = **kế thừa setting**
    /// `Inv.ChannelLimit.Unit`.
    ///
    /// **VÌ SAO CẦN Ở TỪNG DÒNG (đây là nợ số 1 về nghiệp vụ, `inv-003` §16.8 nợ 1):**
    /// nghiệp vụ thật dùng **hai đơn vị cho hai kênh khác nhau**: kênh A cam kết theo **`%` tồn khả dụng**
    /// (vì tồn biến động), còn kênh B cam kết theo **số lượng tuyệt đối** (vì hợp đồng ghi *"200 chiếc"*).
    /// ⚠️ Với **một** đơn vị dùng chung, người vận hành phải **chọn một trong hai** ⇒ kênh còn lại bị
    /// **diễn giải sai** — 🚨 và sai **im lặng**: `100` đọc là `100%` thay vì `100 chiếc` thì hạn mức
    /// **không** chặn gì cả, **không có lỗi nào**.
    ///
    /// 🔒 **`null` CÓ NGHĨA — và đó là lựa chọn thiết kế, không phải sơ suất:**
    /// <list type="bullet">
    ///   <item><c>null</c> = **KHÔNG override** ⇒ dùng setting chung. 📌 Nhờ vậy **dữ liệu cũ** (48 dòng tiềm
    ///     năng) **không bị đổi hành vi** khi thêm cột — migration này **an toàn**;</item>
    ///   <item>⚠️ **KHÔNG** dùng `0`/giá trị "mặc định" làm dấu *"không override"*: `Percent` = `0` là một
    ///     **đơn vị hợp lệ**, nên dùng nó làm cờ sẽ tạo **hai cách diễn đạt cùng một ý** — đúng lỗi mà
    ///     `GiaTri` đã tránh (xem `Tat()` và lời cảnh báo về số âm ở hàm tạo).</item>
    /// </list>
    ///
    /// 📌 **VÌ SAO CHỌN ĐƯỢC Ở CẢ DÒNG `(kênh, kho)`:** hạn mức theo kho là **chặt hơn**; nếu nó bị **ép**
    /// dùng đơn vị của dòng cả kênh thì người vận hành **không** diễn đạt được *"cả kênh 50%, nhưng riêng
    /// kho Hà Nội 200 chiếc"* — một cấu hình **có thật** trong vận hành.
    /// </summary>
    public DonViHanMuc? DonVi { get; protected set; }

    protected InvChannelLimit() { }

    public InvChannelLimit(
        Guid id,
        string channelCode,
        decimal giaTri,
        Guid? warehouseId = null,
        HanhDongHanMuc hanhDong = HanhDongHanMuc.Block,
        DonViHanMuc? donVi = null) : base(id)
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
        DonVi = donVi;
    }

    /// <summary>Đổi hạn mức (kèm hành động). 📌 Dùng chung một cửa vào để **không** có đường ghi tắt bỏ qua kiểm.</summary>
    public void DatHanMuc(decimal giaTri, HanhDongHanMuc hanhDong, DonViHanMuc? donVi = null)
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

        // 🔒 Gán `DonVi` ở **CÙNG cửa vào** với `GiaTri`/`HanhDong`: nếu để đường ghi riêng thì có thể
        // đổi đơn vị mà **quên** đổi giá trị ⇒ `100` đổi từ `%` sang `chiếc` **âm thầm** đổi ý nghĩa.
        DonVi = donVi;
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

using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **SERIAL IMEI** của một sản phẩm loại <see cref="ProductType.Imei"/> (`03 §3.1`).
///
/// 🚨 VÌ SAO IMEI LÀ **ENTITY RIÊNG** (không phải "ghi chú trong dòng hoá đơn"): mỗi serial là **một đơn vị
/// tồn kho riêng** — bán cái nào trừ cái đó, bảo hành/hỏng/trả NCC theo **từng máy**. Gộp vào số lượng thì
/// câu hỏi *"máy này đã bán cho ai, còn bảo hành không"* **không trả lời được** (`nhanh-pos-module-map.md`).
///
/// 🔒 `Serial` **duy nhất trong tenant** (DB unique index). Trạng thái chỉ đi qua các giá trị của
/// <see cref="ProductImeiStatus"/> — **không** có số `4` (dữ liệu Nhanh không có giá trị đó).
/// ⚠️ `WarehouseId`/`ImportBillId`/`SoldBillId` là **khoá tham chiếu chéo module** (Inventory/Sales) nên
/// **cố ý không đặt FK**: FK sẽ buộc Catalog phải biết bảng của module khác (phá luật phụ thuộc #1 ở `03 §2`).
/// Việc đối chiếu do app service của module sở hữu chứng từ bảo đảm.
/// </summary>
public class ProductImei : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của serial — khớp `HasMaxLength(64)` ở DbContext.</summary>
    public const int DoDaiSerialToiDa = 64;

    /// <summary>Độ dài tối đa của ghi chú — khớp `HasMaxLength(512)` ở DbContext.</summary>
    public const int DoDaiGhiChuToiDa = 512;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>Số serial/IMEI in trên máy — **duy nhất trong tenant**.</summary>
    public string Serial { get; protected set; } = default!;

    public ProductImeiStatus Status { get; protected set; } = ProductImeiStatus.New;

    /// <summary>Kho đang giữ serial (khoá tham chiếu module `Inventory`, không FK).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Lô nhập của serial (nếu sản phẩm quản theo lô).</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Phiếu nhập đã đưa serial vào kho (khoá tham chiếu module `Inventory`).</summary>
    public Guid? ImportBillId { get; set; }

    /// <summary>Hoá đơn/đơn đã bán serial (khoá tham chiếu module `Sales`/`Pos`).</summary>
    public Guid? SoldBillId { get; set; }

    /// <summary>Hạn bảo hành của riêng serial (khác hạn chung của sản phẩm).</summary>
    public DateTime? WarrantyUntil { get; set; }

    public string? Note { get; set; }

    protected ProductImei() { }

    public ProductImei(Guid id, Guid productId, string serial) : base(id)
    {
        ProductId = productId;
        Serial = ChuanHoaSerial(serial);
    }

    /// <summary>Chuẩn hoá serial: trim, không rỗng, không quá 64 ký tự.</summary>
    public static string ChuanHoaSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Serial")
                .WithData("lyDo", "rỗng");
        }

        var s = serial.Trim();

        if (s.Length > DoDaiSerialToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Serial")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiSerialToiDa);
        }

        return s;
    }

    /// <summary>
    /// Đổi trạng thái serial. 🔒 Chỉ app service của luồng kho/bán/bảo hành gọi — vì mỗi lần đổi là một
    /// **sự kiện vật lý** (xuất kho, giao hàng, nhận bảo hành) chứ không phải "sửa cho đúng".
    /// </summary>
    public void DoiTrangThai(ProductImeiStatus trangThaiMoi) => Status = trangThaiMoi;
}

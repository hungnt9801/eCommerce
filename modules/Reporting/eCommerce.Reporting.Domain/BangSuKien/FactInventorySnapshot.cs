using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **ẢNH TỒN KHO THEO NGÀY** (`RptFactInventorySnapshots`) — `07 §WP-08`: `FactInventorySnapshot`.
///
/// 🚨 **ĐÂY KHÔNG PHẢI LỊCH SỬ TỒN** — nó là **ảnh chụp tại lúc job chạy**, gắn vào ngày kinh doanh chạy.
/// 📌 Nguồn (`InvItems`) chỉ giữ **trạng thái hiện tại**: không có bảng nào ghi lại *"tồn ngày 01/11 là bao
/// nhiêu"* ⇒ chạy lại một ngày cũ sẽ ghi ảnh **hôm nay** vào ngày cũ. Vì vậy:
/// <list type="bullet">
///   <item>báo cáo tồn kho **chỉ đọc ảnh mới nhất** (không hứa "tồn ngày X" quá khứ);</item>
///   <item>job chạy lại cùng ngày ⇒ **thay thế** ảnh của ngày đó (unique `(TenantId, NgayChup, Kho, Sku)`).</item>
/// </list>
/// Muốn có lịch sử thật thì phải chụp **mỗi ngày một lần** (job định kỳ hằng ngày) — đó là lý do job có
/// nhịp ngày, không phải nhịp giờ.
///
/// 🔒 **Hai cột DẪN XUẤT được TÍNH trong constructor** (`InWarehouse`, `Available`) chứ **không nhận từ
/// tham số**: nhận từ ngoài là mở đường cho ảnh tồn có `Available` không khớp 9 chiều — đúng loại sai
/// lệch mà công thức `D-27` sinh ra để chặn.
/// </summary>
public class RptFactInventorySnapshot : CreationAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiSkuToiDa = 64;

    public Guid? TenantId { get; protected set; }

    /// <summary>Ngày chụp (ngày kinh doanh lúc job chạy).</summary>
    public DateOnly SnapshotDate { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    // ── 9 CHIỀU (D-27) ─────────────────────────────────────────────────────────────────────────────
    public decimal OnHand { get; protected set; }

    public decimal Reserved { get; protected set; }

    public decimal Transferring { get; protected set; }

    public decimal Shipping { get; protected set; }

    public decimal Holding { get; protected set; }

    public decimal Damaged { get; protected set; }

    public decimal Warranty { get; protected set; }

    public decimal WarrantyHolding { get; protected set; }

    public decimal PreOrder { get; protected set; }

    public decimal WaitingInbound { get; protected set; }

    /// <summary>`OnHand − Transferring` — tính lại **tại đây** (cùng công thức `InventoryItem.InWarehouse`).</summary>
    public decimal InWarehouse { get; protected set; }

    /// <summary>`OnHand − Reserved − Transferring − Holding − Damaged` — cùng công thức `D-27`.</summary>
    public decimal Available { get; protected set; }

    public DateTime CapturedAtUtc { get; protected set; }

    protected RptFactInventorySnapshot() { }

    /// <summary>Dựng ảnh từ 9 chiều của `InventoryItem` (một cửa — không nhận số dẫn xuất từ ngoài).</summary>
    public static RptFactInventorySnapshot TuTonKho(
        Guid id,
        DateOnly snapshotDate,
        Guid warehouseId,
        string sku,
        decimal onHand,
        decimal reserved,
        decimal transferring,
        decimal shipping,
        decimal holding,
        decimal damaged,
        decimal warranty,
        decimal warrantyHolding,
        decimal preOrder,
        decimal waitingInbound,
        DateTime capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(ReportingErrorCodes.TonKhoAm)
                .WithData("lyDo", "Ảnh tồn kho phải có SKU.");
        }

        if (sku.Trim().Length > DoDaiSkuToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.TonKhoAm)
                .WithData("truong", nameof(sku)).WithData("toiDa", DoDaiSkuToiDa);
        }

        // 🚨 Chặn ảnh XẤU ngay tại cửa: một chiều âm nghĩa là sổ kho đã sai từ trước. Ghi nó vào read-model
        // là nhân bản cái sai ra toàn bộ báo cáo, và lần sau không phân biệt được *"sai ở kho"* với
        // *"sai ở báo cáo"*.
        if (onHand < 0m || reserved < 0m || transferring < 0m || shipping < 0m || holding < 0m
            || damaged < 0m || warranty < 0m || warrantyHolding < 0m || preOrder < 0m || waitingInbound < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.TonKhoAm)
                .WithData("kho", warehouseId)
                .WithData("sku", sku)
                .WithData("onHand", onHand)
                .WithData("lyDo", "Một chiều tồn kho đang ÂM ⇒ không chụp ảnh xấu vào read-model.");
        }

        return new RptFactInventorySnapshot
        {
            Id = id,
            SnapshotDate = snapshotDate,
            WarehouseId = warehouseId,
            Sku = sku.Trim(),
            OnHand = onHand,
            Reserved = reserved,
            Transferring = transferring,
            Shipping = shipping,
            Holding = holding,
            Damaged = damaged,
            Warranty = warranty,
            WarrantyHolding = warrantyHolding,
            PreOrder = preOrder,
            WaitingInbound = waitingInbound,
            InWarehouse = onHand - transferring,
            Available = onHand - reserved - transferring - holding - damaged,
            CapturedAtUtc = capturedAtUtc
        };
    }
}

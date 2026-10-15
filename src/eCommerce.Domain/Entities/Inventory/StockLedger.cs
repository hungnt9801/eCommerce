using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// Tồn kho hiện tại của một SKU trong một kho.
/// Đây là ảnh chụp (snapshot) để đọc nhanh; <b>nguồn sự thật là <see cref="StockLedgerEntry"/></b>.
/// </summary>
public class InventoryItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid WarehouseId { get; set; }

    public string Sku { get; set; } = default!;

    /// <summary>Tổng có trong kho.</summary>
    public int OnHand { get; private set; }

    /// <summary>Đang giữ chỗ cho đơn chưa xuất.</summary>
    public int Reserved { get; private set; }

    /// <summary>Có thể bán = OnHand - Reserved.</summary>
    public int Available => OnHand - Reserved;

    protected InventoryItem() { }

    public InventoryItem(Guid id, Guid warehouseId, string sku) : base(id)
    {
        WarehouseId = warehouseId;
        Sku = sku;
    }

    /// <summary>
    /// Áp dụng một biến động kho. Phải được gọi trong cùng transaction với việc ghi
    /// <see cref="StockLedgerEntry"/> và trong phạm vi distributed lock
    /// (<c>eCommerceConsts.LockKeys.InventorySku</c>) để tránh bán vượt tồn kho.
    /// </summary>
    public void ApplyMovement(StockMovementType type, int quantityDelta)
    {
        // 🚨🚨 **TÍNH TRƯỚC — KIỂM TRA — RỒI MỚI GÁN.** (Sửa gốc 15/10/2026, `INV-001`.)
        // Phiên bản đầu **gán trước rồi kiểm tra**: khi bất biến bị vi phạm, nó **NÉM** nhưng entity đã bị
        // **biến đổi** ⇒ **ở lại trạng thái SAI trong bộ nhớ**. Trong EF Core, entity đó vẫn đang được **theo dõi
        // (tracked)**; nếu người gọi **bắt** exception (ví dụ để thử đường khác) rồi `SaveChanges` vì việc khác
        // thì tồn kho **SAI được ghi xuống DB** — mà `CHECK` ở DB **không** chặn được nếu giá trị vẫn "hợp lệ"
        // (ví dụ `Reserved` vượt `OnHand` sau khi trừ bớt chỗ khác). ⇒ **Ném lỗi mà để lại rác là gieo bug.**
        var onHandMoi = OnHand;
        var reservedMoi = Reserved;

        switch (type)
        {
            case StockMovementType.Receipt:
            case StockMovementType.CustomerReturn:
            case StockMovementType.Adjustment:
            case StockMovementType.Transfer:
            case StockMovementType.StocktakeGain:
            case StockMovementType.TransferIn:
                onHandMoi += quantityDelta;
                break;

            case StockMovementType.Sale:
            case StockMovementType.StocktakeLoss:
            case StockMovementType.TransferOut:
                onHandMoi -= quantityDelta;
                break;

            case StockMovementType.Reservation:
                reservedMoi += quantityDelta;
                break;

            case StockMovementType.ReservationRelease:
                reservedMoi -= quantityDelta;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Loại biến động kho không hỗ trợ.");
        }

        if (onHandMoi < 0 || reservedMoi < 0)
        {
            throw new InvalidOperationException(
                $"Biến động kho {type} làm âm tồn kho của SKU '{Sku}' " +
                $"(OnHand={onHandMoi}, Reserved={reservedMoi}). KHÔNG thay đổi gì.");
        }

        if (reservedMoi > onHandMoi)
        {
            // 🚨 Bất biến: **giữ chỗ KHÔNG được vượt tồn thực**. Trước `INV-001` chỉ kiểm `>= 0`
            // ⇒ `Reserved > OnHand` **LỌT** ⇒ `Available` **ÂM** ⇒ hệ thống tưởng còn hàng để bán
            // (bán thứ không có). DB nay có `CHECK` tương ứng (`ADR-03`) — code là lớp trong, DB là chốt cuối.
            throw new InvalidOperationException(
                $"Giữ chỗ VƯỢT tồn thực của SKU '{Sku}': Reserved={reservedMoi} > OnHand={onHandMoi}. " +
                "KHÔNG thay đổi gì.");
        }

        OnHand = onHandMoi;
        Reserved = reservedMoi;
    }

    public bool CanReserve(int quantity) => Available >= quantity;
}

/// <summary>
/// Sổ cái kho — APPEND-ONLY. Không bao giờ UPDATE/DELETE bản ghi ở bảng này.
/// Mọi thay đổi tồn kho đều đi qua đây để có thể truy vết và đối soát với sàn.
/// </summary>
public class StockLedgerEntry : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public StockMovementType MovementType { get; protected set; }

    /// <summary>Độ lệch (+/-). Quy ước dấu theo <see cref="StockMovementType"/>.</summary>
    public int QuantityDelta { get; protected set; }

    /// <summary>Số dư sau biến động (để đối soát nhanh, không dùng làm nguồn sự thật).</summary>
    public int BalanceAfter { get; protected set; }

    /// <summary>Nguồn gây ra biến động: mã đơn, mã phiếu nhập, mã đơn sàn...</summary>
    public string? ReferenceType { get; protected set; }

    public string? ReferenceId { get; protected set; }

    /// <summary>
    /// 🚨 **`INV-001` ADR-05** — `Id` của **dòng chứng từ** đã sinh ra bút toán này.
    /// Cùng với unique index `(TenantId, ReferenceLineId)` (bộ lọc `IS NOT NULL`) nó là **hàng rào chống ghi trùng**:
    /// ghi sổ một phiếu hai lần (retry, job chạy lại, người bấm hai lần) ⇒ **lần thứ hai bị DB chặn**.
    /// 📌 Nếu chỉ kiểm ở tầng app ("đã ghi chưa?") thì hai request **đồng thời** cùng thấy "chưa ghi"
    /// ⇒ **ghi hai lần** ⇒ tồn kho tăng gấp đôi mà **không có lỗi nào** (đúng họ lỗi `XC-003` F1).
    /// ⚠️ Bộ lọc `IS NOT NULL` là **bắt buộc**: bút toán điều chỉnh tay **không** có dòng chứng từ; nếu unique
    /// không bộ lọc thì toàn hệ thống chỉ được phép có **MỘT** bút toán không tham chiếu.
    /// </summary>
    public Guid? ReferenceLineId { get; protected set; }

    /// <summary>Mã kênh (nếu biến động đến từ một kênh bán) — cần cho việc đối soát đa kênh.</summary>
    public string? ChannelCode { get; protected set; }

    public DateTime OccurredAtUtc { get; protected set; }

    public string? Note { get; set; }

    protected StockLedgerEntry() { }

    public StockLedgerEntry(
        Guid id,
        Guid warehouseId,
        string sku,
        StockMovementType movementType,
        int quantityDelta,
        int balanceAfter,
        DateTime occurredAtUtc,
        string? referenceType = null,
        string? referenceId = null,
        string? channelCode = null,
        Guid? referenceLineId = null) : base(id)
    {
        WarehouseId = warehouseId;
        Sku = sku;
        MovementType = movementType;
        QuantityDelta = quantityDelta;
        BalanceAfter = balanceAfter;
        OccurredAtUtc = occurredAtUtc;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        ChannelCode = channelCode;
        ReferenceLineId = referenceLineId;
    }
}

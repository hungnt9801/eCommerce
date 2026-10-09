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
        switch (type)
        {
            case StockMovementType.Receipt:
            case StockMovementType.CustomerReturn:
                OnHand += quantityDelta;
                break;

            case StockMovementType.Sale:
                OnHand -= quantityDelta;
                break;

            case StockMovementType.Reservation:
                Reserved += quantityDelta;
                break;

            case StockMovementType.ReservationRelease:
                Reserved -= quantityDelta;
                break;

            case StockMovementType.Adjustment:
            case StockMovementType.Transfer:
                OnHand += quantityDelta;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Loại biến động kho không hỗ trợ.");
        }

        if (OnHand < 0 || Reserved < 0)
        {
            throw new InvalidOperationException(
                $"Biến động kho {type} làm âm tồn kho của SKU '{Sku}' (OnHand={OnHand}, Reserved={Reserved}).");
        }
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
        string? channelCode = null) : base(id)
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
    }
}

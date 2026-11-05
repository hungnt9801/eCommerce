using Volo.Abp;
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

    /// <summary>**Tồn** — TỔNG hàng thuộc kho (gồm cả hàng đang ở các chiều bên dưới nếu chiều đó chưa rời kho).</summary>
    public decimal OnHand { get; private set; }

    /// <summary>**Tạm giữ do GIỮ CHỖ** của đơn chưa xuất (`INV-002`) — chiều này do `StockReservationService` ghi.</summary>
    public decimal Reserved { get; private set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **`WP-04`** — 8 CHIỀU CÒN LẠI của vector tồn kho kiểu Nhanh (`docs/nhanh/07` WP-04 việc 1).
    // Cùng với `OnHand` (Tồn) và `Reserved` (Tạm giữ) ⇒ **10 cột** = vector 9 chiều + `Reserved`.
    // 🚨 **KHÔNG phải "9 kho độc lập":** mỗi chiều là một **trạng thái của hàng** (subset/nhãn), và mỗi chiều
    //    có **một** cách ảnh hưởng `OnHand` — ghi rõ ở `ApplyMovement` để không ai tự suy ra chiều ngược lại.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>**Đang chuyển kho** — đã xuất kho nguồn, chưa nhập kho đích (`InWarehouse` trừ chiều này).</summary>
    public decimal Transferring { get; private set; }

    /// <summary>**Đang giao** — đã xuất kho cho đơn vị vận chuyển (`OnHand` đã giảm khi xuất).</summary>
    public decimal Shipping { get; private set; }

    /// <summary>**Tạm giữ thủ công** — giữ hàng cho khách/đơn chưa chốt, **chưa** trừ `OnHand`.</summary>
    public decimal Holding { get; private set; }

    /// <summary>**Lỗi/hỏng** — vẫn nằm trong kho (kiểm kê vẫn đếm) nhưng **không bán được**.</summary>
    public decimal Damaged { get; private set; }

    /// <summary>**Bảo hành** — hàng đã gửi đi bảo hành (`OnHand` đã giảm).</summary>
    public decimal Warranty { get; private set; }

    /// <summary>**Tạm giữ linh kiện bảo hành** — chiều riêng của Nhanh (chưa có đường ghi trong WP-04).</summary>
    public decimal WarrantyHolding { get; private set; }

    /// <summary>**Đặt trước** — hàng đã hứa cho đơn đặt trước (chưa có đường ghi trong WP-04).</summary>
    public decimal PreOrder { get; private set; }

    /// <summary>**Chờ nhập** — hàng đã đặt nhà cung cấp, chưa về kho (chưa có đường ghi trong WP-04).</summary>
    public decimal WaitingInbound { get; private set; }

    /// <summary>
    /// **Tồn trong kho** = `Tồn − Đang chuyển kho` (công thức của Nhanh, `README` §3.1).
    /// 📌 Hàng đang chuyển **vẫn thuộc** kho nguồn nhưng **không còn** nằm trong kho ⇒ tách khỏi số "trong kho".
    /// </summary>
    public decimal InWarehouse => OnHand - Transferring;

    /// <summary>
    /// **Có thể bán** = `Tồn trong kho − Tạm giữ − Lỗi`.
    ///
    /// ⚠️ **VÌ SAO CÓ `Reserved` TRONG CÔNG THỨC** (tài liệu Nhanh chỉ ghi *"Tạm giữ"*): `Reserved` là **tạm giữ
    /// do giữ chỗ của đơn** (`INV-002`) — cùng bản chất "hàng đã hứa cho người khác" với `Holding`, chỉ khác
    /// **đường ghi**. Nếu bỏ `Reserved` ra khỏi công thức thì `Available` sẽ **tăng vọt** ngay khi có đơn giữ chỗ
    /// ⇒ bán vượt tồn (oversell) — đúng lỗi mà `INV-002` sinh ra để chặn.
    /// 📌 `Shipping`/`Warranty` **không** có trong công thức vì khi hàng đi giao/đi bảo hành thì `OnHand` **đã
    /// giảm** (không trừ hai lần).
    /// </summary>
    public decimal Available => OnHand - Reserved - Transferring - Holding - Damaged;

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
    public void ApplyMovement(StockMovementType type, decimal quantityDelta)
    {
        // 🚨🚨 **TÍNH TRƯỚC — KIỂM TRA — RỒI MỚI GÁN.** (Sửa gốc 15/10/2026, `INV-001`.)
        // Phiên bản đầu **gán trước rồi kiểm tra**: khi bất biến bị vi phạm, nó **NÉM** nhưng entity đã bị
        // **biến đổi** ⇒ **ở lại trạng thái SAI trong bộ nhớ**. Trong EF Core, entity đó vẫn đang được **theo dõi
        // (tracked)**; nếu người gọi **bắt** exception (ví dụ để thử đường khác) rồi `SaveChanges` vì việc khác
        // thì tồn kho **SAI được ghi xuống DB** — mà `CHECK` ở DB **không** chặn được nếu giá trị vẫn "hợp lệ"
        // (ví dụ `Reserved` vượt `OnHand` sau khi trừ bớt chỗ khác). ⇒ **Ném lỗi mà để lại rác là gieo bug.**
        var onHandMoi = OnHand;
        var reservedMoi = Reserved;
        var transferringMoi = Transferring;
        var shippingMoi = Shipping;
        var holdingMoi = Holding;
        var damagedMoi = Damaged;
        var warrantyMoi = Warranty;

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
            case StockMovementType.RetailSaleOut:
            // 🆕 `WP-04` — hai loại **xuất** thêm cho phiếu kiểu Nhanh (`mode = 5 Nhà cung cấp` / `10 Khác`).
            // 📌 Thiếu hai nhánh này thì phiếu xuất NCC **NÉM** khi ghi sổ (ca `Moi_loai_bien_dong…` bắt được).
            case StockMovementType.SupplierReturnOut:
            case StockMovementType.OtherOut:
                onHandMoi -= quantityDelta;
                break;

            case StockMovementType.Reservation:
                reservedMoi += quantityDelta;
                break;

            case StockMovementType.ReservationRelease:
                reservedMoi -= quantityDelta;
                break;

            // ── 🆕 `WP-04` — các chiều mới. 📌 Mỗi nhánh ghi rõ `OnHand` **có** đổi hay **không**: đây là chỗ
            // dễ sai nhất (trừ hai lần ⇒ báo cáo sai mà không có lỗi nào).
            case StockMovementType.ShippingOut:
                // Hàng RỜI kho đi giao ⇒ `OnHand` giảm; chiều `Shipping` giữ dấu vết (không trừ vào `Available`).
                onHandMoi -= quantityDelta;
                shippingMoi += quantityDelta;
                break;

            case StockMovementType.ShippingReturn:
                // Khách trả lại hàng đang giao ⇒ hàng về kho.
                shippingMoi -= quantityDelta;
                onHandMoi += quantityDelta;
                break;

            case StockMovementType.DamagedIn:
                // Hàng lỗi VẪN nằm trong kho (kiểm kê vẫn đếm) ⇒ `OnHand` KHÔNG đổi, chỉ bị loại khỏi `Available`.
                damagedMoi += quantityDelta;
                break;

            case StockMovementType.DamagedOut:
                // Xuất huỷ hàng lỗi ⇒ hàng ra khỏi kho hẳn: nhả nhãn lỗi VÀ giảm `OnHand` (Available không đổi).
                damagedMoi -= quantityDelta;
                onHandMoi -= quantityDelta;
                break;

            case StockMovementType.WarrantyHold:
                // Gửi hàng đi bảo hành ⇒ hàng rời kho: `OnHand` giảm, chiều `Warranty` tăng.
                onHandMoi -= quantityDelta;
                warrantyMoi += quantityDelta;
                break;

            case StockMovementType.WarrantyRelease:
                // Nhận lại từ bảo hành ⇒ hàng về kho.
                warrantyMoi -= quantityDelta;
                onHandMoi += quantityDelta;
                break;

            case StockMovementType.Hold:
                // Tạm giữ thủ công: hàng VẪN trong kho ⇒ chỉ tăng nhãn, `Available` giảm.
                holdingMoi += quantityDelta;
                break;

            case StockMovementType.HoldRelease:
                holdingMoi -= quantityDelta;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Loại biến động kho không hỗ trợ.");
        }

        var availableMoi = onHandMoi - reservedMoi - transferringMoi - holdingMoi - damagedMoi;

        if (onHandMoi < 0 || reservedMoi < 0 || transferringMoi < 0 || shippingMoi < 0
            || holdingMoi < 0 || damagedMoi < 0 || warrantyMoi < 0)
        {
            // 🚨 **LỖI NGHIỆP VỤ, KHÔNG phải lỗi hệ thống**: trước 15/10/2026 chỗ này ném
            // `InvalidOperationException` ⇒ ABP map thành **HTTP 500** ⇒ client/CSKH thấy "lỗi hệ thống"
            // trong khi thực chất là *"không đủ hàng để xuất"* — **người dùng không biết phải làm gì**.
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("sku", Sku)
                .WithData("onHand", onHandMoi)
                .WithData("reserved", reservedMoi)
                .WithData("damaged", damagedMoi)
                .WithData("movementType", type.ToString())
                .WithData("lyDo", "Biến động kho làm ÂM tồn kho hoặc ÂM một chiều tồn. KHÔNG thay đổi gì.");
        }

        if (reservedMoi > onHandMoi)
        {
            // 🚨 Bất biến: **giữ chỗ KHÔNG được vượt tồn thực**. Trước `INV-001` chỉ kiểm `>= 0`
            // ⇒ `Reserved > OnHand` **LỌT** ⇒ `Available` **ÂM** ⇒ hệ thống tưởng còn hàng để bán
            // (bán thứ không có). DB nay có `CHECK` tương ứng (`ADR-03`) — code là lớp trong, DB là chốt cuối.
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("sku", Sku)
                .WithData("onHand", onHandMoi)
                .WithData("reserved", reservedMoi)
                .WithData("lyDo", "Giữ chỗ VƯỢT tồn thực (Reserved > OnHand) ⇒ Available ÂM. KHÔNG thay đổi gì.");
        }

        if (availableMoi < 0)
        {
            // 🆕 `WP-04`: với 8 chiều mới, `OnHand >= 0` **KHÔNG đủ** — hàng có thể đã bị đánh dấu lỗi/tạm giữ
            // ⇒ `Available` âm trong khi `OnHand` dương. `Available` âm = **sẽ bán thứ không bán được**.
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("sku", Sku)
                .WithData("onHand", onHandMoi)
                .WithData("reserved", reservedMoi)
                .WithData("holding", holdingMoi)
                .WithData("damaged", damagedMoi)
                .WithData("available", availableMoi)
                .WithData("movementType", type.ToString())
                .WithData("lyDo", "Biến động làm HÀNG KHẢ DỤNG (Available) ÂM. KHÔNG thay đổi gì.");
        }

        OnHand = onHandMoi;
        Reserved = reservedMoi;
        Transferring = transferringMoi;
        Shipping = shippingMoi;
        Holding = holdingMoi;
        Damaged = damagedMoi;
        Warranty = warrantyMoi;
    }

    public bool CanReserve(decimal quantity) => Available >= quantity;
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
    public decimal QuantityDelta { get; protected set; }

    /// <summary>Số dư sau biến động (để đối soát nhanh, không dùng làm nguồn sự thật).</summary>
    public decimal BalanceAfter { get; protected set; }

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
        decimal quantityDelta,
        decimal balanceAfter,
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

using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>Vòng đời **ngân sách tồn của một kênh** (`INV-003` §3). 📌 `ChuaCap` **không** phải giá trị lưu —
/// nó là **không có hàng** (`D6`): không có dòng ⇒ kênh **chưa** được cấp ngân sách.</summary>
public enum StockAllotmentStatus
{
    Active = 0,
    Frozen = 1,
    Released = 2
}

/// <summary>Lý do ghi sổ **ĐÓNG** (`INV-003` §5.2.2) — không cho free text, để đối soát đếm được.</summary>
public enum StockAllotmentLedgerReason
{
    ManualAllot = 0,
    ManualAdjust = 1,
    ReclaimOnStockDecrease = 2,
    ReclaimWhileFrozen = 3,
    Freeze = 4,
    Unfreeze = 5,
    Release = 6,
    ReAllot = 7,
    ReconcileFix = 8
}

/// <summary>
/// 🆕 **`S13` — NGÂN SÁCH TỒN THEO KÊNH** (`InvStockAllotments`) — `INV-003` §5.2.1.
///
/// ⚠️ **PHÂN BỔ KHÔNG PHẢI BIẾN ĐỘNG KHO** (`D2`): nó **không** đổi `InvItems.OnHand`/`Reserved` ⇒ **cấm**
/// thêm thành viên `StockMovementType`; lịch sử ngân sách ghi vào sổ cái **riêng**
/// (<see cref="StockAllotmentLedger"/>) — 📌 trộn hai sổ cái làm `BalanceAfter` của sổ cái kho mất nghĩa.
///
/// 🚨 **BẤT BIẾN:** `0 ≤ ReservedQuantity ≤ AllottedQuantity` (ATP không âm) — giữ ở **tầng domain**
/// **và** `CHECK` ở DB (hàng rào cuối khi có đường ghi tắt).
/// </summary>
public class StockAllotment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    /// <summary>Mã kênh — **tham chiếu MỀM** (chuỗi), không FK sang module ChannelHub (`INV-003` §5.1).</summary>
    public string ChannelCode { get; protected set; } = default!;

    /// <summary>Ngân sách đã cấp (`≥ 0`).</summary>
    public int AllottedQuantity { get; protected set; }

    /// <summary>Phần kênh này **đang giữ chỗ** (bản sao tổng hợp của `InvStockReservations` — `BR-16`).</summary>
    public int ReservedQuantity { get; protected set; }

    /// <summary>Ưu tiên **thu hồi** — số **nhỏ** được giữ trước (cùng quy ước `CHN-004`).</summary>
    public int Priority { get; protected set; }

    public StockAllotmentStatus Status { get; protected set; } = StockAllotmentStatus.Active;

    public DateTime? FrozenAtUtc { get; protected set; }

    public Guid? FrozenByUserId { get; protected set; }

    public DateTime? ReleasedAtUtc { get; protected set; }

    public string? ReleaseReason { get; protected set; }

    /// <summary>`Allotted − Reserved` khi `Active`; **`0`** khi `Frozen`/`Released` (**BR-18** — tính, KHÔNG lưu cột).</summary>
    public int AvailableToPromise => Status == StockAllotmentStatus.Active
        ? AllottedQuantity - ReservedQuantity
        : 0;

    /// <summary>Còn **giữ** ngân sách (`Active` hoặc `Frozen`) — dùng cho bất biến tổng `Σ Allotted ≤ OnHand`.</summary>
    public bool ConGiuNganSach => Status != StockAllotmentStatus.Released;

    protected StockAllotment() { }

    public StockAllotment(
        Guid id,
        Guid warehouseId,
        string sku,
        string channelCode,
        int allottedQuantity,
        int priority = 0) : base(id)
    {
        if (warehouseId == Guid.Empty)
        {
            throw Loi("Thiếu `WarehouseId`.").WithData("truong", nameof(warehouseId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw Loi("Thiếu `Sku`.").WithData("truong", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw Loi("Thiếu `ChannelCode` — ngân sách **luôn** gắn một kênh.").WithData("truong", nameof(channelCode));
        }

        if (allottedQuantity < 0)
        {
            throw Loi("Ngân sách KHÔNG được âm.").WithData("soLuong", allottedQuantity);
        }

        WarehouseId = warehouseId;
        Sku = sku.Trim();
        ChannelCode = InvChannelLimit.ChuẩnHoáKenh(channelCode);
        AllottedQuantity = allottedQuantity;
        Priority = priority;
    }

    private static BusinessException Loi(string lyDo)
        => new BusinessException(InventoryErrorCodes.NganSachTonKhongHopLe).WithData("lyDo", lyDo);

    /// <summary>
    /// Đặt lại ngân sách (`ManualAllot`/`ManualAdjust`). **NÉM** khi: đã `Released`, số mới âm, hoặc
    /// **số mới nhỏ hơn phần đang giữ chỗ** (⇒ ATP âm ⇒ bán thứ không có).
    /// </summary>
    public void DatNganSach(int soLuongMoi)
    {
        if (Status == StockAllotmentStatus.Released)
        {
            throw Loi("Ngân sách ĐÃ NHẢ ⇒ không sửa được; hãy cấp lại (`ReAllot`).").WithData("status", Status.ToString());
        }

        if (soLuongMoi < 0)
        {
            throw Loi("Ngân sách KHÔNG được âm.").WithData("soLuong", soLuongMoi);
        }

        if (soLuongMoi < ReservedQuantity)
        {
            throw Loi("Ngân sách mới NHỎ HƠN phần đang giữ chỗ ⇒ ATP sẽ âm.")
                .WithData("soLuongMoi", soLuongMoi)
                .WithData("reserved", ReservedQuantity);
        }

        AllottedQuantity = soLuongMoi;
    }

    /// <summary>`Active → Frozen` — ngừng bán qua kênh nhưng **giữ** ngân sách (không ai lấy được).</summary>
    public void DongBang(Guid? nguoiDungId, DateTime atUtc)
    {
        if (Status != StockAllotmentStatus.Active)
        {
            throw Loi("Chỉ ngân sách `Active` mới đóng băng được.").WithData("status", Status.ToString());
        }

        Status = StockAllotmentStatus.Frozen;
        FrozenAtUtc = atUtc;
        FrozenByUserId = nguoiDungId;
    }

    /// <summary>`Frozen → Active`.</summary>
    public void MoDongBang(Guid? nguoiDungId)
    {
        if (Status != StockAllotmentStatus.Frozen)
        {
            throw Loi("Chỉ ngân sách `Frozen` mới mở đóng băng được.").WithData("status", Status.ToString());
        }

        Status = StockAllotmentStatus.Active;
        FrozenAtUtc = null;
        FrozenByUserId = nguoiDungId;
    }

    /// <summary>
    /// Nhả ngân sách (`→ Released`, `Allotted = 0`). 🚨 **NÉM** nếu còn giữ chỗ: nhả khi còn `Reserved`
    /// nghĩa là **bỏ rơi** phần hàng đã hứa với khách.
    /// </summary>
    public void Nha(string lyDo, DateTime atUtc)
    {
        if (Status == StockAllotmentStatus.Released)
        {
            throw Loi("Ngân sách ĐÃ nhả rồi.").WithData("status", Status.ToString());
        }

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw Loi("Nhả ngân sách BẮT BUỘC có lý do (`BR-12`).");
        }

        if (ReservedQuantity > 0)
        {
            throw Loi("Còn phần ĐANG GIỮ CHỖ ⇒ không nhả được (xử lý giữ chỗ trước).")
                .WithData("reserved", ReservedQuantity);
        }

        AllottedQuantity = 0;
        Status = StockAllotmentStatus.Released;
        ReleasedAtUtc = atUtc;
        ReleaseReason = lyDo.Length > 512 ? lyDo[..512] : lyDo;
    }

    /// <summary>
    /// Tăng phần giữ chỗ của kênh (**gọi trong luồng giữ chỗ**). **NÉM** khi ngân sách không `Active`,
    /// số lượng ≤ 0, hoặc **vượt ATP**.
    /// </summary>
    public void TangGiuCho(int soLuong)
    {
        if (Status != StockAllotmentStatus.Active)
        {
            throw Loi("Ngân sách KHÔNG ở trạng thái `Active` ⇒ kênh không bán được.").WithData("status", Status.ToString());
        }

        if (soLuong < 1)
        {
            throw Loi("Số lượng giữ chỗ phải ≥ 1.").WithData("soLuong", soLuong);
        }

        if (soLuong > AvailableToPromise)
        {
            throw Loi("Vượt ngân sách tồn của kênh.")
                .WithData("yeuCau", soLuong)
                .WithData("atp", AvailableToPromise)
                .WithData("allotted", AllottedQuantity)
                .WithData("reserved", ReservedQuantity);
        }

        ReservedQuantity += soLuong;
    }

    /// <summary>Giảm phần giữ chỗ (nhả chỗ / hết hạn / chốt đơn). **NÉM** nếu giảm quá số đang giữ.</summary>
    public void GiamGiuCho(int soLuong)
    {
        if (soLuong < 1)
        {
            throw Loi("Số lượng phải ≥ 1.").WithData("soLuong", soLuong);
        }

        if (soLuong > ReservedQuantity)
        {
            throw Loi("Giảm giữ chỗ NHIỀU HƠN số đang giữ ⇒ dữ liệu không nhất quán.")
                .WithData("soLuong", soLuong)
                .WithData("reserved", ReservedQuantity);
        }

        ReservedQuantity -= soLuong;
    }
}

/// <summary>
/// 🆕 **SỔ CÁI NGÂN SÁCH — APPEND-ONLY** (`InvStockAllotmentLedgers`) — `INV-003` §5.2.2 / **BR-07**.
/// 📌 Mỗi thay đổi ngân sách sinh **đúng một** dòng, **cùng UoW** với bảng trạng thái. Cấm UPDATE/DELETE.
/// </summary>
public class StockAllotmentLedger : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid StockAllotmentId { get; protected set; }

    /// <summary>Sao chép để tra cứu không cần join (cùng phong cách `StockLedgerEntry`).</summary>
    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public string ChannelCode { get; protected set; } = default!;

    public int QuantityDelta { get; protected set; }

    public int AllottedAfter { get; protected set; }

    public StockAllotmentLedgerReason Reason { get; protected set; }

    public string? ReferenceType { get; protected set; }

    public string? ReferenceId { get; protected set; }

    /// <summary>Khoá chống trùng của biên vào (`XC-002` BR-005) — **unique index có bộ lọc** ở DB.</summary>
    public string? IdempotencyKey { get; protected set; }

    public DateTime OccurredAtUtc { get; protected set; }

    public Guid? PerformedByUserId { get; protected set; }

    protected StockAllotmentLedger() { }

    public StockAllotmentLedger(
        Guid id,
        Guid stockAllotmentId,
        Guid warehouseId,
        string sku,
        string channelCode,
        int quantityDelta,
        int allottedAfter,
        StockAllotmentLedgerReason reason,
        DateTime occurredAtUtc,
        Guid? performedByUserId = null,
        string? referenceType = null,
        string? referenceId = null,
        string? idempotencyKey = null) : base(id)
    {
        StockAllotmentId = stockAllotmentId;
        WarehouseId = warehouseId;
        Sku = sku;
        ChannelCode = channelCode;
        QuantityDelta = quantityDelta;
        AllottedAfter = allottedAfter;
        Reason = reason;
        OccurredAtUtc = occurredAtUtc;
        PerformedByUserId = performedByUserId;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        IdempotencyKey = idempotencyKey;
    }
}

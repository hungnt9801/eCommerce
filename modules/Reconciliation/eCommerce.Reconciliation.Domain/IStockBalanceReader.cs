namespace eCommerce.Reconciliation;

/// <summary>Ảnh chụp tồn kho của **một** cặp `(kho, SKU)` — chỉ dữ liệu đối soát cần.</summary>
public readonly record struct TonKhoDoc(Guid WarehouseId, string Sku, decimal OnHand);

/// <summary>Một bút toán sổ cái ảnh hưởng `OnHand` (đối soát tự lọc theo <c>MovementType</c>).</summary>
public readonly record struct ButToanDoc(Guid WarehouseId, string Sku, StockMovementType MovementType, decimal QuantityDelta);

/// <summary>
/// 🚨 **HỢP ĐỒNG ĐỌC TỒN KHO** (`P0 #1`, 26/10/2026) — gỡ nợ *"module `Reconciliation` tham chiếu
/// `Inventory.Domain`"*.
///
/// 🔑 **VÌ SAO CẦN:** đối soát chỉ **đọc** hai thứ (`InventoryItem.OnHand`, `StockLedgerEntry`); nó **không**
/// được ghi, không gọi hàm miền, không cần entity. Cho module này thấy **entity** của Inventory là mở đường
/// cho người sau gọi `ApplyMovement`/sửa tồn từ module khác — đúng loại vi phạm biên giới khó thấy về sau.
///
/// 📌 **HIỆN THỰC Ở HOST** (`src/eCommerce.EntityFrameworkCore/Reconciliation/StockBalanceReader.cs`) vì host
/// được phép thấy cả hai module. 📌 Khi Inventory có `Application.Contracts` đọc tồn dùng chung thì chuyển
/// hiện thực về đó; **hợp đồng này không đổi**.
/// ⚠️ Hợp đồng **cố ý trả toàn bộ** (không phân trang) — khớp hành vi hiện tại; đối soát theo cửa sổ là việc
/// của `INV-001` §20.4, không phải của hợp đồng.
/// </summary>
public interface IStockBalanceReader
{
    /// <summary>Toàn bộ ảnh chụp tồn kho (mọi cặp kho, SKU đang có bản ghi).</summary>
    Task<IReadOnlyList<TonKhoDoc>> LayTonKhoAsync();

    /// <summary>Toàn bộ bút toán sổ cái (đối soát tự lọc `MovementType` ảnh hưởng `OnHand`).</summary>
    Task<IReadOnlyList<ButToanDoc>> LayButToanAsync();
}

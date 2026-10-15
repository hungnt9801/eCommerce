namespace eCommerce.Inventory;

/// <summary>
/// **TÊN KHOÁ PHÂN TÁN CỦA MIỀN KHO** (`INV-001` **ADR-08**; quy ước `XC-003` §5.4).
///
/// 🚨 **KHÔNG có tiền tố `ecommerce:`** — `AbpDistributedLock` đã tự thêm `KeyPrefix`
/// (bài học `PRC-001` **N3**: tài liệu `PRC-001` §8.2 ghi có tiền tố, **ngược** `XC-003` §5.4 ⇒ code theo `XC-003`).
///
/// 📌 **VÌ SAO KHOÁ THEO `(kho, SKU)` mà KHÔNG theo cả kho:** khoá theo cả kho thì hai nhân viên nhập hàng cho
/// **hai SKU khác nhau** trong cùng kho phải **chờ nhau** — chậm vô ích. Khoá theo `(kho, SKU)` chỉ tuần tự hoá
/// **đúng chỗ tranh chấp** (cùng một SKU trong cùng một kho).
/// </summary>
public static class InventoryLockKeys
{
    /// <summary>Khoá theo `(tenant, kho, SKU)` — dùng khi ghi sổ/giữ chỗ để chống bán vượt tồn.</summary>
    public static string TheoSku(Guid? tenantId, Guid warehouseId, string sku)
        => $"inventory:{tenantId?.ToString() ?? "host"}:{warehouseId}:{sku.Trim().ToLowerInvariant()}";
}

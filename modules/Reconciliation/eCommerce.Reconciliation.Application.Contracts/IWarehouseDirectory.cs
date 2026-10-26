namespace eCommerce.Reconciliation;

/// <summary>
/// Hợp đồng tra **mã kho** cho UI đối soát (host hiện thực) — gỡ nốt việc Application của module đọc
/// entity `Warehouse` của Inventory (`P0 #1`, 26/10/2026).
/// </summary>
public interface IWarehouseDirectory
{
    /// <summary>Trả map `id → Code` cho các kho được hỏi (kho không tồn tại thì vắng mặt).</summary>
    Task<Dictionary<Guid, string>> LayTenKhoAsync(IReadOnlyCollection<Guid> idKho);
}

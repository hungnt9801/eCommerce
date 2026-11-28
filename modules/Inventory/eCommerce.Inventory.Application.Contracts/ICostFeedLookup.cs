namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`W17-2` — LOẠI CHUYỂN ĐỘNG THEO NGHĨA ĐỊNH GIÁ** (`PRC-001-arch` §3.3; `D-321`).
/// 📌 Không phải nghĩa kế toán kho — chỉ để Pricing biết chuyển động nào làm **tăng tồn có giá** / **tăng không
/// giá** / **giảm tồn**. **Không** đổi số của enum cũ nào (`D-17`) — đây là dải mới.
/// </summary>
public enum LoaiChuyenDongGiaVon
{
    /// <summary>Tăng `OnHand` **+ có đơn giá** ⇒ tính lại bình quân.</summary>
    NhapCoGia = 0,

    /// <summary>Tăng `OnHand` **không có đơn giá dùng được** ⇒ giá vốn từ mốc này **không xác định**.</summary>
    NhapKhongGia = 1,

    /// <summary>Giảm `OnHand` ⇒ giảm số lượng, **không** đổi đơn giá bình quân.</summary>
    Xuat = 2
}

/// <summary>
/// Một chuyển động kho **đã chuẩn hoá cho định giá** (`PRC-001-arch` §3.3). Adapter Inventory sinh DTO này;
/// Pricing ánh xạ sang kiểu giá trị nội bộ (Domain Pricing **không** tham chiếu Inventory).
/// </summary>
public class CostMovementDto
{
    public string Sku { get; set; } = default!;

    /// <summary>Ngày theo **UTC+7** (`D-177` ③) — tính ở adapter (biên I/O).</summary>
    public DateOnly NgayKinhDoanh { get; set; }

    /// <summary>Thời điểm ghi sổ (UTC) — tiebreak tất định trong cùng ngày.</summary>
    public DateTime OccurredAtUtc { get; set; }

    public LoaiChuyenDongGiaVon Loai { get; set; }

    /// <summary>Dấu: nhập `&gt; 0`, xuất `&lt; 0`.</summary>
    public decimal SoLuongDelta { get; set; }

    /// <summary>Chỉ khi <see cref="LoaiChuyenDongGiaVon.NhapCoGia"/>; as-built = `StockDocumentLine.UnitPrice`.</summary>
    public decimal? DonGiaNhap { get; set; }

    /// <summary>
    /// `null` ⇒ **VND của tenant** (`D-321`). As-built `UnitPrice` **không có** cột tiền tệ và **không** có FX
    /// (`XC-001`) ⇒ v1 luôn `null`.
    /// </summary>
    public string? TienTeNhap { get; set; }

    /// <summary>Neo idempotency (dòng chứng từ / bút toán).</summary>
    public Guid ReferenceLineId { get; set; }
}

/// <summary>
/// 🚨 **`W17-2` — PORT ĐỌC SỔ CÁI KHO CHO PRICING** (`PRC-001-arch` §3.3). **CHỈ ĐỌC.**
///
/// Cùng khuôn `IStockReservationGateway`: interface **trần**, **không** `IApplicationService`, **không**
/// `[Authorize]`, **không** thành endpoint. Hiện thực ở `Inventory.Application` (đọc entity Domain **của chính
/// module Inventory**) — Pricing chỉ thấy DTO/port ở Contracts (`D-186` ③/M2).
/// 📌 Grain = **SKU toàn hệ thống**: **bỏ** cặp chuyển kho (net 0) + giữ chỗ/tạm giữ (`D-318` C9/`D-321`).
/// </summary>
public interface ICostFeedLookup
{
    /// <summary>
    /// Chuyển động ảnh hưởng định giá trong `[from, to]` (ngày kinh doanh UTC+7), **tất định** theo
    /// `(NgayKinhDoanh, OccurredAtUtc, ReferenceLineId)`. Bỏ chuyển kho + giữ chỗ/tạm giữ.
    /// Loại lạ / lệch dấu ⇒ **NÉM `eCommerce:Pricing:0014`** (fail-closed — `D-321`).
    /// </summary>
    Task<List<CostMovementDto>> LayChuyenDongAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<string>? skus = null);
}

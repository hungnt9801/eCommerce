using System.ComponentModel.DataAnnotations;
using eCommerce.Pricing.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Pricing.Dtos
{
    /// <summary>Một dòng cần tra giá vốn: `Sku` tại ngày <see cref="At"/>.</summary>
    public class CostLookupItemDto
    {
        public string Sku { get; set; } = default!;

        public DateOnly At { get; set; }
    }

    /// <summary>Đầu vào tra giá vốn **theo lô** (job báo cáo — tránh N+1). Dùng DTO, **KHÔNG** tuple.</summary>
    public class GetApprovedCostManyInput
    {
        public List<CostLookupItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// 🆕 **`W17-2`** — kết quả tra giá vốn **ĐÃ DUYỆT** tại một ngày (`PRC-001-arch` §3.1).
    ///
    /// 🚨 `AmountValue = null` ⇒ **KHÔNG** có dòng `DaDuyet` hiệu lực ⇒ bên gọi **PHẢI** đặt `CostMissing = true`
    /// và để cột tiền `NULL` — **CẤM ghi `0`** (`D-177` ⑥). Giá vốn `0` **hợp lệ** (`CoGiaVon = true`).
    /// </summary>
    public class CostPriceLookupDto
    {
        public string Sku { get; set; } = default!;

        public DateOnly At { get; set; }

        /// <summary>`null` = chưa có dòng `DaDuyet` hiệu lực.</summary>
        public decimal? AmountValue { get; set; }

        /// <summary>`null` khi <see cref="AmountValue"/> `null`.</summary>
        public string? CurrencyCode { get; set; }

        public bool CoGiaVon => AmountValue is not null;
    }

    /// <summary>
    /// 🆕 **`W17-2`** — đầu vào **tạo** giá vốn thủ công (`PRC-001-arch` §3.2). Tạo ra dòng **CHƯA duyệt**
    /// (`DaDuyet = false`) — vẫn giữ **2 bước** (`Q4` còn `Open`).
    /// </summary>
    public class CreateCostPriceDto
    {
        [Required, StringLength(64)]
        public string Sku { get; set; } = default!;

        // `PrcCostPrices.AmountValue` là numeric(18,4) ⇒ tối đa 14 chữ số nguyên (KHÔNG dùng biên 18,2 của dòng phiếu kho).
        [Range(typeof(decimal), "0", "99999999999999.9999")]
        public decimal AmountValue { get; set; }

        [Required, StringLength(8)]
        public string CurrencyCode { get; set; } = "VND";

        public DateOnly EffectiveFrom { get; set; }

        public DateOnly? EffectiveTo { get; set; }

        /// <summary>🚨 **BẮT BUỘC** (`XC-002`) — cùng khoá ⇒ trả dòng đã tạo, **không** tạo thứ hai.</summary>
        [Required, StringLength(128)]
        public required string IdempotencyKey { get; set; }
    }

    /// <summary>🆕 **`W17-2`** — tóm tắt một lần chạy động cơ (endpoint command; KHÔNG trả entity).</summary>
    public class CostEngineRunDto
    {
        public Guid Id { get; set; }

        public string RunKey { get; set; } = default!;

        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public string Trigger { get; set; } = default!;

        public string TrangThai { get; set; } = default!;

        public int Attempt { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public DateTime? FinishedAtUtc { get; set; }

        public int RowsUpserted { get; set; }

        public int RowsSkippedApproved { get; set; }

        public int RowsFlaggedMissing { get; set; }

        public string? ErrorMessage { get; set; }
    }
}

namespace eCommerce.Pricing
{
    /// <summary>
    /// 🚨 **`W17-2` — PORT ĐỌC NỘI BỘ CHO MODULE TIÊU THỤ** (`PRC-001-arch` §3.1).
    ///
    /// **KHÔNG** phải `IApplicationService`, **KHÔNG** `[Authorize]`, **KHÔNG** thành endpoint — cùng khuôn
    /// `IStockReservationGateway`. Lý do **P1**: `CostPriceAppService` có `[Authorize]` **cấp class** ⇒ job nền
    /// không có principal gọi in-process sẽ **NÉM** `AbpAuthorizationException`.
    /// 🔒 **CHỈ trả dòng `DaDuyet = true`** hiệu lực tại ngày hỏi (`D-177` ④/`D-186` ②).
    /// </summary>
    public interface ICostPriceLookup
    {
        /// <summary>Giá vốn **ĐÃ DUYỆT** hiệu lực tại `at`. `null` = **KHÔNG** có ⇒ bên gọi đặt `CostMissing`.</summary>
        Task<CostPriceLookupDto?> GetApprovedCostAsync(string sku, DateOnly at);

        /// <summary>Lô cho job báo cáo (tránh N+1). Dùng DTO — **KHÔNG** tuple.</summary>
        Task<List<CostPriceLookupDto>> GetApprovedCostManyAsync(GetApprovedCostManyInput input);
    }

    /// <summary>
    /// 🆕 **`W17-2` — ĐƯỜNG GHI + DUYỆT GIÁ VỐN** (`PRC-001-arch` §3.2). `[Authorize]` gắn ở **lớp hiện thực**
    /// (Contracts không tham chiếu MVC — giữ layering).
    /// </summary>
    public interface ICostPriceAdminAppService : IApplicationService
    {
        /// <summary>Tạo giá vốn **CHƯA duyệt**; cùng `IdempotencyKey` ⇒ trả dòng cũ (không tạo thứ hai).</summary>
        Task<CostPriceDto> CreateAsync(CreateCostPriceDto input);

        /// <summary>Gọi `CostPrice.Duyet`; duyệt lặp ⇒ **NÉM `0009`** (không trả kết quả cũ — `D12`).</summary>
        Task<CostPriceDto> ApproveAsync(Guid id);
    }

    /// <summary>
    /// 🆕 **`W17-2`** — **ENDPOINT COMMAND ON-DEMAND** chạy động cơ giá vốn (`D-323`).
    /// 📌 Worker nền **KHÔNG** gọi lớp này (worker không có principal — resolve `CostEngineService` trực tiếp).
    /// </summary>
    public interface ICostEngineAppService : IApplicationService
    {
        /// <summary>
        /// Chạy động cơ cho khoảng `[from, to]` (ngày kinh doanh UTC+7). `null` = động cơ **đang tắt**
        /// (`Pricing:GiaVonDongCo = false`); cấu hình sai/loại chuyển động lạ ⇒ **NÉM `0014`**.
        /// </summary>
        Task<CostEngineRunDto?> ChayAsync(DateOnly from, DateOnly to, string trigger);
    }
}

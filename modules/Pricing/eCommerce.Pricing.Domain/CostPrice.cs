using eCommerce.Kernel.Money;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// **Giá vốn theo SKU và theo kỳ** (`PrcCostPrices`) — `PRC-001` §5.2, **`S12`**.
///
/// 🚨 **BR-013 — ĐÂY LÀ DỮ LIỆU NỘI BỘ, KHÔNG BAO GIỜ ĐƯỢC TRẢ CHO NGƯỜI MUA.**
/// Giá vốn là **biên lợi nhuận** của công ty: lộ ra là đối thủ đọc được bằng một lệnh `curl`
/// (`S8-W3`). Vì vậy:
///   • app service đọc giá vốn có **quyền RIÊNG** (`eCommerce.Pricing.CostPrice.View`), **không** dùng
///     quyền xem giá bán (`Pricing.Default`);
///   • **cấm** luồng công khai/storefront chạm tới (`StorefrontCongKhaiTests` + E2E `S8-W3` canh);
///   • **KHÔNG** có API ghi trong `S12`: nguồn dữ liệu giá vốn (nhập tay / import NCC / bình quân gia quyền)
///     là **câu hỏi mở `Q2`** của bản bàn giao — bịa ra một đường ghi là quyết định của chủ dự án, không
///     phải của phiên code.
///
/// 📌 **VÌ SAO `Amount`/`CurrencyCode` là HAI CỘT (không map thẳng `Money`):** `PRC-001` **BR-001** cấm
/// lưu tiền mà **không** có tiền tệ đi kèm; `Money` là lớp bất biến có ctor riêng nên EF không
/// value-convert sạch được. Cách làm này **giống hệt** `PriceListItem`/`ChannelPrice` — đọc lại đúng
/// loại tiền, không tự gán `Money.Vnd` cho một dòng **USD** (sai 25.000 lần, không lỗi nào).
///
/// 📌 **VÌ SAO `DateOnly` (không `timestamptz`):** giá vốn đổi theo **NGÀY**, không theo giờ
/// (`PRC-001` **D4/BR-003**). Dùng `timestamp` khiến câu hỏi *"đơn ngày 20/10 tính giá vốn nào?"*
/// phụ thuộc **múi giờ của máy chạy** — cùng một đơn ra hai đáp án. Tiền lệ trong repo: `TaxRateSchedule`
/// (`EffectiveFrom` kiểu `date`).
///
/// 📌 **VÌ SAO KHÔNG có state machine:** giá vốn **không** phát hành cho khách như bảng giá; nó chỉ có
/// trạng thái "đã duyệt" (kế toán duyệt) hay chưa — `ApprovedBy`/`ApprovedAtUtc`. Duyệt **hai lần**
/// ⇒ **NÉM** (`D12`: cấm no-op im lặng), vì lần duyệt thứ hai ghi đè người duyệt đầu tiên và **xoá dấu
/// vết ai chịu trách nhiệm** về con số đó.
/// </summary>
public class CostPrice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>
    /// SKU nội bộ — **chuỗi**, KHÔNG phải FK tới `ProductVariant` (`PRC-001` §15.1 **Q1**:
    /// Pricing **không** tham chiếu Catalog). Trùng SKU với Catalog là **hợp lệ về kiểu** và được kiểm
    /// ở tầng ứng dụng/đối soát, không bằng khoá ngoại.
    /// </summary>
    public string Sku { get; protected set; } = default!;

    /// <summary>Giá vốn (`decimal`) — **luôn** đi kèm <see cref="CurrencyCode"/> (`PRC-001` **BR-001**).</summary>
    public decimal AmountValue { get; protected set; }

    /// <summary>Mã ISO loại tiền (`VND`, `USD`…) — cột **bắt buộc** đi kèm mọi cột tiền.</summary>
    public string CurrencyCode { get; protected set; } = "VND";

    /// <summary>Giá vốn dưới dạng `Money` (computed) — so sánh/cộng **phải** đi qua đây (`BR-002`).</summary>
    public Money Amount => Money.Cua(AmountValue, Currency.FromCode(CurrencyCode));

    /// <summary>Ngày bắt đầu hiệu lực (**`date`** — xem chú thích lớp).</summary>
    public DateOnly EffectiveFrom { get; protected set; }

    /// <summary>Ngày kết thúc hiệu lực; `NULL` = vô thời hạn (`PRC-001` **D4**).</summary>
    public DateOnly? EffectiveTo { get; protected set; }

    /// <summary>
    /// 🆕 **`W17-2`** — nguồn sinh dòng (`PRC-001-arch` §4.1 **S4**). `NULL` = dòng **trước** `W17-2` ⇒ coi như
    /// thủ công. ⚠️ Cột **nullable**, **không** `SET NOT NULL` (expand-only — `D-318` C4).
    /// </summary>
    public CostPriceSource? NguonTinh { get; protected set; }

    /// <summary>🆕 **`W17-2`** — `Id` lần chạy động cơ sinh/cập nhật dòng (`PrcCostEngineRuns`). **Không** FK.</summary>
    public Guid? RunId { get; protected set; }

    /// <summary>🆕 **`W17-2`** — thời điểm động cơ tính (UTC). `NULL` = dòng thủ công/dữ liệu cũ.</summary>
    public DateTime? ComputedAtUtc { get; protected set; }

    /// <summary>
    /// 🆕 **`W17-2`** — khoá idempotency của **đường ghi thủ công** (`PRC-001-arch` §4.1 **S7**/§5.6).
    /// `NULL` = dòng do động cơ sinh hoặc dữ liệu cũ (unique index **có bộ lọc** `IS NOT NULL`).
    /// </summary>
    public string? IdempotencyKey { get; protected set; }

    /// <summary>Kế toán duyệt — `NULL` = **chưa duyệt** (giá vốn chưa duyệt **không** dùng để tính lãi gộp).</summary>
    public Guid? ApprovedBy { get; protected set; }

    public DateTime? ApprovedAtUtc { get; protected set; }

    /// <summary>Đã duyệt hay chưa. 📌 Điều kiện là **cả hai** cột: thiếu một cột là dấu vết duyệt không đầy đủ.</summary>
    public bool DaDuyet => ApprovedBy is not null && ApprovedAtUtc is not null;

    protected CostPrice() { }

    public CostPrice(
        Guid id,
        string sku,
        Money amount,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("truong", nameof(sku));
        }

        // 🚨 `PRC-001` §5.2: giá bán/vốn **KHÔNG ÂM**. Điều chỉnh giảm dùng khuyến mãi (`PRO-001`),
        // **không** dùng giá âm. ⚠️ Cố ý **KHÔNG** đòi `> 0`: giá vốn `0` là **hợp lệ** (hàng tặng,
        // hàng mẫu) — cùng quyết định với `CHECK (Amount >= 0)` của `PriceListItem`.
        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "Giá vốn KHÔNG được âm.")
                .WithData("sku", sku)
                .WithData("amount", amount.Amount);
        }

        // 📌 Khoảng hiệu lực phải **tiến**: kỳ NGƯỢC (`EffectiveTo < EffectiveFrom`) không tồn tại ⇒ mọi
        // truy vấn theo ngày **không bao giờ** thấy dòng này, và giá vốn của SKU đó **biến mất im lặng**.
        // 📌 `EffectiveTo == EffectiveFrom` là kỳ **MỘT NGÀY** — **HỢP LỆ**: hai ngày nhập liền kề sinh kỳ
        // `From == To` theo đúng thiết kế (`D-326`, `PRC-001` §5.2 CK `>=`, `CoHieuLucTai` bao gồm).
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "`EffectiveTo` phải ≥ `EffectiveFrom` (kỳ MỘT NGÀY hợp lệ).")
                .WithData("tu", effectiveFrom)
                .WithData("den", effectiveTo);
        }

        Sku = sku.Trim();
        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;

        // 📌 Dòng tạo qua constructor (đường ghi thủ công/seed) mặc định là `ThuCong`; động cơ gọi
        // `DanhDauDongCo(...)` ngay sau đó để đổi thành `DongCo`. Dòng **cũ trong DB** vẫn `NULL` (additive).
        NguonTinh = CostPriceSource.ThuCong;
    }

    /// <summary>
    /// 🆕 **`W17-2`** — gắn **khoá idempotency** cho đường ghi thủ công (`PRC-001-arch` §5.6).
    /// **NÉM** khi rỗng: gọi tạo mà **không** có khoá ⇒ mỗi lần retry sinh một dòng trùng (`XC-002`).
    /// </summary>
    public void GanKhoaIdempotency(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessException(PricingErrorCodes.ThieuKhoaIdempotency)
                .WithData("sku", Sku)
                .WithData("lyDo", "Đường ghi giá vốn BẮT BUỘC kèm `IdempotencyKey` (XC-002).");
        }

        IdempotencyKey = idempotencyKey.Trim();
    }

    /// <summary>
    /// 🆕 **`W17-2`** — đánh dấu dòng do **động cơ** sinh/cập nhật (`NguonTinh = DongCo`, `RunId`, `ComputedAtUtc`).
    /// </summary>
    public void DanhDauDongCo(Guid runId, DateTime computedAtUtc)
    {
        if (runId == Guid.Empty)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "Động cơ giá vốn phải có `RunId` (không nhận `Guid.Empty`).")
                .WithData("sku", Sku);
        }

        NguonTinh = CostPriceSource.DongCo;
        RunId = runId;
        ComputedAtUtc = computedAtUtc;
    }

    /// <summary>
    /// 🆕 **`W17-2`** — cập nhật dòng do động cơ tính lại **khi DÒNG CHƯA DUYỆT**.
    ///
    /// 🚨 **ĐÃ DUYỆT ⇒ NÉM `0015`** — **không** ghi đè im lặng số đã duyệt (`D-177` ③/§5.4). Đây là **lớp
    /// trong**; động cơ còn kiểm trước để áp `Pricing:ChoNhapLuiNgay` (bỏ qua + đếm, hoặc NÉM).
    /// </summary>
    public void CapNhatGiaVonChuaDuyet(
        Money amount, DateOnly? effectiveTo, Guid runId, DateTime computedAtUtc)
    {
        if (DaDuyet)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongTheTinhLai)
                .WithData("sku", Sku)
                .WithData("effectiveFrom", EffectiveFrom)
                .WithData("lyDo", "Dòng ĐÃ DUYỆT ⇒ KHÔNG được tính lại/ghi đè (D-177 ③).");
        }

        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "Giá vốn KHÔNG được âm.")
                .WithData("sku", Sku)
                .WithData("amount", amount.Amount);
        }

        // 📌 `EffectiveTo == EffectiveFrom` là kỳ **MỘT NGÀY** — **HỢP LỆ** (`D-326`); chỉ kỳ **NGƯỢC**
        // (`EffectiveTo < EffectiveFrom`) mới NÉM, khớp CK `CK_PrcCostPrices_EffectiveRange` (`>=`).
        if (effectiveTo is not null && effectiveTo < EffectiveFrom)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "`EffectiveTo` phải ≥ `EffectiveFrom` (kỳ MỘT NGÀY hợp lệ).")
                .WithData("sku", Sku);
        }

        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
        EffectiveTo = effectiveTo;
        DanhDauDongCo(runId, computedAtUtc);
    }

    /// <summary>Giá vốn có hiệu lực tại ngày <paramref name="at"/> không (`PRC-001` **BR-003**: bắt buộc kèm ngày).</summary>
    public bool CoHieuLucTai(DateOnly at)
        => at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);

    /// <summary>
    /// Kế toán duyệt giá vốn. 🚨 **NÉM** khi thiếu người duyệt / thiếu thời điểm / **đã duyệt rồi**
    /// (`D12`: cấm no-op im lặng — xem chú thích lớp).
    /// </summary>
    public void Duyet(Guid nguoiDuyet, DateTime atUtc)
    {
        if (nguoiDuyet == Guid.Empty)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "Phải có NGƯỜI DUYỆT (không nhận `Guid.Empty`).")
                .WithData("sku", Sku);
        }

        if (DaDuyet)
        {
            throw new BusinessException(PricingErrorCodes.GiaVonKhongHopLe)
                .WithData("lyDo", "Giá vốn này ĐÃ được duyệt ⇒ không duyệt lại (sẽ xoá dấu vết người duyệt trước).")
                .WithData("sku", Sku)
                .WithData("approvedBy", ApprovedBy!.Value);
        }

        ApprovedBy = nguoiDuyet;
        ApprovedAtUtc = atUtc;
    }
}

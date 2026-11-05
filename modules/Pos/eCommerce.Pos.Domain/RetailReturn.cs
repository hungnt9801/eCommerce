using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pos;

/// <summary>
/// ↩️ **PHIẾU TRẢ HÀNG** (`PosRetailReturns`) — `WP-06`, màn #14 `pos/return` (`04 §5`).
///
/// 📌 **CHỈ TRẢ TỪ HOÁ ĐƠN** (`RetailBillId` **bắt buộc**): Nhanh có cả *"trả không cần hoá đơn"*, nhưng
/// đường đó đòi **chọn kho nhận + loại phiếu + giá vốn** và **không** đảo được công nợ (không biết trả cho
/// hoá đơn nào). P0 chọn đường **có chứng từ gốc** — trả không hoá đơn ghi ở `handover-n7` §5 (nợ có mã).
///
/// 🚨 **BA BẤT BIẾN:**
/// <list type="number">
///   <item>**Trả không vượt số đã mua**: `Σ qty các phiếu trả ≤ SL dòng hoá đơn` — kiểm ở domain
///     (`RetailBillLine.CongTra`) **và** `CHECK` ở DB (`0 ≤ ReturnedQuantity ≤ Quantity`).</item>
///   <item>**Hoàn tiền §5.4**: `tổng trả = tiền hàng trả − phí trả hàng`; phần này **trừ công nợ trước**
///     (`DebtReductionAmount`), còn lại mới **hoàn tiền mặt/CK** (`RefundAmount`). 📌 Trả tiền mặt cho hoá đơn
///     còn nợ là **đưa tiền ra** trong khi khách còn nợ — hai chiều tiền ngược nhau trên cùng một khách.</item>
///   <item>**Nhập lại kho là chứng từ RIÊNG** (`StockDocumentId`) — sổ cái append-only: không sửa bút toán
///     xuất cũ, chỉ thêm bút toán nhập (`CustomerReturn`).</item>
/// </list>
/// </summary>
public class RetailReturn : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiSoPhieuToiDa = 64;
    public const int DoDaiGhiChuToiDa = 1024;

    public Guid? TenantId { get; protected set; }

    /// <summary>Số phiếu trả (`PT-yyyyMMdd-NN`) — in cho khách.</summary>
    public string Number { get; protected set; } = default!;

    /// <summary>Hoá đơn gốc — **bắt buộc** (`D-42`).</summary>
    public Guid RetailBillId { get; protected set; }

    /// <summary>Kho **nhận hàng trả** — lấy theo kho của hoá đơn gốc (Nhanh: trả có hoá đơn **không** chọn kho).</summary>
    public Guid WarehouseId { get; protected set; }

    /// <summary>Ca thu ngân lúc trả (để trừ `CashRefund` khỏi kết ca) — `null` khi trả ngoài ca.</summary>
    public Guid? CashSessionId { get; protected set; }

    public Guid? CustomerId { get; protected set; }

    public RetailReturnStatus Status { get; protected set; } = RetailReturnStatus.Posted;

    /// <summary>Tiền hàng trả = Σ (SL trả × đơn giá **snapshot** của dòng hoá đơn gốc).</summary>
    public decimal GoodsAmount { get; protected set; }

    /// <summary>Phí trả hàng (Nhanh: *"Phí trả hàng"* — có thể 0).</summary>
    public decimal ReturnFeeAmount { get; protected set; }

    /// <summary>Phần **trừ vào công nợ** của hoá đơn gốc.</summary>
    public decimal DebtReductionAmount { get; protected set; }

    /// <summary>Phần **hoàn tiền** cho khách (mặt/CK) = `GoodsAmount − ReturnFeeAmount − DebtReductionAmount`.</summary>
    public decimal RefundAmount { get; protected set; }

    public RetailPaymentMethod RefundMethod { get; protected set; } = RetailPaymentMethod.Cash;

    /// <summary>Điểm đã **thu hồi** (trừ lại điểm đã tích của phần hàng trả) — `WP-07` mới ghi vào sổ khách.</summary>
    public decimal PointsReversed { get; protected set; }

    public string? Note { get; set; }

    /// <summary>Chứng từ kho `CustomerReturn` đã sinh (nhập lại hàng).</summary>
    public Guid? StockDocumentId { get; protected set; }

    public List<RetailReturnLine> Lines { get; protected set; } = new();

    protected RetailReturn() { }

    public RetailReturn(
        Guid id,
        string number,
        Guid retailBillId,
        Guid warehouseId,
        Guid? cashSessionId = null,
        Guid? customerId = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("truong", nameof(number))
                .WithData("lyDo", "Số phiếu trả là BẮT BUỘC.");
        }

        if (number.Trim().Length > DoDaiSoPhieuToiDa)
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("truong", nameof(number)).WithData("toiDa", DoDaiSoPhieuToiDa);
        }

        if (retailBillId == Guid.Empty)
        {
            throw new BusinessException(PosErrorCodes.KhongTimThayHoaDon)
                .WithData("lyDo", "Phiếu trả PHẢI gắn hoá đơn gốc (P0 chỉ hỗ trợ trả có hoá đơn).");
        }

        if (warehouseId == Guid.Empty)
        {
            throw new BusinessException(PosErrorCodes.ThieuKho)
                .WithData("lyDo", "Phiếu trả phải biết KHO nhận hàng (lấy từ hoá đơn gốc).");
        }

        Number = number.Trim();
        RetailBillId = retailBillId;
        WarehouseId = warehouseId;
        CashSessionId = cashSessionId;
        CustomerId = customerId;
    }

    /// <summary>
    /// 🚨 **TÍNH TIỀN TRẢ** — hàm **thuần** về mặt tiền; `conNoCuaHoaDon` do app service truyền vào
    /// (domain không đọc DB). 📌 Ném `0019` nếu tổng trả vượt `conNo + có thể hoàn`… thực tế **không** ném:
    /// trả hàng cho hoá đơn **đã trả đủ** là chuyện bình thường (hoàn tiền mặt).
    /// </summary>
    public void TinhTien(
        decimal returnFeeAmount,
        decimal conNoCuaHoaDon,
        RetailPaymentMethod refundMethod,
        decimal pointsReversed = 0m,
        string? ghiChu = null)
    {
        if (Lines.Count == 0)
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("lyDo", "Phiếu trả phải có ÍT NHẤT một dòng.");
        }

        if (returnFeeAmount < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("returnFeeAmount", returnFeeAmount)
                .WithData("lyDo", "Phí trả hàng KHÔNG được âm.");
        }

        if (conNoCuaHoaDon < 0m)
        {
            conNoCuaHoaDon = 0m;
        }

        if (pointsReversed < 0m)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("pointsReversed", pointsReversed)
                .WithData("lyDo", "Điểm thu hồi KHÔNG được âm.");
        }

        if (!Enum.IsDefined(refundMethod))
        {
            throw new BusinessException(PosErrorCodes.PhuongThucKhongHopLe)
                .WithData("phuongThuc", (int)refundMethod);
        }

        // 📌 Phương thức **điểm** không dùng để HOÀN TIỀN (không "trả lại điểm" bằng dòng thanh toán);
        //    điểm hoàn nằm ở `PointsReversed` (WP-07 ghi sổ khách).
        if (refundMethod == RetailPaymentMethod.Points)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("phuongThuc", refundMethod.ToString())
                .WithData("lyDo", "Hoàn tiền KHÔNG dùng phương thức Điểm — dùng PointsReversed.");
        }

        GoodsAmount = Math.Round(Lines.Sum(x => x.RefundAmount), 2, MidpointRounding.AwayFromZero);

        var conLai = GoodsAmount - returnFeeAmount;

        if (conLai < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("goodsAmount", GoodsAmount)
                .WithData("returnFeeAmount", returnFeeAmount)
                .WithData("lyDo", "Phí trả hàng VƯỢT tiền hàng trả ⇒ khách phải trả thêm — P0 chưa hỗ trợ.");
        }

        ReturnFeeAmount = Math.Round(returnFeeAmount, 2, MidpointRounding.AwayFromZero);

        // 🚨 THỨ TỰ §5.4: trừ CÔNG NỢ trước, hoàn tiền sau.
        DebtReductionAmount = Math.Round(Math.Min(conLai, conNoCuaHoaDon), 2, MidpointRounding.AwayFromZero);
        RefundAmount = Math.Round(conLai - DebtReductionAmount, 2, MidpointRounding.AwayFromZero);
        RefundMethod = refundMethod;
        PointsReversed = Math.Round(pointsReversed, 2, MidpointRounding.AwayFromZero);
        Note = string.IsNullOrWhiteSpace(ghiChu) ? Note : ghiChu.Trim();
        Status = RetailReturnStatus.Posted;
    }

    /// <summary>Gắn chứng từ kho vừa sinh (nhập lại hàng).</summary>
    public void GanChungTuKho(Guid stockDocumentId)
    {
        StockDocumentId = stockDocumentId;
    }

    /// <summary>Nạp dòng đã đọc từ DB (tầng Application) — không phải đường ghi.</summary>
    public void GanDong(IReadOnlyList<RetailReturnLine> dong)
    {
        Lines = dong.ToList();
    }
}

/// <summary>**MỘT DÒNG PHIẾU TRẢ** (`PosRetailReturnLines`).</summary>
public class RetailReturnLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiSkuToiDa = 64;

    public Guid? TenantId { get; protected set; }

    public Guid RetailReturnId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public decimal Quantity { get; protected set; }

    /// <summary>Đơn giá **snapshot** lấy từ dòng hoá đơn gốc — 📌 KHÔNG lấy giá hiện tại: khách trả theo
    /// giá **đã mua**, không theo giá hôm nay.</summary>
    public decimal UnitPriceSnapshot { get; protected set; }

    /// <summary>Tiền hoàn của dòng = `SL × đơn giá snapshot`.</summary>
    public decimal RefundAmount { get; protected set; }

    protected RetailReturnLine() { }

    public RetailReturnLine(Guid id, Guid retailReturnId, string sku, decimal quantity, decimal unitPriceSnapshot)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("truong", nameof(sku))
                .WithData("lyDo", "Dòng trả phải có SKU.");
        }

        if (sku.Trim().Length > DoDaiSkuToiDa)
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("truong", nameof(sku)).WithData("toiDa", DoDaiSkuToiDa);
        }

        if (quantity <= 0m)
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("sku", sku).WithData("quantity", quantity)
                .WithData("lyDo", "Số lượng trả phải LỚN HƠN 0.");
        }

        if (unitPriceSnapshot < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("unitPriceSnapshot", unitPriceSnapshot)
                .WithData("lyDo", "Đơn giá snapshot KHÔNG được âm.");
        }

        RetailReturnId = retailReturnId;
        Sku = sku.Trim();
        Quantity = quantity;
        UnitPriceSnapshot = unitPriceSnapshot;
        RefundAmount = Math.Round(quantity * unitPriceSnapshot, 2, MidpointRounding.AwayFromZero);
    }
}

using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pos;

/// <summary>
/// 🧾 **HOÁ ĐƠN BÁN LẺ** (`PosRetailBills`) — `WP-06` (`07 §WP-06` việc 1).
///
/// 🔑 **AGGREGATE RIÊNG, KHÔNG TÁI DÙNG `Order`** (`D-18`, `02 §4`): hoá đơn quầy **khác** đơn online ở
/// bốn điểm không thể ép chung: ① **không có** vòng đời giao hàng; ② **luôn** đã thu tiền (hoặc ghi nợ có hạn)
/// tại lúc lưu; ③ gắn **ca thu ngân** — tiền mặt phải khớp két khi kết ca; ④ **nhiều phương thức** trên một
/// hoá đơn là chuyện bình thường (tiền mặt + chuyển khoản + điểm).
///
/// 🚨 **BỐN BẤT BIẾN:**
/// <list type="number">
///   <item>**Tiền do HÀM THUẦN tính** (`TinhTienHoaDon`) — entity **không** nhận `TotalPayment` từ client.
///     Nhận số client gửi là mở đường cho *"client sửa tổng tiền"* (một dạng gian lận giá).</item>
///   <item>**Idempotent theo `ClientBillId`** (`D-42`): offline gửi lại ⇒ **một** hoá đơn. Khoá do **máy POS
///     sinh** (UUID v4) và **không đổi** giữa các lần retry; DB có unique index `(TenantId, ClientBillId)`.</item>
///   <item>**Huỷ là ĐẢO, không phải XOÁ**: hoá đơn đã bán đã đẩy tồn kho + đã ghi tiền ⇒ huỷ phải sinh
///     chứng từ nhập lại **và** đảo công nợ/điểm (audit thấy cả hai vế). Huỷ **hai lần** không đảo hai lần.</item>
///   <item>**Ca đã kết ⇒ KHÔNG huỷ** (`0009`/`0010`): tiền mặt của ca đã **bàn giao và đếm**; huỷ sau đó làm
///     báo cáo kết ca đã in **sai** mà không ai đối chiếu lại được. Phải làm **phiếu trả hàng** (có chứng từ).</item>
/// </list>
/// </summary>
public class RetailBill : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiSoHoaDonToiDa = 64;
    public const int DoDaiGhiChuToiDa = 2048;
    public const int DoDaiLyDoHuyToiDa = 512;
    public const int DoDaiMaThamChieuToiDa = 128;

    public Guid? TenantId { get; protected set; }

    /// <summary>Số hoá đơn (duy nhất trong tenant) — in lên bill, đối chiếu với phiếu xuất kho.</summary>
    public string Number { get; protected set; } = default!;

    /// <summary>
    /// 🚨 **KHOÁ IDEMPOTENCY do MÁY POS sinh** (`D-42`) — giữ nguyên khi offline retry.
    /// DB: unique `(TenantId, ClientBillId)` với `NULLS NOT DISTINCT` + **không** lọc NULL.
    /// </summary>
    public Guid ClientBillId { get; protected set; }

    /// <summary>Kho xuất hàng — mọi dòng của hoá đơn trừ tồn **cùng một kho** (quầy bán tại một kho).</summary>
    public Guid WarehouseId { get; protected set; }

    /// <summary>Ca thu ngân đang mở lúc bán — dùng để tính `ExpectedCash` khi kết ca.</summary>
    public Guid CashSessionId { get; protected set; }

    /// <summary>`null` = **khách vãng lai** (`ADR-03`: bán được mà không cần hồ sơ khách).</summary>
    public Guid? CustomerId { get; protected set; }

    /// <summary>Nhân viên bán hàng (tính doanh số/lương) — khác người thu tiền (`CreatorId`).</summary>
    public Guid? SaleStaffId { get; protected set; }

    public RetailBillStatus Status { get; protected set; } = RetailBillStatus.Completed;

    // ── TIỀN (mọi số do `TinhTienHoaDon` tính, KHÔNG nhận từ client) ─────────────────────────────

    /// <summary>Tiền hàng = Σ thành tiền dòng **sau** chiết khấu dòng.</summary>
    public decimal Subtotal { get; protected set; }

    /// <summary>Tổng chiết khấu **theo dòng** — nếu &gt; 0 thì `DiscountTotal` **bắt buộc** bằng 0.</summary>
    public decimal LineDiscountTotal { get; protected set; }

    /// <summary>Chiết khấu **tổng hoá đơn** (`F6`) — nếu &gt; 0 thì mọi chiết khấu dòng **bắt buộc** bằng 0.</summary>
    public decimal DiscountTotal { get; protected set; }

    public decimal VatTotal { get; protected set; }

    /// <summary>
    /// **Tiền mặt khách đưa** (Nhanh: *"Tiền khách đưa"*) — chỉ để tính tiền thối và in bill;
    /// **không** phải một dòng thanh toán (nên không làm lệch `PaidAmount`).
    /// </summary>
    public decimal CustomerGivenAmount { get; protected set; }

    public decimal PointsUsed { get; protected set; }

    public decimal PointsUsedAmount { get; protected set; }

    /// <summary>Điểm **tích** của hoá đơn (`WP-06` chỉ **ghi nhận**; cộng vào khách là việc `WP-07`).</summary>
    public decimal PointsEarned { get; protected set; }

    /// <summary>**Phải trả** = `Subtotal − DiscountTotal − PointsUsedAmount + VatTotal`.</summary>
    public decimal TotalPayment { get; protected set; }

    /// <summary>Đã trả = Σ tiền các phương thức (tiền mặt + CK + thẻ + điểm + trả góp).</summary>
    public decimal PaidAmount { get; protected set; }

    /// <summary>Tiền thối lại khách (`PaidAmount − TotalPayment`, ≥ 0) — trả **bằng tiền mặt**.</summary>
    public decimal ChangeAmount { get; protected set; }

    /// <summary>Còn nợ (`TotalPayment − PaidAmount`, ≥ 0) — **chỉ** khi có khách + hạn thanh toán.</summary>
    public decimal DebtAmount { get; protected set; }

    /// <summary>Hạn thanh toán công nợ — **bắt buộc** khi `DebtAmount > 0` (`0006`).</summary>
    public DateTime? DueDateUtc { get; protected set; }

    public string? Note { get; set; }

    /// <summary>Chứng từ kho đã sinh khi bán (`PosRetailBills` ↔ `InvStockDocuments`) — đối chiếu được.</summary>
    public Guid? StockDocumentId { get; protected set; }

    // ── HUỶ (đảo) ───────────────────────────────────────────────────────────────────────────────
    public DateTime? VoidedAtUtc { get; protected set; }
    public Guid? VoidedBy { get; protected set; }
    public string? VoidReason { get; protected set; }

    /// <summary>Chứng từ **nhập lại kho** khi huỷ — `null` nếu chưa huỷ.</summary>
    public Guid? VoidStockDocumentId { get; protected set; }

    public List<RetailBillLine> Lines { get; protected set; } = new();

    public List<RetailBillPayment> Payments { get; protected set; } = new();

    protected RetailBill() { }

    public RetailBill(
        Guid id,
        string number,
        Guid clientBillId,
        Guid warehouseId,
        Guid cashSessionId,
        Guid? customerId = null,
        Guid? saleStaffId = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessException(PosErrorCodes.HoaDonThieuDong)
                .WithData("truong", nameof(number))
                .WithData("lyDo", "Số hoá đơn là BẮT BUỘC.");
        }

        if (number.Trim().Length > DoDaiSoHoaDonToiDa)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("truong", nameof(number)).WithData("toiDa", DoDaiSoHoaDonToiDa);
        }

        if (warehouseId == Guid.Empty)
        {
            throw new BusinessException(PosErrorCodes.ThieuKho)
                .WithData("lyDo", "Hoá đơn phải có KHO xuất hàng (không biết trừ tồn kho nào).");
        }

        if (clientBillId == Guid.Empty)
        {
            // 🚨 Khoá rỗng làm **mọi** hoá đơn offline trùng nhau ⇒ chỉ bán được MỘT hoá đơn rồi mọi lần
            // gửi sau đều bị coi là "gửi lại". Đây là lỗi im lặng đắt nhất của POS offline ⇒ chặn ở đây.
            throw new BusinessException(PosErrorCodes.TrungKhoaKhacNoiDung)
                .WithData("lyDo", "`ClientBillId` KHÔNG được là GUID rỗng (idempotency sẽ gộp mọi hoá đơn).");
        }

        Number = number.Trim();
        ClientBillId = clientBillId;
        WarehouseId = warehouseId;
        CashSessionId = cashSessionId;
        CustomerId = customerId;
        SaleStaffId = saleStaffId;
    }

    /// <summary>
    /// 🚨 **HOÀN TẤT HOÁ ĐƠN** — tính tiền bằng **hàm thuần** rồi ghi các số **suy ra**.
    ///
    /// 📌 Đây là **đường duy nhất** được đặt `Subtotal`/`TotalPayment`/`DebtAmount`: nhận tiền từ client
    /// là mở đường sửa giá. 📌 Gọi **một lần** lúc tạo (hoá đơn bất biến sau đó — muốn sửa thì huỷ + bán lại).
    /// </summary>
    public TinhTienHoaDon.KetQua HoanTat(
        IReadOnlyList<RetailBillLine> dong,
        IReadOnlyList<RetailBillPayment> thanhToan,
        decimal chietKhauHoaDon = 0m,
        decimal soDiemSuDung = 0m,
        decimal tyLeDiem = TinhTienHoaDon.TyLeDiemMacDinh,
        decimal tienKhachDua = 0m,
        decimal diemTich = 0m,
        DateTime? hanThanhToan = null,
        string? ghiChu = null)
    {
        if (Status == RetailBillStatus.Voided)
        {
            throw new BusinessException(PosErrorCodes.HoaDonDaHuy).WithData("number", Number);
        }

        var tienDiem = TinhTienHoaDon.QuyDoiDiem(soDiemSuDung, tyLeDiem);

        var ketQua = TinhTienHoaDon.Tinh(
            dong.Select(x => new TinhTienHoaDon.DongVao(x.Quantity, x.UnitPrice, x.DiscountAmount, x.VatPercent)).ToList(),
            chietKhauHoaDon,
            tienDiem);

        // Trả số đã tính **vào** dòng (client không gửi `Amount`) — báo cáo đọc thẳng cột, không nhân lại.
        foreach (var d in dong)
        {
            var thanhTien = TinhTienHoaDon.ThanhTienDong(d.Quantity, d.UnitPrice, d.DiscountAmount);
            d.DatTien(thanhTien, TinhTienHoaDon.TienVat(thanhTien, d.VatPercent));
        }

        var daTra = thanhToan.Sum(x => x.Amount);
        var (tienThua, conNo) = TinhTienHoaDon.ChiaPhanDaTra(ketQua.TongThanhToan, daTra);

        if (conNo > 0m)
        {
            // 🚨 Nợ mà không biết **ai nợ** / **khi nào trả** là khoản phải thu không đòi được:
            // Nhanh bắt chọn khách + ngày hẹn thanh toán (§5.1 bước 3). Chặn ở domain (không chỉ UI).
            if (CustomerId is null)
            {
                throw new BusinessException(PosErrorCodes.ThieuThongTinCongNo)
                    .WithData("conNo", conNo)
                    .WithData("lyDo", "Hoá đơn còn NỢ phải có KHÁCH HÀNG (biết ai nợ).");
            }

            if (hanThanhToan is null)
            {
                throw new BusinessException(PosErrorCodes.ThieuThongTinCongNo)
                    .WithData("conNo", conNo)
                    .WithData("lyDo", "Hoá đơn còn NỢ phải có HẠN THANH TOÁN (biết khi nào thu).");
            }
        }

        TinhTienHoaDon.KiemPhuongThuc(thanhToan.Select(x => x.Method));

        Lines = dong.ToList();
        Payments = thanhToan.ToList();

        Status = RetailBillStatus.Completed;
        Subtotal = ketQua.TienHang;
        LineDiscountTotal = ketQua.ChietKhauDong;
        DiscountTotal = ketQua.ChietKhauHoaDon;
        VatTotal = ketQua.TienVat;
        PointsUsed = soDiemSuDung;
        PointsUsedAmount = ketQua.TienDiem;
        PointsEarned = diemTich < 0m ? 0m : diemTich;
        CustomerGivenAmount = tienKhachDua < 0m ? 0m : tienKhachDua;
        TotalPayment = ketQua.TongThanhToan;
        PaidAmount = Math.Round(daTra, 2, MidpointRounding.AwayFromZero);
        ChangeAmount = tienThua;
        DebtAmount = conNo;
        DueDateUtc = conNo > 0m ? hanThanhToan : null;
        Note = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();

        return ketQua;
    }

    /// <summary>Gắn chứng từ kho vừa sinh (đường ghi duy nhất: sau khi `Inventory` ghi sổ thành công).</summary>
    public void GanChungTuKho(Guid stockDocumentId)
    {
        StockDocumentId = stockDocumentId;
    }

    /// <summary>
    /// Nạp dòng đã đọc từ DB (app service, không dùng `Include`). 📌 **Không** phải đường ghi: dòng hoá đơn
    /// chỉ được tạo qua <see cref="HoanTat"/>; hàm này để tầng Application gắn danh sách đã đọc cho DTO/mapper.
    /// </summary>
    public void GanDong(IReadOnlyList<RetailBillLine> dong)
    {
        Lines = dong.ToList();
    }

    /// <summary>Nạp dòng thanh toán đã đọc từ DB — cùng lý do <see cref="GanDong"/>.</summary>
    public void GanThanhToan(IReadOnlyList<RetailBillPayment> thanhToan)
    {
        Payments = thanhToan.ToList();
    }

    /// <summary>
    /// **GIẢM CÔNG NỢ** của hoá đơn khi khách **trả hàng** (`WP-06` §5.4: *"trừ công nợ trước, hoàn tiền sau"*).
    /// 📌 Trả thừa ⇒ **ném** `0019`-cùng-họ (`TraVuotSoDaMua` không phù hợp về tiền) ⇒ thực ra app service đã
    /// `min()` trước khi gọi, nên đây là **hàng rào cuối** chống gọi sai.
    /// 📌 Nợ về **0** ⇒ xoá `DueDateUtc`: giữ hạn thanh toán của khoản nợ **không còn** sẽ làm báo cáo
    /// *"nợ quá hạn"* đếm một hoá đơn đã hết nợ.
    /// </summary>
    public void GiamNo(decimal soTien)
    {
        if (soTien < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("soTien", soTien)
                .WithData("lyDo", "Số tiền giảm nợ KHÔNG được âm.");
        }

        if (soTien > DebtAmount)
        {
            throw new BusinessException(PosErrorCodes.TienHoanKhongHopLe)
                .WithData("soTien", soTien)
                .WithData("conNo", DebtAmount)
                .WithData("lyDo", "Giảm nợ VƯỢT số còn nợ của hoá đơn.");
        }

        DebtAmount = Math.Round(DebtAmount - soTien, 2, MidpointRounding.AwayFromZero);

        if (DebtAmount == 0m)
        {
            DueDateUtc = null;
        }
    }

    /// <summary>
    /// **HUỶ HOÁ ĐƠN (ĐẢO)** — cần `chungTuNhapLai` do module kho sinh (nhập lại đúng số đã xuất).
    /// 📌 `Idempotent`: huỷ lần hai **không** đảo lần hai (trả `false`).
    /// 🚨 Chặn khi ca đã kết — xem bất biến (4) ở chú thích lớp; app service đọc trạng thái ca nên
    /// hàm này nhận cờ `caDaKetThuc` **tường minh** (domain không đọc DB).
    /// </summary>
    public bool Huy(Guid nguoiHuy, string lyDo, DateTime atUtc, bool caDaKetThuc)
    {
        if (Status == RetailBillStatus.Voided)
        {
            return false;
        }

        if (caDaKetThuc)
        {
            throw new BusinessException(PosErrorCodes.CaDaKetThuc)
                .WithData("number", Number)
                .WithData("lyDo",
                    "Hoá đơn thuộc ca ĐÃ KẾT ⇒ không huỷ được (tiền mặt đã bàn giao). " +
                    "Hãy tạo PHIẾU TRẢ HÀNG để có chứng từ.");
        }

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("truong", nameof(lyDo))
                .WithData("lyDo", "Huỷ hoá đơn phải nêu LÝ DO (audit + đối chiếu két).");
        }

        if (lyDo.Trim().Length > DoDaiLyDoHuyToiDa)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("truong", nameof(lyDo)).WithData("toiDa", DoDaiLyDoHuyToiDa);
        }

        Status = RetailBillStatus.Voided;
        VoidedAtUtc = atUtc;
        VoidedBy = nguoiHuy;
        VoidReason = lyDo.Trim();

        return true;
    }

    /// <summary>Gắn chứng từ nhập lại kho (chỉ có nghĩa sau khi huỷ).</summary>
    public void GanChungTuHuy(Guid stockDocumentId)
    {
        VoidStockDocumentId = stockDocumentId;
    }

    /// <summary>
    /// **Tiền MẶT thực thu của hoá đơn** — `Σ tiền mặt − tiền thối`.
    /// 🚨 VÌ SAO TRỪ TIỀN THỐI: khách đưa 500.000 cho hoá đơn 450.000 ⇒ két chỉ có **450.000**;
    /// cộng đủ 500.000 thì `ExpectedCash` **cao hơn** két thật đúng 50.000 ⇒ mọi ca đều **báo thiếu tiền**
    /// (và thu ngân bị quy trách nhiệm oan).
    /// </summary>
    public decimal TienMatThucThu()
    {
        if (Status == RetailBillStatus.Voided)
        {
            return 0m;
        }

        var tienMat = Payments.Where(x => x.Method == RetailPaymentMethod.Cash).Sum(x => x.Amount);

        return Math.Round(Math.Max(0m, tienMat - ChangeAmount), 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// **MỘT DÒNG HOÁ ĐƠN BÁN LẺ** (`PosRetailBillLines`).
///
/// 📌 `Amount`/`VatAmount` **lưu** (không suy lại khi đọc): hoá đơn là **chứng từ bất biến** — đổi cách
/// tính VAT sau này **không** được làm thay đổi số tiền của hoá đơn đã in.
/// 📌 `ReturnedQuantity` để chặn **trả vượt** (màn #14) — bất biến ở DB: `0 ≤ ReturnedQuantity ≤ Quantity`.
/// </summary>
public class RetailBillLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiSkuToiDa = 64;

    public Guid? TenantId { get; protected set; }

    public Guid RetailBillId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public decimal Quantity { get; protected set; }

    public decimal UnitPrice { get; protected set; }

    /// <summary>Chiết khấu **theo dòng** (tiền) — `null`/0 khi hoá đơn chiết khấu theo tổng.</summary>
    public decimal DiscountAmount { get; protected set; }

    public decimal? VatPercent { get; protected set; }

    /// <summary>Thành tiền dòng = `SL × đơn giá − chiết khấu dòng` (do domain tính).</summary>
    public decimal Amount { get; protected set; }

    public decimal? VatAmount { get; protected set; }

    /// <summary>Đã trả lại bao nhiêu (cộng dồn qua các phiếu trả) — chặn trả vượt.</summary>
    public decimal ReturnedQuantity { get; protected set; }

    /// <summary>Tên sản phẩm **tại lúc bán** (in lên bill) — không join sang Catalog: bill là chứng từ.</summary>
    public string? ProductName { get; protected set; }

    protected RetailBillLine() { }

    public RetailBillLine(
        Guid id,
        Guid retailBillId,
        string sku,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount = 0m,
        decimal? vatPercent = null,
        string? productName = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PosErrorCodes.HoaDonThieuDong)
                .WithData("truong", nameof(sku))
                .WithData("lyDo", "Dòng hoá đơn phải có SKU.");
        }

        if (sku.Trim().Length > DoDaiSkuToiDa)
        {
            throw new BusinessException(PosErrorCodes.SoLuongKhongHopLe)
                .WithData("truong", nameof(sku)).WithData("toiDa", DoDaiSkuToiDa);
        }

        // Tính ngay để ném sớm (SL ≤ 0 / chiết khấu vượt) — trước khi chạm DB.
        _ = TinhTienHoaDon.ThanhTienDong(quantity, unitPrice, discountAmount);

        RetailBillId = retailBillId;
        Sku = sku.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        VatPercent = vatPercent;
        ProductName = string.IsNullOrWhiteSpace(productName) ? null : productName.Trim();
    }

    /// <summary>Ghi số tiền **đã tính** (chỉ gọi từ <see cref="RetailBill.HoanTat"/>).</summary>
    internal void DatTien(decimal thanhTien, decimal? tienVat)
    {
        Amount = thanhTien;
        VatAmount = tienVat;
    }

    /// <summary>
    /// Cộng dồn **số đã trả** — ném `0019` khi vượt `Quantity`.
    /// 🚨 Hàng rào CUỐI vẫn là `CHECK` ở DB (`0 ≤ ReturnedQuantity ≤ Quantity`): tầng ứng dụng có thể
    /// tính sai khi **hai phiếu trả đồng thời**; CHECK chặn được cả đường ghi tắt.
    /// </summary>
    public void CongTra(decimal soLuongTra)
    {
        if (soLuongTra <= 0m)
        {
            throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                .WithData("sku", Sku).WithData("soLuongTra", soLuongTra);
        }

        if (ReturnedQuantity + soLuongTra > Quantity)
        {
            throw new BusinessException(PosErrorCodes.TraVuotSoDaMua)
                .WithData("sku", Sku)
                .WithData("daMua", Quantity)
                .WithData("daTra", ReturnedQuantity)
                .WithData("lanNay", soLuongTra);
        }

        ReturnedQuantity += soLuongTra;
    }
}

/// <summary>
/// **MỘT DÒNG THANH TOÁN** của hoá đơn (`PosRetailBillPayments`) — Nhanh: *"thanh toán chia nhiều phương
/// thức"* (`04 §6.3`). 📌 Nhiều dòng **cùng phương thức** được phép (khách đưa 2 lần tiền mặt).
/// </summary>
public class RetailBillPayment : FullAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiMaThamChieuToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public Guid RetailBillId { get; protected set; }

    public RetailPaymentMethod Method { get; protected set; }

    public decimal Amount { get; protected set; }

    /// <summary>Mã giao dịch (mã QR/UNC/mã thẻ) — đối chiếu với sao kê khi khách khiếu nại.</summary>
    public string? ReferenceCode { get; protected set; }

    public string? Note { get; protected set; }

    protected RetailBillPayment() { }

    public RetailBillPayment(
        Guid id,
        Guid retailBillId,
        RetailPaymentMethod method,
        decimal amount,
        string? referenceCode = null,
        string? note = null) : base(id)
    {
        if (amount < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("amount", amount)
                .WithData("lyDo", "Số tiền một dòng thanh toán KHÔNG được âm.");
        }

        if (referenceCode is { Length: > DoDaiMaThamChieuToiDa })
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("truong", nameof(referenceCode)).WithData("toiDa", DoDaiMaThamChieuToiDa);
        }

        RetailBillId = retailBillId;
        Method = method;
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        ReferenceCode = string.IsNullOrWhiteSpace(referenceCode) ? null : referenceCode.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

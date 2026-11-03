using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// **Kho** (`InvWarehouses`) — `INV-001` **ADR-01**.
///
/// 📌 **VÌ SAO CẦN BẢNG NÀY (trước đây chỉ có `InventoryItem.WarehouseId` là `Guid` "trần"):**
/// không có bảng kho thì **không** trả lời được: kho này **còn hoạt động** không? **thuộc tenant nào**?
/// **tên** là gì (chỉ in ra được mã GUID cho người dùng)? Kho đã **ngừng dùng** mà vẫn nhập hàng được —
/// mà **không có gì chặn**.
///
/// ⚠️ **`InventoryItem.WarehouseId` vẫn là `Guid` (KHÔNG FK cứng)** theo ADR-01 — để **không** chặn việc
/// nạp dữ liệu tồn kho trước khi khai báo kho (di trú dữ liệu), và để **kho cũ** (đã xoá/ngừng dùng) vẫn
/// **giải thích được** lịch sử sổ cái đã ghi.
/// </summary>
public class Warehouse : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã kho (duy nhất trong tenant) — dùng trong chứng từ và báo cáo.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    public string? Address { get; set; }

    /// <summary>Kho **ngừng dùng** ⇒ **CẤM** tạo chứng từ mới (chứng từ cũ vẫn đọc được).</summary>
    public bool IsActive { get; protected set; } = true;

    protected Warehouse() { }

    public Warehouse(Guid id, string code, string name, string? address = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("`Code` và `Name` của kho là BẮT BUỘC.", nameof(code));
        }

        Code = code.Trim();
        Name = name.Trim();
        Address = address;
    }

    public void NgungSuDung()
    {
        IsActive = false;
    }

    public void KichHoatLai()
    {
        IsActive = true;
    }

    /// <summary>
    /// 🚨 **BẤT BIẾN nghiệp vụ (DB KHÔNG bảo vệ được):** **KHÔNG** được tạo/g hi sổ chứng từ kho trên
    /// kho đã **ngừng dùng**. Kiểm ở đây (không chỉ ở app service) vì **mọi** đường sinh chứng từ đều phải chặn —
    /// nhập hàng vào kho đã đóng sẽ làm **tồn kho "ảo"** mà báo cáo vẫn tin.
    /// </summary>
    public void YeuCauDangHoatDong()
    {
        if (!IsActive)
        {
            throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseCode", Code)
                .WithData("lyDo", "Kho đã NGỪNG dùng ⇒ không tạo/ghi sổ chứng từ kho được.");
        }
    }
}

/// <summary>
/// **Chứng từ kho** (`InvStockDocuments`) — `INV-001` ADR-02.
///
/// 🚨 **VÌ SAO "NHÁP → GHI SỔ" LÀ HAI BƯỚC (không ghi thẳng):** ghi thẳng bút toán khi tạo phiếu nghĩa là
/// một phiếu **nhập sai số** đã **làm tăng tồn kho ngay**; sửa lại thì phải **xoá bút toán** — mà sổ cái là
/// **append-only** (bất biến `BR-001`). Hai bước cho phép **duyệt trước khi ảnh hưởng tồn kho**, và giữ được
/// sổ cái **bất biến**.
/// </summary>
public class StockDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public StockDocumentType Type { get; protected set; }

    public StockDocumentStatus Status { get; protected set; } = StockDocumentStatus.Draft;

    /// <summary>Số chứng từ (do người dùng/chuỗi cấp) — duy nhất trong tenant.</summary>
    public string Number { get; protected set; } = default!;

    /// <summary>Kho **nguồn** (nhập: kho nhận; chuyển: kho đi; kiểm kê: kho đếm).</summary>
    public Guid WarehouseId { get; protected set; }

    /// <summary>Kho **đích** — chỉ dùng cho `StockTransfer`; **NÉM** nếu `null` mà loại là chuyển kho.</summary>
    public Guid? ToWarehouseId { get; protected set; }

    public DateTime OccurredAtUtc { get; protected set; }

    public string? Note { get; set; }

    /// <summary>Khoá idempotency **ổn định** (`INV-001` ADR-07) — dùng khi ghi sổ.</summary>
    public string? IdempotencyKey { get; set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **`WP-04`** — các trường của **phiếu kho kiểu Nhanh** (`docs/nhanh/07` WP-04 việc 2).
    // 📌 **TẤT CẢ NULLABLE**: phiếu cũ (tạo trước WP-04) không có những trường này ⇒ migration **additive**,
    //    và màn DS phiếu cũ **không** bị đổi hành vi lọc.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// **Chiều** của phiếu (`In`/`Out`). **NULL = kiểm kê** (một phiếu có thể vừa thừa vừa thiếu).
    /// 🔒 Suy ra từ `Type` **tại lúc tạo** và **không** cho đổi — đổi chiều của phiếu đã lập là **viết lại
    /// lịch sử** (báo cáo nhập/xuất đã chạy sẽ không còn khớp).
    /// </summary>
    public StockDocumentDirection? Direction { get; protected set; }

    /// <summary>**Kiểu phiếu** kiểu Nhanh (`bill.mode`) — xem `StockDocumentMode` (15 giá trị đo được).</summary>
    public StockDocumentMode? Mode { get; protected set; }

    /// <summary>Loại đối tượng của phiếu (KH/NCC/nhân viên/sàn…).</summary>
    public StockCounterpartyType? CounterpartyType { get; protected set; }

    /// <summary>`Id` đối tượng (khách/NCC/…) — **KHÔNG** FK cứng: phiếu cũ và dữ liệu nhập từ Nhanh có thể trỏ
    /// tới đối tượng chưa được di trú; FK cứng sẽ **chặn nhập dữ liệu** (cùng lý do ADR-01 với `WarehouseId`).</summary>
    public Guid? CounterpartyId { get; protected set; }

    /// <summary>Loại chứng từ nguồn (`Order`, `RetailBill`, `StockTransfer`…) — chuỗi, không phải FK.</summary>
    public string? RefType { get; protected set; }

    /// <summary>Mã chứng từ nguồn (`OrdOrders.Number`, `PosRetailBills.Number`…) — chuỗi để truy vết chéo.</summary>
    public string? RefId { get; protected set; }

    /// <summary>Chiết khấu trên phiếu (tiền) — `null` nếu phiếu không có chiết khấu.</summary>
    public decimal? DiscountAmount { get; protected set; }

    /// <summary>Đã trả cho đối tác (tiền) — dùng cho phiếu nhập NCC.</summary>
    public decimal? PaidAmount { get; protected set; }

    protected StockDocument() { }

    public StockDocument(
        Guid id, StockDocumentType type, string number, Guid warehouseId,
        DateTime occurredAtUtc, Guid? toWarehouseId = null, StockDocumentMode? mode = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("Số chứng từ là BẮT BUỘC.", nameof(number));
        }

        if (type == StockDocumentType.StockTransfer)
        {
            if (toWarehouseId is null)
            {
                throw new BusinessException(InventoryErrorCodes.ChuyenKhoThieuKhoDich);
            }

            if (toWarehouseId == warehouseId)
            {
                // 🚨 Chuyển kho từ A đến A: nếu cho phép, hệ thống sinh **hai** bút toán (đi và đến) trên
                // **cùng một kho** ⇒ tồn kho **không đổi** nhưng sổ cái có thêm 2 dòng **vô nghĩa**
                // ⇒ báo cáo chuyển kho đếm sai. DB không bảo vệ được ⇒ NÉM.
                throw new BusinessException(InventoryErrorCodes.ChuyenKhoCungMotKho)
                    .WithData("warehouseId", warehouseId);
            }
        }
        else if (toWarehouseId is not null)
        {
            throw new BusinessException(InventoryErrorCodes.ChuyenKhoThuaKhoDich)
                .WithData("type", type.ToString());
        }

        Type = type;
        Number = number.Trim();
        WarehouseId = warehouseId;
        ToWarehouseId = toWarehouseId;
        OccurredAtUtc = occurredAtUtc;
        Mode = ChuanHoaMode(type, mode);
        Direction = HuongMacDinh(type);
    }

    /// <summary>
    /// **CHIỀU mặc định suy từ loại phiếu** (hàm **THUẦN** — test được không cần DB):
    /// nhập/trả hàng ⇒ `In`; chuyển kho ⇒ `Out` (phiếu là *"xuất ở kho nguồn"*; bút toán `TransferIn` vẫn sinh
    /// ở kho đích khi ghi sổ); kiểm kê ⇒ `null` (vừa thừa vừa thiếu).
    /// </summary>
    public static StockDocumentDirection? HuongMacDinh(StockDocumentType type) => type switch
    {
        StockDocumentType.GoodsReceipt => StockDocumentDirection.In,
        StockDocumentType.CustomerReturn => StockDocumentDirection.In,
        // 🆕 `WP-04` — phiếu XUẤT (Nhanh `bill.type = 2`): bán lẻ / giao hàng / trả NCC / xuất khác.
        StockDocumentType.GoodsIssue => StockDocumentDirection.Out,
        StockDocumentType.StockTransfer => StockDocumentDirection.Out,
        StockDocumentType.Stocktake => null,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Loại chứng từ không hỗ trợ.")
    };

    /// <summary>
    /// Kiểm **kiểu phiếu ↔ loại phiếu** (hàm **THUẦN**). Chỉ chặn **mâu thuẫn rõ ràng** — KHÔNG suy ra toàn bộ
    /// ánh xạ `mode → type` vì Nhanh dùng **cùng một mode** cho cả nhập và xuất (`Supplier` 5, `Other` 10…):
    /// suy rộng ra sẽ **chặn oan** dữ liệu hợp lệ.
    /// </summary>
    public static StockDocumentMode? ChuanHoaMode(StockDocumentType type, StockDocumentMode? mode)
    {
        if (mode is null)
        {
            // 🆕 `WP-04`: phiếu XUẤT **bắt buộc** có mode — mode là thứ duy nhất quyết định bút toán
            // (`Retail ⇒ RetailSaleOut`, `Delivery ⇒ ShippingOut`, `Supplier ⇒ SupplierReturnOut`).
            // 📌 Không có mode thì hệ thống **không biết** hàng đi vì lý do gì ⇒ ghi sổ sẽ phải đoán.
            if (type == StockDocumentType.GoodsIssue)
            {
                throw new BusinessException(InventoryErrorCodes.PhieuXuatThieuMode)
                    .WithData("type", type.ToString())
                    .WithData("lyDo", "Phiếu XUẤT kho phải khai `Mode` (bán lẻ/giao hàng/trả NCC/xuất khác).");
            }

            return null;
        }

        var mauThuan =
            (type == StockDocumentType.StockTransfer && mode != StockDocumentMode.Transfer)
            || (mode == StockDocumentMode.Transfer && type != StockDocumentType.StockTransfer)
            || (type == StockDocumentType.Stocktake && mode != StockDocumentMode.Stocktake)
            || (mode == StockDocumentMode.Stocktake && type != StockDocumentType.Stocktake);

        if (mauThuan)
        {
            throw new BusinessException(InventoryErrorCodes.KieuPhieuKhongKhopLoai)
                .WithData("type", type.ToString())
                .WithData("mode", mode.Value.ToString())
                .WithData("lyDo", "Kiểu phiếu (mode) KHÔNG khớp loại chứng từ — ví dụ kiểu `Transfer` với phiếu nhập.");
        }

        return mode;
    }

    /// <summary>Gắn **đối tượng** của phiếu (KH/NCC/nhân viên/sàn). `CounterpartyId` là `Guid` **trần** (không FK).</summary>
    public void DatDoiTac(StockCounterpartyType? loai, Guid? id)
    {
        if (loai is null && id is not null)
        {
            throw new BusinessException(InventoryErrorCodes.ThieuLoaiDoiTac)
                .WithData("counterpartyId", id)
                .WithData("lyDo", "Có `CounterpartyId` nhưng THIẾU loại đối tượng ⇒ không biết tra bảng nào.");
        }

        CounterpartyType = loai;
        CounterpartyId = id;
    }

    /// <summary>Gắn **chứng từ nguồn** (đơn bán/POS/chuyển kho) — dùng để truy vết chéo và chống ghi trùng.</summary>
    public void DatNguon(string? refType, string? refId)
    {
        RefType = string.IsNullOrWhiteSpace(refType) ? null : refType.Trim();
        RefId = string.IsNullOrWhiteSpace(refId) ? null : refId.Trim();

        if (RefId is not null && RefType is null)
        {
            throw new BusinessException(InventoryErrorCodes.ThieuLoaiNguon)
                .WithData("refId", RefId)
                .WithData("lyDo", "Có `RefId` nhưng THIẾU `RefType` ⇒ không biết đối chiếu với bảng nào.");
        }
    }

    /// <summary>Gắn **tiền của phiếu** (chiết khấu / đã trả). **NÉM** nếu âm — tiền âm ở đây là dữ liệu hỏng.</summary>
    public void DatTien(decimal? chietKhau, decimal? daTra)
    {
        if (chietKhau is < 0m)
        {
            throw new BusinessException(InventoryErrorCodes.TienTrenPhieuKhongHopLe)
                .WithData("discountAmount", chietKhau).WithData("lyDo", "Chiết khấu trên phiếu KHÔNG được âm.");
        }

        if (daTra is < 0m)
        {
            throw new BusinessException(InventoryErrorCodes.TienTrenPhieuKhongHopLe)
                .WithData("paidAmount", daTra).WithData("lyDo", "Số đã trả trên phiếu KHÔNG được âm.");
        }

        DiscountAmount = chietKhau;
        PaidAmount = daTra;
    }

    /// <summary>`Draft → Posted`. **NÉM** nếu không ở `Draft` hoặc **chưa có dòng** nào.</summary>
    public void GhiSo(int soDongThucTe)
    {
        if (Status != StockDocumentStatus.Draft)
        {
            // 🚨 Ghi sổ hai lần = sinh bút toán **HAI LẦN** ⇒ tồn kho tăng/giảm gấp đôi. NÉM ở đây là lớp trong;
            // hàng rào cuối là unique `(TenantId, ReferenceLineId)` trên sổ cái (`INV-001` ADR-05).
            throw new BusinessException(InventoryErrorCodes.ChungTuKhongOTrangThaiNhap)
                .WithData("number", Number)
                .WithData("status", Status.ToString());
        }

        // Số dòng TRUYỀN VÀO (đọc từ DB) — bài học `PRC-001` §17.2 **L1**.
        if (soDongThucTe <= 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong)
                .WithData("number", Number);
        }

        Status = StockDocumentStatus.Posted;
    }

    /// <summary>`Draft → Cancelled`; **NÉM** nếu đã ghi sổ (sổ cái append-only ⇒ không "huỷ" được bút toán).</summary>
    public void Huy()
    {
        if (Status == StockDocumentStatus.Posted)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuDaGhiSoKhongHuyDuoc)
                .WithData("number", Number)
                .WithData("lyDo", "Chứng từ đã ghi sổ ⇒ muốn đảo phải tạo chứng từ ĐẢO, KHÔNG xoá bút toán cũ.");
        }

        Status = StockDocumentStatus.Cancelled;
    }

    /// <summary>🚨 **Append-only**: chứng từ đã ghi sổ **KHÔNG** sửa được nội dung.</summary>
    public void YeuCauChoPhepSuaNoiDung()
    {
        if (Status != StockDocumentStatus.Draft)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuDaGhiSoKhongSuaDuoc)
                .WithData("number", Number)
                .WithData("status", Status.ToString());
        }
    }
}

/// <summary>Một **dòng** chứng từ kho (`InvStockDocumentLines`).</summary>
public class StockDocumentLine : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid StockDocumentId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    /// <summary>Số lượng **luôn dương**; **chiều** do `StockDocumentType` + vị trí (nguồn/đích) quyết định.</summary>
    public decimal Quantity { get; protected set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **`WP-04`** — trường dòng phiếu kiểu Nhanh (`docs/nhanh/07` WP-04 việc 2).
    // 📌 **NULLABLE hết** ⇒ phiếu/dòng cũ **không đổi hành vi**; migration additive.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// **Đơn vị tính** của dòng (`ProductUnit.Id` — `CAT-002`/`WP-03`).
    /// ⚠️ **`Guid` TRẦN, KHÔNG FK**: `ProductUnit` mới có từ WP-03 và dòng phiếu cũ không có ĐVT ⇒ FK cứng sẽ
    /// **chặn ghi** dữ liệu lịch sử. Tầng đọc kiểm sự tồn tại khi cần hiển thị tên ĐVT.
    /// </summary>
    public Guid? UnitId { get; protected set; }

    /// <summary>**% VAT** của dòng (Nhanh: `vatPercent`) — `null` = không khai báo ở dòng, dùng mặc định phiếu.</summary>
    public decimal? VatPercent { get; protected set; }

    /// <summary>**Lô** của dòng (`ProductBatch.Id`) — `null` = hàng không theo lô. `Guid` trần (cùng lý do `UnitId`).</summary>
    public Guid? BatchId { get; protected set; }

    /// <summary>
    /// 🆕 **`WP-04` (việc 8)** — **ĐƠN GIÁ** của dòng (Nhanh: `price`); `null` = **chưa khai giá**.
    /// 🚨 **KHÔNG phải nguồn sự thật của giá bán:** giá bán nằm ở `ProductUnit.SalePrice`; cột này **ghi lại**
    /// giá tại thời điểm lập phiếu (để in phiếu/tính thành tiền và **giữ nguyên lịch sử** khi bảng giá đổi).
    /// 📌 `numeric(18,2)` — tiền, khác độ chính xác của `Quantity` (18,3) là **có chủ ý** (D-37).
    /// </summary>
    public decimal? UnitPrice { get; protected set; }

    /// <summary>
    /// **Danh sách serial/IMEI** của dòng, phân tách bằng dấu phẩy (Nhanh: `imexs`).
    /// 📌 Lưu **chuỗi** ở WP-04 vì bảng `CatProductImeis` (WP-03) chưa có đường ghi; khi có, mỗi serial phải
    /// trở thành **một dòng IMEI thật** (nửa sau WP-03) — ghi rõ để không ai tưởng cột này là nguồn sự thật.
    /// </summary>
    public string? ImeiSerials { get; protected set; }

    protected StockDocumentLine() { }

    public StockDocumentLine(Guid id, Guid stockDocumentId, string sku, decimal quantity) : base(id)
    {
        if (stockDocumentId == Guid.Empty)
        {
            throw new ArgumentException("`StockDocumentId` là BẮT BUỘC.", nameof(stockDocumentId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("`Sku` là BẮT BUỘC.", nameof(sku));
        }

        if (quantity <= 0m)
        {
            // 🚨 Số lượng **0** hoặc **âm**: nếu cho phép, bút toán 0 lọt vào sổ cái (DB đã có CHECK chặn
            // `QuantityDelta <> 0`) hoặc **đảo chiều nghiệp vụ** (nhập âm = xuất) mà người nhập không biết.
            throw new BusinessException(InventoryErrorCodes.SoLuongPhaiDuong)
                .WithData("sku", sku)
                .WithData("quantity", quantity);
        }

        StockDocumentId = stockDocumentId;
        Sku = sku.Trim();
        Quantity = quantity;
    }

    /// <summary>
    /// Gắn **ĐVT / VAT / lô / serial** cho dòng (Nhanh: `unitId`, `vatPercent`, `batch`, `imexs`).
    ///
    /// 🚨 **BẤT BIẾN SERIAL — số serial PHẢI bằng `Quantity`:** dòng 3 cái mà khai 2 serial nghĩa là một cái
    /// **không truy vết được** ⇒ mất bảo hành/không đối soát được với hãng. Nếu **không** khai serial (hàng
    /// không quản IMEI) thì để `null` — **không** được khai **rỗng** (`""`) vì hai cách diễn đạt cùng một ý.
    /// </summary>
    public void DatChiTiet(Guid? unitId = null, decimal? vatPercent = null, Guid? batchId = null,
        string? imeiSerials = null, decimal? unitPrice = null)
    {
        if (vatPercent is < 0m or > 100m)
        {
            throw new BusinessException(InventoryErrorCodes.VatPercentKhongHopLe)
                .WithData("sku", Sku)
                .WithData("vatPercent", vatPercent);
        }

        // 🆕 `WP-04` (việc 8) — đơn giá âm: không có nghĩa nghiệp vụ nào (giảm giá đi đường `DiscountAmount`).
        if (unitPrice is < 0m)
        {
            throw new BusinessException(InventoryErrorCodes.DonGiaKhongHopLe)
                .WithData("sku", Sku)
                .WithData("unitPrice", unitPrice)
                .WithData("lyDo", "Đơn giá dòng KHÔNG được âm — chiết khấu là tiền của PHIẾU, không phải giá âm.");
        }

        var serials = (imeiSerials ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (serials.Length > 0)
        {
            if (serials.Length != Quantity)
            {
                throw new BusinessException(InventoryErrorCodes.SoSerialKhacSoLuong)
                    .WithData("sku", Sku)
                    .WithData("quantity", Quantity)
                    .WithData("serialCount", serials.Length)
                    .WithData("lyDo", "Số serial/IMEI PHẢI bằng số lượng dòng — nếu không, có đơn vị KHÔNG truy vết được.");
            }

            if (serials.Distinct(StringComparer.Ordinal).Count() != serials.Length)
            {
                throw new BusinessException(InventoryErrorCodes.SerialTrungTrongDong)
                    .WithData("sku", Sku)
                    .WithData("imeiSerials", imeiSerials ?? string.Empty);
            }
        }

        if (serials.Length > 0 && batchId is not null)
        {
            // 📌 Không phải luật tuyệt đối của Nhanh, nhưng trong WP-04 ta **chưa** có đường ghi lô/serial
            // (nửa sau WP-03) ⇒ cho phép cả hai cùng lúc sẽ tạo ra dữ liệu **không kiểm chứng được**.
            // Ghi rõ ở đây thay vì để người sau tự đoán.
            throw new BusinessException(InventoryErrorCodes.LoVaSerialCungLuc)
                .WithData("sku", Sku)
                .WithData("lyDo", "Một dòng KHÔNG được khai CẢ lô VÀ serial trong WP-04 (chưa có đường ghi lô).");
        }

        UnitId = unitId;
        VatPercent = vatPercent;
        BatchId = batchId;
        ImeiSerials = serials.Length == 0 ? null : string.Join(',', serials);
        UnitPrice = unitPrice;
    }
}

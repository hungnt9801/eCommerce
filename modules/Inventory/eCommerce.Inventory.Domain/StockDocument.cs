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

    protected StockDocument() { }

    public StockDocument(
        Guid id, StockDocumentType type, string number, Guid warehouseId,
        DateTime occurredAtUtc, Guid? toWarehouseId = null) : base(id)
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
    public int Quantity { get; protected set; }

    protected StockDocumentLine() { }

    public StockDocumentLine(Guid id, Guid stockDocumentId, string sku, int quantity) : base(id)
    {
        if (stockDocumentId == Guid.Empty)
        {
            throw new ArgumentException("`StockDocumentId` là BẮT BUỘC.", nameof(stockDocumentId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("`Sku` là BẮT BUỘC.", nameof(sku));
        }

        if (quantity < 1)
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
}

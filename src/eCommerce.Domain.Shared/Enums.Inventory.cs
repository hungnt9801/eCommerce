namespace eCommerce.Inventory;

/// <summary>Loại **chứng từ kho** (`INV-001` §1 — ba nghiệp vụ nền).</summary>
public enum StockDocumentType
{
    /// <summary>Phiếu **nhập kho** (hàng về; cũng dùng cho khách trả hàng — §1 Alternative Flow A1).</summary>
    GoodsReceipt = 0,

    /// <summary>Phiếu **chuyển kho** (giữa hai kho).</summary>
    StockTransfer = 1,

    /// <summary>Phiếu **kiểm kê** (đếm thực tế; ghi phần **lệch**).</summary>
    Stocktake = 2
}

/// <summary>Vòng đời chứng từ kho (`INV-001` **ADR-02**: **chứng từ + ghi sổ là HAI bước**).</summary>
public enum StockDocumentStatus
{
    /// <summary>Nháp — **CHƯA** ảnh hưởng tồn kho.</summary>
    Draft = 0,

    /// <summary>Đã ghi sổ — **ĐÃ** sinh bút toán sổ cái; **KHÔNG SỬA ĐƯỢC**.</summary>
    Posted = 1,

    /// <summary>Đã huỷ (chỉ huỷ được khi còn `Draft`).</summary>
    Cancelled = 2
}

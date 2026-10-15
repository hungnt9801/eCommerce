namespace eCommerce.Inventory;

/// <summary>Loại **chứng từ kho** (`INV-001` §1 — ba nghiệp vụ nền).</summary>
public enum StockDocumentType
{
    /// <summary>Phiếu **nhập kho** (hàng về; cũng dùng cho khách trả hàng — §1 Alternative Flow A1).</summary>
    GoodsReceipt = 0,

    /// <summary>Phiếu **chuyển kho** (giữa hai kho).</summary>
    StockTransfer = 1,

    /// <summary>Phiếu **kiểm kê** (đếm thực tế; ghi phần **lệch**).</summary>
    Stocktake = 2,

    // 🚨 **THÊM Ở CUỐI DẢI — KHÔNG BAO GIỜ ĐỔI SỐ CỦA GIÁ TRỊ CŨ.**
    // `Type` lưu dạng **SỐ** trong `InvStockDocuments.Type` ⇒ đổi số của giá trị cũ sẽ **diễn giải lại
    // toàn bộ lịch sử kho** (phiếu nhập thành phiếu kiểm kê…) mà **không có lỗi nào**.
    // 📌 Trước vòng 39, phiếu **trả hàng** phải "mượn" `GoodsReceipt` ⇒ sổ cái ghi `Receipt` ⇒
    // báo cáo *"nhập kho"* **lẫn** hàng nhập mua với hàng khách trả ⇒ **không tách được** `bán/trả`.

    /// <summary>Phiếu **trả hàng** (khách gửi lại) ⇒ biến động `CustomerReturn` (`SAL-004`).</summary>
    CustomerReturn = 3
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

/// <summary>Trạng thái một lượt **giữ chỗ** (`INV-002`).</summary>
public enum ReservationStatus
{
    /// <summary>Đang giữ — hàng đã bị trừ khỏi `Available` nhưng **chưa** xuất kho.</summary>
    Held = 0,

    /// <summary>Đã **nhả** (khách huỷ / hết TTL) — hàng trở lại `Available`.</summary>
    Released = 1,

    /// <summary>Đã **chốt** thành xuất kho thật (đơn được xác nhận).</summary>
    Committed = 2,

    /// <summary>Hết hạn (do job nhả) — **phân biệt** với `Released` để biết *vì sao* hàng được nhả.</summary>
    Expired = 3
}


namespace eCommerce.Reconciliation;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN ĐỐI SOÁT** (`REC-001`) — dải riêng `eCommerce:Recon:0xxx`.
///
/// 📌 **VÌ SAO DẢI RIÊNG (không mượn mã của miền kho):** mã lỗi là thứ **CSKH đọc để phân loại sự cố**.
/// Ca dưới đây **không** phải *"kho thiếu hàng"* cũng **không** phải *"hạn mức kênh"* — nó là *"hai lượt
/// ghi cùng chốt một cặp lệch"*, tức là việc của **đối soát**. Mượn mã miền khác ⇒ người nhận **chỉ dẫn sai**
/// (bài học đã trả giá **hai lần**: `0040` và `0042` — `docs/tiep-tuc-phien-moi.md` §4.1 #5).
/// </summary>
public static class ReconErrorCodes
{
    private const string Nhom = "eCommerce:Recon:";

    /// <summary>
    /// **Cặp `(kho, SKU)` ĐÃ có dòng lệch trong CÙNG một lượt đối soát** — hàng rào DB
    /// `IX_ReconRunMismatches_TenantId_Run_Kho_Sku` (`NULLS NOT DISTINCT`).
    /// 📌 Ca thật: job chạy lại / hai instance cùng quét (khoá phân tán hết hạn) / đường ghi tay.
    /// 🔒 **Không** được nuốt thành `500`: đây là **tranh chấp bình thường** — nói rõ *"lượt này đã ghi cặp này rồi"*
    /// để người vận hành biết **không cần làm gì**, thay vì tưởng hệ thống hỏng và **chạy lại** (vòng lặp).
    /// </summary>
    public const string CapLechDaGhiTrongLuot = Nhom + "0001";
}

namespace eCommerce.Reconciliation;

/// <summary>
/// 🆕 **`S15` — LUẬT "GIAO DỊCH TREO QUÁ HẠN"** (`REC-001` §5.4 `StalePendingTransaction`, **hàm THUẦN**).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **VÌ SAO PHẢI LÀ HÀM THUẦN Ở TẦNG DOMAIN (không viết thẳng trong job):**
/// luật này quyết định **giao dịch nào bị gắn cờ "treo"** — một cờ vận hành mà người ta dùng để **đóng/mở**
/// việc dọn dẹp. Nếu nó nằm trong job (host) thì:
///   • **không test được** nhánh biên (đúng bằng ngưỡng · ngưỡng = 0 · cổng nói "đã có giao dịch");
///   • và luật *"cổng chưa có giao dịch"* sẽ bị **chép lại** ở lần thứ hai (app service chạy tay) ⇒ hai định
///     nghĩa, lệch khi ai đó sửa một chỗ — đúng họ lỗi `XC-003` F2 đã trả giá.
/// 📌 Tiền lệ trong dự án: `NguongLech`, `DoiSoatRetention`, `LuatWatermark` — **luật thuần** + I/O ở ngoài.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO NGƯỠNG MẶC ĐỊNH LÀ 24 GIỜ (quyết định `Q1` — ghi ở `REC-001` §20.2):**
/// 1. **VNPay tự đóng giao dịch sau ~15 phút** ⇒ một giao dịch chưa có kết quả sau 24 giờ **gần như chắc chắn**
///    là treo (IPN mất / khách bỏ dở), **không** phải "đang xử lý".
/// 2. **Lớn hơn `DedupWindowHours` (20 giờ)** của cùng job ⇒ sau khi một giao dịch đã được đối soát một lần,
///    lượt kế tiếp mới gắn cờ ⇒ **không** gắn cờ lặp lại mỗi 15 phút (mỗi cờ là một dòng `Discrepancy` phải
///    xử lý tay — rác vận hành).
/// 3. **Nhỏ hơn 1 ngày làm việc**: cờ xuất hiện **trong ngày làm việc kế tiếp** ⇒ còn người xử lý trước khi
///    khách khiếu nại.
/// ⚠️ Ngưỡng **cấu hình được** (`Reconciliation:StalePendingHours`); `0` hoặc âm = **TẮT** (cùng quy ước
/// `Recon.Retention.Days`: `0` = tắt, **không** phải "chạy với ngưỡng 0").
/// </summary>
public static class LuatStalePending
{
    /// <summary>Ngưỡng mặc định **24 giờ** — lý do ở chú thích lớp (quyết định `Q1`).</summary>
    public const int NguongMacDinhGio = 24;

    /// <summary>
    /// Giao dịch có bị coi là **treo quá hạn** không.
    /// </summary>
    /// <param name="daKetThuc">Giao dịch đã ở trạng thái **cuối** chưa (`Succeeded`/`Failed`/`Refunded`…).
    /// 📌 Đã kết thúc thì **không** bao giờ là "treo" — dù tuổi lớn tới đâu.</param>
    /// <param name="taoLucUtc">`PaymentTransaction.CreationTime` (UTC).</param>
    /// <param name="bayGioUtc">Thời điểm hiện tại (UTC) — **truyền vào**, không đọc đồng hồ trong hàm thuần.</param>
    /// <param name="responseCodeCong">`vnp_ResponseCode` của cổng; `"91"` = **cổng KHÔNG có giao dịch**.
    /// 🚨 `null` = **không truy vấn được** ⇒ **KHÔNG** kết luận treo (đó là `ProviderUnreachable`, mức khác).</param>
    /// <param name="nguongGio">Ngưỡng giờ; `<= 0` = **TẮT**.</param>
    public static bool LaStale(
        bool daKetThuc,
        DateTime taoLucUtc,
        DateTime bayGioUtc,
        string? responseCodeCong,
        int nguongGio)
    {
        if (daKetThuc)
        {
            return false;
        }

        if (nguongGio <= 0)
        {
            return false;
        }

        // 📌 Tuổi ÂM (đồng hồ máy lệch / dữ liệu tương lai) ⇒ **KHÔNG** gắn cờ. Một cờ "treo" dựa trên mốc
        // tương lai là cờ SAI, và cờ sai làm người vận hành mất niềm tin vào cả danh sách.
        var tuoi = bayGioUtc - taoLucUtc;

        if (tuoi < TimeSpan.FromHours(nguongGio))
        {
            return false;
        }

        // 🚨 **CHỈ khi cổng XÁC NHẬN "không có giao dịch" (`91`).**
        // 📌 `null` (không truy vấn được) ⇒ đã có `ProviderUnreachable`; gắn thêm "treo" là **hai cờ cho một
        // nguyên nhân**, và cờ "treo" thì **không ai dọn được** vì cổng có thể chỉ đang chậm.
        return string.Equals(responseCodeCong, "91", StringComparison.Ordinal);
    }

    /// <summary>
    /// Đọc ngưỡng từ cấu hình (chuỗi). 🔒 Giá trị **không đọc được** ⇒ dùng **mặc định 24**, **không** ném:
    /// một chuỗi cấu hình gõ sai **không** được phép làm job tiền ngừng chạy.
    /// 📌 `0`/âm ⇒ **TẮT** (trả `0`), cùng quy ước `Recon.Retention.Days`.
    /// </summary>
    public static int DocNguong(string? giaTri)
    {
        if (int.TryParse(giaTri, out var so))
        {
            return so <= 0 ? 0 : so;
        }

        return NguongMacDinhGio;
    }
}

namespace eCommerce.Settings;

/// <summary>
/// 🚨 **TÍNH HẠN MỨC THEO KÊNH — HÀM THUẦN** (`INV-003`, phần **cưỡng chế**).
///
/// 📌 **VÌ SAO TÁCH RIÊNG KHỎI DỊCH VỤ:** đây là **số học nghiệp vụ** (phần trăm vs tuyệt đối, biên, làm tròn) —
/// chỗ **dễ sai** và **không cần DB** ⇒ tách ra để **kiểm được bằng unit test** trước khi tin nó trong đường đặt hàng.
/// 🔒 Hàm **không ném**, **không side effect**, chỉ trả **quyết định**.
///
/// ⚠️ **PHẠM VI v1 (ghi rõ, không giấu):**
///   • hạn mức lấy từ **MỘT** giá trị dùng chung (`Inv.ChannelLimit.DefaultValue`) — **bảng riêng cho từng kênh** là lát cắt sau;
///   • **`Percent`** = phần trăm của **tồn khả dụng CỦA TỪNG `(kho, SKU)`** tại thời điểm giữ chỗ — không phải tổng của cả kênh
///     (tồn kho chưa mô hình hoá "theo kênh"; đây là **giới hạn đã biết**, ghi ở `web-001` §94);
///   • **`PhamVi`** (`Channel` | `ChannelWarehouse`) **đã khai báo** nhưng chưa làm khác nhau ở v1 ⇒ **nợ**.
/// </summary>
public static class HanMucKenh
{
    /// <summary>
    /// Tính **hạn mức số lượng** được giữ cho một `(kho, SKU)`.
    /// <list type="bullet">
    ///   <item><see cref="DonViHanMuc.Absolute"/>: hạn mức = `giaTri` (số lượng).</item>
    ///   <item><see cref="DonViHanMuc.Percent"/>: hạn mức = `floor(tồnKhảDụng × giaTri / 100)` — **làm tròn XUỐNG**
    ///   (⚠️ cố ý: làm tròn **lên** sẽ cho phép giữ **nhiều hơn** tỷ lệ đã chọn ⇒ phá chính chính sách).</item>
    /// </list>
    /// 🔒 `giaTri &lt;= 0` ⇒ **0** ⇒ nghĩa là **KHÔNG áp hạn mức** (quy ước: `DefaultValue = 0` là **tắt**).
    /// </summary>
    public static int TinhHanMuc(DonViHanMuc donVi, decimal giaTri, int tonKhaDung)
    {
        if (giaTri <= 0) { return 0; }

        if (donVi == DonViHanMuc.Absolute) { return (int)decimal.Truncate(giaTri); }

        if (tonKhaDung <= 0) { return 0; }

        var hanMuc = decimal.Truncate(tonKhaDung * giaTri / 100m);

        return (int)hanMuc;
    }

    /// <summary>
    /// Quyết định **có vượt hạn mức** không. 🔒 `hanMuc &lt;= 0` ⇒ **KHÔNG** áp hạn mức ⇒ **không** bao giờ vượt
    /// (đây là đường "tắt chính sách" **duy nhất**, để không ai tình cờ chặn hết hàng).
    /// </summary>
    public static bool VuotHanMuc(int soLuongYeuCau, int hanMuc)
    {
        if (hanMuc <= 0) { return false; }

        return soLuongYeuCau > hanMuc;
    }

    /// <summary>
    /// **Có nên CHẶN không** — kết hợp *vượt hạn mức* **và** *hành động đã chọn*.
    /// 🔒 `Warn` ⇒ **KHÔNG** chặn (cho qua + ghi cảnh báo) — theo đúng lựa chọn `Inv.ChannelLimit.Action`.
    /// </summary>
    public static bool NenChan(HanhDongHanMuc hanhDong, int soLuongYeuCau, int hanMuc)
        => hanhDong == HanhDongHanMuc.Block && VuotHanMuc(soLuongYeuCau, hanMuc);

    /// <summary>
    /// 🚨 **CẢNH BÁO "HẠN MỨC VÔ NGHĨA"** — cấu hình **CÓ Ý ĐỊNH** áp hạn mức (`giaTri &gt; 0`) nhưng hạn mức tính ra **`0`**
    /// (vì `Percent` **làm tròn xuống** trên tồn khả dụng nhỏ) ⇒ theo quy ước `0` = **TẮT**, chính sách sẽ chạy **như không cấu hình**.
    ///
    /// 📌 **VÌ SAO PHẢI PHÁT HIỆN:** đây đúng loại **"tắt im lặng"** mà dự án đã trả giá nhiều lần (``CHN-002`` · ``inv-001`` §27):
    /// người vận hành đặt ``50%``, thấy **không có gì bị chặn**, rồi **tin** rằng hạn mức đang hoạt động.
    /// 🔒 Hàm này **không** đổi hành vi (vẫn `0` = tắt) — nó chỉ **nói ra** để chỗ gọi **ghi cảnh báo**;
    /// quyết định *"chặn hay không"* vẫn thuộc `Action`, **không** bị hàm này lén đổi.
    /// </summary>
    public static bool HanMucVoNghia(DonViHanMuc donVi, decimal giaTri, int tonKhaDung)
        => giaTri > 0 && TinhHanMuc(donVi, giaTri, tonKhaDung) <= 0;
}

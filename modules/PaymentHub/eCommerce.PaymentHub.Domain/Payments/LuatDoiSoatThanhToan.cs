namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// 🚨 **LUẬT ĐỐI SOÁT `querydr` — HÀM THUẦN** (`REC-001` mục 7).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO TÁCH RA (đây là điều kiện để có JOB ĐỊNH KỲ, không phải chi tiết kỹ thuật):**
///
/// Luật này trước đây **chỉ** nằm trong `PaymentAppService.PostTruyVanTrangThaiAsync`. ⚠️ Và
/// `PaymentAppService` mang **`[Authorize]` ở cấp class** (quy ước #4 **bắt buộc**) ⇒ 🔒 **job nền KHÔNG gọi
/// được nó**: interceptor của ABP kiểm quyền **ngay cả khi gọi trong tiến trình**, mà worker **không có
/// principal** ⇒ **`AbpAuthorizationException`**.
///
/// 📌 **Lỗi đó ĐÃ XẢY RA THẬT** ở `SAL-005` (`MarketplaceReconciliationWorker` gọi `IDeadLetterAppService`
/// ⇒ job **chết giữa đường**, log chỉ có neo **mở đầu** và **không** có neo kết thúc). Xem `web-001` §97 và
/// `sal-005` §19.
///
/// ⚠️ **HAI LỐI SAI — và cả hai đều tệ:**
/// <list type="number">
///   <item>**Chép luật sang worker** ⇒ luật *"CHỈ TIẾN, KHÔNG LÙI"* có **HAI** định nghĩa ⇒ 🔒 chúng sẽ
///     **lệch** khi ai đó sửa một chỗ — đúng loại lỗi mà dự án đã trả giá (nhiều nguồn sự thật);</item>
///   <item>**Cho worker gọi app service** ⇒ **`AbpAuthorizationException`**, job **chết im lặng**.</item>
/// </list>
///
/// ✅ **LỐI ĐÚNG (lượt này):** đặt luật ở **một hàm THUẦN trong tầng Domain** — 🔑 **không** `[Authorize]`,
/// **không** DI, **không** I/O ⇒ **cả** app service **và** worker gọi được, mà luật chỉ có **MỘT** định nghĩa.
/// 📌 Cùng khuôn đã trả giá và đã thắng ở `CAT-003` (`SinhSlug`), `SAL-005` (`LuatWatermark`),
/// `INV-003` (`HanMucKenh.ChonHanMuc`, `ChieuDongHanMuc`).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public static class LuatDoiSoatThanhToan
{
    /// <summary>Việc cần làm sau khi cổng trả lời.</summary>
    public enum QuyetDinh
    {
        /// <summary>
        /// **KHÔNG làm gì.** 🔑 Gồm **HAI** ca **rất khác nhau**, và 📌 chỗ gọi **phải phân biệt** (xem
        /// <see cref="DaKetThuc"/>): (a) cổng nói *"chưa biết"* ⇒ **KHÔNG KIỂM ĐƯỢC**;
        /// (b) giao dịch **đã ở trạng thái CUỐI** ⇒ **KHÔNG CẦN** kiểm.
        /// </summary>
        KhongDoi,

        /// <summary>Cổng xác nhận **đã thu tiền** ⇒ gọi `MarkSucceeded`.</summary>
        GhiDaThuTien,

        /// <summary>Cổng xác nhận **không thành công**/khách huỷ ⇒ gọi `MarkFailed`.</summary>
        GhiThatBai
    }

    /// <summary>
    /// 🚨 **BẤT BIẾN TRUNG TÂM: giao dịch đã ở trạng thái CUỐI thì KHÔNG đối soát nữa.**
    ///
    /// 📌 **VÌ SAO PHẢI LÀ CÙNG MỘT HÀM:** `MarkSucceeded` **và** `MarkFailed` (trong entity) **mỗi hàm tự**
    /// kiểm *"đã có tiền chưa"*. 🔒 Nếu hàm quyết định dùng **danh sách trạng thái cuối KHÁC** với hai hàm đó
    /// thì sinh ra ca **vô nghĩa**: hàm quyết định nói *"cứ ghi"*, rồi entity **im lặng** `return` ⇒
    /// 📌 chỗ gọi **đếm** là *"đã đổi trạng thái"* trong khi **DB không đổi** ⇒ 🔑 **báo cáo đối soát SAI**.
    ///
    /// ⚠️ **LƯU Ý VỀ `Failed`:** nó **KHÔNG** nằm trong danh sách cuối — 🔒 **cố ý**: một giao dịch `Failed`
    /// **vẫn được** đối soát lại (IPN có thể đến muộn, hoặc lần trước cổng trả lỗi tạm thời) ⇒ cần cơ hội
    /// **sửa thành `Succeeded`** khi cổng xác nhận tiền **đã** vào. 📌 Đây chính là lưới an toàn cho **IPN mất**.
    /// </summary>
    public static bool DaKetThuc(PaymentTransactionStatus trangThaiHienTai)
        => trangThaiHienTai is PaymentTransactionStatus.Succeeded
            or PaymentTransactionStatus.PartiallyRefunded
            or PaymentTransactionStatus.Refunded;

    /// <summary>
    /// 🎯 **QUYẾT ĐỊNH LÀM GÌ** — hàm **THUẦN**, không I/O, không side effect.
    ///
    /// **LUẬT (bốn nhánh, và thứ tự nhánh QUAN TRỌNG):**
    /// <list type="number">
    ///   <item>Đã ở **trạng thái cuối** ⇒ `KhongDoi` — 🔒 **kể cả khi** cổng nói `Failed`/`Cancelled`: ghi đè
    ///     lên một giao dịch **đã hoàn tiền** sẽ **xoá dấu vết dòng tiền** (cùng lý do với chú thích trong
    ///     `PaymentTransaction.MarkFailed` — đọc ở đó để thấy hậu quả kế toán);</item>
    ///   <item>Cổng xác nhận `Succeeded` ⇒ `GhiDaThuTien` — 🔑 **kể cả khi** đang là `Failed`: tiền **đã** vào
    ///     thì sổ phải nói **đã** thu. 📌 Đây là nhánh **lưới an toàn cho IPN mất**;</item>
    ///   <item>Cổng xác nhận `Failed`/`Cancelled` ⇒ `GhiThatBai`;</item>
    ///   <item>🔑 Mọi giá trị **khác** (`Pending`, `Initiated`) ⇒ `KhongDoi` — ⚠️ **KHÔNG** hạ trạng thái:
    ///     *"chưa biết"* **khác** *"không thành công"*, và hạ trạng thái sẽ mở đường cho **thu lại** hoặc
    ///     **huỷ đơn oan**.</item>
    /// </list>
    /// </summary>
    /// <param name="hienTai">Trạng thái giao dịch **trong sổ** (entity).</param>
    /// <param name="tuCong">Trạng thái cổng trả lời qua `querydr`.</param>
    public static QuyetDinh Quyet(PaymentTransactionStatus hienTai, PaymentTransactionStatus tuCong)
    {
        // ① TRẠNG THÁI CUỐI ⇒ KHÔNG ĐỤNG. ⚠️ Nhánh này phải đứng TRƯỚC mọi nhánh ghi.
        if (DaKetThuc(hienTai))
        {
            return QuyetDinh.KhongDoi;
        }

        // ② Tiền ĐÃ vào ⇒ sổ phải nói đã thu (kể cả đang `Failed`). 🔑 Lưới an toàn cho IPN mất.
        if (tuCong == PaymentTransactionStatus.Succeeded)
        {
            return QuyetDinh.GhiDaThuTien;
        }

        // ③ Cổng nói KHÔNG thành công / khách huỷ ⇒ ghi thất bại.
        if (tuCong is PaymentTransactionStatus.Failed or PaymentTransactionStatus.Cancelled)
        {
            return QuyetDinh.GhiThatBai;
        }

        // ④ "CHƯA BIẾT" (`Pending`/`Initiated`) ⇒ KHÔNG LÙI. 📌 Chỗ gọi phải ghi nhận là "KHÔNG KIỂM ĐƯỢC",
        //    ⚠️ KHÔNG phải "đã kiểm và ổn".
        return QuyetDinh.KhongDoi;
    }
}

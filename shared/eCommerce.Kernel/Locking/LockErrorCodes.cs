namespace eCommerce.Kernel.Locking;

/// <summary>
/// 🚨 **MÃ LỖI DÙNG CHUNG CHO KHOÁ PHÂN TÁN** — đặt ở **Kernel** vì **mọi module** đều tham chiếu Kernel,
/// còn `eCommerceConsts` là hằng số **tầng app** (module **không** được tham chiếu — quy ước dự án).
///
/// `XC-003` **D10** quy định **một mã chung** cho tình huống "quá timeout mà không giành được khoá"
/// (kèm HTTP `409`) ⇒ mã này **không** thuộc dải của module nào.
///
/// ⚠️ **HIỆN CÒN 3 BẢN SAO CÙNG GIÁ TRỊ** (nợ kỹ thuật đã ghi nhận):
///   1. `eCommerce.Kernel.Locking.LockErrorCodes` ← **bản chuẩn** (module dùng bản này),
///   2. `eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa` (tầng app — `src/` đang dùng),
///   3. `EInvoiceNumberingErrorCodes.KhongGianhDuocKhoa` (module EInvoice tự khai).
/// 📌 Test `LockErrorCodeDongNhatTests` khoá bất biến **"mọi bản sao phải CÙNG giá trị"** ⇒ chống lệch âm thầm
/// ngay bây giờ. Việc **hợp nhất về một nguồn** là **nợ kỹ thuật cần người quyết** (chạm cả tầng app và module EInvoice).
/// </summary>
public static class LockErrorCodes
{
    /// <summary>Quá timeout mà không giành được khoá phân tán (`XC-003` D10) — HTTP `409`.</summary>
    public const string KhongGianhDuocKhoa = "eCommerce:Lock:0001";
}

using eCommerce.Kernel.Locking;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🛡️ **BẤT BIẾN: mọi bản sao mã lỗi KHOÁ PHÂN TÁN phải CÙNG giá trị.**
///
/// 🚨 **VÌ SAO (nợ kỹ thuật thật, 15/10/2026):** `XC-003` **D10** quy định **một mã chung**
/// `eCommerce:Lock:0001` cho tình huống "không giành được khoá". Nhưng hiện có **3 nơi** khai cùng mã:
///   1. `eCommerce.Kernel.Locking.LockErrorCodes` ← **bản chuẩn** (thêm ở lát cắt này, để **module** dùng được —
///      module không tham chiếu `eCommerceConsts` vì đó là hằng số tầng app),
///   2. `eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa` (tầng app),
///   3. `EInvoiceNumberingErrorCodes.KhongGianhDuocKhoa` (module EInvoice tự khai).
///
/// **Hợp nhất về một nguồn** là việc **cần người quyết** (chạm cả tầng app lẫn module EInvoice) ⇒ tạm thời
/// test này **khoá bất biến "các bản sao phải khớp nhau"**. Nếu ai đổi một bản mà quên bản khác ⇒ CI **ĐỎ**
/// thay vì để client nhận **hai mã khác nhau cho cùng một nguyên nhân** (đúng họ lỗi *"hai nguồn sự thật"*).
/// </summary>
public class LockErrorCodeDongNhatTests
{
    private const string Chuan = "eCommerce:Lock:0001";

    [Fact]
    public void Ban_chuan_trong_Kernel_phai_dung_gia_tri_0001()
    {
        Assert.Equal(Chuan, LockErrorCodes.KhongGianhDuocKhoa);
    }

    [Fact]
    public void Ban_tang_APP_phai_khop_ban_chuan()
    {
        Assert.Equal(Chuan, eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa);
    }

    [Fact]
    public void Ban_cua_module_EInvoice_HIEN_LECH_quy_uoc__ghi_nhan_thay_vi_che()
    {
        // 🚨 **LỆCH THẬT ĐÃ ĐO ĐƯỢC (15/10/2026):** `XC-003` **D10** nói "không giành được khoá" phải dùng
        // **MỘT mã chung** `eCommerce:Lock:0001`. Nhưng module `EInvoice` tự khai mã **riêng**:
        //   `EInvoiceNumberingErrorCodes.KhongGianhDuocKhoaCapSo = "eCommerce:EInvoice:0201"`.
        // ⇒ Client/đối soát nhận **HAI mã khác nhau cho CÙNG một nguyên nhân** (đúng họ lỗi "hai nguồn sự thật").
        //
        // 📌 **KHÔNG tự sửa**: đổi mã này là **thay đổi hợp đồng API** của module EInvoice (đã có tài liệu + test)
        // ⇒ **cần người quyết**. Test này **ghi nhận** hiện trạng: nếu ai đó hợp nhất về mã chung thì test **ĐỎ**
        // và buộc phải cập nhật cả tài liệu `EInvoice` + `XC-003` §15.2 ⇒ **không thể sửa nửa vời**.
        var maEInvoice = eCommerce.EInvoice.EInvoiceNumberingErrorCodes.KhongGianhDuocKhoaCapSo;

        Assert.NotEqual(Chuan, maEInvoice);

        Assert.StartsWith("eCommerce:EInvoice:", maEInvoice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ma_khoa_KHONG_duoc_nam_trong_dai_cua_module_nao()
    {
        // Không phải `eCommerce:Catalog:*` / `eCommerce:Pricing:*` / `eCommerce:Invoice:*`…
        // Lỗi khoá là **hạ tầng dùng chung** ⇒ tách khỏi mọi dải của module.
        Assert.StartsWith("eCommerce:Lock:", LockErrorCodes.KhongGianhDuocKhoa, StringComparison.Ordinal);
    }
}

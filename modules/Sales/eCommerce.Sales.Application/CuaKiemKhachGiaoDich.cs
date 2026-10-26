using eCommerce.Customers;
using Volo.Abp;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🚨 **CỔNG KIỂM "KHÁCH NÀY CÓ ĐƯỢC GIAO DỊCH KHÔNG"** — `CUS-001` **`ADR-04`** + nợ **§16.4 #4** (đóng ở `S3`).
///
/// **VÌ SAO TỆP NÀY TỒN TẠI (lỗi THẬT đã đo được, không phải phòng xa):**
/// `ICustomerDirectory.ChoPhepGiaoDichAsync` được định nghĩa từ 16/10/2026 nhưng **được gọi 0 nơi**
/// (`gd1-coding-sessions.md` §2.1 #3) ⇒ **khách `Blocked` VẪN ĐẶT ĐƯỢC HÀNG** ở đường thật.
/// 🔑 Bài học: *"một cổng được định nghĩa mà không ai gọi thì **không phải** cổng"* — cùng họ với
/// *"cổng không chạy tự động thì không phải cổng"*. Vì vậy cổng được **gom về MỘT chỗ** để:
/// <list type="number">
///   <item>**không** có hai bản sao của luật (sửa một nơi, quên nơi kia ⇒ hai nguồn sự thật);</item>
///   <item>**đếm được** số chỗ gọi (test `CuaKiemKhachBiChanTests` khoá cả **hai chiều**: có gọi / không gọi);</item>
///   <item>mã lỗi + thông điệp **chỉ một nơi**, nên CSKH đọc cùng một câu ở mọi đường.</item>
/// </list>
///
/// 🔒 **NÉM, KHÔNG TRẢ `bool`:** bên gọi **không được** lựa chọn *"bỏ qua cổng"*. Ở đây `bool` là
/// *"có một nhánh code mà ai đó có thể quên kiểm"* — còn `throw` là **đường duy nhất** đi tiếp.
///
/// ⚠️ **FAIL-CLOSED, CÓ CHỦ Ý:** khách **có** `Id` nhưng **không tìm thấy** (id sai / đã xoá mềm) ⇒ **CHẶN**
/// (`ChoPhepGiaoDichAsync` trả `false`). 📌 Im lặng cho qua sẽ tạo đơn gắn một `CustomerId` **không tồn tại**
/// ⇒ lịch sử mua **không tra được**, và bản xuất dữ liệu cá nhân của *"chủ thể"* đó **rỗng một cách vô lý**.
/// ✅ Còn khách **vãng lai** (`customerId == null`) **luôn được mua** (`ADR-03`) — chặn ở đây là chặn bán cho
/// người chưa từng tương tác, phá luôn luồng đặt hàng của khách mới.
/// </summary>
internal static class CuaKiemKhachGiaoDich
{
    /// <summary>
    /// **NÉM `eCommerce:Customers:0005` nếu khách không được giao dịch.** Gọi **TRƯỚC** mọi tác dụng phụ
    /// (tính tiền, giữ chỗ, ghi đơn) — 📌 nếu gọi sau, ta đã *"chặn đơn"* nhưng **hàng đã bị giữ** /
    /// **sổ giải trình đã ghi** ⇒ cổng chặn đơn lại **làm bẩn** kho và sổ.
    /// </summary>
    /// <param name="thuMucKhach">Hợp đồng đọc của module `Customers` (`ADR-12`) — **không** đọc thẳng bảng.</param>
    /// <param name="customerId">`null` = khách vãng lai ⇒ **được mua**.</param>
    public static async Task DamBaoDuocGiaoDichAsync(ICustomerDirectory thuMucKhach, Guid? customerId)
    {
        ArgumentNullException.ThrowIfNull(thuMucKhach);

        if (customerId is null)
        {
            // 🔑 Khách vãng lai: KHÔNG tra DB — vừa đúng nghiệp vụ (`ADR-03`), vừa không tốn một truy vấn
            // cho ca phổ biến nhất của một cửa hàng mới.
            return;
        }

        if (await thuMucKhach.ChoPhepGiaoDichAsync(customerId))
        {
            return;
        }

        // 🔒 MÃ LỖI `0005` (`KhachDangBiChan`) — KHÔNG mượn mã miền khác: `0005` ⇒ *"liên hệ quản trị để gỡ
        // chặn"*, còn ví dụ `0001` (`KhongTimThayKhach`) ⇒ *"hồ sơ không tồn tại"*. Hai việc khác nhau nên
        // hướng xử lý khác nhau (`INV-003` `0042`: mã đúng kỹ thuật nhưng sai nghiệp vụ là lỗi thật).
        // 📌 Kèm `customerId` + `lyDo` để người đọc log/UI biết **ai** bị chặn và **vì sao** bị từ chối.
        throw new BusinessException(CustomerErrorCodes.KhachDangBiChan)
            .WithData("customerId", customerId.Value)
            .WithData("lyDo",
                "Khách đang bị CHẶN nên KHÔNG được tạo giao dịch mới (CUS-001 ADR-04). " +
                "Cần người có quyền gỡ chặn (kèm lý do) trước khi bán tiếp.");
    }
}

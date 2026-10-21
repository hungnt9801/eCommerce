using eCommerce.PaymentHub.Payments;

namespace eCommerce.PaymentHub.Providers;

/// <summary>
/// Cấu hình **CỔNG GIẢ LẬP** (`S8`) — chỉ dùng ở `Development`. Đúng `PAY-001` §7.7.2.
///
/// 🔒 **CỐ Ý KHÔNG CÓ `HashSecret` Ở ĐÂY.** Bản thiết kế §7.7.2 ghi `Payments:Fake:HashSecret`, nhưng khi
/// thi hành thì nó **không thể chạy**: bên **xác thực** là `VnPayPaymentProvider` — nó đọc **`PaymentHub:VnPay:HashSecret`**.
/// Chữ ký do trang giả lập sinh ra **phải** khớp khoá mà bên xác thực dùng, nếu không mọi IPN giả lập bị
/// `400 Invalid Checksum` — 📌 và đó **không** phải lỗi cần "nới lỏng kiểm tra", mà là **ta đang ký sai khoá**.
/// ⇒ Dùng **cùng một** khoá `PaymentHub:VnPay:HashSecret` (giá trị **dev** đặt ở `appsettings.Development.json`),
/// giữ nguyên bất biến §7.7.4 #1: **KHÔNG** bỏ bước kiểm chữ ký. 📄 Đã sửa tài liệu trước khi code (§7.7.2).
/// </summary>
public class FakePaymentOptions
{
    /// <summary>
    /// Gốc URL của **trang giả lập** (tiến trình Web, mặc định `http://localhost:5100`).
    /// 🔑 Vì sao cấu hình chứ không hard-code: E2E và dev có thể chạy cổng khác; 📌 một URL cứng sai cổng
    /// cho ra `302` tới trang **không tồn tại** — trông như "cổng lỗi" trong khi cấu hình mới là thứ sai.
    /// </summary>
    public string SimulatorBaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>
    /// Gốc URL của **IPN THẬT** (tiến trình `HttpApi.Host`, mặc định `http://localhost:5000`).
    /// 🚨 **BẮT BUỘC LẤY TỪ CẤU HÌNH PHÍA MÁY CHỦ, KHÔNG BAO GIỜ TỪ THAM SỐ NGƯỜI DÙNG:** đây là nơi ta
    /// gửi **IPN đã ký**; cho client chọn đích nghĩa là cho họ chọn chỗ nhận chữ ký (SSRF + lộ khoá qua log
    /// của máy lạ). 📌 Cùng nguyên tắc `PAY-001` D3/D5.
    /// </summary>
    public string IpnBaseUrl { get; set; } = "http://localhost:5000";
}

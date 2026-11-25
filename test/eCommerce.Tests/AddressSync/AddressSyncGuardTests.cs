using eCommerce.Address;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` BR-010** — guard khởi động chặn nguồn danh mục ĐVHC CÔNG KHAI ngoài môi trường cho phép
/// (`D-78`). Đây là hàng rào **fail-closed**: nếu host vẫn start ở production với nguồn công khai thì mỗi
/// lần job chạy là một lần phụ thuộc dịch vụ bên thứ ba không SLA, và danh mục pháp lý có thể bị đóng
/// hàng loạt nếu nguồn trả về một phần.
///
/// 📌 Guard là **hàm thuần** — không DI, không host ⇒ test đơn vị thuần theo đúng phạm vi W18-2.
/// Ca khởi động Host thật (TC-22) thuộc W18-4.
/// </summary>
public class AddressSyncGuardTests
{
    private const string BaseUrlMacDinh = "https://provinces.open-api.vn/";

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData(" development ")]   // Trim + không phân biệt hoa/thường
    [InlineData("TESTING")]
    public void BR010_nguon_cong_khai_o_moi_truong_TRONG_allowlist_thi_KHONG_nem(string moiTruong)
    {
        // Không ném ⇒ host được start như thiết kế dev/test.
        AddressSyncGuard.KiemTra(enabled: true, baseUrl: BaseUrlMacDinh, environmentName: moiTruong);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BR010_nguon_cong_khai_o_moi_truong_NGOAI_allowlist_thi_NEM_fail_closed(string? moiTruong)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AddressSyncGuard.KiemTra(enabled: true, baseUrl: BaseUrlMacDinh, environmentName: moiTruong));

        // Mã BR phải có trong thông điệp để người trực đọc log biết vì sao host không start.
        Assert.Contains("BR-010", ex.Message, StringComparison.Ordinal);
        Assert.Contains(AddressSyncGuard.HostCongKhaiMacDinh, ex.Message, StringComparison.Ordinal);
        Assert.Contains("Development/Testing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BR010_tat_dong_bo_thi_guard_KHONG_y_kien_du_moi_truong_la_Production()
    {
        // `Address:Sync:Enabled=false` là mặc định an toàn ở production (`D-77`/`D-93`).
        AddressSyncGuard.KiemTra(enabled: false, baseUrl: BaseUrlMacDinh, environmentName: "Production");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR010_baseUrl_rong_thi_COI_la_nguon_cong_khai_mac_dinh_va_NEM_o_Production(string? baseUrl)
    {
        // "kể cả BaseUrl rỗng ⇒ mặc định provinces.open-api.vn" (BR-010) — KHÔNG được coi là "không có nguồn".
        var ex = Assert.Throws<InvalidOperationException>(
            () => AddressSyncGuard.KiemTra(enabled: true, baseUrl: baseUrl, environmentName: "Production"));

        Assert.Contains("BR-010", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BR010_host_KHAC_nguon_cong_khai_thi_guard_KHONG_y_kien_ke_ca_o_Production()
    {
        // TC-22 ca g — hành vi hiện định (lỗ hở ghi nhận, giảm thiểu bằng `Enabled=false`):
        // guard CHỈ chặn nguồn công khai, không chặn mọi nguồn ở production.
        AddressSyncGuard.KiemTra(enabled: true, baseUrl: "https://noi-bo.example.vn/", environmentName: "Production");
    }

    [Fact]
    public void BR010_baseUrl_khong_hop_le_thi_guard_KHONG_y_kien_hien_dinh()
    {
        // Ghi nhận hành vi hiện có của `LayHost`: URL không phải absolute ⇒ host null ⇒ guard bỏ qua.
        // Đổi hành vi này cần `D-xx`; test khóa lại để thay đổi không lọt im lặng.
        AddressSyncGuard.KiemTra(enabled: true, baseUrl: "khong-phai-url", environmentName: "Production");
    }
}

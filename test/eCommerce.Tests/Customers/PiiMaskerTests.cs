using eCommerce.Customers;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🚨 **CHE PII TRƯỚC KHI VÀO LOG** (`CUS-001` **ADR-11**) — hàm **THUẦN**.
///
/// **VÌ SAO CẦN TEST RIÊNG:** PII (email/SĐT) của khách **sẽ** lọt vào log nếu chỉ dựa vào kỷ luật —
/// và **log là nơi KHÓ XOÁ NHẤT** khi chủ thể dữ liệu yêu cầu xoá (Luật BV dữ liệu cá nhân **91/2025/QH15**,
/// hiệu lực 01/01/2026). 📌 Tiền lệ **đã có trong repo**: `PaymentTransaction.MaskSensitiveData` (che số thẻ).
///
/// 🔒 **Nguyên tắc: GIỮ ĐỦ ĐỂ ĐỐI CHIẾU, KHÔNG ĐỦ ĐỂ DÙNG.** Nếu che **sạch** (`***`) thì CSKH **không** tra
/// được *"log này của khách nào"* ⇒ họ **tìm cách khác** (chụp màn hình, xuất Excel) ⇒ **PII rò rỉ ra ngoài log**
/// — tệ hơn cả việc che vừa phải.
/// </summary>
public class PiiMaskerTests
{
    [Theory]
    [InlineData("nguyen.van.a@example.com", "ngu***@exa***")]
    [InlineData("a@b.co", "a***@b.c***")]
    [InlineData("A@EXAMPLE.COM", "A***@EXA***")]
    public void CheEmail__giu_dau_ten_va_dau_mien(string email, string mongDoi)
    {
        Assert.Equal(mongDoi, PiiMasker.CheEmail(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CheEmail__rong__tra_ro_la_KHONG_CO(string? email)
    {
        // 📌 Trả `"(không có)"` thay vì chuỗi rỗng: log phải **nói ra** là thiếu, không để khoảng trắng khó thấy.
        Assert.Equal("(không có)", PiiMasker.CheEmail(email));
    }

    [Fact]
    public void CheEmail__chuoi_KHONG_co_a__KHONG_in_nguyen_van()
    {
        // ⚠️ Ca thật: người dùng gõ **SĐT/CCCD** vào ô email. Nếu không có `@` mà ta in nguyên văn thì
        // **PII đầy đủ** vào log — đúng thứ hàm này tồn tại để chặn.
        var ketQua = PiiMasker.CheEmail("0912345678");

        Assert.DoesNotContain("0912345678", ketQua, StringComparison.Ordinal);
        Assert.EndsWith("***", ketQua, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0912345678", "091***678")]
    [InlineData("+84912345678", "849***678")]
    [InlineData("091 234 5678", "091***678")]
    [InlineData("091-234-5678", "091***678")]
    public void CheDienThoai__giu_3_dau_va_3_cuoi(string sdt, string mongDoi)
    {
        Assert.Equal(mongDoi, PiiMasker.CheDienThoai(sdt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0912")]      // quá ngắn ⇒ che TOÀN BỘ (không đủ chỗ để giữ mà vẫn vô danh)
    [InlineData("abc")]
    public void CheDienThoai__khong_du_hoac_khong_hop_le__che_toan_bo(string? sdt)
    {
        var ketQua = PiiMasker.CheDienThoai(sdt);

        Assert.True(ketQua is "(không có)" or "***", $"Kết quả không an toàn: '{ketQua}'");
    }

    [Fact]
    public void CheTrongChuoi__che_email_va_SDT_lan_trong_payload()
    {
        var payload = """{"customer":"Nguyễn Văn A","email":"a@example.com","phone":"0912345678"}""";

        var ketQua = PiiMasker.CheTrongChuoi(payload);

        Assert.NotNull(ketQua);
        Assert.DoesNotContain("a@example.com", ketQua, StringComparison.Ordinal);
        Assert.DoesNotContain("0912345678", ketQua, StringComparison.Ordinal);
        // 🔒 Phần KHÔNG phải PII phải còn nguyên — nếu che cả payload thì log vô dụng và người ta sẽ
        // quay lại in raw ⇒ mất luôn tác dụng của hàm này.
        Assert.Contains("Nguyễn Văn A", ketQua, StringComparison.Ordinal);
        Assert.Contains("customer", ketQua, StringComparison.Ordinal);
    }

    [Fact]
    public void CheTrongChuoi__chuoi_rong__tra_nguyen__khong_nem()
    {
        Assert.Null(PiiMasker.CheTrongChuoi(null));
        Assert.Equal(string.Empty, PiiMasker.CheTrongChuoi(string.Empty));
    }

    [Fact]
    public void CheTrongChuoi__IDEMPOTENT__che_lan_hai_khong_lam_hong()
    {
        var lan1 = PiiMasker.CheTrongChuoi("email a@example.com");

        // 📌 Quan trọng vì payload có thể đi qua **nhiều tầng** log, mỗi tầng che một lần. Nếu lần hai
        // làm hỏng (ví dụ cắt thêm) thì log trở nên vô nghĩa.
        var lan2 = PiiMasker.CheTrongChuoi(lan1);

        Assert.Equal(lan1, lan2);
    }
}

using eCommerce.PaymentHub.Payments;
using Volo.Abp;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **BẤT BIẾN CỦA `MerchantReference` (`vnp_TxnRef`) — ỔN ĐỊNH, TẤT ĐỊNH, VỪA CỘT** (`S4`).
///
/// **VÌ SAO CẦN TEST NÀY (nó chặn đúng một lỗi đã được ghi thành nợ):** trước `S4`, mã tham chiếu **không có
/// chỗ nào sinh** ở đường sản phẩm (`PAY-001` §7.5). Cách dễ nhất — và **sai kép** — là `Guid.NewGuid()` mỗi lần:
/// <list type="bullet">
///   <item>**mất idempotency**: retry của cùng một ý định thu tiền sinh mã **mới** ⇒ hệ thống không nhận ra
///     ⇒ nguy cơ **thu hai lần** (`XC-002` **BR-007**);</item>
///   <item>**trùng trong ngày** là điều VNPay cấm với `vnp_TxnRef`.</item>
/// </list>
/// 📌 Vì vậy test dưới đây không kiểm *"hàm chạy không ném"* — nó kiểm **bốn tính chất** mà nếu mất một cái là
/// mất tiền: **tất định**, **có tiền tố ngày GMT+7**, **vừa 64 ký tự**, và **gốc là tiền tố** (điều kiện để đếm
/// số lần thử mà không cần thêm cột).
/// </summary>
public class SinhMaThamChieuTests
{
    private static readonly DateTime Ngay18Utc = new(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);

    // ── (1) TẤT ĐỊNH — tính chất sống còn ────────────────────────────────────────────────────────

    [Fact]
    public void Cung_dau_vao__CUNG_ma__goi_hai_lan()
    {
        var lan1 = SinhMaThamChieu.Sinh("WEB-WEB-0123456789abcdef", Ngay18Utc);
        var lan2 = SinhMaThamChieu.Sinh("WEB-WEB-0123456789abcdef", Ngay18Utc);

        Assert.Equal(lan1, lan2);

        // 🔑 Chốt giá trị cụ thể: đổi định dạng mã là đổi **hợp đồng với cổng** ⇒ phải là thay đổi có ý thức,
        // không phải trôi theo lần refactor.
        Assert.Equal("PAY20261018-WEB-WEB-0123456789abcdef-1", lan1);
    }

    [Fact]
    public void Khac_lan_thu__KHAC_ma_nhung_CUNG_goc()
    {
        var lan1 = SinhMaThamChieu.Sinh("DH-001", Ngay18Utc, 1);
        var lan2 = SinhMaThamChieu.Sinh("DH-001", Ngay18Utc, 2);

        Assert.NotEqual(lan1, lan2);
        Assert.EndsWith("-1", lan1, StringComparison.Ordinal);
        Assert.EndsWith("-2", lan2, StringComparison.Ordinal);

        var goc = SinhMaThamChieu.Goc("DH-001", Ngay18Utc);
        Assert.StartsWith(goc, lan1, StringComparison.Ordinal);
        Assert.StartsWith(goc, lan2, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🚨 **GỐC PHẢI LÀ TIỀN TỐ CỦA MÃ Ở MỌI ĐỘ DÀI** — kể cả khi số đơn **bị cắt**.
    ///
    /// **VÌ SAO TEST RIÊNG CA NÀY:** `TaoGiaoDichAsync` **đếm số lần thử** bằng
    /// `MerchantReference.StartsWith(gốc)`. Nếu phần thân bị cắt theo **độ dài còn lại** (phụ thuộc số lần thử)
    /// thì gốc của lần 1 **không** còn là tiền tố của mã lần 2 ⇒ phép đếm trả `0` ⇒ **sinh trùng mã** ⇒ đụng
    /// unique index (`23505`) thay vì tạo lần thử mới. 📌 Đây là lỗi *"hỏng âm thầm"* điển hình: nó chỉ lộ ra
    /// khi số đơn dài (**hoặc** khi lần thử ≥ 10) — tức là ở dữ liệu thật, không phải ở test đầu tiên.
    /// </summary>
    [Theory]
    [InlineData("DH-1")]
    [InlineData("WEB-WEB-0123456789abcdef")]
    [InlineData("RAT-DAI-DH-0123456789012345678901234567890123456789")]
    [InlineData("DAI-HON-CA-GIOI-HAN-64-KY-TU-0123456789012345678901234567890123456789012345678901234567890123456789")]
    public void Goc_LUON_la_tien_to_cua_ma__ke_ca_khi_bi_cat(string soDon)
    {
        var goc = SinhMaThamChieu.Goc(soDon, Ngay18Utc);

        foreach (var lanThu in new[] { 1, 2, 9, 10, 99 })
        {
            var ma = SinhMaThamChieu.Sinh(soDon, Ngay18Utc, lanThu);

            Assert.StartsWith(goc, ma, StringComparison.Ordinal);
            Assert.True(ma.Length <= SinhMaThamChieu.DoDaiToiDa, $"Mã dài {ma.Length} > {SinhMaThamChieu.DoDaiToiDa}: {ma}");
        }
    }

    // ── (2) TIỀN TỐ NGÀY theo GMT+7 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Ngay_theo_GMT_7__20h_UTC_la_NGAY_HOM_SAU()
    {
        // 20:00 UTC ngày 18 = 03:00 ngày 19 giờ Việt Nam. VNPay tính "ngày" theo giờ VN ⇒ tiền tố phải là 19.
        var ma = SinhMaThamChieu.Sinh("DH-001", new DateTime(2026, 10, 18, 20, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("PAY20261019-", ma, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🚨 **BẪY #6 CỦA `handover` §5.1 — `DateTimeKind.Unspecified` KHÔNG được coi là giờ địa phương.**
    ///
    /// `ToUniversalTime()` trên một `DateTime` `Unspecified` sẽ **trừ offset của máy** ⇒ trên máy UTC+7, mã sinh
    /// ra **lệch 7 tiếng** so với cùng giá trị đó ở `Kind = Utc`. Đây **không** phải ca lý thuyết: `Clock.Now`
    /// của ABP trả UTC, nhưng dữ liệu đọc từ cột `timestamp` (không có zone) hoặc từ test dựng tay thì
    /// `Kind = Unspecified`.
    /// </summary>
    [Fact]
    public void Kind_Unspecified__coi_la_UTC__KHONG_bi_tru_offset_may()
    {
        var utc = SinhMaThamChieu.Sinh("DH-001", new DateTime(2026, 10, 18, 20, 0, 0, DateTimeKind.Utc));
        var khongRo = SinhMaThamChieu.Sinh("DH-001", new DateTime(2026, 10, 18, 20, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(utc, khongRo);
    }

    // ── (3) CHUẨN HOÁ — mã đi vào query string của cổng ─────────────────────────────────────────

    [Fact]
    public void Ky_tu_co_nghia_trong_query_string__bi_bo()
    {
        // Nếu để `&`/`=` lọt vào `vnp_TxnRef` thì URL thanh toán bị **tách tham số** ⇒ cổng nhận sai mã
        // ⇒ IPN về không tra được giao dịch. 📌 Cùng họ với bẫy `?` (alias `Where-Object`) ở `handover` §5.1 #1.
        var ma = SinhMaThamChieu.Sinh("DH&001=x?y", Ngay18Utc);

        Assert.DoesNotContain('&', ma);
        Assert.DoesNotContain('=', ma);
        Assert.DoesNotContain('?', ma);
        Assert.StartsWith("PAY20261018-DH001xy-1", ma, StringComparison.Ordinal);
    }

    [Fact]
    public void So_don_rong__NEM__0006()
    {
        var loi = Assert.Throws<BusinessException>(() => SinhMaThamChieu.Sinh("   ", Ngay18Utc));

        Assert.Equal(PaymentErrorCodes.MaThamChieuKhongHopLe, loi.Code);
    }

    [Fact]
    public void So_don_chi_co_ky_tu_la__NEM__0006()
    {
        // Chỉ có ký tự **không phải chữ Latin** ⇒ chuẩn hoá thành rỗng. Nếu **không** ném thì mã sẽ là
        // `PAY20261018--1` — hai đơn khác nhau cho ra **cùng** một mã ⇒ chính là lỗi mà hàm này chống.
        var loi = Assert.Throws<BusinessException>(() => SinhMaThamChieu.Sinh("名前", Ngay18Utc));

        Assert.Equal(PaymentErrorCodes.MaThamChieuKhongHopLe, loi.Code);
    }

    /// <summary>
    /// 🚨 **SỐ ĐƠN TIẾNG VIỆT: PHẢI BỎ DẤU, KHÔNG ĐƯỢC XOÁ KÝ TỰ.**
    ///
    /// **Đây là lỗi THẬT của chính `S4`, do test này bắt ngay lượt đầu:** bản đầu chỉ *"bỏ ký tự không hợp lệ"* ⇒
    /// `ĐƠN-001` thành **`N-001`** (mất `ĐƠ`, còn `N`) ⇒ hai đơn khác nhau (`ĐƠN-001` và `N-001`) cho ra **cùng
    /// một mã** ⇒ unique index chặn ⇒ đơn bình thường **không tạo được giao dịch**, và lý do **không ai đoán ra**
    /// (triệu chứng là `23505` ở một chỗ không liên quan tới dấu tiếng Việt).
    /// 🔑 `FormD` + `Đ→D` cho ra `DON-001` — đúng thứ người viết số đơn muốn.
    /// </summary>
    [Fact]
    public void So_don_tieng_Viet__BO_DAU__khong_mat_chu_cai()
    {
        Assert.Equal(
            "PAY20261018-DON-001-1",
            SinhMaThamChieu.Sinh("ĐƠN-001", Ngay18Utc));

        // Chiều ngược: số đơn **đã** ASCII phải giữ nguyên (không bị "chuẩn hoá" thành thứ khác).
        Assert.Equal(
            "PAY20261018-DON-001-1",
            SinhMaThamChieu.Sinh("DON-001", Ngay18Utc));
    }

    [Fact]
    public void Lan_thu_nho_hon_1__NEM()
    {
        Assert.Throws<BusinessException>(() => SinhMaThamChieu.Sinh("DH-001", Ngay18Utc, 0));
        Assert.Throws<BusinessException>(() => SinhMaThamChieu.Sinh("DH-001", Ngay18Utc, -3));
    }
}

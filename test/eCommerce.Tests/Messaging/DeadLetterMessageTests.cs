using eCommerce.Messaging;
using eCommerce.Messaging.DeadLetter;
using Volo.Abp;

namespace eCommerce.Tests.Messaging;

/// <summary>
/// 🚨 **HÀNG ĐỢI THƯ CHẾT** (`XC-002`) — bất biến ở **tầng domain**.
///
/// 🔒 **VÌ SAO DLQ TỒN TẠI (và vì sao test này quan trọng hơn vẻ ngoài):** khi một sự kiện **không xử lý được**,
/// hai lựa chọn "quen thuộc" đều **sai**:
/// <list type="number">
///   <item>🚨 **Ném lỗi để nơi phát retry** ⇒ với luồng đơn từ sàn, nơi phát **giữ con trỏ đồng bộ** ⇒ sàn gửi lại
///     **đúng** đơn đó ⇒ **lặp vô hạn** và **chặn CẢ KÊNH**. 📌 Đã **đo được thật** (con trỏ kẹt ở `NULL` qua
///     nhiều nhịp) ⇒ một đơn nghi vấn **sập luồng bán hàng**;</item>
///   <item>🚨 **Chỉ `LogError`** ⇒ sự kiện **biến mất**. 📌 Log **xoay vòng** (retention vài ngày), **không** tra
///     cứu được theo khoá nghiệp vụ, **không ai** biết còn tồn bao nhiêu ⇒ **mất dữ liệu nhưng có vẻ như đã xử lý**.</item>
/// </list>
/// ⇒ DLQ là lựa chọn **duy nhất** vừa **không mất dữ liệu** vừa **không chặn luồng**. 🔑 Và ba bất biến dưới đây
/// là điều làm nó **dùng được** (không chỉ **ghi được**).
/// </summary>
public class DeadLetterMessageTests
{
    private const string TenSuKien = "eCommerce.ChannelHub.Events.ChannelOrderPulledEto";
    private static readonly DateTime MocVao = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private static DeadLetterMessage Tao(
        string tenSuKien = TenSuKien,
        string khoa = "shopee:order:12345",
        string payload = """{"ExternalOrderId":"12345"}""",
        string moTa = "Cùng khoá đơn nhưng nội dung khác bản đã nhận.")
        => new(Guid.NewGuid(), tenSuKien, khoa, payload, "abc123", LyDoVaoDlq.XungDotPayload, moTa, MocVao);

    // ── (1) BẤT BIẾN: PAYLOAD GỐC ĐƯỢC GIỮ NGUYÊN VẸN ────────────────────────────────────────

    [Fact]
    public void Payload_goc__duoc_giu_NGUYEN_VEN__khong_cat_khong_chuan_hoa_lai()
    {
        // 🚨 Đây là bất biến **quan trọng nhất** của DLQ.
        // 🔒 Nếu **cắt** payload ⇒ **không phát lại được**. Nếu **chuẩn hoá lại** ⇒ **mất** chính khác biệt
        // là **nguyên nhân** vào DLQ (xung đột payload là *"cùng khoá, khác nội dung"* — chuẩn hoá lại thì
        // hai bản có thể trông **giống nhau**, và ta **mất bằng chứng**).
        const string payloadLa = "  {\"b\":2,\"a\":1}  \n";

        var ban = Tao(payload: payloadLa);

        Assert.Equal(payloadLa, ban.PayloadJson);
    }

    [Fact]
    public void Payload_RONG__van_duoc_chap_nhan()
    {
        // 📌 `""` **không** phải `null`: JSON rỗng là **giá trị hợp lệ về mặt kiểu**. 🔒 Cấm `null` (không phát lại
        // được), nhưng **không** cấm chuỗi rỗng — vì từ chối nó sẽ chặn một sự kiện **có thật** khỏi DLQ
        // ⇒ sự kiện đó **mất** ⇒ đúng điều DLQ tồn tại để ngăn.
        var ban = Tao(payload: string.Empty);

        Assert.Equal(string.Empty, ban.PayloadJson);
    }

    // ── (2) BẤT BIẾN: KHOÁ NGHIỆP VỤ ĐƯỢC GHI RIÊNG ──────────────────────────────────────────

    [Fact]
    public void Khoa_nghiep_vu__duoc_ghi_RIENG__khong_phai_boc_tu_JSON()
    {
        // 🔒 Vì sao tách trường: câu hỏi vận hành phổ biến nhất là *"đơn `SHOPEE/12345` có vấn đề gì?"*.
        // 📌 Không có cột này thì phải **quét cả bảng** bằng `LIKE` trên JSON — chậm, và **sai** khi khoá
        // xuất hiện trong trường khác.
        var ban = Tao(khoa: "shopee:order:12345");

        Assert.Equal("shopee:order:12345", ban.KhoaNghiepVu);
    }

    // ── (3) BẤT BIẾN: MÔ TẢ LÝ DO BẮT BUỘC (mã enum KHÔNG ĐỦ) ───────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Thieu_MO_TA_ly_do__NEM(string moTaRong)
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(moTa: moTaRong));

        Assert.Equal(MessagingErrorCodes.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Loi_thieu_mo_ta__noi_RO_la_thieu_mo_ta_chu_khong_chi_noi_thieu_tham_so()
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(moTa: ""));

        // 🔒 Một mã enum (`XungDotPayload`) **không** cho người trực biết **phải làm gì**. 📌 Vì vậy mô tả dạng chữ
        // là **bắt buộc**, và lỗi phải nói **rõ trường nào**.
        Assert.Equal("moTaLyDo", loi.Data["truong"]);
        Assert.Contains("mô tả", (string)loi.Data["lyDo"]!, StringComparison.Ordinal);
    }

    // ── (4) DỮ LIỆU VÀO: rỗng + quá dài ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Thieu_TEN_SU_KIEN__NEM(string tenRong)
    {
        // 🔒 Thiếu tên kiểu ⇒ payload là **JSON vô chủ** ⇒ không biết **deserialize về đâu** ⇒ **không phát lại được**.
        Assert.Throws<BusinessException>(() => Tao(tenSuKien: tenRong));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Thieu_KHOA_NGHIEP_VU__NEM(string khoaRong)
    {
        Assert.Throws<BusinessException>(() => Tao(khoa: khoaRong));
    }

    [Fact]
    public void Ten_su_kien__qua_dai__NEM()
    {
        var quaDai = new string('x', DeadLetterMessage.DoDaiTenSuKienToiDa + 1);

        Assert.Throws<BusinessException>(() => Tao(tenSuKien: quaDai));
    }

    [Fact]
    public void Mo_ta__qua_dai__NEM()
    {
        var quaDai = new string('x', DeadLetterMessage.DoDaiLyDoToiDa + 1);

        Assert.Throws<BusinessException>(() => Tao(moTa: quaDai));
    }

    [Fact]
    public void Ban_ghi_moi__CHUA_xu_ly__va_khong_co_dau_vet_xu_ly()
    {
        var ban = Tao();

        // 🔒 Mặc định `false` ⇒ **mọi** sự kiện vào DLQ đều *"chưa xử lý"* ⇒ không dòng nào **vô tình**
        // được coi là đã xong.
        Assert.False(ban.DaXuLy);
        Assert.Null(ban.XuLyLucUtc);
        Assert.Null(ban.NguoiXuLy);
        Assert.Null(ban.LyDoXuLy);
        Assert.Equal(MocVao, ban.VaoDlqLucUtc);
    }

    // ── (5) XỬ LÝ: bắt buộc lý do + người · KHÔNG ghi đè bằng chứng ─────────────────────────

    [Fact]
    public void DanhDauDaXuLy__ghi_du_nguoi_ly_do_va_moc()
    {
        var ban = Tao();
        var moc = MocVao.AddHours(1);

        ban.DanhDauDaXuLy(moc, "nguyen.van.a", "Đã phát lại thủ công thành công");

        Assert.True(ban.DaXuLy);
        Assert.Equal(moc, ban.XuLyLucUtc);
        Assert.Equal("nguyen.van.a", ban.NguoiXuLy);
        Assert.Contains("phát lại", ban.LyDoXuLy!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DanhDauDaXuLy__thieu_LY_DO__NEM(string lyDoRong)
    {
        var ban = Tao();

        var loi = Assert.Throws<BusinessException>(
            () => ban.DanhDauDaXuLy(MocVao, "nguyen.van.a", lyDoRong));

        Assert.Equal(MessagingErrorCodes.ThieuThongTinBatBuoc, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": entity PHẢI nguyên vẹn sau khi ném.
        Assert.False(ban.DaXuLy);
        Assert.Null(ban.XuLyLucUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DanhDauDaXuLy__thieu_NGUOI_XU_LY__NEM(string nguoiRong)
    {
        var ban = Tao();

        var loi = Assert.Throws<BusinessException>(
            () => ban.DanhDauDaXuLy(MocVao, nguoiRong, "lý do hợp lệ"));

        Assert.Equal(MessagingErrorCodes.ThieuThongTinBatBuoc, loi.Code);
        Assert.False(ban.DaXuLy);
    }

    [Fact]
    public void Loi__mang_khoa_nghiep_vu__de_biet_dong_nao()
    {
        var ban = Tao(khoa: "lazada:order:999");

        var loi = Assert.Throws<BusinessException>(() => ban.DanhDauDaXuLy(MocVao, "a", ""));

        // 📌 Một lỗi *"thiếu lý do"* **không nói được** đang xử lý **sự kiện nào** thì người trực phải tự đi tìm —
        // và DLQ có thể có hàng nghìn dòng.
        Assert.Equal("lazada:order:999", loi.Data["khoa"]);
    }

    [Fact]
    public void Goi_LAI__no_op__KHONG_ghi_de_bang_chung()
    {
        var ban = Tao();
        ban.DanhDauDaXuLy(MocVao, "nguyen.van.a", "lý do ĐẦU TIÊN");

        ban.DanhDauDaXuLy(MocVao.AddDays(3), "tran.thi.b", "lý do THỨ HAI (cố đổi)");

        // 🚨 Bất biến: lần xử lý **đầu** là **bằng chứng** ai đã quyết định gì với sự kiện hỏng.
        // 🔒 Cho ghi đè ⇒ người sau **thay được bằng chứng** — và **không có lỗi nào**.
        Assert.Equal("nguyen.van.a", ban.NguoiXuLy);
        Assert.Equal("lý do ĐẦU TIÊN", ban.LyDoXuLy);
        Assert.Equal(MocVao, ban.XuLyLucUtc);
    }

    [Fact]
    public void Goi_LAI_VOI_LY_DO_RONG__van_NO_OP__KHONG_nem()
    {
        var ban = Tao();
        ban.DanhDauDaXuLy(MocVao, "a", "lý do đầu");

        // 🔑 **THỨ TỰ KIỂM LÀ MỘT PHẦN CỦA ĐẶC TẢ:** kiểm `DaXuLy` (no-op) **TRƯỚC** kiểm rỗng.
        // 📌 Nếu kiểm rỗng trước thì **retry** không mang lý do sẽ **NÉM** trên dòng **đã xong**
        // ⇒ consumer **retry vô hạn** vì một việc **không cần làm gì** (bài học `REC-001` §17.3).
        ban.DanhDauDaXuLy(MocVao.AddDays(1), "", "");

        Assert.Equal("a", ban.NguoiXuLy);
        Assert.Equal("lý do đầu", ban.LyDoXuLy);
    }

    // ── (6) LÝ DO VÀO DLQ: phân biệt được NGUYÊN NHÂN ────────────────────────────────────────

    [Theory]
    [InlineData(LyDoVaoDlq.XungDotPayload)]
    [InlineData(LyDoVaoDlq.HaWatermark)]
    [InlineData(LyDoVaoDlq.LoiXuLy)]
    public void LyDo__luu_dung_nguyen_nhan(LyDoVaoDlq lyDo)
    {
        // 🔒 Vì sao phân biệt: ba nguyên nhân cần **ba cách xử lý khác nhau**:
        //   • `XungDotPayload` ⇒ người quyết định **bản nào đúng**;
        //   • `HaWatermark`    ⇒ **sàn gửi sai thứ tự** (hoặc hai nguồn tranh nhau) — **không phải** lỗi hệ thống;
        //   • `LoiXuLy`        ⇒ **lỗi** (dữ liệu sai, hạ tầng) ⇒ cần sửa **mã**, không phải sửa dữ liệu.
        // 📌 Gộp cả ba vào một mã ⇒ người trực **không biết** phải làm gì.
        var ban = new DeadLetterMessage(
            Guid.NewGuid(), TenSuKien, "shopee:order:1", "{}", "h", lyDo, "mô tả", MocVao);

        Assert.Equal(lyDo, ban.LyDo);
    }
}

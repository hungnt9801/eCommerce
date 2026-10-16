using eCommerce.Inventory;
using eCommerce.Reconciliation;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🚨 **XỬ LÝ LỆCH ĐỐI SOÁT TỒN KHO** (`REC-001`) — **chênh lệch TÀI SẢN**, không phải thông báo.
///
/// **VÌ SAO PHẢI CÓ TEST (nợ đã ghi ở `rec-001` §16.8, và đây là lý do nó nặng):**
/// `ReconRun`/`ReconRunMismatch` + service đối soát + worker **đã có, đã chạy, đã ghi dữ liệu** — nhưng bảng
/// lệch là **append-only, KHÔNG có trạng thái**: một dòng đã xử lý xong **trông y hệt** một dòng chưa ai
/// đụng tới. Hệ quả **đo được**: mỗi nhịp job, người trực phải đọc lại **toàn bộ** danh sách cũ để tìm cái
/// mới ⇒ tới lúc danh sách dài thì **không ai đọc nữa**.
///
/// 🔒 **Ba bất biến được khoá ở đây:**
/// <list type="number">
///   <item><b>Lý do + người xử lý BẮT BUỘC</b> ⇒ chặn *"bấm nút cho hết danh sách"*. Không có lý do thì
///     6 tháng sau **không ai** phân biệt được *"đã kiểm kê và khớp lại"* với *"ai đó bấm cho xong"*;</item>
///   <item><b>KHÔNG ghi đè bằng chứng</b> ⇒ lần xử lý **đầu** là bằng chứng ai đã chấp nhận chênh lệch;
///     cho ghi đè thì người sau **thay được bằng chứng** — và **không có lỗi nào** để lần ra;</item>
///   <item><b>KHÔNG sửa SỐ ĐO</b> (`OnHand`/`TongSoCai`/`ChenhLech`) — chúng là **ảnh chụp tại thời điểm quét**;
///     sửa là **phá bằng chứng**, biến lịch sử đối soát thành vô nghĩa.</item>
/// </list>
/// </summary>
public class XuLyLechDoiSoatTests
{
    private static ReconRunMismatch Tao(int onHand = 96, int tongSoCai = 100, bool vuotNguong = true)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-001", onHand, tongSoCai, vuotNguong);

    private static readonly DateTime MocXuLy = new(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc);

    // ── (1) TRẠNG THÁI MẶC ĐỊNH: CHƯA xử lý ────────────────────────────────────────────────────

    [Fact]
    public void Dong_lech_MOI__CHUA_xu_ly__va_khong_co_dau_vet_xu_ly()
    {
        var d = Tao();

        // 🔒 Mặc định `false` ⇒ **mọi** dòng mới đều "chưa xử lý" ⇒ không dòng nào **vô tình** được coi là xong.
        Assert.False(d.DaXuLy);
        Assert.Null(d.XuLyLucUtc);
        Assert.Null(d.NguoiXuLy);
        Assert.Null(d.LyDoXuLy);
    }

    // ── (2) ĐÁNH DẤU ĐÃ XỬ LÝ: có lý do + người + mốc ──────────────────────────────────────────

    [Fact]
    public void DanhDauDaXuLy__ghi_du_nguoi_ly_do_va_moc()
    {
        var d = Tao();

        d.DanhDauDaXuLy(MocXuLy, "nguyen.van.a", "Đã kiểm kê lại kho, khớp sổ");

        Assert.True(d.DaXuLy);
        Assert.Equal(MocXuLy, d.XuLyLucUtc);
        Assert.Equal("nguyen.van.a", d.NguoiXuLy);
        Assert.Contains("kiểm kê", d.LyDoXuLy, StringComparison.Ordinal);
    }

    [Fact]
    public void DanhDauDaXuLy__moc_thoi_gian_do_NGUOI_GOI_truyen__ham_TAT_DINH()
    {
        var d1 = Tao();
        var d2 = Tao();

        d1.DanhDauDaXuLy(MocXuLy, "a", "lý do");
        d2.DanhDauDaXuLy(MocXuLy, "a", "lý do");

        // 📌 Không đọc `DateTime.UtcNow` bên trong ⇒ hai lần gọi với **cùng** đầu vào cho **cùng** kết quả.
        // Nhờ vậy test được, và mốc ghi vào DB là mốc **người gọi quyết định** (không lệ thuộc đồng hồ máy).
        Assert.Equal(d1.XuLyLucUtc, d2.XuLyLucUtc);
    }

    // ── (3) LÝ DO / NGƯỜI XỬ LÝ: BẮT BUỘC ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Thieu_LY_DO__NEM__va_KHONG_doi_trang_thai(string lyDoRong)
    {
        var d = Tao();

        var loi = Assert.Throws<BusinessException>(() => d.DanhDauDaXuLy(MocXuLy, "nguyen.van.a", lyDoRong));

        Assert.Equal(ReconErrorCodes.ThieuLyDoXuLyLech, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": entity PHẢI nguyên vẹn sau khi ném.
        Assert.False(d.DaXuLy);
        Assert.Null(d.XuLyLucUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Thieu_NGUOI_XU_LY__NEM(string nguoiRong)
    {
        var d = Tao();

        var loi = Assert.Throws<BusinessException>(() => d.DanhDauDaXuLy(MocXuLy, nguoiRong, "lý do hợp lệ"));

        Assert.Equal(ReconErrorCodes.ThieuLyDoXuLyLech, loi.Code);
        Assert.False(d.DaXuLy);
    }

    [Fact]
    public void Loi__co_du_du_lieu_de_dieu_tra__khong_chi_noi_thieu_tham_so()
    {
        var d = Tao(onHand: 96, tongSoCai: 100);

        var loi = Assert.Throws<BusinessException>(() => d.DanhDauDaXuLy(MocXuLy, "a", ""));

        // 📌 Lỗi phải nói được **cặp nào lệch bao nhiêu** — nếu chỉ nói "thiếu lý do" thì người dùng không
        // biết đang xử lý dòng nào (danh sách có thể dài), và họ phải tự đi tìm.
        Assert.True(loi.Data.Contains("sku"), "Lỗi phải mang `sku` để điều tra.");
        Assert.True(loi.Data.Contains("chenhLech"), "Lỗi phải mang `chenhLech` để biết lệch bao nhiêu.");
        Assert.Equal("SKU-001", loi.Data["sku"]);
        Assert.Equal(-4, loi.Data["chenhLech"]);     // 96 − 100, GIỮ DẤU (âm = thiếu so với sổ)
    }

    // ── (4) KHÔNG GHI ĐÈ BẰNG CHỨNG ────────────────────────────────────────────────────────────

    [Fact]
    public void Goi_LAI__no_op__KHONG_ghi_de_ly_do_nguoi_va_moc()
    {
        var d = Tao();
        d.DanhDauDaXuLy(MocXuLy, "nguyen.van.a", "lý do ĐẦU TIÊN");

        var mocSau = MocXuLy.AddHours(5);
        d.DanhDauDaXuLy(mocSau, "tran.thi.b", "lý do THỨ HAI (cố đổi)");

        // 🚨 **ĐÂY LÀ BẤT BIẾN QUAN TRỌNG NHẤT CỦA FILE.** Lần xử lý **đầu** là **bằng chứng** ai đã chấp
        // nhận chênh lệch tài sản. Nếu cho ghi đè thì người sau **thay được bằng chứng** — không lỗi nào.
        Assert.Equal("nguyen.van.a", d.NguoiXuLy);
        Assert.Equal("lý do ĐẦU TIÊN", d.LyDoXuLy);
        Assert.Equal(MocXuLy, d.XuLyLucUtc);
    }

    [Fact]
    public void Goi_LAI_VOI_LY_DO_RONG__van_NO_OP__KHONG_nem()
    {
        var d = Tao();
        d.DanhDauDaXuLy(MocXuLy, "a", "lý do đầu");

        // 📌 Thứ tự kiểm là **một phần của đặc tả**: kiểm `DaXuLy` (no-op) **TRƯỚC** kiểm rỗng.
        // Nếu kiểm rỗng trước thì retry của một job (không mang lý do) sẽ **NÉM** trên một dòng **đã xong**
        // ⇒ consumer retry vô hạn vì một việc **không cần làm gì**.
        d.DanhDauDaXuLy(MocXuLy.AddDays(1), "", "");

        Assert.Equal("a", d.NguoiXuLy);
        Assert.Equal("lý do đầu", d.LyDoXuLy);
    }

    // ── (5) KHÔNG SỬA SỐ ĐO ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Xu_ly_lech__KHONG_duoc_sua_SO_DO__chung_la_ANH_CHUP_luc_quét()
    {
        var d = Tao(onHand: 96, tongSoCai: 100);

        d.DanhDauDaXuLy(MocXuLy, "a", "đã kiểm kê");

        // 🔒 `OnHand`/`TongSoCai`/`ChenhLech` là **ảnh chụp tại thời điểm quét** — sửa chúng là **phá bằng chứng**.
        // 📌 Và đây là lý do kiểu của ba trường đó là `protected set`: **không có** đường ghi từ ngoài.
        Assert.Equal(96, d.OnHand);
        Assert.Equal(100, d.TongSoCai);
        Assert.Equal(-4, d.ChenhLech);
        Assert.True(d.VuotNguong);
    }

    // ── (6) CHENH LECH GIU DAU (de biet lech VE PHIA NAO) ─────────────────────────────────────

    [Theory]
    [InlineData(96, 100, -4)]    // âm = hệ thống ghi THIẾU so với sổ
    [InlineData(104, 100, 4)]    // dương = hệ thống ghi THỪA so với sổ
    [InlineData(100, 100, 0)]
    public void ChenhLech__GIU_DAU__de_biet_lech_ve_phia_nao(int onHand, int tongSoCai, int mongDoi)
    {
        var d = Tao(onHand, tongSoCai);

        Assert.Equal(mongDoi, d.ChenhLech);
    }
}

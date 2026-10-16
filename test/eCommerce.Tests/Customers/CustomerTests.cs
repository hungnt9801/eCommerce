using eCommerce.Customers;
using Volo.Abp;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🚨 **MÁY TRẠNG THÁI KHÁCH HÀNG + NÂNG CẤP VÃNG LAI** (`CUS-001`) — chỗ **dễ mất tiền/uy tín** nhất
/// của miền khách.
///
/// **VÌ SAO PHẢI LÀ MÁY TRẠNG THÁI NÉM LỖI (bài học #3 của tài liệu, đã trả giá):**
/// *"guard trạng thái no-op im lặng làm sự kiện rơi không để lại dấu vết"* (từ `SetPaidStatus()` của eShop,
/// ghi ở `XC-002` §0 và quy ước **#11**). Nếu `Customer.Status` có `set` công khai thì mọi đường ghi đều
/// đặt được trạng thái tuỳ ý, và một lần đặt sai **không** sinh lỗi nào.
///
/// 🔒 **Luật kiểm:**
///   (1) `Blocked` là trạng thái **hấp thụ CÓ ĐIỀU KIỆN**: `KichHoat` **KHÔNG** ra được, chỉ `MoChan` (cần lý do);
///   (2) `Chan`/`MoChan` **BẮT BUỘC có lý do** — chặn mà không ghi lý do thì 6 tháng sau không ai dám gỡ;
///   (3) `MoChan` khách **không** bị chặn ⇒ **NÉM** (nếu im lặng, người vận hành **tin** đã gỡ cho người khác);
///   (4) `NangCapVangLai`: **một tài khoản ⇒ một khách** (`ADR-07`) — đã có chủ ⇒ **NÉM**, đang bị chặn ⇒ **NÉM**,
///       **nhưng** gọi lại **đúng** tài khoản đó ⇒ **idempotent** (không ném — nếu ném thì consumer retry vô hạn);
///   (5) 🚨 **`Address` KHÔNG thuộc `Customer`** (`ADR-01`) — test reflection chặn "hai mô hình địa chỉ".
/// </summary>
public class CustomerTests
{
    private static Customer Tao(string ten = "Nguyễn Văn A", string? email = "a@example.com", string? phone = "0912345678")
        => new(Guid.NewGuid(), ten, email, phone);

    // ── (1) TRẠNG THÁI ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tao_moi__la_Inactive__va_VAN_dung_duoc()
    {
        var k = Tao();

        // 📌 `Inactive` **KHÔNG** chặn bán: khách vãng lai là hạng nhất (`ADR-03`) — chặn ở đây là chặn
        // bán cho người chưa từng tương tác, phá luồng đặt hàng.
        Assert.Equal(CustomerStatus.Inactive, k.Status);
        Assert.True(k.DungDuoc);
    }

    [Fact]
    public void KichHoat__Inactive_sang_Active()
    {
        var k = Tao();
        k.KichHoat();

        Assert.Equal(CustomerStatus.Active, k.Status);
    }

    [Fact]
    public void KichHoat__goi_LAI_khi_da_Active__KHONG_nem__idempotent()
    {
        var k = Tao();
        k.KichHoat();
        k.KichHoat();   // retry/double-click ⇒ phải vô hại

        Assert.Equal(CustomerStatus.Active, k.Status);
    }

    [Fact]
    public void Chan__BAT_BUOC_co_ly_do()
    {
        var k = Tao();

        var loi = Assert.Throws<BusinessException>(() => k.Chan("   "));

        Assert.Equal(CustomerErrorCodes.ThieuThongTinBatBuoc, loi.Code);
        Assert.NotEqual(CustomerStatus.Blocked, k.Status);
    }

    [Fact]
    public void Chan__dat_trang_thai_Blocked__va_KHONG_con_dung_duoc()
    {
        var k = Tao();
        k.Chan("Chây nợ 2 đơn COD");

        Assert.Equal(CustomerStatus.Blocked, k.Status);
        Assert.False(k.DungDuoc);
        Assert.Contains("Chây nợ", k.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void KichHoat__KHONG_ra_duoc_khoi_Blocked__PHAI_dung_MoChan()
    {
        var k = Tao();
        k.Chan("gian lận");

        // 🚨 ĐÂY LÀ BẤT BIẾN QUAN TRỌNG NHẤT: nếu `KichHoat` ra được khỏi `Blocked` thì **lệnh chặn tự mất
        // hiệu lực** sau một lần gọi vô ý (ví dụ retry của job, hoặc UI gọi nhầm) — và **không lỗi nào**.
        var loi = Assert.Throws<BusinessException>(() => k.KichHoat());

        Assert.Equal(CustomerErrorCodes.KhachDangBiChan, loi.Code);
        Assert.Equal(CustomerStatus.Blocked, k.Status);
    }

    [Fact]
    public void MoChan__khach_KHONG_bi_chan__NEM__khong_im_lang()
    {
        var k = Tao();
        k.KichHoat();

        // Nếu im lặng cho qua: người vận hành **tin** đã gỡ chặn cho người đang bị chặn, trong khi thực tế
        // họ đang thao tác trên **bản ghi khác** ⇒ người bị chặn **vẫn bị chặn** mà không ai biết.
        var loi = Assert.Throws<BusinessException>(() => k.MoChan("gỡ nhầm"));

        Assert.Equal(CustomerErrorCodes.ChuyenTrangThaiKhongHopLe, loi.Code);
    }

    [Fact]
    public void MoChan__BAT_BUOC_co_ly_do__va_dua_ve_Active()
    {
        var k = Tao();
        k.Chan("gian lận");

        Assert.Throws<BusinessException>(() => k.MoChan(""));
        Assert.Equal(CustomerStatus.Blocked, k.Status);

        k.MoChan("khách đã thanh toán đủ, xác nhận của kế toán");
        Assert.Equal(CustomerStatus.Active, k.Status);
        Assert.True(k.DungDuoc);
    }

    // ── (2) NÂNG CẤP KHÁCH VÃNG LAI (`ADR-07`) ─────────────────────────────────────────────────

    [Fact]
    public void NangCap__khach_vang_lai__duoc__va_tu_dong_Active()
    {
        var k = Tao();
        var tk = Guid.NewGuid();

        Assert.True(k.IdentityUserId is null, "Tiền đề: khách vãng lai có IdentityUserId = null");

        k.NangCapVangLai(tk);

        Assert.Equal(tk, k.IdentityUserId);
        Assert.Equal(CustomerStatus.Active, k.Status);
    }

    [Fact]
    public void NangCap__goi_LAI_cung_tai_khoan__idempotent__KHONG_nem()
    {
        var k = Tao();
        var tk = Guid.NewGuid();
        k.NangCapVangLai(tk);

        // 🔒 Nếu ca này NÉM thì consumer (queue/webhook) sẽ **retry vô hạn** — vì "đã đúng trạng thái"
        // bị báo thành lỗi.
        k.NangCapVangLai(tk);

        Assert.Equal(tk, k.IdentityUserId);
    }

    [Fact]
    public void NangCap__khach_DA_co_chu_KHAC__NEM__vi_do_la_DOI_CHU_SO_HUU()
    {
        var k = Tao();
        k.NangCapVangLai(Guid.NewGuid());

        var loi = Assert.Throws<BusinessException>(() => k.NangCapVangLai(Guid.NewGuid()));

        // 🚨 Gán đè = **lấy tài khoản của người khác** (hoặc ghi đè chủ cũ ⇒ **mất lịch sử của họ**).
        Assert.Equal(CustomerErrorCodes.KhachDaCoChu, loi.Code);
    }

    [Fact]
    public void NangCap__khach_dang_BI_CHAN__NEM__khong_cho_duong_vong()
    {
        var k = Tao();
        k.Chan("gian lận COD");

        var loi = Assert.Throws<BusinessException>(() => k.NangCapVangLai(Guid.NewGuid()));

        // 📌 Nếu cho qua: khách chây nợ **hợp thức hoá** bằng cách tạo tài khoản mới ⇒ **đường vòng qua lệnh chặn**.
        Assert.Equal(CustomerErrorCodes.KhachDangBiChan, loi.Code);
        Assert.True(k.IdentityUserId is null);
    }

    [Fact]
    public void NangCap__tai_khoan_rong__NEM()
    {
        var k = Tao();

        Assert.Throws<BusinessException>(() => k.NangCapVangLai(Guid.Empty));
    }

    // ── (3) LIÊN HỆ (PII + định dạng) ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("khong-co-a-mien")]
    [InlineData("thieu@mien")]
    [InlineData("@example.com")]
    [InlineData("a b@example.com")]
    public void Email_SAI_dinh_dang__NEM(string email)
    {
        var loi = Assert.Throws<BusinessException>(() => new Customer(Guid.NewGuid(), "A", email, null));

        Assert.Equal(CustomerErrorCodes.LienHeKhongHopLe, loi.Code);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0912")]
    [InlineData("0912-345-678")]
    public void Dien_thoai_SAI_dinh_dang__NEM(string phone)
    {
        var loi = Assert.Throws<BusinessException>(() => new Customer(Guid.NewGuid(), "A", null, phone));

        Assert.Equal(CustomerErrorCodes.LienHeKhongHopLe, loi.Code);
    }

    [Fact]
    public void Email_duoc_CHUAN_HOA_chu_thuong__de_chong_trung_KHONG_phan_biet_hoa_thuong()
    {
        var k = new Customer(Guid.NewGuid(), "A", "A@Example.COM", null);

        // 🔑 Nếu không chuẩn hoá: `A@Example.com` và `a@example.com` là **hai khách** cho **một người**,
        // và unique index (so khớp `Ordinal`) **không** bắt được.
        Assert.Equal("a@example.com", k.Email);
    }

    [Fact]
    public void Khong_co_lien_he__van_tao_duoc__vi_khach_vang_lai_la_hang_nhat()
    {
        var k = new Customer(Guid.NewGuid(), "Khách lẻ");

        Assert.Null(k.Email);
        Assert.Null(k.Phone);
        Assert.True(k.DungDuoc);
    }

    [Fact]
    public void Ten_rong__NEM()
    {
        Assert.Throws<BusinessException>(() => new Customer(Guid.NewGuid(), "  "));
    }

    // ── (4) KHOÁ IDEMPOTENCY (`ADR-05`) ────────────────────────────────────────────────────────

    [Fact]
    public void DatKhoaIdempotency__KHONG_ghi_de_khoa_da_co()
    {
        var k = Tao();
        k.DatKhoaIdempotency("shopee:order:12345");
        k.DatKhoaIdempotency("shopee:order:99999");

        // 🔒 Khoá cũ là **bằng chứng** của lần tạo đầu; ghi đè ⇒ **mất khả năng chống trùng** cho các retry
        // đang bay (chúng mang khoá cũ ⇒ sẽ **tạo khách thứ hai**).
        Assert.Equal("shopee:order:12345", k.IdempotencyKey);
    }

    // ── (5) 🚨 BẤT BIẾN `ADR-01`: Customer KHÔNG có cột địa chỉ ────────────────────────────────

    [Fact]
    public void TC14_Customer_KHONG_duoc_co_cot_DIA_CHI__chong_mo_hinh_hai_nguon_su_that()
    {
        // 🚨 **VÌ SAO TEST NÀY TỒN TẠI:** mô hình *"hai nguồn sự thật về địa chỉ"* (kiểu nopCommerce:
        // `Customer.StreetAddress…` **+** entity `Address` **+** bảng liên kết) đã bị ghi nhận là
        // **anti-pattern phải tránh** của dự án (`docs/business-modules-proposal.md`).
        // ⇒ `Customer` **KHÔNG** được có cột `Street`/`Ward`/`Province`/`Address`/`District` nào.
        // 📌 Đây là **bất biến bằng reflection** — không phải "nhắc nhau trong review".
        var tenCotCam = new[] { "Street", "Ward", "Province", "District", "Address", "AddressLine", "PostalCode", "FullAddress" };

        var coCot = typeof(Customer)
            .GetProperties()
            .Where(p => tenCotCam.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        Assert.True(
            coCot.Count == 0,
            "🚨 `Customer` có cột ĐỊA CHỈ ⇒ mô hình HAI NGUỒN SỰ THẬT đã quay lại (ADR-01 cấm):\n  "
            + string.Join("\n  ", coCot)
            + "\n⇒ Địa chỉ thuộc `AddressHub`; `Customer` chỉ giữ LIÊN KẾT (sổ địa chỉ).");
    }
}

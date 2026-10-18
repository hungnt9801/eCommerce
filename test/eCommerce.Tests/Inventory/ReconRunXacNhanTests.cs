using eCommerce.Inventory;
using eCommerce.Reconciliation;
using eCommerce.Settings;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🆕 `S7` — **XÁC NHẬN LƯỢT ĐỐI SOÁT** (`REC-001` · quyết định `Q5` = **A**: đóng cảnh báo + audit,
/// **KHÔNG** sửa tồn/sổ cái).
///
/// **VÌ SAO PHẢI KIỂM Ở TẦNG ENTITY:** trước `S7` **KHÔNG có đường xác nhận nào** ⇒ lượt `VuotCanXacNhan`
/// nằm ở trạng thái `2` **vĩnh viễn** (cảnh báo tồn đọng). Một hàm xác nhận sai sẽ tạo ra loại hỏng
/// **tệ hơn** hiện trạng: cảnh báo **biến mất** khỏi danh sách cần xem trong khi **chưa ai xem**
/// — và **không có lỗi nào** để lần ra.
///
/// 📌 Mọi ca dưới đây **không cần DB** (hàm entity thuần) ⇒ chạy trong bước unit test của cổng.
/// </summary>
public class ReconRunXacNhanTests
{
    private static readonly DateTime T0 = new(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = T0.AddSeconds(2);
    private static readonly DateTime LucXacNhan = new(2026, 10, 18, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid NguoiDung = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>Lượt **đang chờ xác nhận**: `RequireConfirmation` + có lệch vượt ngưỡng.</summary>
    private static ReconRun ChoXacNhan(int soLech = 5, int soVuot = 2)
    {
        var luot = new ReconRun(Guid.NewGuid(), T0, T1, 10, DonViNguong.Percent, 0.5m, HanhDongLech.RequireConfirmation, true);
        luot.ChotKetQua(soLech, soVuot);
        return luot;
    }

    /// <summary>Lượt **không cần xác nhận**: chính sách `Warn` ⇒ chỉ cảnh báo.</summary>
    private static ReconRun VuotCanhBao()
    {
        var luot = new ReconRun(Guid.NewGuid(), T0, T1, 10, DonViNguong.Percent, 0.5m, HanhDongLech.Warn, true);
        luot.ChotKetQua(5, 2);
        return luot;
    }

    [Fact]
    public void Luot_RequireConfirmation_co_lech_vuot__dang_CHO_XAC_NHAN()
    {
        var luot = ChoXacNhan();

        Assert.Equal(KetLuanDoiSoat.VuotCanXacNhan, luot.KetLuan);
        Assert.True(luot.ChoXacNhan);
        Assert.Null(luot.ConfirmedAtUtc);
    }

    [Fact]
    public void XacNhan__ghi_DU_ba_truong_audit__va_KHONG_dung_toi_so_do()
    {
        var luot = ChoXacNhan(soLech: 5, soVuot: 2);

        luot.XacNhan(NguoiDung, LucXacNhan, "  Đã kiểm kê kho Hà Nội, khớp lại sổ.  ");

        Assert.Equal(NguoiDung, luot.ConfirmedByUserId);
        Assert.Equal(LucXacNhan, luot.ConfirmedAtUtc);

        // 🔒 Cắt khoảng trắng: ghi chú là **bằng chứng**, không phải chuỗi người dùng gõ.
        Assert.Equal("Đã kiểm kê kho Hà Nội, khớp lại sổ.", luot.ConfirmNote);

        // 🚨 BẤT BIẾN TRUNG TÂM CỦA `Q5`: xác nhận **KHÔNG** đổi số đo và **KHÔNG** đổi kết luận.
        Assert.Equal(5, luot.SoLech);
        Assert.Equal(2, luot.SoLechVuotNguong);
        Assert.Equal(KetLuanDoiSoat.VuotCanXacNhan, luot.KetLuan);
    }

    [Fact]
    public void Sau_khi_xac_nhan__KHONG_con_CHO_XAC_NHAN_nhung_VAN_la_Da_xac_nhan()
    {
        var luot = ChoXacNhan();

        luot.XacNhan(NguoiDung, LucXacNhan, "kiểm kê khớp");

        // 📌 Hai cờ này KHÁC nghĩa: `ChoXacNhan = false` (không còn việc) và `ConfirmedAtUtc != null` (bằng chứng).
        // Nếu gộp một cờ thì màn hình **không phân biệt** *"đã đóng"* với *"không cần đóng"*.
        Assert.False(luot.ChoXacNhan);
        Assert.NotNull(luot.ConfirmedAtUtc);
    }

    [Fact]
    public void XacNhan_HAI_LAN__la_NO_OP__khong_ghi_de_bang_chung_lan_dau()
    {
        var luot = ChoXacNhan();

        luot.XacNhan(NguoiDung, LucXacNhan, "lần đầu");
        luot.XacNhan(Guid.NewGuid(), LucXacNhan.AddHours(1), "lần hai (không được ghi đè)");

        Assert.Equal(NguoiDung, luot.ConfirmedByUserId);
        Assert.Equal(LucXacNhan, luot.ConfirmedAtUtc);
        Assert.Equal("lần đầu", luot.ConfirmNote);
    }

    [Fact]
    public void XacNhan_lai_KHONG_mang_ghi_chu__KHONG_nem__vi_do_la_retry()
    {
        var luot = ChoXacNhan();
        luot.XacNhan(NguoiDung, LucXacNhan, "lần đầu");

        // 🚨 Giống hệt bài học `ReconRunMismatch.DanhDauDaXuLy`: nếu kiểm tham số TRƯỚC khi kiểm no-op thì
        // một **retry/double-click không mang ghi chú** sẽ NÉM trên một lượt **đã xong** ⇒ consumer retry vô hạn.
        var loi = Record.Exception(() => luot.XacNhan(NguoiDung, LucXacNhan, string.Empty));

        Assert.Null(loi);
        Assert.Equal("lần đầu", luot.ConfirmNote);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void XacNhan_THIEU_GHI_CHU__NEM_0005__va_KHONG_ghi_gi(string? ghiChu)
    {
        var luot = ChoXacNhan();

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => luot.XacNhan(NguoiDung, LucXacNhan, ghiChu!));

        Assert.Equal(ReconErrorCodes.ThieuGhiChuXacNhan, ex.Code);

        // 🔒 Exception ném **TRƯỚC** mọi phép gán ⇒ entity không bị "nửa xác nhận".
        Assert.Null(luot.ConfirmedAtUtc);
        Assert.Null(luot.ConfirmedByUserId);
        Assert.Null(luot.ConfirmNote);
        Assert.True(luot.ChoXacNhan);
    }

    [Fact]
    public void XacNhan_luot_VuotCanhBao__NEM_0004__vi_khong_co_canh_bao_nao_de_dong()
    {
        var luot = VuotCanhBao();

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => luot.XacNhan(NguoiDung, LucXacNhan, "ghi chú"));

        Assert.Equal(ReconErrorCodes.LuotKhongChoXacNhan, ex.Code);
        Assert.Null(luot.ConfirmedAtUtc);
    }

    [Fact]
    public void XacNhan_luot_KHONG_vuot_nguong__NEM_0004()
    {
        var luot = new ReconRun(Guid.NewGuid(), T0, T1, 10, DonViNguong.Percent, 0.5m, HanhDongLech.RequireConfirmation, true);
        luot.ChotKetQua(soLech: 3, soLechVuotNguong: 0);

        Assert.Equal(KetLuanDoiSoat.KhongVuot, luot.KetLuan);
        Assert.Throws<Volo.Abp.BusinessException>(() => luot.XacNhan(NguoiDung, LucXacNhan, "ghi chú"));
    }
}

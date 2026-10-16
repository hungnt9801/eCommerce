using eCommerce.Inventory;
using eCommerce.Settings;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🚨 **NGƯỠNG LỆCH ĐỐI SOÁT** (`REC-001` v2) — hàm **THUẦN**, chỗ biến *"ô chọn ngưỡng"*
/// thành **hành vi thật**.
///
/// **VÌ SAO CẦN:** trước v2, job đọc `Recon.Threshold.*` rồi **chỉ ghi log** ⇒ lệch `1` và lệch `10 000`
/// **giống hệt nhau**; người vận hành đặt `0.5%` và **tin** mình chỉ bị báo khi lệch đáng kể.
/// Sai ở hàm này ⇒ **báo động giả** (mất tin vào cảnh báo) hoặc **bỏ sót lệch lớn**.
///
/// 🔒 **Luật kiểm:**
///   (1) `Absolute`: vượt khi `|lệch| > giá trị` — **BẰNG thì KHÔNG vượt** (ngưỡng `5` nghĩa *"5 vẫn chấp nhận được"*);
///   (2) `Percent`: mẫu số là **`TongSoCai`** (nguồn sự thật), **KHÔNG** phải `OnHand` (giá trị đang nghi ngờ);
///   (3) `TongSoCai = 0` mà có lệch ⇒ **vượt** (không có cơ sở để tính tỷ lệ; đây là ca lệch nặng nhất);
///   (4) `giá trị <= 0` ⇒ **mọi** lệch đều vượt (hành vi cũ, fail-closed) — `0` **KHÔNG** có nghĩa "bỏ qua mọi lệch";
///   (5) **dấu** của lệch không ảnh hưởng (lệch âm nặng bằng lệch dương).
/// </summary>
public class NguongLechTests
{
    // ── (1) TUYỆT ĐỐI ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5, 6, true)]     // 6 > 5   ⇒ vượt
    [InlineData(5, 5, false)]    // 5 = 5   ⇒ KHÔNG vượt (biên: người vận hành đã tuyên bố 5 là chấp nhận được)
    [InlineData(5, 4, false)]
    [InlineData(5, 0, false)]
    [InlineData(5, -6, true)]    // dấu không quan trọng
    public void Absolute__vuot_khi_DO_LON_lon_hon_nguong(int nguong, int chenhLech, bool mongDoi)
    {
        Assert.Equal(mongDoi, NguongLech.VuotNguong(DonViNguong.Absolute, nguong, tongSoCai: 100, chenhLech));
    }

    // ── (2) PHẦN TRĂM — mẫu số là SỔ CÁI ─────────────────────────────────────────────────────────

    [Fact]
    public void Percent__mau_so_la_TONG_SO_CAI__khong_phai_OnHand()
    {
        // Sổ cái = 100, lệch = 1 ⇒ 1% > 0,5% ⇒ VƯỢT.
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 0.5m, tongSoCai: 100, chenhLech: 1));

        // Cùng độ lệch nhưng sổ cái = 1000 ⇒ 0,1% < 0,5% ⇒ KHÔNG vượt.
        Assert.False(NguongLech.VuotNguong(DonViNguong.Percent, 0.5m, tongSoCai: 1000, chenhLech: 1));
    }

    [Fact]
    public void Percent__neu_lay_OnHand_lam_mau_so_thi_nguong_tu_triet_tieu__test_nay_khoa_lai_su_khac_biet()
    {
        // `TongSoCai = 0` (sổ cái KHÔNG có gì) mà `OnHand = 1000` ⇒ lệch 1000.
        // Nếu lấy mẫu số là `|OnHand|` thì tỷ lệ LUÔN = 100% ở ca này ⇒ mọi ngưỡng < 100% đều "vượt",
        // nhưng với `OnHand = 5` và ngưỡng 0,5% thì tỷ lệ vẫn 100% ⇒ ngưỡng KHÔNG phân biệt được gì.
        // ⇒ Chọn `TongSoCai` làm cơ sở, VÀ quy định rõ ca `TongSoCai = 0` (bên dưới) là VƯỢT.
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 0.5m, tongSoCai: 0, chenhLech: 1000));
    }

    [Fact]
    public void Percent__khong_dung_so_thuc__khong_mat_chinh_xac()
    {
        // 33,33% của 3 = 0,9999 ⇒ lệch 1 > 0,9999 ⇒ VƯỢT.
        // 📌 Nếu tính bằng `int` hoặc làm tròn SỚM (`floor`) thì 0,9999 → 0 ⇒ ca này **lọt** ⇒ bỏ sót lệch.
        // Đây là lý do hàm nhân `100` hai vế (so `decimal >`) thay vì chia rồi làm tròn.
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 33.33m, tongSoCai: 3, chenhLech: 1));
    }

    [Fact]
    public void Percent__bien__bang_dung_nguong_thi_KHONG_vuot()
    {
        // 10% của 100 = 10; lệch ĐÚNG 10 ⇒ KHÔNG vượt (> chứ không >=).
        Assert.False(NguongLech.VuotNguong(DonViNguong.Percent, 10m, tongSoCai: 100, chenhLech: 10));
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 10m, tongSoCai: 100, chenhLech: 11));
    }

    [Fact]
    public void Percent__co_so_AM_van_tinh_dung__dung_DO_LON_lam_mau_so()
    {
        // Sổ cái âm là dữ liệu bất thường (đã có CHECK riêng), nhưng hàm **không** được ném/misbehave:
        // dùng |cơ sở| để phép so vẫn có nghĩa, thay vì đảo dấu bất ngờ.
        Assert.False(NguongLech.VuotNguong(DonViNguong.Percent, 50m, tongSoCai: -10, chenhLech: 1));
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 50m, tongSoCai: -10, chenhLech: 6));
    }

    // ── (3) `TongSoCai = 0` ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Percent__so_cai_BANG_0_va_CO_lech__la_VUOT__khong_the_tinh_ty_le()
    {
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 0.5m, tongSoCai: 0, chenhLech: 1));
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, 999m, tongSoCai: 0, chenhLech: -5));
    }

    // ── (4) NGƯỠNG `0` = "KHÔNG CÓ NGƯỠNG", KHÔNG phải "bỏ qua mọi lệch" ───────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nguong_khong_duong__MOI_lech_deu_VUOT__khong_bao_gio_im_lang(int nguong)
    {
        Assert.True(NguongLech.VuotNguong(DonViNguong.Absolute, nguong, 100, chenhLech: 1));
        Assert.True(NguongLech.VuotNguong(DonViNguong.Percent, nguong, 100, chenhLech: -1));
    }

    [Fact]
    public void Nguong_khong_duong_va_KHONG_co_lech__thi_khong_vuot()
    {
        Assert.False(NguongLech.VuotNguong(DonViNguong.Absolute, 0m, 100, chenhLech: 0));
        Assert.False(NguongLech.VuotNguong(DonViNguong.Percent, 0m, 0, chenhLech: 0));
    }

    // ── (5) Trả về KẾT QUẢ (không chỉ bool) để chỗ gọi ghi được lý do ─────────────────────────────

    [Fact]
    public void Xet__tra_ve_KET_QUA_de_ghi_duoc_ly_do()
    {
        Assert.Equal(NguongLech.KetQua.VuotNguong, NguongLech.Xet(DonViNguong.Absolute, 1m, 100, 2));
        Assert.Equal(NguongLech.KetQua.TrongNguong, NguongLech.Xet(DonViNguong.Absolute, 1m, 100, 1));
    }
}

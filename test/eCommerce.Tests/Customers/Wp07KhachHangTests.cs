using eCommerce.Customers;
using Volo.Abp;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🚨 **`WP-07` — ĐIỂM · TIỀN TÍCH LUỸ · CÔNG NỢ · CẤP KHÁCH · TAB PHÂN KHÚC** (`07 §WP-07`).
///
/// **VÌ SAO BỘ TEST NÀY LÀ CHỖ QUAN TRỌNG NHẤT CỦA `WP-07`:** ba con số `Points`/`AccumulatedAmount`/
/// `DebtBalance` là **số dư cộng dồn**. Sai một phép cộng là **số dư của khách sai vĩnh viễn**, và
/// **không có lỗi nào nổi lên**: không ai biết cho tới khi khách khiếu nại hoặc kế toán đối chiếu.
/// ⇒ Mọi luật được kiểm **ở đây**, trên hàm thuần/entity, **không cần DB**.
///
/// 🔒 **Luật kiểm:**
///   (1) điểm: cộng/trừ đúng; **âm ⇒ NÉM `0018`** (tiêu điểm chưa kiếm được = khoản nợ ẩn);
///   (2) công nợ: `DebtLimit = 0` = **KHÔNG đặt** hạn mức (không chặn) — khác *"hạn mức 0 đồng"*;
///       vượt ⇒ **NÉM `0019`**;
///   (3) cấp khách: chọn ngưỡng **cao nhất mà ≤ tiền tích luỹ**; hai cấp trùng ngưỡng ⇒ chọn **tất định**
///       (`SortOrder` rồi `Id`) — nếu không, *"bao nhiêu khách VIP"* **nhảy số** mà không có giao dịch nào;
///   (4) **đảo** (huỷ/trả) **kẹp ở 0**, không ném — huỷ là đường **sửa sai**, không được làm hỏng thêm;
///   (5) khách **BỊ CHẶN** không ghi nhận giao dịch (kể cả POS — đường đó **không** đi qua `CuaKiemKhachGiaoDich`);
///   (6) 5 tab phân khúc là **dữ liệu** (`CustomerTabs`) và khoá lạ ⇒ **`null`** (chỗ gọi phải NÉM).
/// </summary>
public class Wp07KhachHangTests
{
    private static Customer Tao(decimal diem = 0m, decimal tichLuy = 0m, decimal hanMucNo = 0m,
        CustomerStatus trangThai = CustomerStatus.Active)
    {
        var k = new Customer(Guid.NewGuid(), "Nguyễn Văn A", "a@example.com", "0912345678");

        k.KichHoat();

        // 📌 Dùng đúng đường ghi của sản phẩm (không `set` trực tiếp) để test kiểm luôn các guard.
        if (diem > 0m || tichLuy > 0m)
        {
            k.GhiNhanMuaHang(tichLuy, diemTich: diem, diemSuDung: 0m, noTang: 0m);
        }

        if (hanMucNo > 0m)
        {
            k.DatPhanKhach(CustomerType.Retail, null, hanMucNo);
        }

        if (trangThai == CustomerStatus.Blocked)
        {
            k.Chan("chây nợ");
        }

        return k;
    }

    // ── (1) ĐIỂM ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 10, 0, 10)]
    [InlineData(100, 10, 30, 80)]
    [InlineData(5, 0, 5, 0)]
    public void DiemSauGiaoDich_cong_TRU_dung(decimal hienTai, decimal tich, decimal dung, decimal mongDoi)
    {
        Assert.Equal(mongDoi, NghiepVuKhach.DiemSauGiaoDich(hienTai, tich, dung));
    }

    [Fact]
    public void DiemSauGiaoDich_AM__NEM_0018__khong_cho_tieu_diem_chua_kiem_duoc()
    {
        var loi = Assert.Throws<BusinessException>(() => NghiepVuKhach.DiemSauGiaoDich(5m, 0m, 6m));

        Assert.Equal(CustomerErrorCodes.DiemKhongDu, loi.Code);
        Assert.Equal(5m, (decimal)loi.Data["diemHienTai"]!);
        Assert.Equal(6m, (decimal)loi.Data["diemSuDung"]!);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void DiemSauGiaoDich_so_AM__NEM_0017(decimal tich, decimal dung)
    {
        Assert.Equal(CustomerErrorCodes.DiemKhongHopLe,
            Assert.Throws<BusinessException>(() => NghiepVuKhach.DiemSauGiaoDich(10m, tich, dung)).Code);
    }

    // ── (2) CÔNG NỢ + HẠN MỨC ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoSauGiaoDich_han_muc_0__la_KHONG_DAT__khong_chan()
    {
        // 📌 Ca quan trọng: `DebtLimit == 0` = chưa đặt hạn mức. Nếu hiểu là "hạn mức 0 đồng" thì **mọi**
        // khách bị chặn nợ ngay sau khi nâng cấp — và không ai hiểu vì sao.
        Assert.Equal(500_000m, NghiepVuKhach.NoSauGiaoDich(0m, 500_000m, hanMucNo: 0m));
    }

    [Fact]
    public void NoSauGiaoDich_VUOT_han_muc__NEM_0019()
    {
        var loi = Assert.Throws<BusinessException>(() => NghiepVuKhach.NoSauGiaoDich(90_000m, 20_000m, 100_000m));

        Assert.Equal(CustomerErrorCodes.VuotHanMucNo, loi.Code);
        Assert.Equal(110_000m, (decimal)loi.Data["noMoi"]!);
        Assert.Equal(100_000m, (decimal)loi.Data["hanMucNo"]!);
    }

    [Fact]
    public void NoSauGiaoDich_bang_DUNG_han_muc__duoc_phep()
    {
        Assert.Equal(100_000m, NghiepVuKhach.NoSauGiaoDich(90_000m, 10_000m, 100_000m));
    }

    [Fact]
    public void NoSauGiaoDich_so_AM__NEM_0020()
    {
        Assert.Equal(CustomerErrorCodes.SoTienKhongHopLe,
            Assert.Throws<BusinessException>(() => NghiepVuKhach.NoSauGiaoDich(10m, -1m, 0m)).Code);
    }

    // ── (3) CHỌN CẤP KHÁCH ──────────────────────────────────────────────────────────────────────

    private static NghiepVuKhach.NguongCapKhach Cap(Guid id, decimal nguong, int thuTu = 0)
        => new(id, nguong, thuTu);

    [Fact]
    public void ChonCapKhach_lay_nguong_CAO_NHAT_ma_KHONG_VUOT_tien_tich_luy()
    {
        var bac = Guid.NewGuid();
        var vang = Guid.NewGuid();
        var kimCuong = Guid.NewGuid();

        var ds = new[] { Cap(bac, 1_000_000m), Cap(vang, 5_000_000m), Cap(kimCuong, 20_000_000m) };

        Assert.Equal(bac, NghiepVuKhach.ChonCapKhach(ds, 1_000_000m));          // đúng ngưỡng ⇒ đạt
        Assert.Equal(vang, NghiepVuKhach.ChonCapKhach(ds, 19_999_999m));
        Assert.Equal(kimCuong, NghiepVuKhach.ChonCapKhach(ds, 20_000_000m));
    }

    [Fact]
    public void ChonCapKhach_DUOI_nguong_thap_nhat__tra_NULL__khong_phai_cap_mac_dinh()
    {
        var ds = new[] { Cap(Guid.NewGuid(), 1_000_000m) };

        Assert.Null(NghiepVuKhach.ChonCapKhach(ds, 999_999m));
        Assert.Null(NghiepVuKhach.ChonCapKhach([], 10_000_000m));
    }

    [Fact]
    public void ChonCapKhach_TRUNG_nguong__chon_TAT_DINH_theo_SortOrder_roi_Id()
    {
        var a = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // Cùng ngưỡng 5 triệu: `SortOrder` 1 thắng 2 ⇒ luôn ra `a` (không phụ thuộc thứ tự DB trả).
        var ds = new[] { Cap(b, 5_000_000m, 2), Cap(a, 5_000_000m, 1) };

        Assert.Equal(a, NghiepVuKhach.ChonCapKhach(ds, 5_000_000m));
        Assert.Equal(a, NghiepVuKhach.ChonCapKhach(ds.Reverse().ToArray(), 5_000_000m));
    }

    // ── (4) GHI NHẬN / ĐẢO TRÊN ENTITY ──────────────────────────────────────────────────────────

    [Fact]
    public void GhiNhanMuaHang_cong_dung_ca_ba_so_du()
    {
        var k = Tao();

        k.GhiNhanMuaHang(tienHang: 150_000m, diemTich: 15m, diemSuDung: 3m, noTang: 50_000m);

        Assert.Equal(12m, k.Points);                       // 15 tích − 3 tiêu
        Assert.Equal(150_000m, k.AccumulatedAmount);
        Assert.Equal(50_000m, k.DebtBalance);
    }

    [Fact]
    public void GhiNhanMuaHang_khach_BI_CHAN__NEM_0005__ke_ca_duong_POS()
    {
        var k = Tao(trangThai: CustomerStatus.Blocked);

        Assert.Equal(CustomerErrorCodes.KhachDangBiChan,
            Assert.Throws<BusinessException>(() => k.GhiNhanMuaHang(100_000m, 0m, 0m, 0m)).Code);
    }

    [Fact]
    public void GhiNhanMuaHang_VUOT_han_muc__NEM_0019__va_KHONG_doi_so_du()
    {
        var k = Tao(hanMucNo: 100_000m);

        Assert.Equal(CustomerErrorCodes.VuotHanMucNo,
            Assert.Throws<BusinessException>(() => k.GhiNhanMuaHang(0m, 0m, 0m, 150_000m)).Code);

        // 🚨 BẤT BIẾN: ném ⇒ **không** được để lại số dư dở dang (UoW sẽ rollback, nhưng entity phải sạch).
        Assert.Equal(0m, k.DebtBalance);
    }

    [Fact]
    public void DaoGhiNhanMuaHang_tra_lai_dung_va_kep_o_0()
    {
        var k = Tao();
        k.GhiNhanMuaHang(150_000m, 15m, 3m, 50_000m);

        var dao = k.DaoGhiNhanMuaHang(tienHang: 150_000m, diemTich: 15m, diemSuDung: 3m, noGiam: 50_000m);

        Assert.Equal(0m, k.Points);
        Assert.Equal(0m, k.AccumulatedAmount);
        Assert.Equal(0m, k.DebtBalance);
        Assert.Equal(12m, dao.DiemDaDao);
        Assert.Equal(150_000m, dao.TienDaDao);
        Assert.Equal(50_000m, dao.NoDaGiam);

        // 📌 Đảo LẦN HAI (dữ liệu đã lệch) ⇒ **kẹp 0**, KHÔNG ném: huỷ là đường sửa sai.
        var lan2 = k.DaoGhiNhanMuaHang(150_000m, 15m, 3m, 50_000m);
        Assert.Equal(0m, lan2.TienDaDao);
        Assert.Equal(0m, k.AccumulatedAmount);
    }

    [Fact]
    public void Tra_hang_chi_giam_phan_hang_tra__khong_phai_toan_bo_hoa_don()
    {
        var k = Tao();
        k.GhiNhanMuaHang(1_000_000m, 100m, 0m, 0m);

        k.DaoGhiNhanMuaHang(tienHang: 200_000m, diemTich: 20m, diemSuDung: 0m, noGiam: 0m);

        Assert.Equal(800_000m, k.AccumulatedAmount);
        Assert.Equal(80m, k.Points);
    }

    // ── (5) PHÂN KHÁCH (loại/cấp/hạn mức/MST/ngày sinh) ────────────────────────────────────────

    [Fact]
    public void DatPhanKhach_luu_dung_gia_tri()
    {
        var k = Tao();
        var cap = Guid.NewGuid();
        var nv = Guid.NewGuid();

        k.DatPhanKhach(CustomerType.Wholesale, cap, 5_000_000m, nv, null, "0101234567-001",
            new DateTime(1990, 5, 20));

        Assert.Equal(CustomerType.Wholesale, k.Type);
        Assert.Equal(cap, k.LevelId);
        Assert.Equal(5_000_000m, k.DebtLimit);
        Assert.Equal(nv, k.SaleStaffId);
        Assert.Equal("0101234567-001", k.TaxCode);
        Assert.Equal(new DateTime(1990, 5, 20), k.Birthday);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("0101234567-01")]
    [InlineData("12345678901234")]
    public void DatPhanKhach_MST_sai_dinh_dang__NEM__vi_MST_di_vao_hoa_don_dien_tu(string mst)
    {
        var k = Tao();

        Assert.Throws<BusinessException>(() =>
            k.DatPhanKhach(CustomerType.Agency, null, 0m, maSoThue: mst));
    }

    [Fact]
    public void DatPhanKhach_han_muc_AM__NEM_0020()
    {
        Assert.Equal(CustomerErrorCodes.SoTienKhongHopLe,
            Assert.Throws<BusinessException>(() => Tao().DatPhanKhach(CustomerType.Retail, null, -1m)).Code);
    }

    [Fact]
    public void DatPhanKhach_tu_gioi_thieu_chinh_minh__NEM()
    {
        var k = Tao();

        Assert.Throws<BusinessException>(() =>
            k.DatPhanKhach(CustomerType.Retail, null, 0m, nguoiGioiThieu: k.Id));
    }

    [Fact]
    public void DatPhanKhach_ngay_sinh_TUONG_LAI__NEM()
    {
        Assert.Throws<BusinessException>(() =>
            Tao().DatPhanKhach(CustomerType.Retail, null, 0m, sinhNhat: DateTime.UtcNow.Date.AddDays(1)));
    }

    // ── (6) CẤP KHÁCH / CHĂM SÓC / TAB ─────────────────────────────────────────────────────────

    [Fact]
    public void CustomerLevel_chiet_khau_ngoai_0_100__NEM()
    {
        Assert.Throws<BusinessException>(() => new CustomerLevel(Guid.NewGuid(), "VIP", "VIP", 0m, 120m));
        Assert.Throws<BusinessException>(() => new CustomerLevel(Guid.NewGuid(), "VIP", "VIP", 0m, -1m));
    }

    [Fact]
    public void CustomerLevel_nguong_AM__NEM()
    {
        Assert.Throws<BusinessException>(() => new CustomerLevel(Guid.NewGuid(), "VIP", "VIP", -1m));
    }

    [Fact]
    public void CustomerCare_noi_dung_rong__NEM__bien_ban_khong_noi_dung_thi_khong_phai_bien_ban()
    {
        Assert.Throws<BusinessException>(() =>
            new CustomerCare(Guid.NewGuid(), Guid.NewGuid(), CustomerCareType.Call, "   ", DateTime.UtcNow));
    }

    [Fact]
    public void CustomerTabs_DUNG_5_tab_va_khoa_la__tra_NULL()
    {
        Assert.Equal(5, CustomerTabs.DanhSach.Count);
        Assert.Equal(new[] { "all", "retail", "wholesale", "agency", "debt" },
            CustomerTabs.DanhSach.Select(x => x.Key).ToArray());
        Assert.Null(CustomerTabs.Tim("khong-co-tab-nay"));
    }

    [Fact]
    public void CustomerTabs_loc_DUNG_theo_loai_va_con_no()
    {
        var le = Tao();
        var buon = Tao();
        buon.DatPhanKhach(CustomerType.Wholesale, null, 0m);

        var no = Tao();
        no.GhiNhanMuaHang(100_000m, 0m, 0m, 50_000m);

        var retail = CustomerTabs.Tim("retail")!.Loc.Compile();
        var wholesale = CustomerTabs.Tim("wholesale")!.Loc.Compile();
        var debt = CustomerTabs.Tim("debt")!.Loc.Compile();

        Assert.True(retail(le));
        Assert.False(retail(buon));
        Assert.True(wholesale(buon));
        Assert.True(debt(no));
        Assert.False(debt(le), "Tab 'Còn nợ' là DƯ NỢ > 0 — không phải 'có hạn mức nợ'.");
    }

    // ── (7) HỢP ĐỒNG SỐ CỦA ENUM (append-only) ────────────────────────────────────────────────

    [Fact]
    public void Enum_PhamViDuLieu_giu_DUNG_so__vi_phep_gop_la_MIN()
    {
        // 🚨 Nếu ai đó đổi số (ví dụ `Own = 0`), phép gộp `Min` trong `DataScopeResolver` sẽ **nới quyền**
        // cho người chỉ được cấp `Own` — lỗi bảo mật **im lặng**. Test này khoá hợp đồng đó (D-17).
        Assert.Equal(0, (int)PhamViDuLieu.All);
        Assert.Equal(1, (int)PhamViDuLieu.Store);
        Assert.Equal(2, (int)PhamViDuLieu.Own);

        Assert.Equal(PhamViDuLieu.All, new[] { PhamViDuLieu.Own, PhamViDuLieu.All }.Min());
    }

    [Fact]
    public void Enum_CustomerType_va_CustomerCareType_giu_DUNG_so()
    {
        Assert.Equal(0, (int)CustomerType.Retail);
        Assert.Equal(1, (int)CustomerType.Wholesale);
        Assert.Equal(2, (int)CustomerType.Agency);

        // 📌 Đúng 5 loại chăm sóc như kế hoạch — không tự chế thêm (D-20/D-38).
        Assert.Equal(5, Enum.GetValues<CustomerCareType>().Length);
        Assert.Equal(0, (int)CustomerCareType.Call);
        Assert.Equal(4, (int)CustomerCareType.Other);
    }
}

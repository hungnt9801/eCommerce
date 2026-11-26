using eCommerce.Inventory;
using eCommerce.Kernel.Observability;
using eCommerce.Orders;
using eCommerce.Pos;
using eCommerce.Reporting;
using eCommerce.Tests.Reporting.HaTang;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Tests.Reporting;

/// <summary>
/// **TẦNG APP SERVICE — VẬN HÀNH `WP-26`** (`REP-001` §6 / §14.1).
///
/// 🎯 Dựng `ReportAdminAppService` · `ReportAppService` · `TongHopBaoCaoService` bằng `new` + repository giả
/// in-memory (`D-229`/`D-248`), KHÔNG host ABP, KHÔNG PostgreSQL. Mục đích: chứng minh **hành vi** các cổng
/// chặn của `WP-26` — không chỉ hàm thuần:
/// <list type="bullet">
///   <item>**TC-11** idempotency rebuild: gọi 2 lần cùng khoảng + cùng lý do ⇒ **1** `RepRunLog`, lần 2
///     `AlreadyRequested` (BR-005/§7.3).</item>
///   <item>**TC-17** "chưa tổng hợp" ≠ 0: read-model rỗng ⇒ `DaTongHop=false`/`DaCoDuLieu=false` (BR-014).</item>
///   <item>**TC-19** chốt kỳ khi còn dòng lệch mở ⇒ `0014`, kỳ **không** sang `DaChot` (BR-012).</item>
///   <item>**TC-22** `StartRebuild`: khoảng đảo ⇒ `0002`; lý do 9 ký tự ⇒ `0016`; `TonKho` quá khứ ⇒ `0013`;
///     đúng 10 ký tự + hôm nay ⇒ nhận (BR-011, `D-272`/`D-274`).</item>
///   <item>**TC-04** khoá `report:tong-hop` đang bận ⇒ NÉM `KhongGianhDuocKhoa` (BR-007).</item>
///   <item>**BR-006** mỗi nguồn có dòng mốc sau lượt chạy + mốc tiến tới hôm nay.</item>
///   <item>**TC-03** chạy lại 1.000 lần cùng ngày ⇒ số dòng/số liệu không đổi (BR-005/NFR-005).</item>
/// </list>
/// 📌 Không đánh dấu phủ các ca cần **worker/DB thật** (`DoWorkAsync`, UoW commit theo cửa sổ, cách ly tenant
/// runtime) — ghi "chưa kiểm được" trong testplan.
/// </summary>
public class RepVanHanhAppServiceTests
{
    private static readonly DateOnly HomNay = new(2026, 9, 15);
    private static readonly DateTime Luc = new(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly ReportingOptions TuyChon = new();

    private static string LyDoDu = new('a', 10);

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // Bộ dựng
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private sealed class BoVanHanh
    {
        public RepoBaoCaoGia<RepRunLog> LanChay { get; } = new();
        public RepoBaoCaoGia<RepPeriodClose> KyChot { get; } = new();
        public RepoBaoCaoGia<RepConsistencyCheck> Lech { get; } = new();
        public RepoBaoCaoGia<RptAggDailySales> Agg { get; } = new();
        public RepoBaoCaoGia<RptFactSalesLine> Fact { get; } = new();
        public RepoBaoCaoGia<RptFactInventorySnapshot> AnhTonKho { get; } = new();
        public RepoBaoCaoGia<InventoryItem> TonKho { get; } = new();
        public RepoBaoCaoGia<RptWatermark> Moc { get; } = new();
        public RepoBaoCaoGia<RptDimDate> DimNgay { get; } = new();
        public RepoBaoCaoGia<RptDimProduct> DimSanPham { get; } = new();
        public RepoBaoCaoGia<RptDimStore> DimKho { get; } = new();
        public RepoBaoCaoGia<RptDimStaff> DimNhanVien { get; } = new();
        public RepoBaoCaoGia<RetailBill> HoaDon { get; } = new();
        public RepoBaoCaoGia<RetailBillLine> DongHoaDon { get; } = new();
        public RepoBaoCaoGia<Order> DonHang { get; } = new();
        public RepoBaoCaoGia<OrderLine> DongDonHang { get; } = new();
        public RepoBaoCaoGia<Warehouse> Kho { get; } = new();

        public KhoaBaoCaoGia Khoa { get; } = new();
        public DongHoBaoCaoGia DongHo { get; } = new(Luc);
        public TenantBaoCaoGia Tenant { get; } = new();
        public NguoiDungBaoCaoGia NguoiDung { get; } = new();
        public eCommerceMetrics Metrics { get; } = new();

        public ReportAdminAppService Admin { get; }
        public TongHopBaoCaoService TongHop { get; }
        public ReportAppService Doc { get; }

        public BoVanHanh()
        {
            TongHop = new TongHopBaoCaoService(
                HoaDon, DongHoaDon, DonHang, DongDonHang, TonKho, Kho,
                Fact, Agg, AnhTonKho, DimNgay, DimSanPham, DimKho, DimNhanVien, Moc,
                Khoa, Options.Create(TuyChon), DongHo, new GuidBaoCaoGia(), KyChot);

            Admin = HaTangBaoCao.Noi(
                new ReportAdminAppService(
                    LanChay, KyChot, Lech, Agg, Fact, AnhTonKho, TonKho, TongHop,
                    Khoa, DongHo, Tenant, new GuidBaoCaoGia(), Options.Create(TuyChon), Metrics),
                DongHo, NguoiDung, Tenant);

            Doc = HaTangBaoCao.Noi(
                new ReportAppService(
                    Agg, AnhTonKho, Moc, DonHang, DongDonHang,
                    tongHop: null!, Options.Create(TuyChon)),
                DongHo, NguoiDung, Tenant);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-11 / BR-005 — idempotency rebuild
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC11_StartRebuild_HAI_lan_cung_khoang_cung_ly_do__MOT_dong_lan_hai_AlreadyRequested()
    {
        var bo = new BoVanHanh();
        var input = new StartRebuildInput
        {
            FromDate = HomNay.AddDays(-2), ToDate = HomNay, Projection = RepProjection.BanHang, Reason = LyDoDu
        };

        var lan1 = await bo.Admin.StartRebuildAsync(input);
        var lan2 = await bo.Admin.StartRebuildAsync(input);

        Assert.False(lan1.AlreadyRequested);
        Assert.True(lan2.AlreadyRequested);
        Assert.Equal(lan1.Id, lan2.Id);
        Assert.Equal(1, bo.LanChay.SoLanChen);
        Assert.Single(bo.LanChay.TatCa);
    }

    [Fact]
    public async Task BR011_StartRebuild_hop_le__ghi_MOT_dong_Queued_va_so_cua_so_bang_so_ngay()
    {
        var bo = new BoVanHanh();

        var dto = await bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay.AddDays(-2), ToDate = HomNay, Projection = RepProjection.BanHang, Reason = "  " + LyDoDu + "  "
        });

        var run = Assert.Single(bo.LanChay.TatCa);
        Assert.Equal(RepRunStatus.Queued, run.Status);
        Assert.Equal(3, run.WindowsTotal);
        Assert.Equal(LyDoDu, run.Reason);                  // Trim trước khi lưu
        Assert.Equal(bo.NguoiDung.Id, run.RequestedBy);    // audit: ai yêu cầu
        Assert.Equal(run.Id, dto.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-22 / BR-011 — cổng chặn StartRebuild
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC22_StartRebuild_khoang_DAO_nem_0002_va_KHONG_chen()
    {
        var bo = new BoVanHanh();

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay, ToDate = HomNay.AddDays(-1), Projection = RepProjection.BanHang, Reason = LyDoDu
        }));

        Assert.Equal(ReportingErrorCodes.KhoangKhongHopLe, loi.Code);
        Assert.Equal(0, bo.LanChay.SoLanChen);
    }

    [Fact]
    public async Task TC22_StartRebuild_ly_do_DUNG_9_ky_tu_nem_0016_va_KHONG_chen()
    {
        var bo = new BoVanHanh();

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay, ToDate = HomNay, Projection = RepProjection.BanHang, Reason = new string('a', 9)
        }));

        Assert.Equal(ReportingErrorCodes.ThieuLyDo, loi.Code);
        Assert.Equal(0, bo.LanChay.SoLanChen);
    }

    [Fact]
    public async Task TC22_StartRebuild_TonKho_ngay_QUA_KHU_nem_0013_va_KHONG_chen()
    {
        var bo = new BoVanHanh();

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay.AddDays(-1), ToDate = HomNay.AddDays(-1), Projection = RepProjection.TonKho, Reason = LyDoDu
        }));

        Assert.Equal(ReportingErrorCodes.KhongHoTroRebuild, loi.Code);
        Assert.Equal(0, bo.LanChay.SoLanChen);
    }

    [Fact]
    public async Task TC22_StartRebuild_TonKho_HOM_NAY__nhan_va_MOT_cua_so()
    {
        var bo = new BoVanHanh();

        await bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay, ToDate = HomNay, Projection = RepProjection.TonKho, Reason = LyDoDu
        });

        var run = Assert.Single(bo.LanChay.TatCa);
        Assert.Equal(RepProjection.TonKho, run.Projection);
        Assert.Equal(1, run.WindowsTotal);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-17 / BR-014 — "chưa tổng hợp" ≠ 0
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC17_doanh_thu_read_model_RONG__DaTongHop_false_va_KHONG_co_ngay_moc()
    {
        var bo = new BoVanHanh();

        var dto = await bo.Doc.GetDoanhThuAsync(new DoanhThuInput
        {
            TuNgay = HomNay.AddDays(-1), DenNgay = HomNay
        });

        // 📌 0 đồng nhưng `DaTongHop = false`: UI phải hiện "chưa tổng hợp", KHÔNG hiện "0 đồng".
        Assert.False(dto.DaTongHop);
        Assert.Null(dto.NgayTongHopMoiNhat);
        Assert.Equal(0m, dto.TongDoanhThu);
    }

    [Fact]
    public async Task TC17_ton_kho_read_model_RONG__DaCoDuLieu_false()
    {
        var bo = new BoVanHanh();

        var dto = await bo.Doc.GetTongQuanTonKhoAsync(new TongQuanTonKhoInput());

        Assert.False(dto.DaCoDuLieu);
        Assert.Null(dto.NgayChup);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-19 / BR-012 — chốt kỳ khi còn lệch mở ⇒ 0014, kỳ KHÔNG sang DaChot
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC19_chot_ky_con_dong_lech_MO__nem_0014_va_ky_giu_Mo()
    {
        var bo = new BoVanHanh();

        bo.Lech.Them(RepConsistencyCheck.GhiNhan(
            Guid.NewGuid(), HomNay, RepProjection.BanHang, RepMismatchKind.DoanhThu,
            "ban-hang:2026-09-15", 100_000m, 99_000m, Luc));

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.Admin.ClosePeriodAsync(new ClosePeriodInput
        {
            FromDate = HomNay, ToDate = HomNay, Reason = LyDoDu
        }));

        Assert.Equal(ReportingErrorCodes.ConLechChuaXuLy, loi.Code);

        var ky = Assert.Single(bo.KyChot.TatCa);
        Assert.Equal(RepPeriodStatus.Mo, ky.Status);   // chưa chốt
        Assert.Null(ky.SnapshotHash);
    }

    [Fact]
    public async Task TC19_chot_ky_KHONG_con_lech__ky_sang_DaChot_va_co_hash_64_hex()
    {
        var bo = new BoVanHanh();

        var dto = await bo.Admin.ClosePeriodAsync(new ClosePeriodInput
        {
            FromDate = HomNay, ToDate = HomNay, Reason = LyDoDu
        });

        Assert.Equal(RepPeriodStatus.DaChot, dto.Status);
        Assert.NotNull(dto.SnapshotHash);
        Assert.Matches("^[0-9a-f]{64}$", dto.SnapshotHash!);
        Assert.Equal(1, bo.KyChot.SoLanChen);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-04 / BR-007 — khoá `report:tong-hop` đang bận ⇒ NÉM KhongGianhDuocKhoa
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC04_tong_hop_khi_KHONG_giu_duoc_khoa__nem_KhongGianhDuocKhoa()
    {
        var bo = new BoVanHanh();
        bo.Khoa.ChoGiu = false;

        var loi = await Assert.ThrowsAsync<BusinessException>(
            () => bo.TongHop.ChayAsync(null, null, ghiAnhTonKho: false));

        Assert.Equal(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa, loi.Code);
        Assert.Equal(TongHopBaoCaoService.TenKhoaPhanTan, bo.Khoa.KhoaCuoi);
        Assert.Equal("report:tong-hop", bo.Khoa.KhoaCuoi);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // BR-006 — mỗi nguồn có dòng mốc; mốc tiến tới ngày kinh doanh hôm nay
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BR006_chay_tong_hop_tao_du_BA_dong_moc_va_moc_doanh_thu_TIEN_toi_hom_nay()
    {
        var bo = new BoVanHanh();

        var ketQua = await bo.TongHop.ChayAsync(null, null, ghiAnhTonKho: false);

        // 📌 `NapMocAsync` bảo đảm MỌI nguồn có dòng mốc (3 dòng) — mốc `null` = chưa từng chạy.
        Assert.Equal(3, bo.Moc.TatCa.Count);

        var banLe = Assert.Single(bo.Moc.TatCa, x => x.Nguon == NguonBaoCao.BanLe);
        var donHang = Assert.Single(bo.Moc.TatCa, x => x.Nguon == NguonBaoCao.DonHang);
        var tonKho = Assert.Single(bo.Moc.TatCa, x => x.Nguon == NguonBaoCao.TonKho);

        Assert.Equal(HomNay, banLe.LastAggregatedDate);
        Assert.Equal(HomNay, donHang.LastAggregatedDate);
        Assert.Null(tonKho.LastAggregatedDate);        // không chụp ảnh tồn kho ⇒ mốc tồn kho KHÔNG tiến
        Assert.False(ketQua.Rong);
        Assert.Equal(0, ketQua.SoDongSuKien);          // nguồn rỗng ⇒ 0 dòng, nhưng lượt VẪN chạy
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-03 / BR-005 / NFR-005 — chạy lại 1.000 lần cùng ngày KHÔNG nhân đôi
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC03_chay_lai_1000_lan_cung_ngay__so_dong_va_so_lieu_KHONG_doi()
    {
        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.BanLe, Guid.NewGuid(), "HD-1000", HomNay, Luc, null, null, null, 50_000m);

        var ketQua = TongHopDoanhThu.TinhNgay(
            new[] { chungTu },
            new[]
            {
                new DongBanHangNguon(
                    Guid.NewGuid(), chungTu.ChungTuId, "SP-1", null, 2m, 100_000m, 0m, 200_000m, null, 0m)
            });

        var dong = (IReadOnlyList<DongSuKienBanHang>)Array.Empty<DongSuKienBanHang>();
        var tong = (IReadOnlyList<DongTongHopNgay>)Array.Empty<DongTongHopNgay>();

        for (var i = 0; i < 1_000; i++)
        {
            dong = TongHopDoanhThu.GopTheoNgay(dong, ketQua.Dong);
            tong = TongHopDoanhThu.GopTongHopTheoNgay(tong, ketQua.TongHop);
        }

        Assert.Equal(ketQua.Dong.Count, dong.Count);
        Assert.Equal(ketQua.TongHop.Count, tong.Count);
        Assert.Equal(150_000m, tong.Sum(x => x.DoanhThu));   // 200.000 − 50.000
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // P1 REV vòng 2 — CỔNG `0011` Ở **ĐƯỜNG GHI** (`TongHopBaoCaoService`), không chỉ fail-sớm ở app service
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private static RepPeriodClose TaoKyDaChot(DateOnly from, DateOnly to)
    {
        var ky = new RepPeriodClose(Guid.NewGuid(), from, to, "Lý do đóng kỳ đủ dài", "ky-" + Guid.NewGuid());
        ky.BatDauChot(Guid.NewGuid(), "Lý do đóng kỳ đủ dài");
        ky.Chot(new string('a', 64), Guid.NewGuid(), Luc);
        return ky;
    }

    [Fact]
    public async Task BR011_ChayKhoangAsync_ngay_nam_trong_ky_DaChot__nem_0011_va_KHONG_ghi_gi()
    {
        var bo = new BoVanHanh();
        bo.KyChot.Them(TaoKyDaChot(HomNay, HomNay));

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.TongHop.ChayKhoangAsync(HomNay));

        Assert.Equal(ReportingErrorCodes.KyDaChot, loi.Code);

        // 🚨 Bất biến tài chính: cổng kiểm TRƯỚC `DELETE` ⇒ 0 lời gọi xoá, 0 dòng bị xoá/ghi.
        Assert.Equal(0, bo.Fact.SoLanGoiXoa);
        Assert.Equal(0, bo.Agg.SoLanGoiXoa);
        Assert.Equal(0, bo.Fact.SoLanXoa);
        Assert.Equal(0, bo.Agg.SoLanXoa);
        Assert.Equal(0, bo.Fact.SoLanChen);
        Assert.Equal(0, bo.Agg.SoLanChen);
    }

    [Fact]
    public async Task BR011_ChayAsync_gap_ngay_trong_ky_DaChot__nem_0011()
    {
        var bo = new BoVanHanh();
        bo.KyChot.Them(TaoKyDaChot(HomNay, HomNay));   // kỳ chốt phủ hôm nay = ngày cuối của khoảng

        var loi = await Assert.ThrowsAsync<BusinessException>(
            () => bo.TongHop.ChayAsync(null, null, ghiAnhTonKho: false));

        Assert.Equal(ReportingErrorCodes.KyDaChot, loi.Code);
    }

    [Fact]
    public async Task BR011_ky_DaChot_KHONG_phu_ngay__ChayKhoangAsync_chay_duoc()
    {
        var bo = new BoVanHanh();
        bo.KyChot.Them(TaoKyDaChot(HomNay.AddDays(-40), HomNay.AddDays(-30)));   // không phủ hôm nay

        var soDong = await bo.TongHop.ChayKhoangAsync(HomNay);

        Assert.Equal(0, soDong);
        // 📌 Vẫn chạy lượt thay thế: gọi xoá fact + agg của ngày (2 lời gọi), rồi không chèn (nguồn rỗng).
        Assert.Equal(1, bo.Fact.SoLanGoiXoa);
        Assert.Equal(1, bo.Agg.SoLanGoiXoa);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // P2 #1 REV vòng 2 — đã có lượt `Rebuild` Queued cùng projection, khoảng/lý do KHÁC ⇒ AlreadyRequested
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BR005_StartRebuild_khi_da_co_luot_Queued_cung_projection__AlreadyRequested_khong_chen()
    {
        var bo = new BoVanHanh();

        var dangChay = new RepRunLog(
            Guid.NewGuid(), RepRunLog.TenJob.Rebuild, RepProjection.BanHang,
            HomNay.AddDays(-5), HomNay.AddDays(-3), 3, "Lý do rebuild đủ dài", null, "idem-dang-chay");

        bo.LanChay.Them(dangChay);
        Assert.Equal(RepRunStatus.Queued, dangChay.Status);

        var dto = await bo.Admin.StartRebuildAsync(new StartRebuildInput
        {
            FromDate = HomNay.AddDays(-2), ToDate = HomNay, Projection = RepProjection.BanHang,
            Reason = "Lý do KHÁC đủ dài"
        });

        // 🚨 Trước `UX_RepRunLogs_..._DangChay`: nếu cứ chèn thì `23505` nổi thành 500. Phải trả lượt đang chạy.
        Assert.True(dto.AlreadyRequested);
        Assert.Equal(dangChay.Id, dto.Id);
        Assert.Equal(0, bo.LanChay.SoLanChen);
        Assert.Single(bo.LanChay.TatCa);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // P2 #2 REV vòng 2 — đối chiếu `TonKho` phát ĐÚNG `MismatchKind.AnhTonKho` (3) khi số ảnh lệch
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BR012_doi_chieu_TonKho_lech_so_anh__phat_MismatchKind_AnhTonKho()
    {
        var bo = new BoVanHanh();

        // Nguồn: 1 dòng tồn kho. Read-model: 0 ảnh của hôm nay ⇒ lệch 1.
        bo.TonKho.Them(new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-ANH"));

        var ketQua = await bo.Admin.RunReconciliationAsync(new RunReconciliationInput
        {
            FromDate = HomNay, ToDate = HomNay, Projection = RepProjection.TonKho
        });

        Assert.Equal(1, ketQua.SoDongLechMoi);

        var lech = Assert.Single(bo.Lech.TatCa);
        Assert.Equal(RepMismatchKind.AnhTonKho, lech.MismatchKind);
        Assert.Equal(HomNay, lech.FactDate);
        Assert.Equal(RepProjection.TonKho, lech.Projection);
        Assert.Equal(1m, lech.ExpectedValue);
        Assert.Equal(0m, lech.ActualValue);
        Assert.Equal(-1m, lech.Delta);
    }
}

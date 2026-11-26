using System.Reflection;
using eCommerce.HttpApi.Controllers;
using eCommerce.Kernel.Observability;
using eCommerce.Reporting;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;

namespace eCommerce.Tests.Reporting;

/// <summary>
/// `WP-26` — **VẬN HÀNH GĐ3**: domain thuần (đối chiếu/băm/khoảng), máy trạng thái, mã lỗi, idempotency,
/// reflection enum, bất biến 0 `double/float`, khoá phân tán, metric, và metadata endpoint.
/// 📌 Tất cả **không cần DB** (khuôn `Wp08BaoCaoTests`).
/// </summary>
public class RepVanHanhTests
{
    private static readonly DateOnly Ngay = new(2026, 9, 15);
    private static readonly DateTime Luc = new(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);

    private static RepRunLog TaoRun(
        DateOnly from, DateOnly to, int windows,
        string job = RepRunLog.TenJob.Rebuild,
        RepProjection projection = RepProjection.BanHang)
        => new(Guid.NewGuid(), job, projection, from, to, windows,
            "Lý do rebuild đủ dài", null, "idem-" + Guid.NewGuid());

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 1. ENUM — đúng tập + đúng thứ tự số (append-only)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Enum_RepProjection_dung_tap_va_thu_tu()
    {
        Assert.Equal(new[] { "BanHang", "TonKho" }, Enum.GetNames<RepProjection>());
        Assert.Equal(new short[] { 0, 1 }, Enum.GetValues<RepProjection>().Select(x => (short)x).ToArray());
    }

    [Fact]
    public void Enum_RepRunStatus_dung_tap_va_thu_tu()
    {
        Assert.Equal(
            new[] { "Queued", "Running", "Succeeded", "PartiallySucceeded", "Failed", "Skipped" },
            Enum.GetNames<RepRunStatus>());
        Assert.Equal(
            new short[] { 0, 1, 2, 3, 4, 5 },
            Enum.GetValues<RepRunStatus>().Select(x => (short)x).ToArray());
    }

    [Fact]
    public void Enum_RepPeriodStatus_dung_tap_va_thu_tu__DaChot_bang_2()
    {
        Assert.Equal(new[] { "Mo", "DangChot", "DaChot", "DaMoLai" }, Enum.GetNames<RepPeriodStatus>());
        Assert.Equal(2, (int)RepPeriodStatus.DaChot);
    }

    [Fact]
    public void Enum_RepMismatchKind_dung_tap_va_thu_tu()
    {
        Assert.Equal(
            new[] { "SoDong", "DoanhThu", "SoChungTu", "AnhTonKho", "SuKienTre" },
            Enum.GetNames<RepMismatchKind>());
        Assert.Equal(new short[] { 0, 1, 2, 3, 4 }, Enum.GetValues<RepMismatchKind>().Select(x => (short)x).ToArray());
    }

    [Fact]
    public void ReportingDomain_KHONG_co_kieu_double_float()
    {
        var asm = typeof(RepRunLog).Assembly;
        var xau = new List<string>();

        foreach (var t in asm.GetTypes().Where(t => t.Namespace is not null && t.Namespace.StartsWith("eCommerce.Reporting")))
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType == typeof(double) || p.PropertyType == typeof(float))
                {
                    xau.Add($"{t.Name}.{p.Name}");
                }
            }

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(double) || f.FieldType == typeof(float))
                {
                    xau.Add($"{t.Name}.{f.Name}");
                }
            }
        }

        Assert.Empty(xau);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 2. KHOÁ PHÂN TÁN — KHÔNG tiền tố `ecommerce:`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReportingLockKeys_KHONG_tien_to_ecommerce()
    {
        Assert.Equal("report:tong-hop", ReportingLockKeys.TongHop);
        Assert.Equal("report:rebuild:host:ban-hang", ReportingLockKeys.Rebuild(null, RepProjection.BanHang));
        Assert.Equal("report:rebuild:host:ton-kho", ReportingLockKeys.Rebuild(null, RepProjection.TonKho));
        Assert.StartsWith("report:period-close:", ReportingLockKeys.PeriodClose(null));
        Assert.StartsWith("report:reconciliation:", ReportingLockKeys.Reconciliation(Guid.NewGuid()));

        var khoa = new[]
        {
            ReportingLockKeys.TongHop,
            ReportingLockKeys.Rebuild(null, RepProjection.BanHang),
            ReportingLockKeys.PeriodClose(Guid.NewGuid()),
            ReportingLockKeys.Reconciliation(Guid.NewGuid())
        };

        Assert.All(khoa, k => Assert.False(k.StartsWith("ecommerce:", StringComparison.Ordinal)));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 3. SO ĐỐI CHIẾU — so tiền bằng Delta, không `==`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SoDoiChieu_khop_tra_RONG()
    {
        var lech = SoDoiChieu.KiemNgay(
            RepProjection.BanHang, Ngay, 100_000m, 100_000m, 3, 3, 2, 2);

        Assert.Empty(lech);
    }

    [Fact]
    public void SoDoiChieu_lech_doanh_thu__1_dong__Delta_dung()
    {
        var lech = SoDoiChieu.KiemNgay(
            RepProjection.BanHang, Ngay, 100_000m, 99_000m, 3, 3, 2, 2);

        var l = Assert.Single(lech);
        Assert.Equal(RepMismatchKind.DoanhThu, l.Kind);
        Assert.Equal(-1_000m, l.Delta);
        Assert.Equal(RepProjection.BanHang, l.Projection);
    }

    [Fact]
    public void SoDoiChieu_lech_so_dong_va_so_chung_tu()
    {
        var lech = SoDoiChieu.KiemNgay(
            RepProjection.BanHang, Ngay, 0m, 0m, 3, 2, 2, 5);

        Assert.Contains(lech, x => x.Kind == RepMismatchKind.SoDong);
        Assert.Contains(lech, x => x.Kind == RepMismatchKind.SoChungTu);
        Assert.DoesNotContain(lech, x => x.Kind == RepMismatchKind.DoanhThu);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 4. BĂM KỲ CHỐT — tất định, 64 hex thường
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private static List<DongKy> DongKyMau() => new()
    {
        new DongKy(Ngay, NguonBaoCao.BanLe, 2, 3, 1.5m, 100_000m),
        new DongKy(Ngay, NguonBaoCao.DonHang, 1, 2, 0.5m, 50_000m)
    };

    [Fact]
    public void BamKyChot_tat_dinh_100_lan__64_hex_thuong()
    {
        var dong = DongKyMau();
        var h = BamKyChot.Bam(dong, 5);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(h, BamKyChot.Bam(dong, 5));
        }

        Assert.Equal(64, h.Length);
        Assert.Matches("^[0-9a-f]{64}$", h);
    }

    [Fact]
    public void BamKyChot_doi_so_lieu_thi_KHAC()
    {
        var a = BamKyChot.Bam(DongKyMau(), 5);

        var khac = DongKyMau();
        khac[0] = khac[0] with { DoanhThu = 100_001m };

        Assert.NotEqual(a, BamKyChot.Bam(khac, 5));
    }

    [Fact]
    public void BamKyChot_KHONG_phu_thuoc_thu_tu_input()
    {
        var thuan = DongKyMau();
        var dao = Enumerable.Reverse(thuan).ToList();

        Assert.Equal(BamKyChot.Bam(thuan, 5), BamKyChot.Bam(dao, 5));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 5. KHOẢNG CHỐT — 0002/0003/0013/0014
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void KhoangChot_hop_le()
    {
        Assert.True(KhoangChot.HopLe(Ngay, Ngay.AddDays(10)));
        Assert.True(KhoangChot.HopLe(Ngay, Ngay));
    }

    [Fact]
    public void KhoangChot_dao_ngay_nem_0002()
    {
        var loi = Assert.Throws<BusinessException>(() => KhoangChot.Kiem(Ngay.AddDays(1), Ngay));
        Assert.Equal(ReportingErrorCodes.KhoangKhongHopLe, loi.Code);
    }

    [Fact]
    public void KhoangChot_qua_366_ngay_nem_0003()
    {
        var loi = Assert.Throws<BusinessException>(() => KhoangChot.Kiem(Ngay, Ngay.AddDays(400)));
        Assert.Equal(ReportingErrorCodes.KhoangQuaDai, loi.Code);
    }

    [Fact]
    public void KiemRebuildTonKho_qua_khu_nem_0013__hom_nay_OK()
    {
        KhoangChot.KiemRebuildTonKho(RepProjection.TonKho, Ngay, Ngay, Ngay);

        var loi = Assert.Throws<BusinessException>(() =>
            KhoangChot.KiemRebuildTonKho(RepProjection.TonKho, Ngay.AddDays(-1), Ngay.AddDays(-1), Ngay));

        Assert.Equal(ReportingErrorCodes.KhongHoTroRebuild, loi.Code);

        KhoangChot.KiemRebuildTonKho(RepProjection.BanHang, Ngay.AddDays(-30), Ngay.AddDays(-1), Ngay);
    }

    [Fact]
    public void KiemConLechChuaXuLy_nem_0014()
    {
        KhoangChot.KiemConLechChuaXuLy(0);

        var loi = Assert.Throws<BusinessException>(() => KhoangChot.KiemConLechChuaXuLy(1));
        Assert.Equal(ReportingErrorCodes.ConLechChuaXuLy, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 6. MÁY TRẠNG THÁI RepRunLog — mọi nhánh sai NÉM 0015; 0016/0017
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private static void Nem0015(Action viec)
        => Assert.Equal(ReportingErrorCodes.ChuyenTrangThaiKhongHopLe, Assert.Throws<BusinessException>(viec).Code);

    [Fact]
    public void RunLog_duong_hop_le_Queued_Running_Succeeded()
    {
        var to = Ngay.AddDays(2);
        var run = TaoRun(Ngay, to, 3);

        run.BatDau(Luc, "host");

        run.GhiCuaSo(Ngay, 10);
        run.GhiCuaSo(Ngay.AddDays(1), 20);
        run.GhiCuaSo(to, 30);
        run.HoanTat(Luc);

        Assert.Equal(RepRunStatus.Succeeded, run.Status);
        Assert.Equal(3, run.WindowsDone);
        Assert.Equal(60, run.RowsWritten);
        Assert.Equal(to, run.CheckpointDate);
    }

    [Fact]
    public void RunLog_BatDau_khi_da_Running_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        run.BatDau(Luc, "host");

        Nem0015(() => run.BatDau(Luc, "host"));
    }

    [Fact]
    public void RunLog_HoanTat_khi_chua_du_cua_so_nem_0015()
    {
        var to = Ngay.AddDays(1);
        var run = TaoRun(Ngay, to, 2);
        run.BatDau(Luc, "host");
        run.GhiCuaSo(Ngay, 1);

        Nem0015(() => run.HoanTat(Luc));
    }

    [Fact]
    public void RunLog_HoanTat_khi_Queued_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        Nem0015(() => run.HoanTat(Luc));
    }

    [Fact]
    public void RunLog_GhiCuaSo_khi_chua_Running_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        Nem0015(() => run.GhiCuaSo(Ngay, 1));
    }

    [Fact]
    public void RunLog_GhiCuaSo_cua_so_dau_phai_la_FromDate_nem_0015()
    {
        var to = Ngay.AddDays(2);
        var run = TaoRun(Ngay, to, 3);
        run.BatDau(Luc, "host");

        Nem0015(() => run.GhiCuaSo(Ngay.AddDays(1), 5));
    }

    [Fact]
    public void RunLog_GhiCuaSo_phai_TIEN_khong_lui_nem_0015()
    {
        var to = Ngay.AddDays(2);
        var run = TaoRun(Ngay, to, 3);
        run.BatDau(Luc, "host");
        run.GhiCuaSo(Ngay, 1);
        run.GhiCuaSo(Ngay.AddDays(1), 5);

        Nem0015(() => run.GhiCuaSo(Ngay, 5));
    }

    [Fact]
    public void RunLog_GhiCuaSo_ngoai_khoang_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        run.BatDau(Luc, "host");

        Nem0015(() => run.GhiCuaSo(Ngay.AddDays(1), 5));
    }

    [Fact]
    public void RunLog_MotPhan_khi_da_du_cua_so_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        run.BatDau(Luc, "host");
        run.GhiCuaSo(Ngay, 1);

        Nem0015(() => run.MotPhan("x"));
    }

    [Fact]
    public void RunLog_ThatBai_sau_Succeeded_nem_0015()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        run.BatDau(Luc, "host");
        run.GhiCuaSo(Ngay, 1);
        run.HoanTat(Luc);

        Nem0015(() => run.ThatBai("x", Luc));
        Nem0015(() => run.YeuCauChayLai());
    }

    [Fact]
    public void RunLog_MotRoi_roi_chay_lai_tu_checkpoint()
    {
        var to = Ngay.AddDays(2);
        var run = TaoRun(Ngay, to, 3);
        run.BatDau(Luc, "host");
        run.GhiCuaSo(Ngay, 1);
        run.MotPhan("eCommerce:Reports:0004");

        Assert.Equal(RepRunStatus.PartiallySucceeded, run.Status);
        Assert.Equal(Ngay, run.CheckpointDate);

        run.YeuCauChayLai();
        Assert.Equal(RepRunStatus.Queued, run.Status);
    }

    [Fact]
    public void RunLog_ThatBai_roi_chay_lai_khi_checkpoint_rong_nem_0017()
    {
        var run = TaoRun(Ngay, Ngay, 1);
        run.BatDau(Luc, "host");
        run.ThatBai("eCommerce:Reports:0005", Luc);

        var loi = Assert.Throws<BusinessException>(() => run.YeuCauChayLai());
        Assert.Equal(ReportingErrorCodes.CheckpointKhongCo, loi.Code);
    }

    [Fact]
    public void RunLog_ly_do_qua_ngan_nem_0016()
    {
        var loi = Assert.Throws<BusinessException>(() => new RepRunLog(
            Guid.NewGuid(), RepRunLog.TenJob.Rebuild, RepProjection.BanHang,
            Ngay, Ngay, 1, "ngan", null, "k"));

        Assert.Equal(ReportingErrorCodes.ThieuLyDo, loi.Code);
    }

    [Fact]
    public void RunLog_khoang_dao_nem_0002()
    {
        var loi = Assert.Throws<BusinessException>(() => new RepRunLog(
            Guid.NewGuid(), RepRunLog.TenJob.Rebuild, RepProjection.BanHang,
            Ngay.AddDays(1), Ngay, 1, "Lý do đủ dài rồi", null, "k"));

        Assert.Equal(ReportingErrorCodes.KhoangKhongHopLe, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 7. RepPeriodClose — 0015/0016, tái dùng dòng khi mở lại rồi chốt lại
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private static RepPeriodClose TaoKy() => new(Guid.NewGuid(), Ngay, Ngay.AddDays(2), "Lý do đóng kỳ đủ dài", "ky-1");

    private static string Hash64 => new('a', 64);

    [Fact]
    public void KyChot_ly_do_dong_ngan_nem_0016()
    {
        var loi = Assert.Throws<BusinessException>(() => new RepPeriodClose(Guid.NewGuid(), Ngay, Ngay, "ngan", "k"));
        Assert.Equal(ReportingErrorCodes.ThieuLyDo, loi.Code);
    }

    [Fact]
    public void KyChot_khoang_dao_nem_0002()
    {
        var loi = Assert.Throws<BusinessException>(() =>
            new RepPeriodClose(Guid.NewGuid(), Ngay.AddDays(1), Ngay, "Lý do đóng kỳ đủ dài", "k"));

        Assert.Equal(ReportingErrorCodes.KhoangKhongHopLe, loi.Code);
    }

    [Fact]
    public void KyChot_BatDauChot_ly_do_ngan_nem_0016()
    {
        var ky = TaoKy();
        var loi = Assert.Throws<BusinessException>(() => ky.BatDauChot(Guid.NewGuid(), "ngan"));
        Assert.Equal(ReportingErrorCodes.ThieuLyDo, loi.Code);
    }

    [Fact]
    public void KyChot_Chot_khi_chua_DangChot_nem_0015()
    {
        var ky = TaoKy();
        Nem0015(() => ky.Chot(Hash64, Guid.NewGuid(), Luc));
    }

    [Fact]
    public void KyChot_MoLai_khi_con_Mo_nem_0015()
    {
        var ky = TaoKy();
        Nem0015(() => ky.MoLai(Guid.NewGuid(), "Lý do mở lại đủ hai mươi ký tự", Luc));
    }

    [Fact]
    public void KyChot_MoLai_ly_do_ngan_hon_20_nem_0016()
    {
        var ky = ChotXong();
        var loi = Assert.Throws<BusinessException>(() => ky.MoLai(Guid.NewGuid(), "ngan qua", Luc));
        Assert.Equal(ReportingErrorCodes.ThieuLyDo, loi.Code);
    }

    [Fact]
    public void KyChot_Chot_bad_hash_nem_0015()
    {
        var ky = TaoKy();
        ky.BatDauChot(Guid.NewGuid(), "Lý do đóng kỳ đủ dài");
        Nem0015(() => ky.Chot("khong-phai-hex-64", Guid.NewGuid(), Luc));
    }

    [Fact]
    public void KyChot_mo_roi_chot_lai_TAI_DUNG_dong_giu_audit()
    {
        var ky = ChotXong();
        var idGoc = ky.Id;
        var nguoiMo = Guid.NewGuid();

        ky.MoLai(nguoiMo, "Lý do mở lại đủ hai mươi ký tự", Luc);

        Assert.Equal(RepPeriodStatus.DaMoLai, ky.Status);
        Assert.Equal(nguoiMo, ky.ReopenedBy);

        ky.BatDauChot(Guid.NewGuid(), "Lý do đóng lại đủ dài");
        ky.Chot(new string('b', 64), Guid.NewGuid(), Luc);

        Assert.Equal(RepPeriodStatus.DaChot, ky.Status);
        Assert.Equal(idGoc, ky.Id);
        Assert.Equal(nguoiMo, ky.ReopenedBy);
        Assert.NotNull(ky.ReopenReason);
        Assert.Equal(new string('b', 64), ky.SnapshotHash);
    }

    private static RepPeriodClose ChotXong()
    {
        var ky = TaoKy();
        ky.BatDauChot(Guid.NewGuid(), "Lý do đóng kỳ đủ dài");
        ky.Chot(Hash64, Guid.NewGuid(), Luc);
        return ky;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 8. RepConsistencyCheck — một cửa, Delta nullable, xử lý idempotent
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Lech_DoanhThu_Delta_bang_Actual_tru_Expected()
    {
        var dong = RepConsistencyCheck.GhiNhan(
            Guid.NewGuid(), Ngay, RepProjection.BanHang, RepMismatchKind.DoanhThu, "0:2026-09-15",
            100_000m, 99_000m, Luc);

        Assert.Equal(-1_000m, dong.Delta);
        Assert.False(dong.AutoCorrected);
    }

    [Fact]
    public void Lech_SuKienTre_KHONG_co_so__ca_ba_NULL()
    {
        var dong = RepConsistencyCheck.GhiNhan(
            Guid.NewGuid(), Ngay, RepProjection.BanHang, RepMismatchKind.SuKienTre, "0:2026-09-15",
            100m, 200m, Luc);

        Assert.Null(dong.ExpectedValue);
        Assert.Null(dong.ActualValue);
        Assert.Null(dong.Delta);
    }

    [Fact]
    public void Lech_FactKey_rong_nem_loi()
    {
        var loi = Assert.Throws<BusinessException>(() => RepConsistencyCheck.GhiNhan(
            Guid.NewGuid(), Ngay, RepProjection.BanHang, RepMismatchKind.SoDong, "  ", 1m, 2m, Luc));

        Assert.Equal(ReportingErrorCodes.NguonKhongHopLe, loi.Code);
    }

    [Fact]
    public void Lech_DanhDauDaXuLy_idempotent()
    {
        var dong = RepConsistencyCheck.GhiNhan(
            Guid.NewGuid(), Ngay, RepProjection.BanHang, RepMismatchKind.SoDong, "0:2026-09-15",
            1m, 2m, Luc);

        var byDau = Guid.NewGuid();
        dong.DanhDauDaXuLy(byDau, Luc);
        var lucDau = dong.ResolvedAtUtc;

        dong.DanhDauDaXuLy(Guid.NewGuid(), Luc.AddDays(1));

        Assert.Equal(byDau, dong.ResolvedBy);
        Assert.Equal(lucDau, dong.ResolvedAtUtc);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 9. IDEMPOTENCY KEY — cùng input ⇒ cùng khoá; khác lý do ⇒ khác; không tiền tố
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Idempotency_rebuild_cung_input_cung_khoa__khac_ly_do_khac_khoa()
    {
        var tenant = Guid.NewGuid();
        var a = RepIdempotency.Rebuild(RepProjection.BanHang, tenant, Ngay, Ngay.AddDays(2), "  Lý do A đủ dài  ");
        var b = RepIdempotency.Rebuild(RepProjection.BanHang, tenant, Ngay, Ngay.AddDays(2), "Lý do A đủ dài");
        var c = RepIdempotency.Rebuild(RepProjection.BanHang, tenant, Ngay, Ngay.AddDays(2), "Lý do B đủ dài");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("Report:Rebuild:ban-hang:", a);
        Assert.DoesNotContain("ecommerce:", a, StringComparison.Ordinal);
    }

    [Fact]
    public void Idempotency_host_dung_chu_host_khong_chuoi_rong()
    {
        var k = RepIdempotency.PeriodClose(null, Ngay, Ngay.AddDays(2));
        Assert.Contains(":host:", k, StringComparison.Ordinal);
    }

    [Fact]
    public void Idempotency_reconcile_theo_luot__runId_khac_khac_khoa()
    {
        var r1 = RepIdempotency.Reconcile(null, RepProjection.TonKho, Guid.NewGuid());
        var r2 = RepIdempotency.Reconcile(null, RepProjection.TonKho, Guid.NewGuid());

        Assert.NotEqual(r1, r2);
        Assert.StartsWith("Report:Reconcile:host:ton-kho:", r1);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 10. METRIC — All 30 + nhãn projection
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Metric_All_co_30_ten_va_4_ten_reporting()
    {
        Assert.Equal(30, eCommerceMetricNames.All.Count);
        Assert.Equal(30, eCommerceMetricNames.All.Distinct(StringComparer.Ordinal).Count());

        Assert.Contains(eCommerceMetricNames.ReportingWatermarkLagSeconds, eCommerceMetricNames.All);
        Assert.Contains(eCommerceMetricNames.ReportingProjectionFailedTotal, eCommerceMetricNames.All);
        Assert.Contains(eCommerceMetricNames.ReportingRowsWrittenTotal, eCommerceMetricNames.All);
        Assert.Contains(eCommerceMetricNames.ReportingReplaceConflictTotal, eCommerceMetricNames.All);
    }

    [Fact]
    public void Metric_nhan_projection_duoc_phep()
    {
        Assert.Contains("projection", eCommerceMetricTags.Allowed);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 11. ENDPOINT — [Authorize] cấp class + quyền hẹp cho method GHI (metadata 401/403/2xx)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReportAdminAppService_co_Authorize_cap_CLASS_voi_quyen_DOC()
    {
        var attr = typeof(ReportAdminAppService).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(eCommerceConsts.Permissions.Reports.Default, attr!.Policy);
    }

    [Fact]
    public void ReportAdminAppService_method_GHI_co_quyen_hep()
    {
        AssertPolicyOn(typeof(ReportAdminAppService), nameof(ReportAdminAppService.StartRebuildAsync), eCommerceConsts.Permissions.Reports.Rebuild);
        AssertPolicyOn(typeof(ReportAdminAppService), nameof(ReportAdminAppService.ResumeRebuildAsync), eCommerceConsts.Permissions.Reports.Rebuild);
        AssertPolicyOn(typeof(ReportAdminAppService), nameof(ReportAdminAppService.ClosePeriodAsync), eCommerceConsts.Permissions.Reports.PeriodClose);
        AssertPolicyOn(typeof(ReportAdminAppService), nameof(ReportAdminAppService.ReopenPeriodAsync), eCommerceConsts.Permissions.Reports.PeriodReopen);
        AssertPolicyOn(typeof(ReportAdminAppService), nameof(ReportAdminAppService.RunReconciliationAsync), eCommerceConsts.Permissions.Reports.Reconcile);
    }

    [Fact]
    public void ReportsController_co_quyen_DOC_o_cap_CLASS_va_method_GHI_quyen_hep()
    {
        var classAttr = typeof(ReportsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAttr);
        Assert.Equal(eCommerceConsts.Permissions.Reports.Default, classAttr!.Policy);

        AssertPolicyOn(typeof(ReportsController), nameof(ReportsController.StartRebuildAsync), eCommerceConsts.Permissions.Reports.Rebuild);
        AssertPolicyOn(typeof(ReportsController), nameof(ReportsController.ResumeRebuildAsync), eCommerceConsts.Permissions.Reports.Rebuild);
        AssertPolicyOn(typeof(ReportsController), nameof(ReportsController.ClosePeriodAsync), eCommerceConsts.Permissions.Reports.PeriodClose);
        AssertPolicyOn(typeof(ReportsController), nameof(ReportsController.ReopenPeriodAsync), eCommerceConsts.Permissions.Reports.PeriodReopen);
        AssertPolicyOn(typeof(ReportsController), nameof(ReportsController.RunReconciliationAsync), eCommerceConsts.Permissions.Reports.Reconcile);

        AssertNoMethodPolicy(typeof(ReportsController), nameof(ReportsController.GetLanChayAsync));
        AssertNoMethodPolicy(typeof(ReportsController), nameof(ReportsController.GetLanChaysAsync));
        AssertNoMethodPolicy(typeof(ReportsController), nameof(ReportsController.GetKyChotAsync));
        AssertNoMethodPolicy(typeof(ReportsController), nameof(ReportsController.GetKyChotsAsync));
        AssertNoMethodPolicy(typeof(ReportsController), nameof(ReportsController.GetLechSoAsync));
    }

    [Fact]
    public void ReportsController_khong_co_AllowAnonymous()
    {
        var co = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);

        Assert.False(co);
    }

    [Fact]
    public void Bon_quyen_hep_khai_dung_tien_to_Reports()
    {
        Assert.Equal("eCommerce.Reports.Rebuild", eCommerceConsts.Permissions.Reports.Rebuild);
        Assert.Equal("eCommerce.Reports.PeriodClose", eCommerceConsts.Permissions.Reports.PeriodClose);
        Assert.Equal("eCommerce.Reports.PeriodReopen", eCommerceConsts.Permissions.Reports.PeriodReopen);
        Assert.Equal("eCommerce.Reports.Reconcile", eCommerceConsts.Permissions.Reports.Reconcile);
    }

    private static void AssertPolicyOn(Type loai, string ten, string policy)
    {
        var m = loai.GetMethod(ten, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(m);
        Assert.Equal(policy, m!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    private static void AssertNoMethodPolicy(Type loai, string ten)
    {
        var m = loai.GetMethod(ten, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(m);
        Assert.Null(m!.GetCustomAttribute<AuthorizeAttribute>());
    }
}

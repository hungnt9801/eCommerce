using System.Reflection;
using eCommerce.Address;
using eCommerce.Address.Carrier;
using eCommerce.Address.Sync;
using eCommerce.Tests.AddressSync.HaTang;
using Volo.Abp;
using Volo.Abp.Uow;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002` — TẦNG APPLICATION (worker)** chạy `CarrierAddressSyncWorker.DoWorkAsync` **THẬT**
/// (`D-248`, pass 3 `N80`). Bù đúng lỗ hổng của pass 2: hai bất biến quan trọng nhất của worker trước đây
/// chỉ được phủ bằng **quét mã nguồn** —
///   • **BR-014/TC-07**: mọi ghi trong **một** UoW `requiresNew` + `CompleteAsync` một lần ⇒ ghi dòng lô ném
///     thì **rollback**, **0** ánh xạ đổi (và worker **không** ném ra ngoài);
///   • **BR-011/TC-29**: adapter ném `HttpRequestException` ⇒ dòng lô `Loi`, `ErrorType` = **tên kiểu**,
///     `ErrorCode = null`, `SourceHash = null`, **không** cột nào chứa nội dung nhạy cảm (`SECRET`);
///   • **NC-4**: adapter ném `BusinessException` mã **ngoài** hàng rào ⇒ `Loi` (**không** `BiChan`);
///   • **BR-003/`D-83`/`D-90`/`D-236`**: lượt `DaApDung` 0 việc vẫn ghi **1** dòng; bận khoá / không adapter ⇒ **0** dòng.
///
/// 🎯 Dựng `CarrierAddressSyncWorker` thật bằng `new` qua lớp con phơi `DoWorkAsync` + `PeriodicBackgroundWorkerContext`
/// (ctor **public**, đã kiểm metadata ABP 10.7.0) — **KHÔNG** host ABP, **KHÔNG** PostgreSQL, **KHÔNG** sửa `modules/**`.
/// UoW giả mô hình hoá vùng chờ/`Complete`/rollback (`HaTangDiaChi.cs`) để rollback **quan sát được**.
/// </summary>
public class CarrierAddressSyncWorkerTests
{
    private const string Hang = "GHN";
    private static readonly DateTime T0 = new(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);

    private static CarrierAddressMapping AnhXa(string ma, string ten)
        => CarrierAddressMapping.TaoMoi(
            Hang, "PROVINCE", ma, ten, Guid.NewGuid(), CarrierMatchMethod.ThuCong, "api/ghn", T0);

    private static DanhMucDiaChiHang DanhMuc(IEnumerable<(string Ma, string Ten)> donVis)
        => new(
            Hang,
            donVis.Select(x => new DonViDiaChiHang(Hang, "PROVINCE", x.Ma, x.Ten)).ToList(),
            "api/ghn",
            T0);

    /// <summary>10 đơn vị (≥ `SoDonViToiThieuMacDinh` = 5) — đủ để không vướng hàng rào "quá ít".</summary>
    private static List<(string Ma, string Ten)> MuoiDonVi(string? tenP01 = null)
        => Enumerable.Range(1, 10)
            .Select(i => ($"P-{i:D2}", i == 1 && tenP01 is not null ? tenP01 : $"Tên {i:D2}"))
            .ToList();

    private static void Gieo(BoWorkerDiaChi bo, IEnumerable<(string Ma, string Ten)> ds)
    {
        foreach (var (ma, ten) in ds)
        {
            bo.AnhXa.Them(AnhXa(ma, ten));
        }
    }

    // ── TC-07 / BR-014: ghi dòng lô ném ⇒ rollback ⇒ 0 ánh xạ đổi (ca để NC-2 đỏ được) ──────────────

    [Fact]
    public async Task TC07_BR014_DaApDung_ghi_dong_lo_nem_thi_0_anh_xa_doi()
    {
        var ds = MuoiDonVi(tenP01: "Tên MỚI");
        var danhMuc = DanhMuc(ds);
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter: KhungDiaChiHangGia.Tra(Hang, danhMuc));

        // 10 ánh xạ hiện có; P-01 đổi tên ⇒ có đúng 1 việc `CapNhatTen` (1/10 ≤ 20% ⇒ không vướng 0508).
        Gieo(bo, MuoiDonVi());

        // Ghi dòng lô ném ở CẢ hai đường (lượt `DaApDung` và dòng `Loi` của `GhiLoThatBaiAsync`).
        bo.Lo.NemKhiChen = true;

        // UoW ngoài (chủ gọi) — để quan sát `requiresNew` của worker có thật sự tách biệt hay không (NC-2).
        var baoNgoai = bo.Uow.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);

        var ngoaiLe = await Record.ExceptionAsync(() => worker.ChayAsync(ctx));

        // Chủ gọi commit UoW ngoài: nếu thay đổi của worker rò rỉ sang đây thì chúng bị ghi thật.
        await baoNgoai.CompleteAsync();

        Assert.Null(ngoaiLe);                                    // worker KHÔNG ném ra ngoài
        Assert.True(bo.AnhXa.SoLanSua > 0);                      // đã có cố gắng ghi ánh xạ (không phải lượt rỗng)
        Assert.Equal(0, bo.AnhXa.SoLanApDung);                   // rollback: KHÔNG ánh xạ nào bị ghi
        Assert.Empty(bo.Lo.TatCa);                               // 0 dòng lô (chèn ném)
        Assert.Empty(bo.DongCon.TatCa);                          // 0 dòng con
    }

    // ── TC-29 / BR-011: adapter ném HttpRequestException ⇒ Loi, ErrorType = tên kiểu, KHÔNG lọt SECRET ──

    [Fact]
    public async Task TC29_BR011_adapter_nem_HttpRequestException_thi_Loi_ErrorType_va_KHONG_lo_SECRET()
    {
        var adapter = KhungDiaChiHangGia.Nem(
            Hang,
            new HttpRequestException("gọi GHN thất bại: https://api.test/danh-muc?token=SECRET — timeout"));
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter);

        await worker.ChayAsync(ctx);

        var lo = Assert.Single(bo.Lo.TatCa);
        Assert.Equal(CarrierAddressSyncOutcome.Loi, lo.Outcome);
        Assert.Null(lo.ErrorCode);
        Assert.Null(lo.SourceHash);
        Assert.Equal("System.Net.Http.HttpRequestException", lo.ErrorType);   // chỉ TÊN KIỂU
        Assert.Equal(0, lo.SoMaNhan);
        Assert.Equal(0, lo.SoCapNhatTen);
        Assert.Equal(0, lo.SoDanhDauNghiNgo);
        Assert.Equal(0, lo.SoCanNguoiAnhXa);

        var chuoi = typeof(CarrierAddressSyncRun)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(lo))
            .ToList();

        Assert.DoesNotContain(chuoi, s => s is not null && s.Contains("SECRET", StringComparison.Ordinal));

        // Không áp dụng gì: 0 ánh xạ ghi, 0 dòng con.
        Assert.Equal(0, bo.AnhXa.SoLanApDung);
        Assert.Empty(bo.DongCon.TatCa);
    }

    // ── NC-4 (arch §7.2 khuyến nghị): BusinessException mã NGOÀI hàng rào ⇒ Loi, KHÔNG BiChan ────────

    [Fact]
    public async Task NC4_adapter_nem_BusinessException_ma_ngoai_hang_rao_thi_Loi_khong_BiChan()
    {
        var adapter = KhungDiaChiHangGia.Nem(
            Hang, new BusinessException("eCommerce:Address:9999"));
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter);

        await worker.ChayAsync(ctx);

        var lo = Assert.Single(bo.Lo.TatCa);
        Assert.Equal(CarrierAddressSyncOutcome.Loi, lo.Outcome);     // KHÔNG `BiChan`
        Assert.Null(lo.ErrorCode);                                   // mã lỗi chỉ dành cho hàng rào `BiChan`
        Assert.Equal("Volo.Abp.BusinessException", lo.ErrorType);
        Assert.Null(lo.SourceHash);
        Assert.Equal(0, bo.AnhXa.SoLanApDung);
    }

    // ── BR-003 / D-83: lượt `DaApDung` 0 việc vẫn ghi ĐÚNG 1 dòng lô, 3 số đếm biến đổi = 0, 0 dòng con ──

    [Fact]
    public async Task BR003_DaApDung_0_viec_thi_dung_1_dong_lo_3_so_bien_doi_0_va_0_dong_con()
    {
        var ds = MuoiDonVi();
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter: KhungDiaChiHangGia.Tra(Hang, DanhMuc(ds)));
        Gieo(bo, ds);

        await worker.ChayAsync(ctx);

        var lo = Assert.Single(bo.Lo.TatCa);
        Assert.Equal(CarrierAddressSyncOutcome.DaApDung, lo.Outcome);
        Assert.Equal(ds.Count, lo.SoMaNhan);          // số đơn vị danh mục (không phải "số biến đổi")
        Assert.Equal(0, lo.SoCapNhatTen);
        Assert.Equal(0, lo.SoDanhDauNghiNgo);
        Assert.Equal(0, lo.SoCanNguoiAnhXa);
        Assert.Null(lo.MauCanNguoiAnhXa);             // 0 việc ⇒ `null`, không ghi `{}` (D-234)
        Assert.NotNull(lo.SourceHash);
        Assert.Empty(bo.DongCon.TatCa);               // 0 dòng con
        Assert.Equal(0, bo.AnhXa.SoLanApDung);        // 0 việc ⇒ không ghi ánh xạ
    }

    // ── BR-003 / D-90: hàng rào `LapKeHoach` ⇒ 1 dòng `BiChan`, ErrorCode = mã, ErrorType = null ─────

    [Fact]
    public async Task BR003_hang_rao_danh_muc_rong_thi_BiChan_1_dong_ErrorCode_0506()
    {
        var danhMuc = DanhMuc(Array.Empty<(string, string)>());
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter: KhungDiaChiHangGia.Tra(Hang, danhMuc));

        await worker.ChayAsync(ctx);

        var lo = Assert.Single(bo.Lo.TatCa);
        Assert.Equal(CarrierAddressSyncOutcome.BiChan, lo.Outcome);
        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangRong, lo.ErrorCode);
        Assert.Null(lo.ErrorType);
        Assert.Equal(0, bo.AnhXa.SoLanApDung);
    }

    // ── BR-003 / D-90 / D-236: bận khoá ⇒ 0 dòng; không adapter ⇒ 0 dòng ───────────────────────────

    [Fact]
    public async Task BR003_D236_ban_khoa_thi_0_dong_lo()
    {
        var ds = MuoiDonVi();
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(
            adapter: KhungDiaChiHangGia.Tra(Hang, DanhMuc(ds)),
            luonBanKhoa: true);

        await worker.ChayAsync(ctx);

        Assert.Single(bo.Khoa.TenDaXin);            // đã thử giành khoá
        Assert.Empty(bo.Lo.TatCa);
        Assert.Empty(bo.DongCon.TatCa);
    }

    [Fact]
    public async Task BR003_D236_khong_co_adapter_thi_0_dong_lo()
    {
        var (worker, ctx, bo) = HaTangWorkerDiaChi.Dung(adapter: null, carriers: Hang);

        await worker.ChayAsync(ctx);

        Assert.Empty(bo.Lo.TatCa);
        Assert.Empty(bo.DongCon.TatCa);
    }
}

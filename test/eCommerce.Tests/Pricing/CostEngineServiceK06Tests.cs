using eCommerce.Inventory;
using eCommerce.Kernel.Locking;
using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Tests.AddressSync.HaTang;
using eCommerce.Tests.Pricing.HaTang;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — CA `K-06` TẦNG APPLICATION CỦA ĐỘNG CƠ GIÁ VỐN** (`D-320` vi; `PRC-001-arch` §5.2–§5.6).
///
/// Dựng `CostEngineService` **thật** bằng `new` + hạ tầng giả (`D-229`: `RepoGia`, `SettingGia`,
/// `UowManagerGia`/`KhoaPhanTanGia` của `ADDR-001`), **KHÔNG** host ABP, **KHÔNG** PostgreSQL.
/// Phủ: `K06-04` (nhập lùi `false` ⇒ bỏ qua + đếm), `K06-05` (`true` đụng kỳ đã duyệt ⇒ NÉM `0015`),
/// `K06-08` (cùng `RunKey` xong ⇒ trả run cũ), `K06-08b` (`DangChay` ⇒ bận; `Loi` ⇒ `Attempt++`),
/// `K06-11` (setting sai ⇒ NÉM `0014`), `K06-13` (kỳ đã duyệt bất biến) và ca `0014` tiền tệ nhập khác VND.
///
/// 📌 Ca **bền vững của sổ run qua rollback UoW ngoài** (`K06-08b`) đo ở tầng PostgreSQL thật, xem
/// `PostgreSqlPricingCostEngineTests` — ở đây đo **nhánh trạng thái** trên hợp đồng đọc/ghi.
/// </summary>
public class CostEngineServiceK06Tests
{
    private static readonly DateOnly Ngay1 = new(2026, 1, 1);
    private const string RunKey = "cost:null:2026-01-01:2026-01-01:Manual";

    private static DateTime Luc(DateOnly ngay, int gio = 1)
        => new(ngay.Year, ngay.Month, ngay.Day, gio, 0, 0, DateTimeKind.Utc);

    private static CostMovementDto Nhap(
        string sku, DateOnly ngay, decimal soLuong, decimal donGia, string? tienTe = null)
        => new()
        {
            Sku = sku,
            NgayKinhDoanh = ngay,
            OccurredAtUtc = Luc(ngay),
            Loai = LoaiChuyenDongGiaVon.NhapCoGia,
            SoLuongDelta = soLuong,
            DonGiaNhap = donGia,
            TienTeNhap = tienTe,
            ReferenceLineId = Guid.NewGuid(),
        };

    private static CostPrice DaDuyet(string sku, decimal tien, DateOnly from)
    {
        var giaVon = new CostPrice(Guid.NewGuid(), sku, Money.Vnd(tien), from);
        giaVon.Duyet(Guid.NewGuid(), Luc(from, 8));

        return giaVon;
    }

    private sealed class FeedGia : ICostFeedLookup
    {
        public List<CostMovementDto> CacChuyenDong { get; } = new();

        public int SoLanGoi { get; private set; }

        public Task<List<CostMovementDto>> LayChuyenDongAsync(
            DateOnly from, DateOnly to, IReadOnlyCollection<string>? skus = null)
        {
            SoLanGoi++;

            return Task.FromResult(CacChuyenDong
                .Where(x => x.NgayKinhDoanh >= from && x.NgayKinhDoanh <= to)
                .ToList());
        }
    }

    private sealed class Bo
    {
        public RepoGia<CostEngineRun> Runs { get; } = new();
        public RepoGia<CostPrice> GiaVon { get; } = new();
        public FeedGia Feed { get; } = new();
        public SettingGia Setting { get; }
        public UowManagerGia Uow { get; } = new();
        public KhoaPhanTanGia Khoa { get; } = new();
        public CostEngineService Sut { get; }

        public Bo(
            bool choNhapLui = false,
            string phuongPhap = PricingChinhSach.PhuongPhapBinhQuanGiaQuyenDiDong,
            string theoKho = "false",
            string dongCo = "true")
        {
            Setting = new SettingGia(new Dictionary<string, string?>
            {
                [PRC001SettingTenKhoa.PhuongPhapGiaVon] = phuongPhap,
                [PRC001SettingTenKhoa.GiaVonTheoKho] = theoKho,
                [PRC001SettingTenKhoa.ChoNhapLuiNgay] = choNhapLui ? "true" : "false",
                [PRC001SettingTenKhoa.GiaVonDongCo] = dongCo,
                [PRC002SettingTenKhoa.ChinhSachLamTron] = "half-away-from-zero",
            });

            Sut = new CostEngineService(
                Runs, GiaVon, Feed, Setting, Khoa, new GuidGia(), new TenantGia(), Uow,
                NullLogger<CostEngineService>.Instance);
        }
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class TenantGia : ICurrentTenant
    {
        public bool IsAvailable => true;
        public Guid? Id => null;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => new KhongDoi();

        private sealed class KhongDoi : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    // ── K06-04: nhập lùi, ChoNhapLuiNgay = false ────────────────────────────────────────────────

    /// <summary>
    /// K06-04 — kỳ **đã duyệt** không đổi; phiếu nhập lùi bị bỏ qua cho định giá; `RowsSkippedApproved++`.
    /// </summary>
    [Fact]
    public async Task K06_04_nhap_lui__ChoNhapLuiNgay_false__bo_qua_va_dem()
    {
        var bo = new Bo(choNhapLui: false);
        var daDuyet = DaDuyet("SKU-1", 90_000m, Ngay1);
        bo.GiaVon.Them(daDuyet);
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m));

        var run = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");

        Assert.NotNull(run);
        Assert.Equal(TrangThaiChayDongCo.ThanhCong, run!.TrangThai);
        Assert.Equal(1, run.RowsSkippedApproved);
        Assert.Equal(0, run.RowsUpserted);
        Assert.Equal(90_000m, daDuyet.AmountValue); // số đã duyệt GIỮ NGUYÊN
    }

    // ── K06-05: nhập lùi, ChoNhapLuiNgay = true ⇒ NÉM 0015 ──────────────────────────────────────

    /// <summary>
    /// K06-05 — tính lại đụng kỳ **đã duyệt** mà giá trị sẽ đổi ⇒ **NÉM `0015`**; **không** ghi đè số đã duyệt.
    /// </summary>
    [Fact]
    public async Task K06_05_nhap_lui__ChoNhapLuiNgay_true__nem_0015_khong_ghi_de()
    {
        var bo = new Bo(choNhapLui: true);
        var daDuyet = DaDuyet("SKU-1", 90_000m, Ngay1);
        bo.GiaVon.Them(daDuyet);
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual"));

        Assert.Equal(PricingErrorCodes.GiaVonKhongTheTinhLai, ex.Code);
        Assert.Equal(90_000m, daDuyet.AmountValue);

        var run = Assert.Single(bo.Runs.TatCa);
        Assert.Equal(TrangThaiChayDongCo.Loi, run.TrangThai);
    }

    // ── K06-08: idempotent cùng RunKey sau khi THÀNH CÔNG ───────────────────────────────────────

    /// <summary>K06-08 — chạy hai lần cùng khoảng & trigger: lần 2 **trả run cũ**, không tính lại, không ghi thêm.</summary>
    [Fact]
    public async Task K06_08_chay_hai_lan_cung_khoa__lan_hai_tra_run_cu()
    {
        var bo = new Bo();
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m));

        var lan1 = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");
        var soGoiSauLan1 = bo.Feed.SoLanGoi;

        var lan2 = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");

        Assert.NotNull(lan1);
        Assert.NotNull(lan2);
        Assert.Equal(lan1!.Id, lan2!.Id);
        Assert.Equal(soGoiSauLan1, bo.Feed.SoLanGoi); // KHÔNG nạp lại chuyển động
        Assert.Single(bo.Runs.TatCa);
        Assert.Single(bo.GiaVon.TatCa);
        Assert.Equal(100_000m, bo.GiaVon.TatCa.Single().AmountValue);
    }

    // ── K06-08b: run trước đang DangChay / đã Loi ───────────────────────────────────────────────

    /// <summary>K06-08b — run cùng `RunKey` đang `DangChay` ⇒ **bận** (khoá), không trả run chưa xong.</summary>
    [Fact]
    public async Task K06_08b_run_DangChay__nem_ban_khong_chay_chong()
    {
        var bo = new Bo();
        bo.Runs.Them(new CostEngineRun(Guid.NewGuid(), RunKey, Ngay1, Ngay1, "Manual", Luc(Ngay1, 8)));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual"));

        Assert.Equal(LockErrorCodes.KhongGianhDuocKhoa, ex.Code);
        Assert.Equal(0, bo.Feed.SoLanGoi);
        Assert.Single(bo.Runs.TatCa);
    }

    /// <summary>K06-08b — run trước đã `Loi` ⇒ **retry attempt MỚI** trên CÙNG dòng (`Attempt++`).</summary>
    [Fact]
    public async Task K06_08b_run_Loi__retry_attempt_moi_cung_dong()
    {
        var bo = new Bo();
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m));

        var cu = new CostEngineRun(Guid.NewGuid(), RunKey, Ngay1, Ngay1, "Manual", Luc(Ngay1, 7));
        cu.ThatBai(Luc(Ngay1, 7), "lỗi lượt trước");
        bo.Runs.Them(cu);

        var run = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");

        Assert.NotNull(run);
        Assert.Equal(cu.Id, run!.Id);
        Assert.Equal(2, run.Attempt);
        Assert.Equal(TrangThaiChayDongCo.ThanhCong, run.TrangThai);
        Assert.Single(bo.Runs.TatCa);
    }

    // ── K06-11: fail-closed cấu hình (`D-177` ②; NÉM `0014`) ────────────────────────────────────

    /// <summary>K06-11 — `PhuongPhapGiaVon = FIFO` ⇒ NÉM `0014`, **không** rơi về mặc định, chưa ghi gì.</summary>
    [Fact]
    public async Task K06_11_phuong_phap_FIFO__nem_0014()
    {
        var bo = new Bo(phuongPhap: "FIFO");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual"));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
        Assert.Empty(bo.Runs.TatCa);
        Assert.Equal(0, bo.Feed.SoLanGoi);
    }

    /// <summary>K06-11 — `GiaVonTheoKho = true` ⇒ NÉM `0014` (v1 chỉ grain SKU toàn hệ thống).</summary>
    [Fact]
    public async Task K06_11_gia_von_theo_kho_true__nem_0014()
    {
        var bo = new Bo(theoKho: "true");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual"));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
        Assert.Empty(bo.Runs.TatCa);
    }

    /// <summary>Đường lùi rẻ nhất — `GiaVonDongCo = false` ⇒ trả `null`, không ghi gì.</summary>
    [Fact]
    public async Task K06_11_dong_co_tat__tra_null_khong_ghi()
    {
        var bo = new Bo(dongCo: "false");

        var run = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");

        Assert.Null(run);
        Assert.Empty(bo.Runs.TatCa);
        Assert.Equal(0, bo.Feed.SoLanGoi);
    }

    // ── Ca bổ sung: `TienTeNhap` khác VND ⇒ NÉM `0014`, KHÔNG tự quy đổi (`D-321`/`D-324` ③) ─────

    /// <summary>Đơn giá nhập ngoại tệ ⇒ NÉM `0014` (v1 chỉ VND; không tự quy đổi — `XC-001`).</summary>
    [Fact]
    public async Task TienTeNhap_khac_VND__nem_0014_khong_quy_doi()
    {
        var bo = new Bo();
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m, tienTe: "USD"));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual"));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
    }

    // ── K06-13: kỳ đã duyệt bất biến khi chạy lại (`D-177` ③) ───────────────────────────────────

    /// <summary>K06-13 — giá trị tính ra **bằng** số đã duyệt ⇒ không UPDATE, không đếm bỏ qua, số giữ nguyên.</summary>
    [Fact]
    public async Task K06_13_ky_da_duyet_bat_bien__chay_lai_khong_doi_so()
    {
        var bo = new Bo(choNhapLui: false);
        var daDuyet = DaDuyet("SKU-1", 100_000m, Ngay1);
        bo.GiaVon.Them(daDuyet);
        bo.Feed.CacChuyenDong.Add(Nhap("SKU-1", Ngay1, 10m, 100_000m));

        var run = await bo.Sut.ChayAsync(Ngay1, Ngay1, "Manual");

        Assert.NotNull(run);
        Assert.Equal(TrangThaiChayDongCo.ThanhCong, run!.TrangThai);
        Assert.Equal(0, run.RowsUpserted);
        Assert.Equal(0, run.RowsSkippedApproved);
        Assert.Equal(100_000m, daDuyet.AmountValue);
        Assert.Equal(0, bo.GiaVon.SoLanSua);
    }
}

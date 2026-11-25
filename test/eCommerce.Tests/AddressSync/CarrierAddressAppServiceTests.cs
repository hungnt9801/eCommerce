using eCommerce.Address;
using eCommerce.Address.Carrier;
using eCommerce.Tests.AddressSync.HaTang;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002` BR-002 + BR-005 + `D-88`/`D-236`** — tầng **APP SERVICE** của
/// `CarrierAddressMappingAppService.GetUnmappedCodesAsync` (danh sách mã hãng **chưa** ánh xạ).
///
/// 🎯 Vì sao phải đo bằng test tầng app service (không chỉ lõi thuần): `GetUnmappedCodesAsync` là đường
/// **DUY NHẤT** liệt kê *đủ* mã chưa ánh xạ cho người vận hành (§A1). Sai ở đây (đếm sai, trả nhầm lượt,
/// NÉM khi chưa có lượt) ⇒ người vận hành **không biết mã nào cần ánh xạ** ⇒ kẹt luồng tạo vận đơn.
///
/// 📌 Dựng app service bằng `new` + repo in-memory (`RepoDiaChiGia`, tái dùng `HaTang`) — KHÔNG host ABP,
/// KHÔNG PostgreSQL (khuôn `D-229`).
/// </summary>
public class CarrierAddressAppServiceTests
{
    private const string Hang = "GHN";
    private static readonly DateTime T0 = new(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);

    private static (CarrierAddressMappingAppService Sut, BoPhuThuoc Bo) Dung()
    {
        var bo = new BoPhuThuoc();

        var sut = new CarrierAddressMappingAppService(
            bo.AnhXa, bo.DonVi, bo.Lo, bo.DongCon, new ResolverGia());

        sut.LazyServiceProvider = new AbpLazyServiceProvider(
            new ServiceCollection().BuildServiceProvider());

        return (sut, bo);
    }

    private static CarrierAddressSyncRun Luot(
        Guid id, CarrierAddressSyncOutcome outcome, DateTime finishedAt)
        => CarrierAddressSyncRun.Ghi(
            Hang, "api/ghn", sourceHash: null,
            startedAtUtc: finishedAt.AddSeconds(-1), finishedAtUtc: finishedAt,
            triggeredBy: CarrierAddressSyncWorker.TriggeredByJob,
            outcome: outcome, errorCode: outcome == CarrierAddressSyncOutcome.BiChan ? "eCommerce:Address:0508" : null,
            errorType: null, soMaNhan: 0, soCapNhatTen: 0, soDanhDauNghiNgo: 0, soCanNguoiAnhXa: 0,
            mauCanNguoiAnhXa: null, tenantId: null, id: id);

    private static List<CarrierAddressSyncRunUnmappedCode> MaChua(
        Guid runId, int soLuong, int batDau = 1)
        => Enumerable.Range(batDau, soLuong)
            .Select(i => CarrierAddressSyncRunUnmappedCode.Ghi(
                runId, Hang, "Phuong", $"P-{i:D5}", $"Phường {i}", tenantId: null))
            .ToList();

    // ── BR-002 / D-88: 3.000 dòng con ⇒ TotalCount = 3.000 ─────────────────────────────────────────

    [Fact]
    public async Task BR002_GetUnmapped_3000_dong_con_tra_TotalCount_3000_va_phan_trang_mac_dinh_100()
    {
        var (sut, bo) = Dung();
        var run = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.DaApDung, T0);
        bo.Lo.Them(run);

        foreach (var con in MaChua(run.Id, 3000))
        {
            bo.DongCon.Them(con);
        }

        var kq = await sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(Hang));

        Assert.Equal(3000, kq.TotalCount);
        Assert.Equal(100, kq.Items.Count);              // `MaxResultCount` mặc định = 100
        Assert.Equal(run.Id, kq.SyncRunId);
        Assert.Equal(T0, kq.FinishedAtUtc);
        Assert.Equal(CarrierAddressSyncOutcome.DaApDung, kq.LatestRunOutcome);
    }

    // ── BR-002 / D-88 ③: trừ ánh xạ TẠI LÚC ĐỌC ⇒ 2.999 ──────────────────────────────────────────

    [Fact]
    public async Task BR002_tao_1_anh_xa_cho_ma_da_co_thi_TotalCount_con_2999()
    {
        var (sut, bo) = Dung();
        var run = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.DaApDung, T0);
        bo.Lo.Them(run);

        foreach (var con in MaChua(run.Id, 3000))
        {
            bo.DongCon.Them(con);
        }

        var donViId = Guid.NewGuid();
        bo.AnhXa.Them(CarrierAddressMapping.TaoMoi(
            Hang, "Phuong", "P-00042", "Phường 42", donViId,
            CarrierMatchMethod.ThuCong, "api/ghn", T0));

        var kq = await sut.GetUnmappedCodesAsync(
            new GetUnmappedCodesRequest(Hang, SkipCount: 0, MaxResultCount: 1000));

        Assert.Equal(2999, kq.TotalCount);
        Assert.DoesNotContain(kq.Items, x => x.CarrierAddressCode == "P-00042");
    }

    // ── BR-005 / D-236: lượt mới nhất `BiChan` KHÔNG che danh sách lượt `DaApDung` ────────────────

    [Fact]
    public async Task BR005_luot_moi_nhat_BiChan_van_tra_luot_DaApDung_va_LatestRunOutcome_1()
    {
        var (sut, bo) = Dung();

        var daApDung = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.DaApDung, T0);
        var biChan = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.BiChan, T0.AddHours(1));
        bo.Lo.Them(daApDung);
        bo.Lo.Them(biChan);

        foreach (var con in MaChua(daApDung.Id, 3))
        {
            bo.DongCon.Them(con);
        }

        var kq = await sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(Hang));

        Assert.Equal(3, kq.TotalCount);
        Assert.Equal(daApDung.Id, kq.SyncRunId);                       // KHÔNG phải lượt BiChan
        Assert.Equal(CarrierAddressSyncOutcome.BiChan, kq.LatestRunOutcome);   // = 1
        Assert.Equal((int)CarrierAddressSyncOutcome.BiChan, (int)kq.LatestRunOutcome!);
    }

    // ── BR-002 / D-236: chưa có lượt `DaApDung` ⇒ rỗng + SyncRunId null, KHÔNG ném ───────────────

    [Fact]
    public async Task BR002_chua_co_luot_DaApDung_tra_RONG_SyncRunId_null_khong_nem()
    {
        var (sut, bo) = Dung();

        // (a) Chưa có lượt nào.
        var rong = await sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(Hang));
        Assert.Empty(rong.Items);
        Assert.Equal(0, rong.TotalCount);
        Assert.Null(rong.SyncRunId);
        Assert.Null(rong.FinishedAtUtc);
        Assert.Null(rong.LatestRunOutcome);

        // (b) Chỉ có lượt `BiChan` (0 dòng con) ⇒ vẫn rỗng, KHÔNG ném, LatestRunOutcome = BiChan.
        bo.Lo.Them(Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.BiChan, T0.AddHours(2)));
        var chiBiChan = await sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(Hang));
        Assert.Empty(chiBiChan.Items);
        Assert.Equal(0, chiBiChan.TotalCount);
        Assert.Null(chiBiChan.SyncRunId);
        Assert.Equal(CarrierAddressSyncOutcome.BiChan, chiBiChan.LatestRunOutcome);
    }

    // ── D-88 ③: phân trang không trùng / không sót ──────────────────────────────────────────────

    [Fact]
    public async Task BR002_phan_trang_khong_trung_khong_sot_tren_3000_dong()
    {
        var (sut, bo) = Dung();
        var run = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.DaApDung, T0);
        bo.Lo.Them(run);

        foreach (var con in MaChua(run.Id, 3000))
        {
            bo.DongCon.Them(con);
        }

        var trang1 = await sut.GetUnmappedCodesAsync(
            new GetUnmappedCodesRequest(Hang, SkipCount: 0, MaxResultCount: 100));
        var trangCuoi = await sut.GetUnmappedCodesAsync(
            new GetUnmappedCodesRequest(Hang, SkipCount: 2900, MaxResultCount: 100));

        Assert.Equal(100, trang1.Items.Count);
        Assert.Equal(100, trangCuoi.Items.Count);
        Assert.Empty(trang1.Items.Select(x => x.CarrierAddressCode)
            .Intersect(trangCuoi.Items.Select(x => x.CarrierAddressCode), StringComparer.Ordinal));
        Assert.Equal(3000, trang1.TotalCount);
    }

    // ── D-88: hãng rỗng ⇒ 0505 (nhất quán `Tra`, tránh "rỗng trả cả bảng") ───────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BR002_hang_rong_thi_NEM_0505(string? hang)
    {
        var (sut, _) = Dung();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(hang!)));

        Assert.Equal(CarrierAddressErrorCodes.ThieuThongTinBatBuoc, ex.Code);
    }

    // ── BR-002: chỉ đụng ĐÚNG hãng đang hỏi ──────────────────────────────────────────────────────

    [Fact]
    public async Task BR002_luot_va_dong_con_cua_HANG_KHAC_khong_bi_tinh()
    {
        var (sut, bo) = Dung();
        var runGhn = Luot(Guid.NewGuid(), CarrierAddressSyncOutcome.DaApDung, T0);
        bo.Lo.Them(runGhn);

        foreach (var con in MaChua(runGhn.Id, 2))
        {
            bo.DongCon.Them(con);
        }

        // Lượt + dòng con của GHTK KHÔNG được lẫn vào kết quả GHN.
        var runGhtk = CarrierAddressSyncRun.Ghi(
            "GHTK", "api/ghtk", null, T0.AddSeconds(-1), T0, CarrierAddressSyncWorker.TriggeredByJob,
            CarrierAddressSyncOutcome.DaApDung, null, null, 0, 0, 0, 0, null, null);
        bo.Lo.Them(runGhtk);
        bo.DongCon.Them(CarrierAddressSyncRunUnmappedCode.Ghi(runGhtk.Id, "GHTK", "Tinh", "G-1", "Tỉnh 1"));

        var kq = await sut.GetUnmappedCodesAsync(new GetUnmappedCodesRequest(Hang));

        Assert.Equal(2, kq.TotalCount);
        Assert.Equal(runGhn.Id, kq.SyncRunId);
        Assert.DoesNotContain(kq.Items, x => x.CarrierAddressCode == "G-1");
    }

    private sealed class BoPhuThuoc
    {
        public RepoDiaChiGia<CarrierAddressMapping> AnhXa { get; } = new();
        public RepoDiaChiGia<AdministrativeUnit> DonVi { get; } = new();
        public RepoDiaChiGia<CarrierAddressSyncRun> Lo { get; } = new();
        public RepoDiaChiGia<CarrierAddressSyncRunUnmappedCode> DongCon { get; } = new();
    }

    private sealed class ResolverGia : ICarrierAddressResolver
    {
        public Task<CarrierAddressCodeResultDto> ResolveCarrierAddressCodeAsync(
            ResolveCarrierAddressCodeRequest request)
            => throw new NotSupportedException("GetUnmappedCodesAsync không dùng resolver (D-232 uỷ quyền tách biệt).");
    }
}

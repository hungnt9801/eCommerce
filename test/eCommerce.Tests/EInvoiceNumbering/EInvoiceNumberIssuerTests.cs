using eCommerce.EInvoice;
using Volo.Abp;
using Volo.Abp.DistributedLocking;

namespace eCommerce.Tests.EInvoiceNumbering;

/// <summary>
/// Test cho **cấp số hoá đơn** (`EInvoiceNumberIssuer`) — phần rủi ro nhất của `EINV-001`.
///
/// 🎯 Vì sao: "đọc số lớn nhất rồi cộng 1" là thao tác **read-modify-write**. Hai yêu cầu đồng thời
/// cùng đọc số cũ ⇒ **cả hai nhận cùng một số** ⇒ **trùng số hoá đơn** (lỗi pháp lý, không phải lỗi
/// chương trình). Và hoá đơn **đã huỷ vẫn giữ số**, nên số tiếp theo **không** được tái sử dụng.
/// </summary>
public class EInvoiceNumberIssuerTests
{
    private sealed class StoreGia : IEInvoiceNumberStore
    {
        public long SoLonNhat { get; set; }

        public string? KyHieuDaHoi { get; private set; }

        public Task<long> SoLonNhatDaCapAsync(string kyHieuMauSo, CancellationToken cancellationToken = default)
        {
            KyHieuDaHoi = kyHieuMauSo;
            return Task.FromResult(SoLonNhat);
        }
    }

    private sealed class KhoaGia : IAbpDistributedLock
    {
        public bool LuonBan { get; set; }

        public string? TenDaXin { get; private set; }

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            TenDaXin = name;

            if (LuonBan)
            {
                return Task.FromResult<IAbpDistributedLockHandle?>(null);
            }

            return Task.FromResult<IAbpDistributedLockHandle?>(new Handle());
        }

        private sealed class Handle : IAbpDistributedLockHandle
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static (EInvoiceNumberIssuer Issuer, StoreGia Store, KhoaGia Khoa) Tao()
    {
        var store = new StoreGia();
        var khoa = new KhoaGia();
        return (new EInvoiceNumberIssuer(store, khoa), store, khoa);
    }

    // ── Cấp số tuần tự ──────────────────────────────────────────────────────

    [Fact]
    public async Task Chua_co_hoa_don_nao_thi_so_dau_tien_la_00000001()
    {
        var (issuer, store, _) = Tao();
        store.SoLonNhat = 0;

        Assert.Equal("00000001", await issuer.CapSoMoiAsync("1C26TAA"));
    }

    [Fact]
    public async Task So_tiep_theo_la_so_lon_nhat_cong_1()
    {
        var (issuer, store, _) = Tao();
        store.SoLonNhat = 41;

        Assert.Equal("00000042", await issuer.CapSoMoiAsync("1C26TAA"));
    }

    [Fact]
    public async Task HOI_DUNG_ky_hieu_mau_so()
    {
        // Số hoá đơn là liên tục **theo từng ký hiệu mẫu số**, không phải toàn hệ thống.
        var (issuer, store, _) = Tao();

        await issuer.CapSoMoiAsync(" 1C26TAA ");

        Assert.Equal("1C26TAA", store.KyHieuDaHoi);
    }

    [Fact]
    public async Task Ky_hieu_rong_thi_NEM_LOI()
    {
        var (issuer, _, _) = Tao();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => issuer.CapSoMoiAsync("   "));
        Assert.Equal(EInvoiceNumberingErrorCodes.ThieuKyHieuMauSo, ex.Code);
    }

    // ── 🚨 Bất biến quan trọng nhất: KHÔNG cấp số khi không giữ khoá ────────

    [Fact]
    public async Task KHONG_giu_duoc_khoa_thi_NEM_LOI_chu_KHONG_cap_so()
    {
        // Nếu vẫn cấp số thì hai yêu cầu đồng thời sẽ nhận CÙNG một số ⇒ trùng số hoá đơn.
        var (issuer, _, khoa) = Tao();
        khoa.LuonBan = true;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => issuer.CapSoMoiAsync("1C26TAA"));

        Assert.Equal(EInvoiceNumberingErrorCodes.KhongGianhDuocKhoaCapSo, ex.Code);
    }

    [Fact]
    public void Khoa_cap_so_KHONG_chua_tien_to_ecommerce_de_tranh_TIEN_TO_DOI()
    {
        var ten = EInvoiceNumberIssuer.KhoaCapSo("1C26TAA");

        Assert.False(ten.StartsWith("ecommerce:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("einvoice:number:1C26TAA", ten);
    }

    [Fact]
    public async Task Khoa_duoc_xin_dung_theo_ky_hieu_mau_so()
    {
        var (issuer, _, khoa) = Tao();

        await issuer.CapSoMoiAsync("2C26TAA");

        Assert.Equal("einvoice:number:2C26TAA", khoa.TenDaXin);
    }

    // ── Định dạng & đọc số ──────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "00000001")]
    [InlineData(42, "00000042")]
    [InlineData(12345678, "12345678")]
    public void Dinh_dang_8_chu_so(long so, string mongDoi)
    {
        Assert.Equal(mongDoi, EInvoiceNumberIssuer.DinhDang(so));
    }

    [Fact]
    public void So_khong_hop_le_thi_NEM_LOI()
    {
        Assert.Equal(EInvoiceNumberingErrorCodes.SoHoaDonKhongHopLe,
            Assert.Throws<BusinessException>(() => EInvoiceNumberIssuer.DinhDang(0)).Code);
        Assert.Equal(EInvoiceNumberingErrorCodes.SoHoaDonKhongHopLe,
            Assert.Throws<BusinessException>(() => EInvoiceNumberIssuer.DocSo("abc")).Code);
        Assert.Equal(EInvoiceNumberingErrorCodes.SoHoaDonKhongHopLe,
            Assert.Throws<BusinessException>(() => EInvoiceNumberIssuer.DocSo("")).Code);
    }

    [Fact]
    public void DocSo_bo_qua_so_0_dau()
    {
        Assert.Equal(42, EInvoiceNumberIssuer.DocSo("00000042"));
        Assert.Equal(42, EInvoiceNumberIssuer.DocSo(" 42 "));
    }

    // ── Kiểm tra tính LIÊN TỤC (đối chiếu với cơ quan thuế) ────────────────

    [Fact]
    public void Day_so_lien_tuc_thi_khong_co_khoang_thieu()
    {
        var thieu = EInvoiceNumberIssuer.TimKhoangThieu(["00000001", "00000002", "00000003"]);

        Assert.Empty(thieu);
    }

    [Fact]
    public void Phat_hien_dung_khoang_so_bi_thieu()
    {
        // Thiếu 4,5,6 ⇒ phải báo (1 khoảng).
        var thieu = EInvoiceNumberIssuer.TimKhoangThieu(["00000001", "00000002", "00000003", "00000007", "00000008"]);

        var khoang = Assert.Single(thieu);
        Assert.Equal((4L, 6L), khoang);
    }

    [Fact]
    public void Phat_hien_nhieu_khoang_thieu()
    {
        var thieu = EInvoiceNumberIssuer.TimKhoangThieu(["00000001", "00000003", "00000010"]);

        Assert.Equal(2, thieu.Count);
        Assert.Equal((2L, 2L), thieu[0]);
        Assert.Equal((4L, 9L), thieu[1]);
    }

    [Fact]
    public void Danh_sach_rong_hoac_mot_so_thi_khong_bao_thieu()
    {
        Assert.Empty(EInvoiceNumberIssuer.TimKhoangThieu([]));
        Assert.Empty(EInvoiceNumberIssuer.TimKhoangThieu(["00000005"]));
    }

    [Fact]
    public void Sap_xep_lai_truoc_khi_kiem_tra_va_bo_qua_so_trung()
    {
        var thieu = EInvoiceNumberIssuer.TimKhoangThieu(["00000003", "00000001", "00000003", "00000002"]);

        Assert.Empty(thieu);   // 1,2,3 ⇒ liên tục sau khi sắp xếp + khử trùng
    }
}

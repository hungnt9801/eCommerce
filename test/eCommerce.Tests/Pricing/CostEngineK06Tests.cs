using eCommerce.Inventory;
using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Tests.Pricing.HaTang;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — CA `K-06` TẦNG DOMAIN THUẦN + ADAPTER ĐỌC SỔ CÁI** (`D-320` vi).
///
/// Phủ các ca `K06-01/06/07/09/10/12/13` trên **hàm thuần** `TinhBinhQuanGiaQuyenDiDong` (không I/O) và
/// `CostFeedLookupAdapter` (ánh xạ loại chuyển động). Mỗi test ghi mã `K06-xx` trong tên + comment luật nguồn.
///
/// 🚨 Hàm thuần **KHÔNG** biết `WarehouseId` (grain SKU toàn hệ thống — `D-318` C9) ⇒ K06-06 kiểm **cả**
/// kết quả gộp lẫn **bề mặt kiểu** (record không có thuộc tính kho).
/// </summary>
public class CostEngineK06Tests
{
    private static readonly DateOnly Ngay1 = new(2026, 1, 1);
    private static readonly DateOnly Ngay2 = new(2026, 1, 2);

    private static DateTime Luc(DateOnly ngay, int gio = 1)
        => new(ngay.Year, ngay.Month, ngay.Day, gio, 0, 0, DateTimeKind.Utc);

    private static ChuyenDongGiaVon NhapCoGia(
        string sku, DateOnly ngay, decimal soLuong, decimal donGia, int gio = 1)
        => new(sku, ngay, Luc(ngay, gio), LoaiDongGiaVon.NhapCoGia, soLuong, Money.Vnd(donGia), Guid.NewGuid());

    private static ChuyenDongGiaVon NhapKhongGia(
        string sku, DateOnly ngay, decimal soLuong, int gio = 1)
        => new(sku, ngay, Luc(ngay, gio), LoaiDongGiaVon.NhapKhongGia, soLuong, null, Guid.NewGuid());

    private static ChuyenDongGiaVon Xuat(
        string sku, DateOnly ngay, decimal soLuong, int gio = 1)
        => new(sku, ngay, Luc(ngay, gio), LoaiDongGiaVon.Xuat, -soLuong, null, Guid.NewGuid());

    // ── K06-01: giá vốn 0 HỢP LỆ (`D-177` ⑥; `CostPrice.cs`) ────────────────────────────────────

    /// <summary>K06-01 — đơn giá nhập `0` sinh kỳ bình quân `0` và **KHÔNG** bị coi là thiếu giá vốn.</summary>
    [Fact]
    public void K06_01_gia_von_0_hop_le__sinh_ky_0_va_khong_phai_thieu()
    {
        var kq = TinhBinhQuanGiaQuyenDiDong.Tinh(
            [NhapCoGia("SKU-0", Ngay1, 10m, 0m)], Currency.Vnd);

        var ky = Assert.Single(kq.CacKy);
        Assert.Equal(Money.Vnd(0m), ky.GiaBinhQuan);
        Assert.Empty(kq.SkuThieuGia);
    }

    /// <summary>K06-01 — entity `CostPrice` nhận số tiền `0` và duyệt được (không đòi `> 0`).</summary>
    [Fact]
    public void K06_01_CostPrice_0_hop_le_va_duyet_duoc()
    {
        var giaVon = new CostPrice(Guid.NewGuid(), "SKU-0", Money.Vnd(0m), Ngay1);

        giaVon.Duyet(Guid.NewGuid(), Luc(Ngay1, 8));

        Assert.Equal(0m, giaVon.AmountValue);
        Assert.True(giaVon.DaDuyet);
    }

    // ── K06-06: grain SKU toàn hệ thống (`D-318` C9) ────────────────────────────────────────────

    /// <summary>
    /// K06-06 — cùng SKU nhập ở hai kho khác nhau **KHÔNG tách** (grain = SKU): một bình quân gia quyền.
    /// 10 × 100.000 + 30 × 200.000 = 7.000.000 / 40 = 175.000.
    /// </summary>
    [Fact]
    public void K06_06_cung_SKU_hai_kho__gop_mot_binh_quan()
    {
        var kq = TinhBinhQuanGiaQuyenDiDong.Tinh(
            [
                NhapCoGia("SKU-1", Ngay1, 10m, 100_000m, gio: 1),
                NhapCoGia("SKU-1", Ngay1, 30m, 200_000m, gio: 2),
            ],
            Currency.Vnd);

        var ky = Assert.Single(kq.CacKy);
        Assert.Equal(Money.Vnd(175_000m), ky.GiaBinhQuan);
    }

    /// <summary>K06-06 — kiểu chuyển động của Domain Pricing **không mang** chiều kho ⇒ không thể tách kho.</summary>
    [Fact]
    public void K06_06_kieu_chuyen_dong_KHONG_co_chieu_kho()
    {
        var viPham = typeof(ChuyenDongGiaVon).GetProperties()
            .Where(p => p.Name.Contains("Warehouse", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Kho", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        Assert.True(viPham.Count == 0,
            "`ChuyenDongGiaVon` KHÔNG được mang chiều kho (grain SKU toàn hệ thống — D-318 C9): "
            + string.Join(", ", viPham));
    }

    // ── K06-07: bán trước lần nhập đầu (`D-177` ⑥) ─────────────────────────────────────────────

    /// <summary>K06-07 — bán trước lần nhập đầu: ngày bán KHÔNG có kỳ ⇒ lookup ngày đó trả `null`.</summary>
    [Fact]
    public void K06_07_ban_truoc_lan_nhap_dau__ngay_ban_khong_co_ky()
    {
        var kq = TinhBinhQuanGiaQuyenDiDong.Tinh(
            [
                Xuat("SKU-7", Ngay1, 5m),
                NhapCoGia("SKU-7", Ngay2, 10m, 100_000m),
            ],
            Currency.Vnd);

        var ky = Assert.Single(kq.CacKy);
        Assert.Equal(Ngay2, ky.EffectiveFrom);
        Assert.DoesNotContain(kq.CacKy, x => x.EffectiveFrom == Ngay1);
    }

    // ── K06-09: nhập kho thiếu đơn giá ⇒ KHÔNG bịa 0 (`§5.5`/`D-177` ⑥) ─────────────────────────

    /// <summary>
    /// K06-09 — `NhapKhongGia` làm bình quân `null` và **v1 KHÔNG phục hồi** (kể cả có lần nhập có giá sau)
    /// ⇒ không sinh kỳ nào; SKU được đánh dấu thiếu để đếm `RowsFlaggedMissing`.
    /// </summary>
    [Fact]
    public void K06_09_nhap_khong_gia__khong_sinh_ky_va_danh_dau_thieu()
    {
        var kq = TinhBinhQuanGiaQuyenDiDong.Tinh(
            [
                NhapKhongGia("SKU-9", Ngay1, 5m),
                NhapCoGia("SKU-9", Ngay2, 10m, 100_000m),
            ],
            Currency.Vnd);

        Assert.Empty(kq.CacKy);
        Assert.Contains("SKU-9", kq.SkuThieuGia);
    }

    // ── K06-10: chuyển kho net 0 (`D-318` C9; `D-321` bảng ánh xạ) ──────────────────────────────

    /// <summary>
    /// K06-10 — cặp `TransferOut`/`TransferIn` **bị adapter BỎ** (net 0 cấp SKU) ⇒ không đổi bình quân.
    /// 🚨 Nếu `TransferIn` bị xử lý như `NhapKhongGia` thì mọi lần chuyển kho làm giá vốn **mất giá trị**.
    /// </summary>
    [Fact]
    public async Task K06_10_chuyen_kho__net_0__adapter_bo_ca_cap()
    {
        var soCai = new RepoGia<StockLedgerEntry>();
        var dongChungTu = new RepoGia<StockDocumentLine>();

        soCai.Them(new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-10", StockMovementType.TransferOut,
            5m, 5m, Luc(Ngay1, 2), referenceLineId: Guid.NewGuid()));
        soCai.Them(new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-10", StockMovementType.TransferIn,
            5m, 5m, Luc(Ngay1, 3), referenceLineId: Guid.NewGuid()));

        var adapter = new CostFeedLookupAdapter(soCai, dongChungTu);

        var chuyenDong = await adapter.LayChuyenDongAsync(Ngay1, Ngay1);

        Assert.Empty(chuyenDong);
    }

    // ── Fail-closed dấu/đơn giá (`D-321`; NÉM `0014` thay vì bỏ qua) ────────────────────────────

    /// <summary>Chuyển động lệch dấu ⇒ NÉM `0014` (không bỏ qua im lặng — bình quân các kỳ sau sẽ sai).</summary>
    [Theory]
    [InlineData(LoaiDongGiaVon.NhapCoGia, -1)]
    [InlineData(LoaiDongGiaVon.NhapKhongGia, -1)]
    [InlineData(LoaiDongGiaVon.Xuat, 1)]
    public void Kiem_dau__lech_quy_uoc__nem_0014(LoaiDongGiaVon loai, decimal delta)
    {
        var cd = new ChuyenDongGiaVon(
            "SKU-X", Ngay1, Luc(Ngay1), loai, delta,
            loai == LoaiDongGiaVon.NhapCoGia ? Money.Vnd(1m) : null, Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(
            () => TinhBinhQuanGiaQuyenDiDong.Tinh([cd], Currency.Vnd));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
    }

    /// <summary>`NhapCoGia` thiếu đơn giá ⇒ NÉM `0014`.</summary>
    [Fact]
    public void Kiem_don_gia__NhapCoGia_thieu_don_gia__nem_0014()
    {
        var cd = new ChuyenDongGiaVon(
            "SKU-X", Ngay1, Luc(Ngay1), LoaiDongGiaVon.NhapCoGia, 1m, null, Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(
            () => TinhBinhQuanGiaQuyenDiDong.Tinh([cd], Currency.Vnd));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
    }

    /// <summary>Đơn giá nhập âm ⇒ NÉM `0014`.</summary>
    [Fact]
    public void Kiem_don_gia__am__nem_0014()
    {
        var cd = new ChuyenDongGiaVon(
            "SKU-X", Ngay1, Luc(Ngay1), LoaiDongGiaVon.NhapCoGia, 1m, Money.Vnd(-1m), Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(
            () => TinhBinhQuanGiaQuyenDiDong.Tinh([cd], Currency.Vnd));

        Assert.Equal(PricingErrorCodes.CauHinhGiaVonKhongHopLe, ex.Code);
    }

    // ── K06-13: replay tất định (`D-176` ②) ─────────────────────────────────────────────────────

    /// <summary>K06-13 — cùng đầu vào ⇒ cùng kỳ (tất định theo Ngày/OccurredAt/ReferenceLineId).</summary>
    [Fact]
    public void K06_13_replay_tat_dinh__cung_dau_vao_cung_ket_qua()
    {
        var ds = new List<ChuyenDongGiaVon>
        {
            NhapCoGia("SKU-13", Ngay1, 10m, 100_000m, gio: 1),
            Xuat("SKU-13", Ngay1, 4m, gio: 2),
            NhapCoGia("SKU-13", Ngay2, 6m, 200_000m, gio: 1),
        };

        var a = TinhBinhQuanGiaQuyenDiDong.Tinh(ds, Currency.Vnd);
        var b = TinhBinhQuanGiaQuyenDiDong.Tinh(ds, Currency.Vnd);

        Assert.Equal(a.CacKy.Count, b.CacKy.Count);
        for (var i = 0; i < a.CacKy.Count; i++)
        {
            Assert.Equal(a.CacKy[i], b.CacKy[i]);
        }
    }

    // ── BR-014: dấu vết kiểm toán khi động cơ ghi giá vốn ───────────────────────────────────────

    /// <summary>
    /// BR-014 — mọi dòng do động cơ sinh/cập nhật mang **dấu vết** (`NguonTinh = DongCo`, `RunId`, `ComputedAtUtc`);
    /// cập nhật dòng **chưa duyệt** ghi đè dấu vết bằng lượt MỚI (không đổi giá trị đã duyệt).
    /// </summary>
    [Fact]
    public void BR_014_danh_dau_dong_co__ghi_nguon_run_va_thoi_diem()
    {
        var giaVon = new CostPrice(Guid.NewGuid(), "SKU-A", Money.Vnd(100_000m), Ngay1);
        var runId = Guid.NewGuid();
        var luc = Luc(Ngay2, 9);

        giaVon.DanhDauDongCo(runId, luc);

        Assert.Equal(CostPriceSource.DongCo, giaVon.NguonTinh);
        Assert.Equal(runId, giaVon.RunId);
        Assert.Equal(luc, giaVon.ComputedAtUtc);

        var runId2 = Guid.NewGuid();
        var luc2 = Luc(Ngay2, 10);

        giaVon.CapNhatGiaVonChuaDuyet(Money.Vnd(120_000m), Ngay2, runId2, luc2);

        Assert.Equal(120_000m, giaVon.AmountValue);
        Assert.Equal(runId2, giaVon.RunId);
        Assert.Equal(luc2, giaVon.ComputedAtUtc);
    }
}

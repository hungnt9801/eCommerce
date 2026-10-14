using eCommerce.Tax;
using eCommerce.Tax.Classifications;
using eCommerce.Tax.RateSchedules;
using Volo.Abp;

namespace eCommerce.Tests.TaxUnclassified;

/// <summary>
/// Test cho **lõi đếm SKU chưa phân loại thuế** — nguồn số liệu của gauge `tax_unclassified_skus`.
///
/// 🎯 Bất biến quan trọng nhất: **KHÔNG được để lỗi biến thành số 0**.
///   Gauge mặc định là 0, mà `0` nghĩa là *"kế toán không còn việc gì"*. Nếu việc đếm **thất bại**
///   (ví dụ bảng thuế suất trống) mà hệ thống báo 0 thì đó là **lời nói dối im lặng** — kế toán
///   tưởng đã xong, tới khi khách xin hoá đơn mới phát hiện. Test khoá cả hai phía:
///   lỗi phải **NÉM RA** (để worker đặt sentinel `-1`), và đếm đúng phải **không** ném.
/// </summary>
public class TaxUnclassifiedSkuCounterTests
{
    private static readonly DateOnly HomNay = new(2026, 10, 14);

    private static readonly Guid SkuA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SkuB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid SkuC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    /// <summary>Thuế suất cơ sở 10% — cần luôn có, nếu thiếu thì resolver ném lỗi (fail-closed).</summary>
    private static List<TaxRateSchedule> BieuThue()
        =>
        [
            new TaxRateSchedule(Guid.NewGuid(), "VAT10", "VAT 10%", 10m, TaxRateKind.Standard, new DateOnly(2025, 7, 1)),
            new TaxRateSchedule(Guid.NewGuid(), "VAT8", "VAT 8%", 8m, TaxRateKind.Reduced, new DateOnly(2025, 7, 1),
                new DateOnly(2026, 12, 31), reducedFromCode: "VAT10")
        ];

    private static TaxClassification PhanLoaiSku(Guid skuId, TaxClassificationStatus trangThai, DateOnly? tuNgay = null)
    {
        var c = new TaxClassification(Guid.NewGuid(), TaxClassificationTargetType.Sku, skuId,
            "VAT10", reductionApplicable: true, effectiveFrom: tuNgay ?? new DateOnly(2026, 1, 1));

        if (trangThai == TaxClassificationStatus.Approved)
        {
            c.Approve(Guid.NewGuid(), DateTime.UtcNow);
        }
        else if (trangThai == TaxClassificationStatus.Active)
        {
            c.Approve(Guid.NewGuid(), DateTime.UtcNow);
            c.ActivateIfDue(HomNay);
        }

        return c;
    }

    // ── Đếm đúng ────────────────────────────────────────────────────────────

    [Fact]
    public void Khong_co_SKU_nao_thi_tra_ve_0()
    {
        var kq = TaxUnclassifiedSkuCounter.Dem([], [], BieuThue(), HomNay);

        Assert.Equal(0, kq.TongSoSkuDangBan);
        Assert.Equal(0, kq.SoChuaPhanLoai);
        Assert.Empty(kq.SkuChuaPhanLoai);
    }

    [Fact]
    public void Tat_ca_SKU_da_phan_loai_thi_chua_phan_loai_bang_0()
    {
        var skus = new[] { new SkuCanDem(SkuA, true), new SkuCanDem(SkuB, true) };
        var phanLoai = new List<TaxClassification> { PhanLoaiSku(SkuA, TaxClassificationStatus.Active), PhanLoaiSku(SkuB, TaxClassificationStatus.Active) };

        var kq = TaxUnclassifiedSkuCounter.Dem(skus, phanLoai, BieuThue(), HomNay);

        Assert.Equal(2, kq.TongSoSkuDangBan);
        Assert.Equal(0, kq.SoChuaPhanLoai);
    }

    [Fact]
    public void Dem_dung_so_chua_phan_loai_va_tra_ve_dung_ID()
    {
        var skus = new[] { new SkuCanDem(SkuA, true), new SkuCanDem(SkuB, true), new SkuCanDem(SkuC, true) };
        var phanLoai = new List<TaxClassification> { PhanLoaiSku(SkuA, TaxClassificationStatus.Active) };

        var kq = TaxUnclassifiedSkuCounter.Dem(skus, phanLoai, BieuThue(), HomNay);

        Assert.Equal(3, kq.TongSoSkuDangBan);
        Assert.Equal(2, kq.SoChuaPhanLoai);
        Assert.Contains(SkuB, kq.SkuChuaPhanLoai);
        Assert.Contains(SkuC, kq.SkuChuaPhanLoai);
        Assert.DoesNotContain(SkuA, kq.SkuChuaPhanLoai);
    }

    // ── Chỉ tính SKU ĐANG BÁN ──────────────────────────────────────────────

    [Fact]
    public void SKU_NGUNG_BAN_khong_duoc_tinh_vao_tong_so()
    {
        var skus = new[] { new SkuCanDem(SkuA, true), new SkuCanDem(SkuB, false) };

        var kq = TaxUnclassifiedSkuCounter.Dem(skus, [], BieuThue(), HomNay);

        // Chỉ SkuA đang bán ⇒ tổng = 1, không phải 2.
        Assert.Equal(1, kq.TongSoSkuDangBan);
    }

    [Fact]
    public void SKU_NGUNG_BAN_chua_phan_loai_thi_KHONG_phai_viec_cua_ke_toan()
    {
        // 🎯 Đây là bất biến giữ cho con số CÓ ÍCH: nếu tính cả SKU đã ngừng bán thì con số phình ra
        // và CHE MẤT những SKU thật sự đang chặn phát hành hoá đơn.
        var skus = new[] { new SkuCanDem(SkuA, false) };   // ngừng bán, chưa phân loại

        var kq = TaxUnclassifiedSkuCounter.Dem(skus, [], BieuThue(), HomNay);

        Assert.Equal(0, kq.TongSoSkuDangBan);
        Assert.Equal(0, kq.SoChuaPhanLoai);
    }

    // ── Chỉ bản ghi CÓ HIỆU LỰC mới tính là "đã phân loại" ────────────────

    [Fact]
    public void Ban_ghi_DRAFT_khong_duoc_tinh_la_da_phan_loai()
    {
        // ⚠️ Hàm thuần này TIN vào danh sách nơi gọi truyền vào (worker đã lọc bằng `IsUsableAt`).
        // Test này ghi lại rõ HỢP ĐỒNG đó: `Draft` không được lọt vào danh sách "dùng được"
        // (BR-007: chỉ Approved/Active mới ảnh hưởng tính thuế).
        var draft = PhanLoaiSku(SkuA, TaxClassificationStatus.Draft);

        Assert.False(draft.IsUsableAt(HomNay));

        var kq = TaxUnclassifiedSkuCounter.Dem([new SkuCanDem(SkuA, true)], [], BieuThue(), HomNay);

        Assert.Equal(1, kq.SoChuaPhanLoai);   // không truyền bản Draft ⇒ vẫn chưa phân loại
    }

    [Fact]
    public void Ban_ghi_da_PHE_DUYET_va_con_hieu_luc_thi_tinh_la_da_phan_loai()
    {
        var daDuyet = PhanLoaiSku(SkuA, TaxClassificationStatus.Approved);

        Assert.True(daDuyet.IsUsableAt(HomNay));

        var kq = TaxUnclassifiedSkuCounter.Dem([new SkuCanDem(SkuA, true)], [daDuyet], BieuThue(), HomNay);

        Assert.Equal(0, kq.SoChuaPhanLoai);
    }

    [Fact]
    public void Nhieu_ban_ghi_cho_cung_SKU_thi_lay_ban_MOI_NHAT()
    {
        var cu = PhanLoaiSku(SkuA, TaxClassificationStatus.Active, new DateOnly(2024, 1, 1));
        var moi = PhanLoaiSku(SkuA, TaxClassificationStatus.Active, new DateOnly(2026, 6, 1));

        var kq = TaxUnclassifiedSkuCounter.Dem([new SkuCanDem(SkuA, true)], [cu, moi], BieuThue(), HomNay);

        Assert.Equal(0, kq.SoChuaPhanLoai);
    }

    // ── 🚨 LỖI PHẢI NÉM RA, KHÔNG ĐƯỢC THÀNH 0 ────────────────────────────

    [Fact]
    public void Bang_thue_suat_TRONG_thi_NEM_LOI_chu_KHONG_tra_ve_0()
    {
        // 🚨 Đây là test quan trọng nhất của file: nếu thiếu biểu thuế mà hàm trả về "0 chưa phân loại"
        // thì kế toán tưởng đã xong trong khi thực tế KHÔNG THỂ tính được thuế cho bất kỳ SKU nào.
        var ex = Assert.Throws<BusinessException>(() =>
            TaxUnclassifiedSkuCounter.Dem([new SkuCanDem(SkuA, true)], [], [], HomNay));

        Assert.Equal(TaxErrorCodes.NoEffectiveTaxRate, ex.Code);
    }

    [Fact]
    public void Sentinel_ChuaXacDinh_la_so_AM_de_phan_biet_voi_0()
    {
        // Bất biến nhỏ nhưng quan trọng: `0` = "không còn việc", nên giá trị "chưa xác định" PHẢI khác 0.
        Assert.Equal(-1, TaxUnclassifiedSkuCounter.ChuaXacDinh);
        Assert.NotEqual(0, TaxUnclassifiedSkuCounter.ChuaXacDinh);
    }

    [Fact]
    public void Sku_da_ngung_ban_KHONG_lam_hong_viec_dem_khi_thieu_bieu_thue()
    {
        // Nếu không có SKU nào đang bán thì KHÔNG cần biểu thuế ⇒ không được ném lỗi (tránh báo động giả
        // trên hệ thống chưa có sản phẩm).
        var kq = TaxUnclassifiedSkuCounter.Dem([new SkuCanDem(SkuA, false)], [], [], HomNay);

        Assert.Equal(0, kq.SoChuaPhanLoai);
    }
}

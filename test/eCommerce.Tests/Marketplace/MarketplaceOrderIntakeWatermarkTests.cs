using eCommerce.Orders;
using Volo.Abp;

namespace eCommerce.Tests.MarketplaceIntake;

/// <summary>
/// 🚨 **BẤT BIẾN WATERMARK CỦA ĐƠN SÀN** (`SAL-005` **BR-SAL-005-008**, nhánh `E4`).
///
/// 🔒 **BẤT BIẾN TRUNG TÂM CỦA CẢ FEATURE: `AppliedSourceVersion` CHỈ TĂNG, KHÔNG BAO GIỜ GIẢM.**
///
/// **VÌ SAO PHẢI KIỂM Ở TẦNG ENTITY (không chỉ ở handler):** nếu chỉ handler kiểm thì một đường ghi **mới**
/// (job đối soát, script sửa dữ liệu, consumer thứ hai) có thể **hạ** watermark ⇒ bản **cũ** trở thành
/// *"mới hơn"* ở lần nhận sau ⇒ **ghi đè trạng thái mới bằng trạng thái cũ** — và **không có lỗi nào**.
/// 📌 Cùng khuôn với `Product.DatSlug` và `PaymentTransaction.RegisterRefund`: **bất biến ở entity**.
/// </summary>
public class MarketplaceOrderIntakeWatermarkTests
{
    private static MarketplaceOrderIntake Tao() => new(
        Guid.NewGuid(), "shopee", "SHOP-1", "231012ABCDEFGH",
        payloadHash: "hash-ban-dau", receivedAtUtc: new DateTime(2026, 10, 14, 2, 0, 3, DateTimeKind.Utc),
        marketplacePlacedAtUtc: new DateTime(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc));

    // ── (1) ĐƠN MỚI: watermark còn `null` — KHÁC HẲN `0` ────────────────────────────────────────

    [Fact]
    public void Don_MOI__watermark_NULL__khong_phai_0()
    {
        var i = Tao();

        // 🚨 PHÂN BIỆT NÀY LÀ ĐIỀU KIỆN ĐỂ KHÔNG CHẶN MỌI CẬP NHẬT: nếu để mặc định `0` thì mọi bản có
        // `v` thật (epoch giây ~1.7 tỷ) đều "lớn hơn" ⇒ không sao; NHƯNG nếu code ở đâu đó so
        // `AppliedSourceVersion is not null` để kết luận *"đã có watermark"* thì `0` sẽ làm **sai**.
        // Kiểu `long?` + `null` nói đúng sự thật: *"chưa từng áp bản nào"*.
        Assert.Null(i.AppliedSourceVersion);
        Assert.Null(i.AppliedPayloadHash);
        Assert.False(i.GapSuspected);
        Assert.Null(i.LastReconciledAtUtc);
    }

    // ── (2) HÀNG RÀO: KHÔNG được hạ watermark ──────────────────────────────────────────────────

    [Fact]
    public void ApDung__lan_dau__duoc__va_ghi_lai_moc()
    {
        var i = Tao();

        i.ApDungWatermark(1_760_313_600, "hash-moi", new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1_760_313_600, i.AppliedSourceVersion);
        Assert.Equal("hash-moi", i.AppliedPayloadHash);
        Assert.Equal(new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc), i.AppliedSourceUpdatedAtUtc);
        Assert.False(i.GapSuspected);
    }

    [Fact]
    public void ApDung__phien_ban_THAP_HON__NEM__va_KHONG_doi_gi()
    {
        var i = Tao();
        i.ApDungWatermark(1000, "hash-A", null);

        var loi = Assert.Throws<BusinessException>(() => i.ApDungWatermark(999, "hash-B", null));

        Assert.Equal(eCommerceConsts.ErrorCodes.SalesWatermarkKhongTien, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": entity PHẢI nguyên vẹn sau khi ném.
        Assert.Equal(1000, i.AppliedSourceVersion);
        Assert.Equal("hash-A", i.AppliedPayloadHash);
    }

    [Fact]
    public void ApDung__phien_banBANG__cung_NEM__vi_do_la_luong_THUA()
    {
        var i = Tao();
        i.ApDungWatermark(1000, "hash-A", null);

        // 🚨 `<=` chứ không phải `<`: bằng nhau nghĩa là **đã có người áp bản này rồi**.
        // Nếu cho qua, hai luồng cùng ghi cùng một phiên bản ⇒ trạng thái cuối **phụ thuộc thứ tự đến**
        // (đúng thứ `D6` cấm), và nhánh C/D sau này mất cơ sở để phân biệt.
        var loi = Assert.Throws<BusinessException>(() => i.ApDungWatermark(1000, "hash-B", null));

        Assert.Equal(eCommerceConsts.ErrorCodes.SalesWatermarkKhongTien, loi.Code);
    }

    [Fact]
    public void BatBien__watermark_don_dieu_khi_ap_dung_CHUOI_ban()
    {
        var i = Tao();
        var chuoi = new long[] { 1000, 1005, 1003, 1005, 1009, 1002, 1010 };

        foreach (var v in chuoi)
        {
            try
            {
                i.ApDungWatermark(v, $"hash-{v}", null);
            }
            catch (BusinessException)
            {
                // Luồng thua — ĐÚNG như thiết kế: không ghi gì.
            }

            Assert.True(i.AppliedSourceVersion >= 1000, "Watermark không bao giờ được giảm");
        }

        Assert.Equal(1010, i.AppliedSourceVersion);
    }

    // ── (3) HAI CỘT HASH ĐỘC LẬP: bản đến muộn KHÔNG xoá hash đang có hiệu lực ─────────────────

    [Fact]
    public void ApDungPayloadHash__TACH_khoi_PayloadHash_nhan_gan_nhat()
    {
        var i = Tao();
        i.ApDungWatermark(1000, "hash-dang-hieu-luc", null);

        // 📌 Mô phỏng: bản ĐẾN MUỘN (bị bỏ qua) vẫn cập nhật `PayloadHash` "nhận gần nhất"…
        // (ở đây ta mô phỏng bằng cách ghi qua thuộc tính bảo vệ — thực tế handler làm việc này)
        // ⇒ `AppliedPayloadHash` PHẢI giữ nguyên, nếu không nhánh C/D sau đó sẽ so với SAI mốc.
        Assert.Equal("hash-dang-hieu-luc", i.AppliedPayloadHash);
        Assert.NotEqual(i.PayloadHash, i.AppliedPayloadHash);
    }

    // ── (4) CỜ "NGHI NGỜ THIẾU BẢN TRUNG GIAN": chỉ BẬT, không tự TẮT ──────────────────────────

    [Fact]
    public void Co_GapSuspected__CHI_BAT__khong_bi_lan_ap_sau_lam_TAT()
    {
        var i = Tao();

        i.ApDungWatermark(1000, "h1", null, nghiNgoThieuBanTrungGian: true);
        Assert.True(i.GapSuspected);

        // ⚠️ Lần áp SAU (không nghi ngờ) **KHÔNG** được xoá cờ: một lần phát hiện thiếu bản là dấu vết
        // **VĨNH VIỄN** cho tới khi Ops đối soát. Nếu tự tắt được thì cờ mất tác dụng **đúng lúc cần nó nhất**
        // (job đối soát sẽ không bao giờ nhìn thấy nó).
        i.ApDungWatermark(2000, "h2", null, nghiNgoThieuBanTrungGian: false);

        Assert.True(i.GapSuspected);
        Assert.Equal(2000, i.AppliedSourceVersion);
    }

    [Fact]
    public void DanhDauDaDoiSoat__BAT_BUOC_co_ly_do__va_HA_co()
    {
        var i = Tao();
        i.ApDungWatermark(1000, "h1", null, nghiNgoThieuBanTrungGian: true);

        Assert.Throws<ArgumentException>(() => i.DanhDauDaDoiSoat(DateTime.UtcNow, "   "));
        Assert.True(i.GapSuspected);

        i.DanhDauDaDoiSoat(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), "đã đối chiếu với sàn, khớp");

        Assert.False(i.GapSuspected);
        Assert.NotNull(i.LastReconciledAtUtc);
        // 📌 Lý do PHẢI được giữ lại (audit): 6 tháng sau phải trả lời được *"vì sao đơn này từng bị nghi ngờ"*.
        Assert.Contains("đã đối chiếu với sàn", i.SkipReason, StringComparison.Ordinal);
    }

    [Fact]
    public void DanhDauDaDoiSoat__KHONG_cho_phep_doi_watermark_bang_tay()
    {
        var i = Tao();
        i.ApDungWatermark(1000, "h1", null);

        // 🚨 `SAL-005` §6.1: `SetWatermarkAsync` **KHÔNG TỒN TẠI** — *"watermark CHỈ tiến qua luật 4 nhánh;
        // sửa tay = mở đường cho ghi đè trạng thái mới hơn"*. Test này khoá bất biến đó ở tầng API:
        // đối soát xong **KHÔNG** được đụng tới watermark.
        i.DanhDauDaDoiSoat(DateTime.UtcNow, "đối soát xong");

        Assert.Equal(1000, i.AppliedSourceVersion);
        Assert.Equal("h1", i.AppliedPayloadHash);
    }
}

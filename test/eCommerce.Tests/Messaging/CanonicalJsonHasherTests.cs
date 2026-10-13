using eCommerce.Messaging;
using eCommerce.Messaging.Idempotency;
using Volo.Abp;

namespace eCommerce.Tests.Messaging;

/// <summary>
/// Test cho **`CanonicalJsonHasher`** và **`IdempotencyKey`**.
///
/// 🎯 Vì sao quan trọng: băm **không ổn định** gây ra hai thảm hoạ ngược nhau —
///   • băm **quá nhạy** (phụ thuộc thứ tự thuộc tính/khoảng trắng) ⇒ **từ chối yêu cầu hợp lệ** (báo xung đột giả);
///   • băm **quá dễ dãi** ⇒ hai payload **khác nghĩa** lại trùng hash ⇒ **bỏ qua yêu cầu thật**.
/// Bộ test này khoá cả hai phía.
/// </summary>
public class CanonicalJsonHasherTests
{
    [Fact]
    public void Thu_tu_thuoc_tinh_KHONG_doi_hash()
    {
        var a = CanonicalJsonHasher.Bam("""{"a":1,"b":2}""");
        var b = CanonicalJsonHasher.Bam("""{"b":2,"a":1}""");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Khoang_trang_va_xuong_dong_KHONG_doi_hash()
    {
        var a = CanonicalJsonHasher.Bam("""{"a":1,"b":[1,2]}""");
        var b = CanonicalJsonHasher.Bam("""
            {
                "b": [ 1, 2 ],
                "a": 1
            }
            """);

        Assert.Equal(a, b);
    }

    [Fact]
    public void So_1_va_1_0_cho_CUNG_hash()
    {
        Assert.Equal(CanonicalJsonHasher.Bam("""{"x":1}"""), CanonicalJsonHasher.Bam("""{"x":1.0}"""));
    }

    [Fact]
    public void Gia_tri_KHAC_nghia_thi_KHAC_hash()
    {
        Assert.NotEqual(CanonicalJsonHasher.Bam("""{"x":1}"""), CanonicalJsonHasher.Bam("""{"x":2}"""));
        Assert.NotEqual(CanonicalJsonHasher.Bam("""{"x":"1"}"""), CanonicalJsonHasher.Bam("""{"x":1}"""));
        Assert.NotEqual(CanonicalJsonHasher.Bam("""{"x":true}"""), CanonicalJsonHasher.Bam("""{"x":"true"}"""));
    }

    [Fact]
    public void Thu_tu_PHAN_TU_mang_CO_doi_hash_vi_mang_co_thu_tu()
    {
        // Mảng là CÓ THỨ TỰ ⇒ [1,2] khác [2,1]. Đây là chủ ý, không phải lỗi.
        Assert.NotEqual(CanonicalJsonHasher.Bam("""[1,2]"""), CanonicalJsonHasher.Bam("""[2,1]"""));
    }

    [Fact]
    public void Doi_PHIEN_BAN_bam_thi_doi_hash_de_tranh_XUNG_DOT_GIA()
    {
        // 🔒 XC-002 Q9: sau khi nâng cấp thuật toán băm, dữ liệu cũ KHÔNG được coi là xung đột giả.
        var v1 = CanonicalJsonHasher.Bam("""{"a":1}""", 1);
        var v2 = CanonicalJsonHasher.Bam("""{"a":1}""", 2);

        Assert.NotEqual(v1, v2);
        Assert.Equal(1, CanonicalJsonHasher.PhienBanHienHanh);
    }

    [Fact]
    public void null_khac_chuoi_null()
    {
        Assert.NotEqual(CanonicalJsonHasher.Bam(null), CanonicalJsonHasher.Bam("null"));
    }

    [Fact]
    public void Payload_khong_hop_le_thi_NEM_LOI()
    {
        var ex = Assert.Throws<BusinessException>(() => CanonicalJsonHasher.Bam("{khong-phai-json"));

        Assert.Equal(MessagingErrorCodes.PayloadKhongHopLe, ex.Code);
    }

    [Fact]
    public void Hash_on_dinh_giua_cac_lan_goi()
    {
        var h1 = CanonicalJsonHasher.Bam("""{"a":1,"b":{"c":2,"d":3}}""");
        var h2 = CanonicalJsonHasher.Bam("""{"b":{"d":3,"c":2},"a":1}""");

        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);   // SHA-256 hex
    }
}

/// <summary>Test cho khoá idempotency (chuẩn hoá + tên khoá phân tán).</summary>
public class IdempotencyKeyTests
{
    [Fact]
    public void Chuan_hoa_trim_de_khong_tao_hai_ban_ghi_cho_cung_mot_khoa()
    {
        var a = IdempotencyKey.Cua(" vnpay:ipn ", " GD-1 ");
        var b = IdempotencyKey.Cua("vnpay:ipn", "GD-1");

        Assert.Equal(a.Scope, b.Scope);
        Assert.Equal(a.Value, b.Value);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Scope_hoac_gia_tri_rong_thi_NEM_LOI()
    {
        Assert.Equal(MessagingErrorCodes.KhoaKhongHopLe, Assert.Throws<BusinessException>(() => IdempotencyKey.Cua("", "x")).Code);
        Assert.Equal(MessagingErrorCodes.KhoaKhongHopLe, Assert.Throws<BusinessException>(() => IdempotencyKey.Cua("x", "   ")).Code);
    }

    [Fact]
    public void Qua_dai_thi_NEM_LOI()
    {
        var dai = new string('a', IdempotencyKey.DoDaiGiaTriToiDa + 1);

        Assert.Equal(MessagingErrorCodes.KhoaKhongHopLe, Assert.Throws<BusinessException>(() => IdempotencyKey.Cua("x", dai)).Code);
    }

    [Fact]
    public void ToString_va_KhoaLock_dung_dinh_dang()
    {
        var key = IdempotencyKey.Cua("vnpay:ipn", "GD-1");

        Assert.Equal("vnpay:ipn:GD-1", key.ToString());
        Assert.Equal("messaging:idempotency:vnpay:ipn:GD-1", key.KhoaLock());
    }
}

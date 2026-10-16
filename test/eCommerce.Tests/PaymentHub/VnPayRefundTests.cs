using eCommerce.PaymentHub.Payments;
using eCommerce.PaymentHub.Providers;
using System.Security.Cryptography;
using System.Text;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **HOÀN TIỀN + TRUY VẤN VNPAY** (`SAL-006`) — lõi **THUẦN** (`VnPayRequestBuilder`).
///
/// **VÌ SAO BỘ TEST NÀY LÀ CHỐT AN TOÀN:** trước 16/10/2026, `RefundAsync` và `QueryStatusAsync` là
/// <c>throw new NotImplementedException</c> ⇒ **không có gì để test**, và **không ai phát hiện** rằng
/// *"hoàn tiền"* — cổng kiểm soát **"KHÔNG MẤT TIỀN"** của GĐ1 — **chưa hề tồn tại**.
///
/// 🔑 **Ba thứ được khoá ở đây (mỗi thứ đều có thể gây MẤT TIỀN THẬT nếu sai):**
/// <list type="number">
///   <item><b>CHỮ KÝ</b> — tự tính lại HMAC-SHA512 và đối chiếu (nếu sai, **mọi** yêu cầu bị cổng từ chối
///     với thông điệp *"Invalid Checksum"* **không** nói sai ở đâu);</item>
///   <item><b>`00` = CỔNG ĐÃ NHẬN, KHÔNG PHẢI TIỀN ĐÃ RA</b> — hoàn tiền VN là **bất đồng bộ**; đọc nhầm
///     thành *"xong"* ⇒ **sổ sách nói dối** và **không ai** theo dõi bước xác nhận;</item>
///   <item><b>LỖI HẠ TẦNG ≠ LỖI NGHIỆP VỤ</b> — timeout nghĩa là **KHÔNG BIẾT** cổng có nhận hay không
///     ⇒ **cấm retry mù** (hoàn **hai lần** = mất tiền thật).</item>
/// </list>
/// </summary>
public class VnPayRefundTests
{
    private const string BiMat = "SECRETKEYTEST1234567890";
    private const string TmnCode = "TESTTMN01";
    private static readonly DateTime MocCoDinh = new(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Tự tính HMAC-SHA512 **độc lập** với mã sản phẩm (dùng .NET trực tiếp, không gọi hàm của ta)
    /// — nếu gọi lại chính hàm của ta thì test chỉ chứng minh *"hàm gọi chính nó"*, **không** chứng minh đúng.
    /// </summary>
    private static string HmacDocLap(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));

        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    // ── (1) DỰNG THAM SỐ HOÀN TIỀN ──────────────────────────────────────────────────────────────

    [Fact]
    public void ThamSoHoanTien__du_cac_truong_bat_buoc_cua_VNPay()
    {
        var p = VnPayRequestBuilder.ThamSoHoanTien(
            TmnCode, "MR-001", 199_000m, laHoanToanPhan: true, lyDo: "Trả hàng", nguoiThucHien: "ops",
            maGiaoDichCuaCong: "14123456", thoiDiemUtc: MocCoDinh);

        Assert.Equal("2.1.0", p["vnp_Version"]);
        Assert.Equal("refund", p["vnp_Command"]);
        Assert.Equal(TmnCode, p["vnp_TmnCode"]);
        Assert.Equal("02", p["vnp_TransactionType"]);   // 02 = hoàn TOÀN PHẦN
        Assert.Equal("MR-001", p["vnp_TxnRef"]);
        Assert.Equal("14123456", p["vnp_TransactionNo"]);
        Assert.Equal("Trả hàng", p["vnp_OrderInfo"]);
        Assert.Equal("ops", p["vnp_CreateBy"]);
        Assert.True(!string.IsNullOrWhiteSpace(p["vnp_RequestId"]));
    }

    [Fact]
    public void ThamSoHoanTien__hoan_MOT_PHAN__dung_ma_03()
    {
        var p = VnPayRequestBuilder.ThamSoHoanTien(
            TmnCode, "MR-001", 50_000m, laHoanToanPhan: false, lyDo: "Trả 1 món", nguoiThucHien: "ops",
            thoiDiemUtc: MocCoDinh);

        // 🔑 Gửi sai loại (`02` trong khi chỉ hoàn một phần) ⇒ cổng **TỪ CHỐI** hoặc **hoàn sai số tiền**.
        Assert.Equal("03", p["vnp_TransactionType"]);
    }

    [Fact]
    public void ThamSoHoanTien__KHONG_co_ma_giao_dich_cua_cong__KHONG_them_truong_rong()
    {
        var p = VnPayRequestBuilder.ThamSoHoanTien(
            TmnCode, "MR-001", 199_000m, true, "lý do", "ops", maGiaoDichCuaCong: null, thoiDiemUtc: MocCoDinh);

        // 📌 KHÔNG thêm `vnp_TransactionNo = ""`: VNPay **bỏ** tham số rỗng khỏi chuỗi ký, nên thêm chuỗi rỗng
        // vào **từ điển của ta** nhưng **không** vào chuỗi ký ⇒ hai bên lệch nhau ⇒ **chữ ký sai**.
        Assert.False(p.ContainsKey("vnp_TransactionNo"));
    }

    [Theory]
    [InlineData(199_000, "19900000")]
    [InlineData(1, "100")]
    [InlineData(0.5, "50")]
    [InlineData(1234.567, "123457")]   // làm tròn AwayFromZero
    [InlineData(1234.564, "123456")]
    public void TinhSoTien__NHAN_100_va_lam_tron_AwayFromZero(decimal vao, string mongDoi)
    {
        // 📌 VNPay yêu cầu **nhân 100**. Dùng `decimal` + `AwayFromZero` (CẤM `double` — XC-001 BR-003).
        // Làm tròn "ngân hàng" (`.5 → chẵn`) sẽ cho **1 đồng khác** ⇒ cổng báo *"số tiền không khớp"*.
        Assert.Equal(mongDoi, VnPayRequestBuilder.TinhSoTien(vao));
    }

    [Fact]
    public void TinhSoTien__KHONG_dung_double__chieu_dai_on_dinh_voi_so_tien_lon()
    {
        // ⚠️ Với `double`, 10^15 × 100 mất chính xác ⇒ số tiền gửi đi **sai** ở đơn hàng lớn.
        Assert.Equal("100000000000000000", VnPayRequestBuilder.TinhSoTien(1_000_000_000_000_000m));
    }

    // ── (2) CHỮ KÝ: tự tính lại bằng .NET ──────────────────────────────────────────────────────

    [Fact]
    public void Ky__khop_HMAC_SHA512_tinh_DOC_LAP()
    {
        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Command"] = "refund",
            ["vnp_TxnRef"] = "MR-001"
        });

        var cuaTa = VnPayRequestBuilder.Ky(BiMat, chuoiKy);
        var docLap = HmacDocLap(BiMat, chuoiKy);

        Assert.Equal(docLap, cuaTa);
        // 🔒 VNPay yêu cầu hex **chữ thường**; chữ HOA sẽ bị coi là sai.
        Assert.Equal(cuaTa.ToLowerInvariant(), cuaTa);
    }

    [Fact]
    public void DungChuoiKy__SAP_XEP_khoa_va_URL_ENCODE()
    {
        var chuoi = VnPayRequestBuilder.DungChuoiKy(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["b"] = "2",
            ["a"] = "1",
            ["c"] = "có dấu & ký tự đặc biệt"
        });

        // 🔑 Thứ tự khóa phải là **a, b, c** (`SortedDictionary` + `Ordinal`). Nếu thứ tự khác thì chữ ký
        // **sai** — mà cổng chỉ nói *"Invalid Checksum"*, **không** nói sai ở đâu.
        Assert.StartsWith("a=1&b=2&c=", chuoi, StringComparison.Ordinal);
        Assert.Contains("%26", chuoi, StringComparison.Ordinal);   // `&` trong GIÁ TRỊ phải được encode
    }

    [Fact]
    public void KiemChuKy__BO_qua_vnp_SecureHash_va_SecureHashType_khi_ky()
    {
        var truong = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_ResponseCode"] = "00",
            ["vnp_TxnRef"] = "MR-001"
        };

        // Ký **đúng chuẩn**: chỉ trên hai trường nghiệp vụ.
        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_ResponseCode"] = "00",
                ["vnp_TxnRef"] = "MR-001"
            });
        truong["vnp_SecureHash"] = HmacDocLap(BiMat, chuoiKy);
        truong["vnp_SecureHashType"] = "SHA512";

        // 🔒 Nếu ta **ký cả** `vnp_SecureHash`/`vnp_SecureHashType` thì chữ ký sẽ **khác** và một phản hồi
        // THẬT sẽ bị coi là **giả** ⇒ hệ thống từ chối thông tin tiền đã vào.
        Assert.True(VnPayRequestBuilder.KiemChuKy(truong, BiMat));
    }

    [Fact]
    public void KiemChuKy__thieu_chu_ky__TRA_FALSE__khong_nem()
    {
        var truong = new Dictionary<string, string>(StringComparer.Ordinal) { ["vnp_ResponseCode"] = "00" };

        Assert.False(VnPayRequestBuilder.KiemChuKy(truong, BiMat));
    }

    [Fact]
    public void KiemChuKy__sai_khoa__TRA_FALSE()
    {
        var truong = new Dictionary<string, string>(StringComparer.Ordinal) { ["vnp_ResponseCode"] = "00" };
        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(
            new SortedDictionary<string, string>(StringComparer.Ordinal) { ["vnp_ResponseCode"] = "00" });
        truong["vnp_SecureHash"] = HmacDocLap("KHOA-KHAC", chuoiKy);

        Assert.False(VnPayRequestBuilder.KiemChuKy(truong, BiMat));
    }

    [Fact]
    public void KiemChuKy__BO_tham_so_RONG__khop_quy_tac_cua_VNPay()
    {
        var truong = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_ResponseCode"] = "00",
            ["vnp_BankCode"] = ""            // tham số RỖNG
        };

        // VNPay **bỏ** tham số rỗng khỏi chuỗi ký ⇒ ta phải bỏ y hệt. Nếu không, chữ ký của một phản hồi
        // HỢP LỆ sẽ bị coi là SAI ⇒ **từ chối một phản hồi THẬT**.
        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(
            new SortedDictionary<string, string>(StringComparer.Ordinal) { ["vnp_ResponseCode"] = "00" });
        truong["vnp_SecureHash"] = HmacDocLap(BiMat, chuoiKy);

        Assert.True(VnPayRequestBuilder.KiemChuKy(truong, BiMat));
    }

    // ── (3) ĐỌC PHẢN HỒI: `00` = ĐÃ NHẬN, KHÔNG PHẢI TIỀN ĐÃ RA ─────────────────────────────────

    /// <summary>Dựng phản hồi JSON **có chữ ký hợp lệ** — ép kiểu hex chữ thường để khớp hợp đồng VNPay.</summary>
    private static string PhanHoiCoChuKy(params (string Khoa, string GiaTri)[] truong)
    {
        var deKy = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (khoa, giaTri) in truong)
        {
            deKy[khoa] = giaTri;
        }

        var chuKy = HmacDocLap(BiMat, VnPayRequestBuilder.DungChuoiKy(deKy));

        var tatCa = truong.Append(("vnp_SecureHash", chuKy));
        // ⚠️ `Append` cho ra `IEnumerable<(string, string)>` với tên phần tử **không** được đặt tên
        // (tuple literal không mang tên khi đi qua generic) ⇒ phải truy cập bằng `Item1`/`Item2`.
        var json = "{" + string.Join(",", tatCa.Select(t => $"\"{t.Item1}\":\"{t.Item2}\"")) + "}";

        return json;
    }

    [Fact]
    public void DocPhanHoi__ma_00__DA_NHAN_yeu_cau_NHUNG_CHUA_xac_nhan_tien_ra()
    {
        var json = PhanHoiCoChuKy(
            ("vnp_ResponseCode", "00"),
            ("vnp_Message", "Success"),
            ("vnp_TransactionNo", "14123456"));

        var kq = VnPayRequestBuilder.DocPhanHoi(json, BiMat);

        // 🚨🚨 **ĐÂY LÀ BẤT BIẾN QUAN TRỌNG NHẤT CỦA CẢ FILE.**
        // `00` nghĩa là **CỔNG ĐÃ NHẬN** yêu cầu hoàn tiền — **KHÔNG** phải tiền đã ra (VNPay xử lý
        // bất đồng bộ ở phía ngân hàng). Nếu test này đổi thành `DaHoanThat = true` thì hệ thống sẽ ghi
        // "đã hoàn" trong khi khách **chưa** nhận tiền ⇒ **sổ sách nói dối** và **không ai** theo dõi.
        Assert.True(kq.DaNhanYeuCau);
        Assert.False(kq.DaHoanThat);
        Assert.True(kq.ChoXacNhan);
        Assert.Equal("00", kq.MaPhanHoi);
        Assert.Equal("14123456", kq.MaGiaoDichHoan);
    }

    [Fact]
    public void DocPhanHoi__CHU_KY_SAI__KHONG_TIN__du_noi_thanh_cong()
    {
        // 🚨 Kẻ tấn công chỉ cần gửi `{"vnp_ResponseCode":"00"}` là hệ thống ghi **đã hoàn tiền** —
        // nếu ta không kiểm chữ ký TRƯỚC khi đọc nội dung.
        var json = """{"vnp_ResponseCode":"00","vnp_Message":"Success","vnp_SecureHash":"deadbeef"}""";

        var kq = VnPayRequestBuilder.DocPhanHoi(json, BiMat);

        Assert.False(kq.DaNhanYeuCau);
        Assert.False(kq.DaHoanThat);
        Assert.False(kq.ChoXacNhan);
        Assert.Contains("Chữ ký", kq.LyDoThatBai, StringComparison.Ordinal);
    }

    [Fact]
    public void DocPhanHoi__THIEU_HashSecret__FAIL_CLOSED__khong_tin()
    {
        var json = PhanHoiCoChuKy(("vnp_ResponseCode", "00"));

        // 🔒 Không kiểm được chữ ký thì **không tin** — kể cả khi nội dung nói thành công.
        var kq = VnPayRequestBuilder.DocPhanHoi(json, biMat: "");

        Assert.False(kq.DaNhanYeuCau);
        Assert.Contains("HashSecret", kq.LyDoThatBai, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("24", "khách huỷ")]
    [InlineData("07", "trừ tiền thành công nhưng giao dịch bị nghi ngờ")]
    [InlineData("09", "thẻ chưa đăng ký")]
    public void DocPhanHoi__ma_KHAC_00__TU_CHOI__co_ly_do(string ma, string thongDiep)
    {
        var json = PhanHoiCoChuKy(("vnp_ResponseCode", ma), ("vnp_Message", thongDiep));

        var kq = VnPayRequestBuilder.DocPhanHoi(json, BiMat);

        Assert.False(kq.DaNhanYeuCau);
        Assert.False(kq.DaHoanThat);
        Assert.Contains(ma, kq.LyDoThatBai, StringComparison.Ordinal);
    }

    [Fact]
    public void DocPhanHoi__than_RONG__khong_nem__co_ly_do()
    {
        var kq = VnPayRequestBuilder.DocPhanHoi("", BiMat);

        Assert.False(kq.DaNhanYeuCau);
        Assert.NotNull(kq.LyDoThatBai);
    }

    [Fact]
    public void DocPhanHoi__KHONG_phai_JSON__khong_nem__ghi_loi_ha_tang()
    {
        var kq = VnPayRequestBuilder.DocPhanHoi("<html>502 Bad Gateway</html>", BiMat);

        Assert.False(kq.DaNhanYeuCau);
        Assert.NotNull(kq.LoiHaTang);
    }

    // ── (4) ĐỌC JSON PHẲNG (giữ nguyên văn — cần cho kiểm chữ ký) ──────────────────────────────

    [Fact]
    public void DocJsonPhang__chuoi_so_bool_va_escape()
    {
        var kq = VnPayRequestBuilder.DocJsonPhang(
            """{"a":"x","b":123,"c":true,"d":"có \"nháy\"","e":null}""");

        Assert.Equal("x", kq["a"]);
        Assert.Equal("123", kq["b"]);       // giữ NGUYÊN VĂN (không parse lại thành số)
        Assert.Equal("true", kq["c"]);
        Assert.Equal("có \"nháy\"", kq["d"]);
        // 📌 `null` ⇒ RỖNG (không phải chuỗi `"null"`): VNPay bỏ tham số rỗng khi ký, nên `"null"` sẽ làm
        // chữ ký **lệch** ⇒ một phản hồi THẬT bị coi là giả.
        Assert.Equal(string.Empty, kq["e"]);
    }

    [Fact]
    public void DocJsonPhang__json_hong__NEM_FormatException()
    {
        Assert.Throws<FormatException>(() => VnPayRequestBuilder.DocJsonPhang("khong-phai-json"));
        Assert.Throws<FormatException>(() => VnPayRequestBuilder.DocJsonPhang("""{"a":1"""));
    }

    // ── (5) THAM SỐ TRUY VẤN (`querydr`) ───────────────────────────────────────────────────────

    [Fact]
    public void ThamSoTruyVan__dung_command_va_du_truong()
    {
        var p = VnPayRequestBuilder.ThamSoTruyVan(TmnCode, "MR-001", MocCoDinh);

        Assert.Equal("querydr", p["vnp_Command"]);
        Assert.Equal("2.1.0", p["vnp_Version"]);
        Assert.Equal(TmnCode, p["vnp_TmnCode"]);
        Assert.Equal("MR-001", p["vnp_TxnRef"]);
        Assert.True(!string.IsNullOrWhiteSpace(p["vnp_RequestId"]));
    }

    // ── (6) GIAO DỊCH ĐỒNG BỘ: `DaHoanThat` CHỈ `true` khi nguồn nói rõ ─────────────────────────

    [Fact]
    public void RefundResult__MAC_DINH__FAIL_CLOSED()
    {
        // 🔒 Mặc định **tất cả** `false`: không chắc thì **không** ghi sổ. Nếu đổi mặc định thành `true`
        // thì mọi lỗi *"chưa xử lý"* sẽ trở thành *"đã hoàn tiền"* — mất tiền không dấu vết.
        var kq = new RefundResult();

        Assert.False(kq.DaNhanYeuCau);
        Assert.False(kq.DaHoanThat);
        Assert.False(kq.ChoXacNhan);
    }
}

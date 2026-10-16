using System.Text.Json;
using eCommerce.Messaging;

namespace eCommerce.Tests.Messaging;

/// <summary>
/// 🚨 **CHUẨN HOÁ PAYLOAD** (`XC-002` §6.4 / `SAL-005` **BR-SAL-005-004**) — hàm **THUẦN**.
///
/// **VÌ SAO TEST NÀY LÀ CHỐT AN TOÀN (lỗi THẬT sẽ xảy ra nếu chuẩn hoá sai — `sal-005` §7.2):**
/// cùng **một** payload nhưng **thứ tự khoá khác** mà cho **hash khác** ⇒ nhánh **C** (*"trùng"*) bị nhận
/// nhầm thành nhánh **D** (*"xung đột"*) ⇒ **DLQ GIẢ** ⇒ Ops bị đánh thức vì sự kiện **vô hại**.
/// Và tệ hơn: **hai instance** băm **cùng** sự kiện ra **hai hash khác nhau** ⇒ một instance coi là *trùng*,
/// instance kia coi là *xung đột* ⇒ **hai kết luận khác nhau cho cùng dữ liệu**.
/// 📌 Sau vài lần DLQ giả, người trực **bắt đầu bỏ qua cảnh báo** — và đó là lúc xung đột **THẬT** bị bỏ qua.
/// </summary>
public class PayloadCanonicalizerTests
{
    private sealed class DonThu
    {
        public string ExternalOrderId { get; set; } = "SP-1";
        public decimal TongTien { get; set; } = 199000m;
        public List<string> Ma { get; set; } = ["B", "A"];
        public DonCon? Con { get; set; }
    }

    private sealed class DonCon
    {
        public int SoLuong { get; set; } = 2;
        public string Ten { get; set; } = "Áo";
    }

    // ── (1) TẤT ĐỊNH: cùng nội dung ⇒ cùng hash, BẤT KỂ thứ tự khoá ─────────────────────────────

    [Fact]
    public void Cung_noi_dung_KHAC_thu_tu_khoa__PHAI_cho_CUNG_hash()
    {
        // 🔑 ĐÂY LÀ CA QUAN TRỌNG NHẤT CỦA CẢ FILE: payload webhook do **hai** đường vào (pull + webhook)
        // sinh ra, và `JsonSerializer` **không** đảm bảo thứ tự khoá giữa các lần chạy/phiên bản.
        const string jsonA = """{"b":2,"a":1,"c":{"y":2,"x":1}}""";
        const string jsonB = """{"a":1,"c":{"x":1,"y":2},"b":2}""";

        var hashA = PayloadCanonicalizer.Bam(JsonDocument.Parse(jsonA).RootElement);
        var hashB = PayloadCanonicalizer.Bam(JsonDocument.Parse(jsonB).RootElement);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void Cung_doi_tuong__BAM_HAI_LAN__cho_CUNG_hash()
    {
        var don = new DonThu { Con = new DonCon() };

        Assert.Equal(PayloadCanonicalizer.Bam(don), PayloadCanonicalizer.Bam(don));
    }

    [Fact]
    public void Hash_dung_dinh_dang__hex_chu_thuong_64_ky_tu()
    {
        var hash = PayloadCanonicalizer.Bam(new { a = 1 });

        Assert.Equal(PayloadCanonicalizer.DoDaiHash, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    // ── (2) KHÁC nội dung ⇒ PHẢI khác hash (nếu không thì hai bản khác nhau bị coi là "trùng") ──

    [Theory]
    [InlineData("""{"a":1}""", """{"a":2}""")]
    [InlineData("""{"a":1}""", """{"a":1,"b":null}""")]   // thêm khoá ⇒ khác nội dung
    [InlineData("""{"a":1}""", """{"a":"1"}""")]           // số vs chuỗi ⇒ khác kiểu
    public void Khac_noi_dung__PHAI_khac_hash(string jsonA, string jsonB)
    {
        var hashA = PayloadCanonicalizer.Bam(JsonDocument.Parse(jsonA).RootElement);
        var hashB = PayloadCanonicalizer.Bam(JsonDocument.Parse(jsonB).RootElement);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Thu_tu_PHAN_TU_trong_mang__PHAI_lam_KHAC_hash()
    {
        // 🚨 Mảng là **có thứ tự** về mặt ngữ nghĩa (dòng đơn hàng, lịch sử trạng thái).
        // Nếu sắp xếp mảng thì hai payload **khác nhau** (dòng đảo thứ tự) sẽ cho **cùng** hash ⇒
        // bản mới bị coi là **trùng** ⇒ **mất cập nhật** (đúng thứ nhánh C/D tồn tại để phân biệt).
        Assert.NotEqual(
            PayloadCanonicalizer.Bam(JsonDocument.Parse("""{"lines":["A","B"]}""").RootElement),
            PayloadCanonicalizer.Bam(JsonDocument.Parse("""{"lines":["B","A"]}""").RootElement));
    }

    [Fact]
    public void So_giu_NGUYEN_VAN__khong_bi_chuan_hoa_lai_thanh_dang_khac()
    {
        // 📌 Parse rồi ghi lại có thể đổi `1.50` thành `1.5` ở một số cấu hình ⇒ **tự tay** tạo hash
        // không tất định giữa các instance. Giữ `GetRawText` để tránh.
        var keoDai = PayloadCanonicalizer.Bam(JsonDocument.Parse("""{"tien":1.50}""").RootElement);
        var ngan = PayloadCanonicalizer.Bam(JsonDocument.Parse("""{"tien":1.5}""").RootElement);

        // ⚠️ Ghi rõ giới hạn ĐÃ BIẾT: hai cách viết **cùng giá trị** vẫn cho hash KHÁC.
        // Đây là đánh đổi CÓ CHỦ Ý: với `SAL-005` payload do **một** connector sinh ⇒ hình dạng ổn định;
        // đổi lại, ta không bao giờ làm hỏng giá trị số của dữ liệu tiền.
        Assert.NotEqual(keoDai, ngan);
    }

    // ── (3) KHOÁ ĐƯỢC SẮP XẾP THẬT (kiểm nội dung canonical, không chỉ hash) ────────────────────

    [Fact]
    public void Canonical__khoa_sap_xep_theo_ORDINAL__khong_khoang_trang()
    {
        var canonical = PayloadCanonicalizer.ChuanHoa(JsonDocument.Parse("""{"b":1,"a":2}""").RootElement);

        Assert.Equal("""{"a":2,"b":1}""", canonical);
    }

    [Fact]
    public void Canonical__long_nhau__sap_xep_DE_QUY()
    {
        // 📌 Nếu chỉ sắp xếp tầng ngoài thì payload webhook (lồng 3–4 tầng) vẫn không tất định.
        var canonical = PayloadCanonicalizer.ChuanHoa(
            JsonDocument.Parse("""{"z":{"b":1,"a":2},"a":[{"y":1,"x":2}]}""").RootElement);

        Assert.Equal("""{"a":[{"x":2,"y":1}],"z":{"a":2,"b":1}}""", canonical);
    }

    [Fact]
    public void Canonical__null__tra_null__khong_nem()
    {
        Assert.Equal("null", PayloadCanonicalizer.ChuanHoa<object?>(null));
    }

    // ── (4) EPOCH GIÂY: LÀM TRÒN XUỐNG (chốt của nhánh D6) ─────────────────────────────────────

    [Fact]
    public void ThanhPhienBan__HAI_MOC_CUNG_GIAY__cho_CUNG_phien_ban__du_khac_mili_giay()
    {
        var t1 = new DateTime(2026, 10, 14, 2, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(100);
        var t2 = new DateTime(2026, 10, 14, 2, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(900);

        // 🚨 ĐÂY LÀ CHỐT CỦA `D6`: nếu làm tròn GẦN NHẤT thì `.900` giây nhảy sang giây sau ⇒
        // hai bản **cùng một cập nhật của sàn** (Shopee có độ phân giải **1 giây**) bị coi là
        // *"bản sau MỚI HƠN"* ⇒ **ghi đè im lặng** — đúng thứ `D6` cấm ("không đoán").
        Assert.Equal(
            PayloadCanonicalizer.ThanhPhienBan(t1),
            PayloadCanonicalizer.ThanhPhienBan(t2));
    }

    [Fact]
    public void ThanhPhienBan__Unspecified__coi_la_UTC__khong_theo_gio_may()
    {
        // ⚠️ `Unspecified` rất hay gặp từ JSON của sàn. Nếu suy diễn theo giờ máy thì **cùng** payload
        // cho **hai** phiên bản khác nhau ở hai máy khác múi giờ ⇒ watermark lệch giữa các instance.
        var moc = new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Unspecified);
        var mocUtc = new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc);

        Assert.Equal(PayloadCanonicalizer.ThanhPhienBan(mocUtc), PayloadCanonicalizer.ThanhPhienBan(moc));
    }

    [Fact]
    public void ThanhPhienBan__round_trip__qua_TuPhienBan()
    {
        var moc = new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc);

        Assert.Equal(moc, PayloadCanonicalizer.TuPhienBan(PayloadCanonicalizer.ThanhPhienBan(moc)));
    }

    [Fact]
    public void ThanhPhienBan__moc_am_truoc_1970__van_dung__khong_nem()
    {
        var moc = new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        Assert.Equal(-1L, PayloadCanonicalizer.ThanhPhienBan(moc));
    }
}

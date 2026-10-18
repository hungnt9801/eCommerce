using System.Net;
using System.Security.Cryptography;
using System.Text;
using eCommerce.PaymentHub.Payments;

namespace eCommerce.PaymentHub.Providers;

/// <summary>
/// 🚨 **LÕI THUẦN CỦA VNPAY** — dựng tham số, ký HMAC-SHA512, đọc phản hồi. **KHÔNG** I/O.
///
/// **VÌ SAO TÁCH RA KHỎI `VnPayPaymentProvider` (bài học đã trả giá nhiều lần trong dự án):**
/// `RefundAsync`/`QueryStatusAsync` trước đây là `throw new NotImplementedException` ⇒ **không có gì để test**
/// và cũng **không có gì để đọc**. Nếu viết thẳng phần dựng-tham-số-và-ký vào lớp có `HttpClient` thì
/// **mọi** phép kiểm phải đi qua mạng ⇒ không kiểm được **chữ ký** (thứ mà cổng thật sẽ từ chối nếu sai),
/// không kiểm được **nhánh mã phản hồi**, và không kiểm được **lỗi hạ tầng**.
/// ⇒ Tách lõi thuần ra đây: test được **toàn bộ** bằng cách **tự tính lại HMAC** và **tự dựng phản hồi**.
///
/// 🔑 **VÌ SAO CHỮ KÝ LÀ CHỖ DỄ SAI NHẤT:** VNPay ký trên **query string ĐÃ SẮP XẾP THEO TÊN KHÓA** và
/// **ĐÃ URL-ENCODE**. Sai một trong hai (hoặc encode khác kiểu) ⇒ chữ ký **sai** ⇒ cổng trả
/// *"Invalid Checksum"* — mà thông điệp đó **không** nói ta sai ở đâu. 📌 Ba quy tắc bất di bất dịch:
/// <list type="number">
///   <item>sắp xếp khóa bằng **`StringComparer.Ordinal`** (không theo culture — culture đổi theo máy);</item>
///   <item>**bỏ** `vnp_SecureHash`/`vnp_SecureHashType` **trước khi** ký (ký cả chính chữ ký là vô nghĩa);</item>
///   <item>🚨 **giá trị RỖNG bị BỎ khỏi chuỗi ký** — VNPay làm đúng như vậy khi sinh chữ ký. 📌 Dòng này đã
///     từng **mâu thuẫn với chính bộ luật của tệp này** (bản trước ghi *"giá trị rỗng vẫn phải có"*, trong khi
///     `KiemChuKy` bỏ chúng) — đó chính là `F-PAY-1`: **hai quy tắc trong cùng một tệp**, và quy tắc sai nằm
///     ở `VerifyWebhookAsync` ⇒ **từ chối IPN THẬT** ⇒ **mất tiền im lặng**. Nay **một** luật, xem
///     <see cref="KiemChuKyTuQueryTho"/>.</item>
/// </list>
///
/// 🚨 **VÀ MỘT QUY TẮC THỨ TƯ, CHỈ PHÁT HIỆN KHI LÀM `S5`:** với **IPN** (query string đến từ Internet), chuỗi
/// ký **phải** được dựng từ **query THÔ** (`vnp_…=…` **đã encode đúng như cổng gửi**), **KHÔNG** được
/// giải mã rồi encode lại. Lý do: `WebUtility.UrlEncode` biến khoảng trắng thành `%20`, còn cổng có thể gửi
/// `+` (một số thư viện dùng `application/x-www-form-urlencoded`); giải mã rồi encode lại **đổi byte** ⇒ chữ ký
/// lệch ⇒ lại **từ chối IPN thật**. 📌 Đây **cùng một họ lỗi** với `F-PAY-1`, chỉ khác lớp: **byte của chuỗi ký**
/// phải là byte của **cổng**, không phải byte do `WebUtility` sinh lại.
/// </summary>
public static class VnPayRequestBuilder
{
    /// <summary>Mã phản hồi **THÀNH CÔNG** của VNPay. Mọi mã khác là lỗi.</summary>
    public const string MaThanhCong = "00";

    /// <summary>
    /// Dựng **query string để ký**: sắp xếp khóa **`Ordinal`** + URL-encode cả khóa và giá trị.
    /// 🔒 Đây là **cùng một** hàm dùng cho cả lúc **KÝ** và lúc **KIỂM** — nếu dùng hai hàm khác nhau thì
    /// chữ ký sẽ đúng ở môi trường này và sai ở môi trường khác (đúng họ lỗi "hai nguồn sự thật").
    /// </summary>
    public static string DungChuoiKy(SortedDictionary<string, string> thamSo)
        => string.Join("&", thamSo.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    /// <summary>HMAC-SHA512 ⇒ hex **chữ thường** (VNPay yêu cầu chữ thường).</summary>
    public static string Ky(string biMat, string chuoiKy)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(biMat));
        var bam = hmac.ComputeHash(Encoding.UTF8.GetBytes(chuoiKy));

        return Convert.ToHexStringLower(bam);
    }

    /// <summary>
    /// **Tham số của `vnp_Command=refund`** (`SAL-006`).
    /// 🔑 **VÌ SAO CÓ CẢ `vnp_TransactionType`:** VNPay phân biệt **hoàn toàn phần** (`02`) với
    /// **hoàn một phần** (`03`), và với **hoàn theo giao dịch gốc** (`01`). Gửi sai loại ⇒ **cổng từ chối**
    /// hoặc **hoàn sai số tiền**. 📌 Ở tầng này ta **luôn** dùng `02` (hoàn toàn phần) hoặc `03`
    /// (một phần) — quyết định do **số tiền** quyết định, không do người gọi truyền bừa.
    /// </summary>
    /// <param name="tmnCode">Mã merchant (`vnp_TmnCode`).</param>
    /// <param name="maGiaoDichGoc">`vnp_TxnRef` của giao dịch **GỐC** (mã của ta, không phải của cổng).</param>
    /// <param name="soTien">Số tiền hoàn (đơn vị tiền tệ thật — hàm này tự nhân 100).</param>
    /// <param name="laHoanToanPhan">`true` ⇒ `02`, `false` ⇒ `03`. Xem chú thích ở trên.</param>
    /// <param name="lyDo">`vnp_OrderInfo` — **bắt buộc không rỗng** ở tầng gọi.</param>
    /// <param name="nguoiThucHien">`vnp_CreateBy` — người/lệnh tạo yêu cầu (audit phía cổng).</param>
    /// <param name="maGiaoDichCuaCong">`vnp_TransactionNo` của giao dịch gốc **nếu biết** (cổng cấp lúc IPN).
    /// ⚠️ Đây là trường **quan trọng**: không có nó, cổng phải tự tra theo `vnp_TxnRef`; nếu giao dịch gốc
    /// được tạo ở **môi trường khác** (sandbox vs thật) thì tra sẽ **không thấy**.</param>
    /// <param name="thoiDiemUtc">Thời điểm tạo lệnh — truyền vào để **test tất định** (không đọc đồng hồ hệ thống
    /// bên trong ⇒ cùng đầu vào cho cùng đầu ra).</param>
    public static SortedDictionary<string, string> ThamSoHoanTien(
        string tmnCode,
        string maGiaoDichGoc,
        decimal soTien,
        bool laHoanToanPhan,
        string lyDo,
        string nguoiThucHien,
        string? maGiaoDichCuaCong = null,
        DateTime? thoiDiemUtc = null)
    {
        var now = (thoiDiemUtc ?? DateTime.UtcNow).AddHours(7); // VNPay dùng GMT+7 (nhất quán với `CreatePaymentAsync`)

        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"),
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "refund",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TransactionType"] = laHoanToanPhan ? "02" : "03",
            ["vnp_TxnRef"] = maGiaoDichGoc,
            ["vnp_Amount"] = TinhSoTien(soTien),
            ["vnp_OrderInfo"] = lyDo,
            ["vnp_TransactionDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CreateBy"] = nguoiThucHien,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_IpAddr"] = "127.0.0.1"
        };

        if (!string.IsNullOrWhiteSpace(maGiaoDichCuaCong))
        {
            p["vnp_TransactionNo"] = maGiaoDichCuaCong;
        }

        return p;
    }

    /// <summary>
    /// **Tham số của `vnp_Command=querydr`** — dùng cho **job đối soát** (`REC-001`/`SAL-006`).
    /// 📌 Vì sao **bắt buộc** có hàm này: IPN/webhook **có thể mất**; nếu không có `querydr` thì giao dịch
    /// đã thu tiền **mãi mãi** ở trạng thái `Pending`, và không ai biết khách đã trả hay chưa.
    /// </summary>
    public static SortedDictionary<string, string> ThamSoTruyVan(
        string tmnCode,
        string maGiaoDichGoc,
        DateTime? thoiDiemUtc = null)
    {
        var now = (thoiDiemUtc ?? DateTime.UtcNow).AddHours(7);

        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"),
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TxnRef"] = maGiaoDichGoc,
            ["vnp_OrderInfo"] = $"Truy van giao dich {maGiaoDichGoc}",
            ["vnp_TransactionDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_IpAddr"] = "127.0.0.1"
        };
    }

    /// <summary>
    /// ⚠️ **VNPay yêu cầu số tiền × 100.** Dùng `decimal` + làm tròn **`AwayFromZero`**, **cấm**
    /// `double`/`float` (`XC-001` **BR-003**). 📌 `AwayFromZero` chứ không phải "ngân hàng": VNPay tính
    /// số nguyên đồng, và làm tròn ngân hàng (`.5 → chẵn`) sẽ cho **1 đồng khác** ⇒ cổng báo *"số tiền không khớp"*.
    /// </summary>
    public static string TinhSoTien(decimal soTien)
        => decimal.Round(soTien * 100m, 0, MidpointRounding.AwayFromZero).ToString("0");

    /// <summary>
    /// **Đọc phản hồi của cổng** (JSON phẳng) ⇒ <see cref="RefundResult"/>.
    ///
    /// 🔒 **Ba bất biến khi đọc phản hồi:**
    /// <list type="number">
    ///   <item>**Kiểm chữ ký trước** — phản hồi không hợp lệ **không** được tin, dù nội dung nói "thành công"
    ///     (nếu tin, kẻ tấn công chỉ cần gửi một JSON `{"vnp_ResponseCode":"00"}` là hệ thống ghi **đã hoàn tiền**);</item>
    ///   <item>**`DaHoanThat` mặc định `false`** — chỉ `true` khi cổng **nói rõ** hoàn tiền thành công;</item>
    ///   <item>**`ChoXacNhan` = `true`** khi cổng **đã nhận** nhưng **chưa** xác nhận tiền ra
    ///     ⇒ **cấm** ghi sổ (xem <see cref="RefundResult"/>).</item>
    /// </list>
    /// </summary>
    /// <param name="thanPhanHoi">Body JSON thô của cổng (VNPay trả **JSON**, khác IPN là query string).</param>
    /// <param name="biMat">`HashSecret` để kiểm chữ ký. ⚠️ Rỗng ⇒ **từ chối** (không thể kiểm thì không tin).</param>
    public static RefundResult DocPhanHoi(string thanPhanHoi, string biMat)
    {
        var kq = new RefundResult();

        if (string.IsNullOrWhiteSpace(thanPhanHoi))
        {
            kq.LyDoThatBai = "Cổng trả phản hồi RỖNG ⇒ không thể kết luận (coi như chưa biết).";

            return kq;
        }

        Dictionary<string, string> truong;

        try
        {
            truong = DocJsonPhang(thanPhanHoi);
        }
        catch (Exception ex)
        {
            // 🚨 **"KHÔNG đọc được phản hồi" ≠ "cổng TỪ CHỐI".** Hai ca này khác hẳn nhau:
            //   • từ chối (mã khác `00`) ⇒ biết chắc cổng **đã xử lý** và **không** hoàn ⇒ an toàn để kết luận;
            //   • KHÔNG đọc được (cổng trả HTML 502, trang lỗi gateway, JSON méo) ⇒ **KHÔNG BIẾT** cổng có
            //     **đã nhận** yêu cầu hay không. Nếu ta trả `ChoXacNhan = false`, tầng gọi **hiểu là**
            //     *"chắc chắn chưa hoàn"* ⇒ có thể **thử lại** ⇒ **hoàn HAI LẦN** — mất tiền thật.
            // ⇒ Đặt `ChoXacNhan = true` **ngay tại đây** để buộc đối soát `querydr` trước mọi lần thử lại.
            // 📌 Đây là ca **đã đo được** (probe `sal-006`): gọi VNPay sandbox không có credential trả về
            // nội dung **không phải JSON** ⇒ `loiHaTang = FormatException`.
            kq.LyDoThatBai = "Phản hồi của cổng KHÔNG phải JSON phẳng hợp lệ ⇒ KHÔNG BIẾT cổng đã nhận " +
                             "yêu cầu hay chưa. CẤM thử lại mù; phải tra `querydr` trước.";
            kq.LoiHaTang = ex.GetType().Name;
            kq.ChoXacNhan = true;

            return kq;
        }

        kq.MaPhanHoi = truong.GetValueOrDefault("vnp_ResponseCode");
        kq.ThongDiep = truong.GetValueOrDefault("vnp_Message");
        kq.MaGiaoDichHoan = truong.GetValueOrDefault("vnp_TransactionNo");

        // ① KIỂM CHỮ KÝ TRƯỚC — không tin nội dung khi chữ ký không hợp lệ.
        if (string.IsNullOrWhiteSpace(biMat))
        {
            kq.LyDoThatBai = "Thiếu HashSecret ⇒ KHÔNG kiểm được chữ ký phản hồi ⇒ không tin phản hồi (fail-closed).";

            return kq;
        }

        if (!KiemChuKy(truong, biMat))
        {
            kq.LyDoThatBai = "Chữ ký phản hồi KHÔNG hợp lệ ⇒ không tin phản hồi (có thể bị giả mạo hoặc lệch khóa).";

            return kq;
        }

        var thanhCong = string.Equals(kq.MaPhanHoi, MaThanhCong, StringComparison.Ordinal);

        kq.DaNhanYeuCau = thanhCong;

        if (thanhCong)
        {
            // 🚨 **ĐIỂM QUYẾT ĐỊNH CỦA CẢ TÍNH NĂNG:** mã `00` nghĩa là **cổng ĐÃ NHẬN** yêu cầu hoàn tiền —
            // **KHÔNG** phải tiền đã ra. VNPay xử lý hoàn tiền **bất đồng bộ** ở phía ngân hàng.
            // Vì vậy: `DaHoanThat = false` + `ChoXacNhan = true` ⇒ tầng gọi **phải** xác nhận bằng `querydr`
            // TRƯỚC khi ghi `RegisterRefund`.
            // 📌 Ghi rõ lý do: nếu ở đây đặt `DaHoanThat = true` thì hệ thống ghi **"đã hoàn"** trong khi
            // tiền **chưa** ra — khách khiếu nại, và **sổ sách nói dối**; tệ nhất là **không ai** theo dõi.
            kq.DaHoanThat = false;
            kq.ChoXacNhan = true;
            kq.ThongDiep ??= "Cổng đã nhận yêu cầu hoàn tiền — CHƯA xác nhận tiền đã ra (phải đối soát).";
        }
        else
        {
            kq.LyDoThatBai = $"Cổng TỪ CHỐI: vnp_ResponseCode={kq.MaPhanHoi ?? "(thiếu)"} · {kq.ThongDiep ?? "(không có thông điệp)"}";
        }

        return kq;
    }

    /// <summary>
    /// Kiểm chữ ký của một tập trường (dùng cho **phản hồi API** dạng JSON, và cho **IPN** dạng query string).
    /// 🔒 **Bỏ** `vnp_SecureHash`/`vnp_SecureHashType` **trước khi** ký (xem chú thích lớp).
    /// </summary>
    public static bool KiemChuKy(IReadOnlyDictionary<string, string> truong, string biMat)
    {
        var nhan = truong.GetValueOrDefault("vnp_SecureHash");

        if (string.IsNullOrWhiteSpace(nhan))
        {
            return false;
        }

        var deKy = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var kv in truong)
        {
            var ten = kv.Key;

            if (ten is "vnp_SecureHash" or "vnp_SecureHashType")
            {
                continue;
            }

            // VNPay **bỏ** tham số rỗng khỏi chuỗi ký ⇒ phải bỏ ở đây cho khớp; nếu không, chữ ký của
            // phản hồi hợp lệ sẽ bị coi là SAI ⇒ hệ thống **từ chối một phản hồi THẬT** (mất tiền mà không biết).
            if (string.IsNullOrEmpty(kv.Value))
            {
                continue;
            }

            deKy[ten] = kv.Value;
        }

        var mongDoi = Ky(biMat, DungChuoiKy(deKy));

        // 🔒 So sánh **Ordinal** (không culture) và **không** dùng `==` trên chuỗi do máy sinh ở chỗ khác.
        return string.Equals(mongDoi, nhan, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Lý do **ĐÓNG** (tập hữu hạn) khi từ chối chữ ký — dùng làm nhãn metric (`NFR-PAY-010`).</summary>
    public static class LyDoTuChoiChuKy
    {
        /// <summary>IPN không có `vnp_SecureHash`.</summary>
        public const string ThieuChuKy = "thieu-chu-ky";

        /// <summary>Query string méo (phần tử không phải `key=value`, hoặc có khoá trùng).</summary>
        public const string QueryKhongHopLe = "query-khong-hop-le";

        /// <summary>Đủ tham số nhưng HMAC tính lại **khác** chữ ký nhận được.</summary>
        public const string ChuKySai = "chu-ky-sai";
    }

    /// <summary>
    /// 🚨 **KIỂM CHỮ KÝ CỦA MỘT IPN — DỰNG CHUỖI KÝ TỪ QUERY **THÔ** (đóng `F-PAY-1`, `S5`).**
    ///
    /// **VÌ SAO PHẢI CÓ HÀM NÀY (và vì sao `VerifyWebhookAsync` cũ SAI):** bản trước của
    /// `VerifyWebhookAsync` tự dựng chuỗi ký bằng cách **giải mã** tham số rồi **encode lại** bằng
    /// `WebUtility.UrlEncode`, và **giữ cả tham số rỗng**. Hai khác biệt so với cách VNPay ký:
    /// <list type="number">
    ///   <item>🚨 **tham số rỗng**: VNPay **bỏ** (đúng như `KiemChuKy` của chính tệp này) ⇒ ta **giữ** ⇒ hash
    ///     khác ⇒ **từ chối IPN THẬT** ⇒ *"tiền đã vào mà sổ không biết"*. 📌 Đây là `F-PAY-1`, mức 🔴 Cao;</item>
    ///   <item>⚠️ **byte của giá trị**: `WebUtility.UrlEncode(' ') = "%20"`, còn cổng gửi `"+"` (hoặc ngược
    ///     lại) ⇒ hash khác dù **nội dung giống hệt**.</item>
    /// </list>
    /// ✅ **Cách đúng:** dùng **nguyên văn** `key=value` mà cổng gửi, chỉ **bỏ** các cặp có giá trị rỗng và hai
    /// khoá `vnp_SecureHash*`, rồi sắp xếp theo **tên khoá** `Ordinal`. Không giải mã, không encode lại.
    ///
    /// 🔒 **Vẫn là FAIL-CLOSED:** thiếu chữ ký, query méo, hoặc `biMat` rỗng ⇒ **`false`** (không bao giờ
    /// "cho qua khi không kiểm được"). 📌 `biMat` rỗng là ca **thật** ở repo này (`HashSecret` = `""`): nếu
    /// vẫn tính HMAC với khoá rỗng thì **bất kỳ ai** cũng ký được một IPN giả ⇒ **giao hàng miễn phí**
    /// (`PAY-001` §7.7.4 #1).
    /// </summary>
    /// <param name="queryTho">Query string **nguyên văn** của IPN (có hoặc không có `?` ở đầu).</param>
    /// <param name="biMat">`HashSecret`. ⚠️ Rỗng ⇒ trả `false` (không thể kiểm thì không tin).</param>
    /// <param name="lyDo">Lý do từ chối (một trong <see cref="LyDoTuChoiChuKy"/>) — `null` khi hợp lệ.</param>
    public static bool KiemChuKyTuQueryTho(string? queryTho, string biMat, out string? lyDo)
    {
        lyDo = null;

        if (string.IsNullOrWhiteSpace(biMat))
        {
            lyDo = LyDoTuChoiChuKy.ChuKySai;

            return false;
        }

        var cap = new List<KeyValuePair<string, string>>();
        string? nhan = null;

        var tho = (queryTho ?? string.Empty).Trim().TrimStart('?');

        if (tho.Length == 0)
        {
            lyDo = LyDoTuChoiChuKy.ThieuChuKy;

            return false;
        }

        foreach (var phan in tho.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var viTriBang = phan.IndexOf('=');

            if (viTriBang <= 0)
            {
                // 🚨 **Không đoán**: một phần tử không phải `key=value` nghĩa là query KHÔNG như cổng gửi ⇒
                // không có cơ sở nào để kết luận chữ ký đúng. Bỏ qua nó là **âm thầm** kiểm một chuỗi khác.
                lyDo = LyDoTuChoiChuKy.QueryKhongHopLe;

                return false;
            }

            var khoaTho = phan[..viTriBang];
            var giaTriTho = phan[(viTriBang + 1)..];

            if (string.Equals(khoaTho, "vnp_SecureHash", StringComparison.Ordinal))
            {
                nhan = WebUtility.UrlDecode(giaTriTho);

                continue;
            }

            if (string.Equals(khoaTho, "vnp_SecureHashType", StringComparison.Ordinal))
            {
                continue;
            }

            // 🔒 Tham số **RỖNG** bị VNPay bỏ khỏi chuỗi ký ⇒ bỏ ở đây (chính là `F-PAY-1`).
            if (giaTriTho.Length == 0)
            {
                continue;
            }

            cap.Add(new KeyValuePair<string, string>(khoaTho, giaTriTho));
        }

        if (string.IsNullOrWhiteSpace(nhan))
        {
            lyDo = LyDoTuChoiChuKy.ThieuChuKy;

            return false;
        }

        // ⚠️ Khoá trùng trong một query string: hành vi "lấy cái cuối" của mọi bộ parse là **không tất định**
        // giữa các tầng ⇒ từ chối thay vì để tầng này đọc khoá đầu còn tầng kia đọc khoá cuối.
        var soKhoa = cap.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count();

        if (soKhoa != cap.Count)
        {
            lyDo = LyDoTuChoiChuKy.QueryKhongHopLe;

            return false;
        }

        var chuoiKy = string.Join("&", cap
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key + "=" + x.Value));

        var hopLe = string.Equals(Ky(biMat, chuoiKy), nhan, StringComparison.OrdinalIgnoreCase);

        if (!hopLe)
        {
            lyDo = LyDoTuChoiChuKy.ChuKySai;
        }

        return hopLe;
    }

    /// <summary>
    /// Đọc **JSON phẳng** (một tầng, giá trị là chuỗi/số/bool) thành từ điển.
    /// 📌 Tự viết thay vì `JsonSerializer.Deserialize<Dictionary<string,object>>` vì phản hồi VNPay là
    /// **JSON phẳng đơn giản**, và cách này **giữ nguyên văn** giá trị (không bị đổi `1.50` → `1.5`,
    /// không bị `null` hoá `""`) — cần thiết để **kiểm chữ ký** khớp với những gì cổng đã ký.
    /// </summary>
    public static Dictionary<string, string> DocJsonPhang(string json)
    {
        var kq = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;

        while (i < json.Length && char.IsWhiteSpace(json[i]))
        {
            i++;
        }

        if (i >= json.Length || json[i] != '{')
        {
            throw new FormatException("JSON phẳng phải bắt đầu bằng `{`.");
        }

        i++;

        while (i < json.Length)
        {
            while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ','))
            {
                i++;
            }

            if (i < json.Length && json[i] == '}')
            {
                return kq;
            }

            var khoa = DocChuoi(json, ref i);

            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }

            if (i >= json.Length || json[i] != ':')
            {
                throw new FormatException($"Thiếu `:` sau khóa `{khoa}`.");
            }

            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }

            kq[khoa] = DocGiaTri(json, ref i);
        }

        throw new FormatException("JSON phẳng không đóng bằng `}`.");
    }

    private static string DocChuoi(string json, ref int i)
    {
        if (i >= json.Length || json[i] != '"')
        {
            throw new FormatException("Khóa JSON phải nằm trong nháy kép.");
        }

        i++;
        var sb = new StringBuilder();

        while (i < json.Length && json[i] != '"')
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                i++;
                sb.Append(json[i] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'b' => '\b',
                    'f' => '\f',
                    _ => json[i]
                });
            }
            else
            {
                sb.Append(json[i]);
            }

            i++;
        }

        if (i >= json.Length)
        {
            throw new FormatException("Chuỗi JSON không đóng.");
        }

        i++;

        return sb.ToString();
    }

    private static string DocGiaTri(string json, ref int i)
    {
        if (i < json.Length && json[i] == '"')
        {
            return DocChuoi(json, ref i);
        }

        if (json.Length >= i + 4 && json.AsSpan(i, 4).SequenceEqual("null"))
        {
            i += 4;

            // ⚠️ `null` ⇒ trả **rỗng** (không trả chuỗi `"null"`): chữ ký của VNPay bỏ tham số rỗng, nên
            // `"null"` sẽ làm chữ ký **lệch** và một phản hồi THẬT bị coi là giả.
            return string.Empty;
        }

        var batDau = i;

        while (i < json.Length && json[i] is not (',' or '}' or ']'))
        {
            i++;
        }

        return json[batDau..i].Trim();
    }
}

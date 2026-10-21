using System.Text.RegularExpressions;
using eCommerce.Kernel.Observability;

namespace eCommerce.Tests.Observability;

/// <summary>
/// 🚨 **CƠ CHẾ CHO "METRIC ĐÃ ĐĂNG KÝ MÀ KHÔNG AI PHÁT"** (`S10`, 21/10/2026).
///
/// **VÌ SAO CÓ LỚP TEST NÀY — sự thật đo được, không phải suy đoán:**
///   • `XC-005` §15–§19 ghi *"12 metric ĐÃ CÓ NƠI PHÁT"* liên tục từ **13/10/2026**;
///   • 📌 nhưng **chưa từng** có phép kiểm nào đối chiếu *"đã đăng ký"* với *"có chỗ gọi thật"*;
///   • 🔑 quét lại `src/` + `modules/` + `shared/` (21/10/2026): **11/15** có điểm phát, **4/15 KHÔNG**
///     (`messaging_outbox_pending` · `messaging_dlq_replayed_total` · `lock_ttl_exceeded_total` ·
///     `logging_failures_total`).
/// ⇒ 🔒 **Con số trong tài liệu đã SAI ít nhất 8 ngày, và nó sai được vì không có cơ chế nào giữ nó đúng.**
/// 📌 Đây đúng là bài học của dự án ở dạng *"observability"*: **một danh sách khai báo không được đo thì
/// là một lời tuyên bố, không phải một bất biến.**
///
/// ✅ **LUẬT (đo được bằng văn bản nguồn — không suy diễn):** mỗi tên trong `eCommerceMetricNames.All` rơi
/// vào **đúng một** trong hai trạng thái, và **cả hai chiều đều phải khớp với mã thật**:
/// <list type="number">
///   <item>**có điểm phát** (`KyHieuPhat`) ⇒ phải tìm thấy **lời gọi thật** `.<KýHiệu>.Add(` /
///     `.<KýHiệu>.Record(` / `.<KýHiệu>(` ở `src`/`modules`/`shared`;</item>
///   <item>**chưa có điểm phát** (`ChuaCoNoiPhat`) ⇒ **KHÔNG** được có lời gọi nào — nếu có thì khai báo
///     **đã cũ** và phải xoá (⚠️ nếu không, danh sách nợ trở thành nơi **giấu** việc đã xong).</item>
/// </list>
/// 🔑 **Vì sao phải kiểm CHIỀU NGƯỢC:** *"mọi thứ đều ổn"* và *"phép quét không thấy gì cả"* **trông giống
/// hệt nhau** nếu chỉ kiểm một phía — bài học đã ghi ở `XC-005` §…/`S9` (ba phép khẳng định của §6.2).
/// </summary>
public class MetricEmitterTests
{
    [Fact]
    public void MOI_metric_da_dang_ky_PHAI_co_DIEM_PHAT__hoac_khai_bao_la_CHUA_CO()
    {
        var tatCaMa = DocTatCaMaNguon();

        var coNoiPhat = new List<string>();
        var khaiBaoChuaCo = new List<string>();

        foreach (var ten in eCommerceMetricNames.All)
        {
            // 📌 Mọi tên PHẢI có ký hiệu để tìm — nếu thiếu thì phép đo không thể chạy (và test phải ĐỎ
            // thay vì lặng lẽ bỏ qua tên đó).
            if (!eCommerceMetricNames.KyHieuMetric.TryGetValue(ten, out var kyHieu))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Metric '{ten}' KHONG co ky hieu trong `eCommerceMetricNames.KyHieuMetric` ⇒ "
                    + "khong the do 'co noi phat hay khong'. Them mot dong vao bang do.");
            }

            var dangDuocPhat = CoLoiGoiThat(tatCaMa, kyHieu);
            var daKhaiBaoChuaCo = eCommerceMetricNames.ChuaCoNoiPhat.ContainsKey(ten);

            if (dangDuocPhat)
            {
                coNoiPhat.Add(ten);
            }

            if (daKhaiBaoChuaCo)
            {
                khaiBaoChuaCo.Add(ten);
            }
        }

        // 🚨 **CHIỀU 1:** khai "CHƯA CÓ" mà mã đã có lời gọi ⇒ 🔒 khai báo CŨ ⇒ phải xoá khỏi danh sách nợ.
        var khaiCu = coNoiPhat.Intersect(khaiBaoChuaCo, StringComparer.Ordinal).ToList();

        // 🚨 **CHIỀU 2:** KHÔNG khai "chưa có" mà mã cũng KHÔNG có lời gọi ⇒ 🔒 **metric ma** — đúng thứ
        // tài liệu đã nói dối về nó trong 8 ngày.
        var metricMa = eCommerceMetricNames.All
            .Where(ten => !coNoiPhat.Contains(ten, StringComparer.Ordinal))
            .Where(ten => !khaiBaoChuaCo.Contains(ten, StringComparer.Ordinal))
            .ToList();

        Assert.True(khaiCu.Count == 0 && metricMa.Count == 0,
            "🚨 BANG METRIC KHONG KHOP VOI MA THAT:\n"
            + (khaiCu.Count > 0
                ? "  • Khai 'CHUA CO NOI PHAT' nhung DA CO loi goi (⇒ xoa khoi `ChuaCoNoiPhat`):\n    "
                  + string.Join("\n    ", khaiCu) + "\n"
                : string.Empty)
            + (metricMa.Count > 0
                ? "  • Dang ky nhung KHONG co loi goi nao, va cung KHONG khai bao (⇒ them diem phat, "
                  + "hoac them vao `ChuaCoNoiPhat` kem LY DO):\n    "
                  + string.Join("\n    ", metricMa)
                : string.Empty));
    }

    [Fact]
    public void Bang_ky_hieu_phai_PHU_DUNG_All__va_bang_no_phai_la_TAP_CON_cua_All()
    {
        var kyHieu = eCommerceMetricNames.KyHieuMetric.Keys.ToList();
        var chuaCo = eCommerceMetricNames.ChuaCoNoiPhat.Keys.ToList();

        // 📌 **Bảng ký hiệu phải PHỦ ĐÚNG `All`**: thiếu ⇒ không đo được tên đó; thừa ⇒ ký hiệu mồ côi
        // (metric đã bị xoá mà bảng còn) ⇒ 🔒 một ngày nào đó phép quét sẽ "thấy" một chuỗi trùng tên
        // ở chỗ khác và báo "có nơi phát" **cho một metric không còn tồn tại**.
        Assert.Equal(
            eCommerceMetricNames.All.OrderBy(x => x, StringComparer.Ordinal),
            kyHieu.OrderBy(x => x, StringComparer.Ordinal));

        // 📌 Bảng nợ phải là **tập con** — ⚠️ không được chứa tên không có thật (gõ sai tên ⇒ "đã khai báo
        // nợ" cho một metric không tồn tại, và tên thật thì **không** được khai ⇒ ca 1 sẽ ĐỎ, nhưng thông
        // điệp ở đây mới chỉ đúng chỗ sai).
        var tenLa = chuaCo.Except(eCommerceMetricNames.All, StringComparer.Ordinal).ToList();

        Assert.True(tenLa.Count == 0,
            "Bang `ChuaCoNoiPhat` co ten KHONG co trong `All` (⇒ go sai ten, hoac metric da bi xoa):\n  "
            + string.Join("\n  ", tenLa));

        // 🔑 Và **bảng nợ KHÔNG được rỗng một cách vô nghĩa** nếu thực tế còn nợ — 📌 nhưng cũng **không**
        // ép nó phải khác rỗng: nếu ai đó đóng hết nợ (phát đủ 15/15) thì danh sách **nên** rỗng.
        // ⇒ Chỉ kiểm **tính nhất quán**, không kiểm con số.
    }

    [Fact]
    public void Pham_vi_quet_KHONG_duoc_thu_hep__phai_thay_DU_cac_noi_phat_da_biet()
    {
        // 🚨 **Chống bài học "phép đo bất ngờ thường là phép đo SAI":** nếu `TimGocRepo`/đường dẫn/việc lọc
        // `bin`-`obj` bị hỏng thì các ca trên sẽ **XANH một cách VÔ NGHĨA** (mọi metric đều "chưa có điểm
        // phát" ⇒ ca chiều 2 ĐỎ, nhưng nếu ai đó "sửa" bằng cách khai hết vào bảng nợ thì XANH giả).
        // 📌 Bắt buộc: phép quét phải thấy **các tệp đã biết** là nơi phát.
        var tatCaMa = DocTatCaMaNguon();

        string[] batBuoc =
        [
            "IdempotencyGuard.cs",                 // messaging_inbox_failed_total
            "MarketplaceReconciliationWorker.cs",  // messaging_dlq_size
            "ChannelTokenManager.cs",              // lock_timeout_total
            "TaxUnclassifiedSkusWorker.cs",         // lock_skipped_total + tax_unclassified_skus
            "RequestDurationRecorder.cs",           // http_request_duration_seconds  (trong `shared/` — ⚠️ dễ bị bỏ sót)
            "VnPayPaymentProvider.cs",              // payment_webhook_rejected_total
            "ChannelShopSyncer.cs",                 // channel_sync_failed_total
            "InventoryReconciliationService.cs",     // inventory_recon_mismatch_skus
            "PaymentReconciliationWorker.cs"        // payment_recon_unresolved_total
        ];

        var tenTep = tatCaMa.Select(x => x.TenTep).ToHashSet(StringComparer.Ordinal);
        var thieu = batBuoc.Where(t => !tenTep.Contains(t)).ToList();

        Assert.True(thieu.Count == 0,
            "Phep quet KHONG thay cac tep da biet la NOI PHAT ⇒ pham vi quet bi thu hep ⇒ cac ca tren VO NGHIA:\n  "
            + string.Join("\n  ", thieu));

        // 📌 Và **số metric có điểm phát** phải đạt ngưỡng đã đo (11 ở `S10`). 🔑 Kiểm **ngưỡng dưới**
        // (không kiểm số cứng) để thêm metric hợp lệ không làm đỏ, nhưng mất điểm phát thì phải đỏ.
        var soCoNoiPhat = eCommerceMetricNames.All.Count(ten =>
            CoLoiGoiThat(tatCaMa, eCommerceMetricNames.KyHieuMetric[ten]));

        Assert.True(soCoNoiPhat >= 11,
            $"Chi thay {soCoNoiPhat} metric co diem phat (da do it nhat 11 o `S10`) ⇒ phep quet hong, "
            + "hoac co diem phat vua bi xoa ma khong ai biet.");
    }

    /// <summary>
    /// Tìm **lời gọi thật** của một thành viên metric trong mã sản phẩm.
    /// 📌 Ba hình dạng hợp lệ: counter `.Add(` · histogram `.Record(` · gauge (setter) `(`.
    /// 🔑 **Neo theo `.` + tên + (dấu gọi)** — ⚠️ **không** dùng `Contains(tên)`: `LockTimeout` là **tiền tố**
    /// của `LockTimeoutSeconds`, 📌 nên `Contains` sẽ báo "có điểm phát" cho một metric **không ai phát**
    /// — đúng loại **phép đo sai** đã cắn dự án nhiều lần (`IndexOf` khớp chuỗi con, `-SimpleMatch`…).
    /// </summary>
    private static bool CoLoiGoiThat(List<(string TenTep, string NoiDung)> tatCaMa, string kyHieu)
    {
        var mau = new Regex(
            @"\." + Regex.Escape(kyHieu) + @"\s*(?:\.\s*(?:Add|Record)\s*\(|\()",
            RegexOptions.CultureInvariant);

        return tatCaMa.Any(x => mau.IsMatch(x.NoiDung));
    }

    // ── Đọc nguồn ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Quét **cả `shared/`** — 🔑 `RequestDurationRecorder` (nơi phát `http_request_duration_seconds`)
    /// nằm ở đó, 📌 nên quét `src` + `modules` như lớp test worker là **thiếu một điểm phát thật** (đã đo:
    /// bỏ `shared` ⇒ 10/15 thay vì 11/15 — suýt kết luận sai rằng metric này "không có nơi phát").
    /// </summary>
    private static List<(string TenTep, string NoiDung)> DocTatCaMaNguon()
    {
        var goc = TimGocRepo();
        var ket = new List<(string, string)>();

        foreach (var thuMuc in new[] { "src", "modules", "shared" })
        {
            var duongDan = Path.Combine(goc, thuMuc);

            if (!Directory.Exists(duongDan))
            {
                continue;
            }

            foreach (var tep in Directory.EnumerateFiles(duongDan, "*.cs", SearchOption.AllDirectories))
            {
                if (tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                // ⚠️ **BỎ CHÍNH TỆP BẢNG ĐĂNG KÝ**: nó `new`/`Create*` mọi instrument ⇒ nếu quét nó thì
                // **mọi** tên đều "có điểm phát" và phép kiểm **luôn xanh** (xanh vì lý do sai).
                if (string.Equals(Path.GetFileName(tep), "eCommerceMetrics.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                ket.Add((Path.GetFileName(tep), BoChuThich(File.ReadAllText(tep))));
            }
        }

        return ket;
    }

    /// <summary>
    /// 🔒 **BỎ CHÚ THÍCH TRƯỚC KHI ÁP LUẬT** — 📌 bài học **đã trả giá** ở `BackgroundWorkerCoCheTests`
    /// (mutation test vòng 37): luật so khớp chuỗi bị **thoả mãn bởi một DÒNG COMMENT** ⇒ cơ chế **bị vô
    /// hiệu mà không ai biết**. ⚠️ Đánh đổi: cách cắt **thô** (không hiểu chuỗi ký tự) — chấp nhận được vì
    /// cắt nhầm chỉ làm luật **CHẶT hơn** (dễ ĐỎ), không làm nó **lỏng đi**.
    /// </summary>
    private static string BoChuThich(string ma)
    {
        var khongKhoi = Regex.Replace(ma, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        return string.Join(
            '\n',
            khongKhoi.Split('\n').Select(dong =>
            {
                var viTri = dong.IndexOf("//", StringComparison.Ordinal);
                return viTri >= 0 ? dong[..viTri] : dong;
            }));
    }

    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Không tìm thấy gốc repo (mốc `eCommerce.slnx`) khi đi ngược từ '{AppContext.BaseDirectory}'.");
    }
}

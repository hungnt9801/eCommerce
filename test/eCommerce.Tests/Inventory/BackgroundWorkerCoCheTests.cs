using System.Text.RegularExpressions;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🚨 **CƠ CHẾ CHO "JOB IM LẶNG"** — mọi `AsyncPeriodicBackgroundWorkerBase` phải **ĐƯỢC ĐĂNG KÝ**
/// và **LOG VÔ ĐIỀU KIỆN**.
///
/// **VÌ SAO — vòng 35 đã TRẢ GIÁ cho cả hai** (`inv-001` **§27**):
///   • **NN1:** dòng `AddBackgroundWorkerAsync<StockReservationExpiryWorker>()` **không hề được ghi** vào tệp
///     (script sửa tệp báo *"thành công"*) ⇒ worker **không bao giờ chạy** ⇒ **hàng bị giữ vĩnh viễn**;
///   • **NN2:** worker **chạy nhưng chỉ log khi có việc** ⇒ *"im lặng"* **không** phân biệt được
///     *"không chạy"* với *"chạy mà không có việc"* ⇒ **hai phép kiểm chứng liên tiếp vô nghĩa**.
///
/// 📌 **Luật chỉ áp cho hai điều KIỂM ĐƯỢC BẰNG VĂN BẢN NGUỒN** (không phải suy diễn):
///   (1) **có đăng ký** `AddBackgroundWorkerAsync<TênWorker>` ở **đâu đó** trong `src/` hoặc `modules/`;
///   (2) **có log vô điều kiện** (`Logger.LogInformation`/`Logger.LogWarning`).
/// ⚠️ **KHÔNG** đưa luật *"phải có `UnitOfWork`"* vào đây: nó chỉ đúng cho worker **có chạm DB**, mà
/// *"có chạm DB"* **không** xác định được đáng tin bằng văn bản nguồn ⇒ luật sẽ **báo sai** và bị **vô hiệu hoá**.
/// 🚨 **Nhưng khảo sát vòng 36 cho thấy 5/6 worker KHÔNG có `UnitOfWork`** — xem `inv-001` **§28.3**
/// (rủi ro tiềm ẩn `ObjectDisposedException`), cần xử lý riêng theo từng worker.
/// </summary>
public class BackgroundWorkerCoCheTests
{
    private const string TenLopCoSo = "AsyncPeriodicBackgroundWorkerBase";

    [Fact]
    public void MOI_worker_phai_DUOC_DANG_KY__neu_khong_thi_no_KHONG_BAO_GIO_chay()
    {
        var tatCaMa = DocTatCaMaNguon();

        var thieu = new List<string>();

        foreach (var (tenTep, noiDung) in tatCaMa)
        {
            if (!noiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            {
                continue;
            }

            var tenLop = LayTenLop(noiDung);

            if (tenLop is null)
            {
                thieu.Add($"{tenTep}: KHÔNG đọc được tên lớp worker");
                continue;
            }

            // Tìm ở MỌI tệp nguồn (đăng ký có thể nằm ở module khác với nơi khai lớp).
            var coDangKy = tatCaMa.Any(x =>
                Regex.IsMatch(x.NoiDung, $@"AddBackgroundWorkerAsync<\s*[\w.]*{Regex.Escape(tenLop)}\s*>"));

            if (!coDangKy)
            {
                thieu.Add($"{tenLop} (khai ở {tenTep})");
            }
        }

        Assert.True(thieu.Count == 0,
            "🚨 WORKER KHÔNG ĐƯỢC ĐĂNG KÝ ⇒ nó **KHÔNG BAO GIỜ CHẠY** mà không có lỗi nào:\n  "
            + string.Join("\n  ", thieu)
            + "\n\n⇒ Thêm `await context.AddBackgroundWorkerAsync<TênWorker>();` vào module "
            + "(xem `eCommerceApplicationModule`). 📌 Đây chính là khuyết điểm đã gây ra sự cố vòng 35 (NN1).");
    }

    [Fact]
    public void MOI_worker_phai_LOG_VO_DIEU_KIEN__de_im_lang_LUON_co_nghia_la_KHONG_CHAY()
    {
        var tatCaMa = DocTatCaMaNguon();
        var thieu = new List<string>();

        foreach (var (tenTep, noiDung) in tatCaMa)
        {
            if (!noiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            {
                continue;
            }

            var coLog = Regex.IsMatch(noiDung, @"Logger\.Log(Information|Warning)\(");

            if (!coLog)
            {
                thieu.Add($"{LayTenLop(noiDung) ?? tenTep} (khai ở {tenTep})");
            }
        }

        Assert.True(thieu.Count == 0,
            "🚨 WORKER KHÔNG LOG VÔ ĐIỀU KIỆN ⇒ *\"im lặng\"* **KHÔNG** phân biệt được "
            + "*\"job không chạy\"* với *\"job chạy mà không có việc\"* ⇒ mọi phép kiểm chứng job **mất giá trị**:\n  "
            + string.Join("\n  ", thieu)
            + "\n\n⇒ Thêm `Logger.LogInformation(\"JOB …: bắt đầu quét …\")` ở **ĐẦU** `DoWorkAsync` "
            + "(📌 đúng một dòng này đã phát hiện worker chạy-rồi-ném ở vòng 35 — NN2).");
    }

    [Fact]
    public void Pham_vi_quet_KHONG_duoc_thu_hep_im_lang__phai_thay_DU_worker_da_biet()
    {
        // 🚨 Chống bài học C16: nếu phép quét hỏng (đổi cấu trúc thư mục, lọc sai) thì các test trên
        // sẽ **XANH một cách VÔ NGHĨA**. Bất biến: các worker **đang tồn tại** PHẢI được nhìn thấy.
        var tatCaMa = DocTatCaMaNguon();
        var tenWorker = tatCaMa
            .Where(x => x.NoiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            .Select(x => LayTenLop(x.NoiDung))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.Ordinal);

        string[] batBuoc =
        [
            "StockReservationExpiryWorker",
            "TaxUnclassifiedSkusWorker",
            "ChannelSyncSupervisorWorker"
        ];

        var thieu = batBuoc.Where(t => !tenWorker.Contains(t)).ToList();

        Assert.True(thieu.Count == 0,
            "Phép quét KHÔNG thấy các worker đã biết ⇒ phạm vi quét bị thu hẹp ⇒ test trên VÔ NGHĨA:\n  "
            + string.Join("\n  ", thieu));

        Assert.True(tenWorker.Count >= 6,
            $"Chỉ thấy {tenWorker.Count} worker (đã biết ít nhất 6) ⇒ phép quét hỏng.");
    }

    // ── Đọc nguồn ────────────────────────────────────────────────────────────

    private static string LayTenLop(string noiDung)
    {
        var m = Regex.Match(noiDung, @"class\s+(\w+)\s*:\s*[\w.]*" + TenLopCoSo);
        return m.Success ? m.Groups[1].Value : null!;
    }

    private static List<(string TenTep, string NoiDung)> DocTatCaMaNguon()
    {
        var goc = TimGocRepo();
        var ket = new List<(string, string)>();

        foreach (var thuMuc in new[] { "src", "modules" })
        {
            var duongDan = Path.Combine(goc, thuMuc);

            if (!Directory.Exists(duongDan))
            {
                continue;
            }

            foreach (var tep in Directory.EnumerateFiles(duongDan, "*.cs", SearchOption.AllDirectories))
            {
                // Bỏ thư mục build (`obj`/`bin`) — chứa bản sao sinh tự động, gây nhiễu và trùng lặp.
                if (tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                ket.Add((Path.GetFileName(tep), File.ReadAllText(tep)));
            }
        }

        return ket;
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

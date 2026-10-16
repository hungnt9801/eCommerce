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
            "ChannelSyncSupervisorWorker",
            "MarketplaceReconciliationWorker",

            // 🚨 `REC-001` muc 7 (17/10/2026) — **JOB DOI SOAT THANH TOAN DINH KY**. 📌 Dua vao danh sach
            // BAT BUOC vi day la job **tra tien**: neu no **im lang** khong chay thi **IPN mat** => giao dich
            // ket `Pending` => 🔑 **tien da vao ma he thong khong biet** - va **khong co loi nao**.
            "PaymentReconciliationWorker"
        ];

        var thieu = batBuoc.Where(t => !tenWorker.Contains(t)).ToList();

        Assert.True(thieu.Count == 0,
            "Phép quét KHÔNG thấy các worker đã biết ⇒ phạm vi quét bị thu hẹp ⇒ test trên VÔ NGHĨA:\n  "
            + string.Join("\n  ", thieu));

        Assert.True(tenWorker.Count >= 8,
            $"Chỉ thấy {tenWorker.Count} worker (đã biết ít nhất 8) ⇒ phép quét hỏng.");
    }

    [Fact]
    public void Worker_KHONG_duoc_goi_APP_SERVICE_co_Authorize__se_nem_AbpAuthorizationException()
    {
        // 🚨 **LUẬT NÀY RÚT TỪ MỘT LỖI ĐO ĐƯỢC NGÀY 16/10/2026** — khi xây job `D12`
        // (`MarketplaceReconciliationWorker`).
        //
        // **Chuyện đã xảy ra:** worker gọi `IDeadLetterAppService.GetThongKeAsync()`. Vì `DeadLetterAppService`
        // mang `[Authorize(...)]` ở **cấp class** (quy ước #4 — `TC01` **bắt buộc** điều đó), interceptor của ABP
        // kiểm quyền **NGAY CẢ KHI GỌI TRONG TIẾN TRÌNH** — mà worker **KHÔNG có principal** ⇒ **`AbpAuthorizationException`**.
        //
        // 📌 **Hệ quả ĐO ĐƯỢC:** job **chết giữa đường**; log chỉ có neo mở đầu và **KHÔNG** có neo kết thúc.
        // 🔑 **Và nguy hiểm hơn:** job chết như vậy **không báo cho ai** — chỉ đọc log mới thấy.
        // ⚠️ **Tệ nhất:** trong lượt đó, các ca kiểm *"job không ghi gì"* **vẫn XANH** ⇒ chúng xanh **vì lý do SAI**
        // (job không ghi vì nó **chết**, không phải vì nó **cố ý** không ghi). 🔑 Đây là lý do phải có **neo kết thúc**
        // **và** luật này.
        //
        // 🔒 **Cách đúng:** worker đọc **repository** trực tiếp (không qua tầng phân quyền); **người dùng** đi qua
        // app service. 📌 Cùng họ với `CatMediaController` (`CAT-003` §22): `[AllowAnonymous]` gọi app service
        // `[Authorize]` ⇒ interceptor chặn **trong tiến trình**.
        var tatCaMa = DocTatCaMaNguon();
        var viPham = new List<string>();

        foreach (var (tenTep, noiDung) in tatCaMa)
        {
            if (!noiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            {
                continue;
            }

            // App service là nơi quy ước #4 đặt `[Authorize]` ⇒ resolve nó trong worker là mầm lỗi.
            var m = Regex.Match(noiDung, @"GetRequiredService<\s*(\w*AppService)\s*>");

            if (m.Success)
            {
                viPham.Add($"{LayTenLop(noiDung) ?? tenTep} (khai ở {tenTep}) gọi '{m.Groups[1].Value}'");
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 WORKER gọi APP SERVICE (có `[Authorize]` ở cấp class) ⇒ `AbpAuthorizationException` vì worker "
            + "KHÔNG có principal ⇒ JOB CHẾT GIỮA ĐƯỜNG mà không báo cho ai:\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ Worker phải đọc REPOSITORY trực tiếp (không qua tầng phân quyền)."
            + "\n📌 Lỗi này ĐÃ ĐO ĐƯỢC (16/10/2026), và nó làm các ca \"job không ghi gì\" XANH VÌ LÝ DO SAI.");
    }

    [Fact]
    public void Worker_resolve_SERVICE_MIEN_thi_PHAI_co_UnitOfWork__luat_theo_BANG_CHUNG_do_duoc()
    {
        // 🚨 **LUẬT NÀY ĐƯỢC RÚT TỪ BẰNG CHỨNG ĐO ĐƯỢC — KHÔNG phải suy đoán.**
        //
        // **Đo được (vòng 35):** `StockReservationExpiryWorker` resolve `StockReservationService` (dịch vụ **miền**)
        // mà không có UoW ⇒ `ObjectDisposedException: Cannot access a disposed context instance`.
        //
        // **Đo được (vòng 37) — ĐÍNH CHÍNH một suy đoán trước đó:** `TaxUnclassifiedSkusWorker` resolve
        // **`IRepository<…>`** mà **KHÔNG** có UoW, và **nó CHẠY ĐƯỢC**: log ghi
        // *"Đếm SKU chưa phân loại thuế: 0/0…"* và suốt phiên chạy **`fail: 0`**.
        // ⇒ Lỗi **không** đến từ việc resolve `IRepository<>`; nó đến từ việc resolve **`*Service`** (đối tượng
        // **giữ repository/DbContext đã tạo ở scope khác**) rồi dùng nó ngoài UoW.
        //
        // 📌 **Vì sao luật phải HẸP:** luật rộng *"mọi worker chạm DB phải có UoW"* sẽ **báo sai** cho
        // `TaxUnclassifiedSkusWorker` ⇒ bị **vô hiệu hoá** (người ta thêm ngoại lệ cho tới khi test vô nghĩa).
        // Luật hẹp thì **ít ồn** và **vẫn chặn đúng** hình mẫu đã gây sự cố.
        var tatCaMa = DocTatCaMaNguon();
        var viPham = new List<string>();

        foreach (var (tenTep, noiDung) in tatCaMa)
        {
            if (!noiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            {
                continue;
            }

            // `GetRequiredService<XxxService>()` — dịch vụ MIỀN (không phải `IRepository<…>`).
            var coResolveService = Regex.IsMatch(noiDung, @"GetRequiredService<\s*\w*Service\s*>");
            var coUow = noiDung.Contains("IUnitOfWorkManager", StringComparison.Ordinal);

            if (coResolveService && !coUow)
            {
                viPham.Add($"{LayTenLop(noiDung) ?? tenTep} (khai ở {tenTep})");
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 WORKER resolve DỊCH VỤ MIỀN mà KHÔNG có `UnitOfWork` ⇒ sẽ ném "
            + "`ObjectDisposedException: Cannot access a disposed context instance` ⇒ **job không làm gì cả "
            + "mà không ai biết** (ĐÃ ĐO ĐƯỢC ở vòng 35):\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ Bọc công việc trong:\n"
            + "   `using var scope = workerContext.ServiceProvider.CreateScope();`\n"
            + "   `var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()"
            + ".Begin(requiresNew: true, isTransactional: true);`\n"
            + "   … lấy service từ `scope` … `await uow.CompleteAsync();`");
    }

    [Fact]
    public void Worker_phai_LOG_NGAY_CAU_LENH_DAU_TIEN_cua_DoWorkAsync__khong_duoc_log_trong_DIEU_KIEN()
    {
        // 🚨 **LUẬT SIẾT (vòng 53) — vì luật cũ QUÁ YẾU:** luật trước chỉ đòi `Logger.Log*` **tồn tại Ở ĐÂU ĐÓ**
        // trong tệp ⇒ nó **XANH** ngay cả khi dòng log nằm **BÊN TRONG một điều kiện** — tức **không** bảo đảm
        // *"log vô điều kiện ở đầu mỗi lượt"* như luật **muốn nói**.
        // 📌 **ĐO ĐƯỢC (vòng 51–52):** **4/7 worker** có dòng log đầu tiên **không** nằm ở đầu `DoWorkAsync`, và
        // **3/7 job im lặng hoàn toàn** trong log khởi động ⇒ **không thể** trả lời *"job có chạy không?"*
        // (chính câu hỏi đã làm mất **2 vòng đo** ở vòng 35).
        // 📌 Một cơ chế **tưởng** đã bảo vệ mà **thực tế không** thì **tệ hơn không có cơ chế**: nó tạo
        // **cảm giác an toàn giả** (Phụ lục C §C.4 · `inv-001` §29.3 · §43.4).
        var tatCaMa = DocTatCaMaNguon();
        var viPham = new List<string>();

        foreach (var (tenTep, noiDung) in tatCaMa)
        {
            if (!noiDung.Contains(TenLopCoSo, StringComparison.Ordinal))
            {
                continue;
            }

            var tenLop = LayTenLop(noiDung) ?? tenTep;
            var than = LayThanDoWork(noiDung);

            if (than is null)
            {
                viPham.Add($"{tenLop} (khai ở {tenTep}): KHÔNG đọc được thân `DoWorkAsync`");
                continue;
            }

            var cauDau = than.FirstOrDefault(d => d.Trim().Length > 0);

            // 📌 Câu lệnh đầu tiên (sau khi BỎ chú thích — `DocTatCaMaNguon` đã bỏ) PHẢI là một lời gọi log.
            if (cauDau is null || !cauDau.TrimStart().StartsWith("Logger.Log", StringComparison.Ordinal))
            {
                viPham.Add($"{tenLop} (khai ở {tenTep}): câu lệnh ĐẦU TIÊN là `{cauDau?.Trim() ?? "(rỗng)"}`");
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 WORKER LOG **BÊN TRONG ĐIỀU KIỆN** (hoặc không log ở đầu) ⇒ *\"im lặng\"* **KHÔNG** phân biệt được "
            + "*\"job không chạy\"* với *\"job chạy mà không có việc\"*:\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ Đặt `Logger.LogInformation(\"JOB …: bắt đầu một lượt (mốc {Moc}).\", DateTime.UtcNow);` "
            + "thành **CÂU LỆNH ĐẦU TIÊN** của `DoWorkAsync`, **trước** mọi `if`/`foreach`/truy vấn.");
    }

    /// <summary>
    /// Thân `DoWorkAsync`: các dòng **sau** dấu `{` mở thân, lấy **15 dòng đầu** (đủ để thấy câu lệnh đầu tiên).
    /// ⚠️ Cách đọc **thô** (không parse C#) — chấp nhận được cho **luật văn bản nguồn**, và **an toàn theo hướng
    /// chặt hơn**: đọc sai chỉ làm luật **dễ ĐỎ**, không làm nó **lỏng đi**.
    /// </summary>
    private static List<string>? LayThanDoWork(string noiDung)
    {
        var dong = noiDung.Split('\n');

        var viTriChuKy = Array.FindIndex(dong, d => d.Contains("override async Task DoWorkAsync", StringComparison.Ordinal));

        if (viTriChuKy < 0)
        {
            return null;
        }

        var viTriMo = Array.FindIndex(dong, viTriChuKy, d => d.Trim() == "{");

        if (viTriMo < 0)
        {
            return null;
        }

        return dong.Skip(viTriMo + 1).Take(15).ToList();
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

                ket.Add((Path.GetFileName(tep), BoChuThich(File.ReadAllText(tep))));
            }
        }

        return ket;
    }

    /// <summary>
    /// 🚨 **BỎ CHÚ THÍCH TRƯỚC KHI ÁP LUẬT** — 🎯 **do chính MUTATION TEST phát hiện (vòng 37)**:
    /// luật dùng **so khớp chuỗi** nên bị **THOẢ MÃN BỞI MỘT DÒNG COMMENT**. Khi thử nghiệm đột biến
    /// (bỏ `UnitOfWork` khỏi worker), tôi để lại chuỗi gốc trong **comment** `// [MUTATION] …` ⇒
    /// luật **vẫn xanh** ⇒ cơ chế **bị vô hiệu mà không ai biết** — đúng loại lỗi mà cả lớp test này tồn tại để chặn.
    /// ⇒ Cắt `//` và `/* */` trước khi kiểm.
    /// ⚠️ **Đánh đổi đã biết:** cách cắt này **thô** (không hiểu chuỗi ký tự) ⇒ nếu ai viết `"//"` **trong một chuỗi**
    /// thì phần sau bị cắt nhầm. Với **luật văn bản nguồn** thì đánh đổi này chấp nhận được — và **an toàn theo
    /// hướng ngược lại**: cắt nhầm chỉ làm luật **chặt hơn** (dễ ĐỎ), không làm nó **lỏng đi** (bỏ sót).
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

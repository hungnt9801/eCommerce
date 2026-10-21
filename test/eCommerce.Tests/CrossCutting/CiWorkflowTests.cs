using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **CI THẬT (`XC-007` · `S9`) — PIPELINE PHẢI MẠNH HƠN NGƯỜI VỘI.**
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO FILE NÀY TỒN TẠI:**
///
/// 🔑 Một workflow YAML **không có lỗi biên dịch nào**. Mọi cách làm yếu cổng đều *"chạy được"*:
///
/// ① 🚨 **Gọi cổng THIẾU `-DayDu`** — `kiem-tat-ca.ps1` chỉ chạy bước ⑧ (E2E) khi có `-DayDu`; nhánh còn
///   lại **in `[BO QUA]` rồi đi tiếp tới `exit 0`**. ⇒ Cùng **một mã thoát**, chỉ khác **một dòng log**.
///   📌 Đây là *"cổng luôn-xanh"* đúng nghĩa (`XC-007` §12 E-02) và **không** có gì đỏ lên.
///
/// ② 🚨 **`continue-on-error: true`** trên step cổng ⇒ đỏ thành xanh, **không ai thấy** (BR-004).
///
/// ③ 🚨 **Ống dẫn output của lệnh cổng** (`... | Tee-Object`) ⇒ `$LASTEXITCODE` là của **lệnh cuối trong
///   ống dẫn** (thường `0`) ⇒ *"tất cả xanh"* trên một lần chạy **ĐỎ**. 🗝 Đúng quy tắc ① của chính
///   `tools/kiem-tat-ca.ps1`, nay áp cho cả workflow.
///
/// ④ 🚨 **Thiếu BUILD LÀM NÓNG** ⇒ trên runner sạch `-GhiNho` trả `exit 2` ⇒ CI đỏ **ngày đầu** vì
///   **môi trường** (`XC-007` F2).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (ghi rõ, không giấu):** đây là phép quét **văn bản YAML**. Nó bảo đảm
/// *"workflow không chứa các mẫu làm yếu cổng"*, **không** bảo đảm *"pipeline chạy xanh trên Linux"*.
/// 📌 Việc thứ hai phải đo bằng **một lần chạy thật** (xem `XC-007` §16).
///
/// 🚨 **BÀI HỌC `S8` — CHÚ THÍCH CŨNG BỊ QUÉT:** nếu quét thô, chính **chú thích cảnh báo**
/// (*"KHÔNG dùng `continue-on-error`"*, *"không dùng `services:`"*) sẽ **tự tố cáo** và test đỏ vì
/// **lý do sai**. ⇒ Phép quét bỏ **dòng chú thích YAML** (`#…`) trước khi so — chú thích không chạy.
/// </summary>
public class CiWorkflowTests
{
    /// <summary>Mẫu chuỗi bị CẤM trong workflow (BR-002, §6.3).</summary>
    private static readonly string[] ThamSoBiCam =
    {
        "-BoQuaIntegration",
        "-BoQuaE2E",
        "-ChoPhepRong",
        "continue-on-error",
        "pull_request_target"
    };

    [Fact]
    public void Phep_quet_KHONG_rong__va_workflow_phai_ton_tai()
    {
        var goc = TimGocRepo();
        var thuMuc = Path.Combine(goc, ".github", "workflows");

        // 🛡️ LƯỚI AN TOÀN CHO CHÍNH PHÉP QUÉT: quét một thư mục không tồn tại = "không tìm thấy chuỗi cấm"
        // = ĐẠT một cách vô nghĩa. 📌 Đúng loại "cảm giác an toàn giả" mà `Allowpoint…`-style test đã cảnh báo.
        Assert.True(Directory.Exists(thuMuc), $"KHÔNG TÌM THẤY `.github/workflows/` ⇒ phép quét VO NGHIA (từ {goc}).");

        var tep = DocTatCaWorkflow(goc);

        Assert.True(tep.Count > 0, "`.github/workflows/` rỗng ⇒ phép quét VO NGHIA.");
        Assert.True(tep.Values.Sum(v => v.Length) > 1_000, "Workflow quá ngắn ⇒ phép quét VO NGHIA.");

        // 🔒 Neo nội dung: đây phải là workflow **của dự án này**, không phải một tệp mẫu cùng tên.
        Assert.Contains("kiem-tat-ca.ps1", NoiDungKhongChuThich(tep));
        Assert.Contains("cong-12-buoc", NoiDungKhongChuThich(tep));
    }

    /// <summary>
    /// 🚨 **TC-18 — KHÔNG tham số nào làm YẾU cổng** (BR-002/BR-004, `XC-007` §6.3).
    /// </summary>
    [Fact]
    public void TC18_khong_co_tham_so_lam_yeu_cong()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        var viPham = ThamSoBiCam
            .Where(c => vanBan.Contains(c, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0,
            "🚨 Workflow chứa tham số/cấu hình BỊ CẤM (làm yếu cổng — BR-002/BR-004):\n  " +
            string.Join("\n  ", viPham));
    }

    /// <summary>
    /// 🚨 **TC-25 — KHÔNG ỐNG DẪN output của lệnh cổng** (`XC-007` §6.3).
    ///
    /// 📌 Nếu có `|` trên cùng dòng lệnh cổng thì `$LASTEXITCODE` là của **lệnh cuối trong ống dẫn**
    /// (thường `0`) ⇒ cổng ĐỎ mà CI **xanh**. Đây là lỗi đã xảy ra **hai lần** ở dự án
    /// (`dotnet build … | Select-String`) nên nó phải có **ca đỏ** riêng.
    /// </summary>
    [Fact]
    public void TC25_khong_ong_danh_output_cua_lenh_cong()
    {
        var cacTep = DocTatCaWorkflow(TimGocRepo());

        foreach (var (ten, tho) in cacTep)
        {
            foreach (var dong in DongCua(tho))
            {
                if (!dong.Contains("kiem-tat-ca.ps1", StringComparison.Ordinal) &&
                    !dong.Contains("kiem-link-tai-lieu.ps1", StringComparison.Ordinal) &&
                    !dong.Contains("ci-khang-dinh.ps1", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.False(dong.Contains('|'),
                    $"🚨 `{ten}` ống dẫn output cua lệnh cổng ⇒ `$LASTEXITCODE` là của lệnh CUỐI (thường 0):\n  {dong.Trim()}");

                Assert.False(dong.Contains("Tee-Object", StringComparison.OrdinalIgnoreCase),
                    $"🚨 `{ten}` dùng `Tee-Object` quanh lệnh cổng ⇒ mất mã thoát thật:\n  {dong.Trim()}");
            }
        }
    }

    /// <summary>
    /// 🚨 **Cổng trên CI PHẢI dùng `-DayDu`** — nếu thiếu, bước ⑧ **im lặng không chạy** mà vẫn `exit 0`.
    /// </summary>
    [Fact]
    public void Cong_tren_CI_phai_goi_DayDu()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.Matches(new Regex(@"kiem-tat-ca\.ps1\s+-DayDu\b"), vanBan);

        // 🚨 MỌI lời gọi cổng đều phải có `-DayDu` — không chỉ lời gọi đầu tiên.
        foreach (var dong in DongKhongChuThich(DocTatCaWorkflow(TimGocRepo())))
        {
            if (dong.Contains("kiem-tat-ca.ps1", StringComparison.Ordinal) &&
                Regex.IsMatch(dong, @"kiem-tat-ca\.ps1\s+-{1,2}\w", RegexOptions.None))
            {
                Assert.Contains("-DayDu", dong);
            }
        }
    }

    /// <summary>
    /// 🚨 **F2 — BUILD LÀM NÓNG phải đứng TRƯỚC bước cổng** (và phải `--no-incremental`).
    ///
    /// 📌 Đo được 21/10/2026: trên runner **sạch**, `kiem-moc-build.ps1 -GhiNho` thấy **0 tệp** ⇒ `exit 2`
    /// ⇒ `kiem-tat-ca.ps1` ném ở bước ② ⇒ **lần chạy đầu tiên luôn đỏ**, và đỏ vì **môi trường**.
    /// 🔒 Cách chữa **bị loại**: cho `-GhiNho` bỏ qua khi chưa có `bin/` — khi đó `-Kiem` thấy mọi tệp là
    /// *"MỚI"* và in **"ĐÃ VÀO"** mà **không có bằng chứng nào** ⇒ xanh vì lý do sai.
    /// </summary>
    [Fact]
    public void F2_build_lam_nong_phai_dung_TRUOC_buoc_cong()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        var viTriBuild = vanBan.IndexOf("BUILD LAM NONG", StringComparison.Ordinal);
        var viTriCong = vanBan.IndexOf("kiem-tat-ca.ps1", StringComparison.Ordinal);

        Assert.True(viTriBuild >= 0, "Workflow KHÔNG có bước BUILD LÀM NÓNG ⇒ vi phạm F2.");
        Assert.True(viTriCong >= 0, "Workflow KHÔNG gọi cổng.");
        Assert.True(viTriBuild < viTriCong,
            "🚨 BUILD LÀM NÓNG phải đứng TRƯỚC bước cổng (F2: `-GhiNho` trả exit 2 trên runner sạch).");

        // 📌 `--no-incremental`: bước kiểm mốc cần nhị phân được GHI LẠI (mtime đổi) — nếu không, phép kiểm
        //    *"mốc phải đổi"* sẽ đỏ trên một lần build TỐT (bài học `test-cong-kiem.ps1` ca A).
        Assert.Contains("--no-incremental", vanBan);
    }

    /// <summary>
    /// 🚨 **`DbMigrator` phải chạy TRƯỚC bước cổng** (`S11`, đo được ở lần chạy GitHub Actions **đầu tiên**).
    ///
    /// 📌 Runner **sạch** có PostgreSQL **TRỐNG**. Bước ⑥ (`kiem-db.ps1`) chết ngay với
    /// `relation "AbpSettings" does not exist`, và E2E/probe không có bảng/quyền. 🔑 Máy dev **không** lộ ra
    /// vì DB đã được migrate từ trước — đúng loại *"phụ thuộc đã có sẵn trên máy"* mà đọc mã không thấy.
    /// 🔒 Đòi **`DbMigrator`** (không phải `dotnet ef database update`): thiếu **seed** thì probe nhận
    /// `401`/`403` dù schema đúng.
    /// </summary>
    [Fact]
    public void DbMigrator_phai_chay_truoc_buoc_cong()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.Contains("dotnet run --project src/eCommerce.DbMigrator", vanBan);

        var viTriMigrate = vanBan.IndexOf("src/eCommerce.DbMigrator", StringComparison.Ordinal);
        var viTriCong = vanBan.IndexOf("kiem-tat-ca.ps1", StringComparison.Ordinal);

        Assert.True(viTriCong >= 0, "Workflow KHÔNG gọi cổng.");
        Assert.True(viTriMigrate < viTriCong,
            "🚨 DbMigrator phải đứng TRƯỚC bước cổng (runner sạch có PostgreSQL TRỐNG).");
    }

    /// <summary>
    /// 🚨 **Sau khi cổng chạy, PHẢI khẳng định đầu ra** (`ci-khang-dinh.ps1`) — `exit 0` không chứng minh
    /// bước ⑧ đã chạy (`XC-007` D4/BR-003).
    /// </summary>
    [Fact]
    public void Gate_phai_co_buoc_khang_dinh_dau_ra()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.Contains("ci-khang-dinh.ps1", vanBan);
        Assert.Contains("Evidence.json", vanBan);
    }

    /// <summary>
    /// 🚨 **Bằng chứng phải giữ ≥ 90 ngày và phải upload KỂ CẢ KHI ĐỎ** (NFR-008).
    /// 📌 *"Bằng chứng của cái ĐỎ cũng là bằng chứng"* — thiếu nó thì mỗi lần đỏ là một cuộc điều tra trí nhớ.
    /// </summary>
    [Fact]
    public void Bang_chung_phai_giu_90_ngay_va_upload_ca_khi_do()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.Contains("retention-days: 90", vanBan);
        Assert.Contains("upload-artifact", vanBan);

        // 📌 Đúng `if: always()` cho **bước bằng chứng** (khác với bước CỔNG: §6.3 cấm `if: always()` ở đó,
        //    vì chạy cổng trong trạng thái đã hỏng là kết quả vô nghĩa). Cần **≥ 2**: bước khẳng định + bước upload.
        var soLanLuonChay = Regex.Matches(vanBan, @"if:\s*always\(\)").Count;
        Assert.True(soLanLuonChay >= 2,
            $"Chỉ có {soLanLuonChay} bước `if: always()` ⇒ thiếu bước khẳng định hoặc thiếu upload bằng chứng khi ĐỎ.");
    }

    /// <summary>
    /// 🚨 **Tên job phải khớp required status check** (`cong-12-buoc`) — đổi tên là **required check mất
    /// hiệu lực** mà không ai thấy (nút merge lại mở dù cổng đỏ; `XC-007` §9.2).
    ///
    /// 📌 **`S11` (21/10/2026):** tên cũ `cong-8-buoc` **nói sai** về cổng (đã 12 bước từ `S5`/`S6`). Đổi tên
    /// **trước** lần chạy CI đầu tiên ⇒ rẻ; sau khi bật required check thì phải sửa **cả** thiết lập GitHub.
    /// 🔑 Và tên trong **tài liệu** cũng là một nguồn sự thật (người trực đọc §9.2 để bật required check) ⇒
    /// phép kiểm này đọc **§9.2** và đòi **đúng** tên của job; hai nơi lệch nhau là **ĐỎ**, không phải "chỉ là chữ".
    /// </summary>
    [Fact]
    public void Ten_job_phai_khop_required_check()
    {
        var goc = TimGocRepo();
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(goc));

        Assert.Matches(new Regex(@"(?m)^\s{2}cong-12-buoc:\s*$"), vanBan);
        Assert.Matches(new Regex(@"(?m)^\s{4}name:\s*cong-12-buoc\s*$"), vanBan);

        // 🔒 Nguồn sự thật thứ hai: mục §9.2 của `XC-007` (chỗ người trực đọc để bật required check).
        var duongDanTaiLieu = Path.Combine(goc, "docs", "features", "cross-cutting", "XC-007-ci-cd-pipeline.md");
        Assert.True(File.Exists(duongDanTaiLieu), $"KHÔNG TÌM THẤY tài liệu `{duongDanTaiLieu}` ⇒ phép kiểm chéo VO NGHIA.");

        var taiLieu = File.ReadAllText(duongDanTaiLieu);
        var dau = taiLieu.IndexOf("### 9.2", StringComparison.Ordinal);
        var cuoi = taiLieu.IndexOf("### 9.3", StringComparison.Ordinal);
        Assert.True(dau >= 0 && cuoi > dau, "KHÔNG TÌM THẤY mục `### 9.2` … `### 9.3` trong `XC-007` ⇒ phép kiểm chéo VO NGHIA.");

        var muc92 = taiLieu[dau..cuoi];
        Assert.Contains("`cong-12-buoc`", muc92);
        Assert.DoesNotContain("cong-8-buoc", muc92);
    }

    /// <summary>
    /// 🚨 **Hạ tầng phải là `docker compose`** — bước ⑥ gọi `docker exec ecommerce-postgres psql`
    /// (`kiem-db.ps1`), nên container **phải có ĐÚNG TÊN** do `docker-compose.yml` đặt.
    /// 📌 `services:` của GitHub sinh tên container theo nền tảng ⇒ **không đặt được** ⇒ bước ⑥ chết.
    /// </summary>
    [Fact]
    public void Ha_tang_phai_la_docker_compose()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.Contains("docker compose up -d", vanBan);

        // ⚠️ Kiểm theo DÒNG và bỏ chú thích: chú thích của chính workflow có nhắc chữ `services:` để giải
        //    thích lý do — quét thô sẽ tự tố cáo (bài học `S8`: chú thích cũng bị quét).
        foreach (var dong in DongKhongChuThich(DocTatCaWorkflow(TimGocRepo())))
        {
            Assert.False(Regex.IsMatch(dong, @"^\s*services:\s*$"),
                $"🚨 Workflow dùng `services:` của GitHub ⇒ container KHÔNG có tên `ecommerce-postgres`:\n  {dong.Trim()}");
        }
    }

    /// <summary>
    /// 🚨 **TC-12 — số index `NULLS NOT DISTINCT` chỉ được tồn tại ở MỘT nơi**
    /// (`test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs`), không được viết cứng trong CI.
    /// 📌 Nguồn sự thật thứ hai là lỗi *"hai nguồn sự thật"* — và ở đây chúng **sẽ** lệch nhau ở lần
    /// migration kế tiếp, khi đó người trực sẽ sửa nhầm chỗ.
    /// </summary>
    [Fact]
    public void TC12_so_index_khong_duoc_viet_cung_trong_workflow()
    {
        var vanBan = NoiDungKhongChuThich(DocTatCaWorkflow(TimGocRepo()));

        Assert.DoesNotContain("NULLS NOT DISTINCT", vanBan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assert.Equal(48", vanBan);
        Assert.DoesNotContain("Assert.Equal(53", vanBan);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TRỢ GIÚP
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Đọc mọi workflow (`.yml`/`.yaml`) — khoá là tên tệp, giá trị là nội dung thô.</summary>
    private static Dictionary<string, string> DocTatCaWorkflow(string goc)
    {
        var thuMuc = Path.Combine(goc, ".github", "workflows");
        var ketQua = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!Directory.Exists(thuMuc))
        {
            return ketQua;
        }

        foreach (var tep in Directory.GetFiles(thuMuc).OrderBy(t => t, StringComparer.Ordinal))
        {
            if (tep.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                tep.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                ketQua[Path.GetFileName(tep)] = File.ReadAllText(tep);
            }
        }

        return ketQua;
    }

    /// <summary>Nối nội dung **đã bỏ chú thích** của mọi workflow.</summary>
    private static string NoiDungKhongChuThich(Dictionary<string, string> cacTep)
        => string.Join("\n", cacTep.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => BoChuThichYaml(k.Value)));

    /// <summary>Các dòng **đã bỏ chú thích** của mọi workflow.</summary>
    private static IEnumerable<string> DongKhongChuThich(Dictionary<string, string> cacTep)
        => cacTep.OrderBy(k => k.Key, StringComparer.Ordinal)
            .SelectMany(k => DongCua(k.Value));

    /// <summary>Các dòng **đã bỏ chú thích** của MỘT workflow.</summary>
    private static IEnumerable<string> DongCua(string noiDung)
        => BoChuThichYaml(noiDung).Split('\n').Select(d => d.TrimEnd('\r'));

    /// <summary>
    /// Bỏ **dòng chú thích YAML** (`#…`). 🚨 Cần thiết vì chính workflow **cảnh báo** về các mẫu bị cấm
    /// (`continue-on-error`, `services:`) — quét thô thì chú thích **tự tố cáo** và test đỏ vì lý do sai.
    /// 📌 Chú thích không chạy ⇒ bỏ chúng là đúng **ngữ nghĩa**, không phải nới lỏng phép kiểm.
    /// </summary>
    private static string BoChuThichYaml(string noiDung)
    {
        var dong = noiDung.Replace("\r\n", "\n").Split('\n');
        var giu = dong.Where(d => !d.TrimStart().StartsWith('#'));
        return string.Join("\n", giu);
    }

    /// <summary>Đi ngược từ thư mục build tới gốc repo (test chạy từ `bin/…`).</summary>
    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (Directory.Exists(Path.Combine(thuMuc.FullName, "src", "eCommerce.Web")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy `src/eCommerce.Web` khi đi lên từ " + AppContext.BaseDirectory);
    }
}

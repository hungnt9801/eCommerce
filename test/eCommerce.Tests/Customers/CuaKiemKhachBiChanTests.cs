using Xunit;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🛡️ **LƯỚI AN TOÀN CHỐNG TÁI DIỄN LỖI "CỔNG ĐƯỢC ĐỊNH NGHĨA MÀ KHÔNG AI GỌI"** (`CUS-001` `ADR-04`, `S3`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **LỖI MÀ TỆP NÀY TỒN TẠI ĐỂ CHẶN (đã xảy ra THẬT, đo được ngày 17/10/2026):**
///
/// `ICustomerDirectory.ChoPhepGiaoDichAsync` được viết từ 16/10/2026, có XML doc rất kỹ, có mã lỗi riêng
/// (`eCommerce:Customers:0005`)… và **được gọi 0 NƠI** (`gd1-coding-sessions.md` §2.1 #3).
/// Hệ quả: 🔒 **KHÁCH ĐANG BỊ CHẶN VẪN ĐẶT ĐƯỢC HÀNG** ở đường bán hàng thật.
///
/// 🚨 **Và đây là phần quan trọng nhất:** trong mã **có** câu chú thích nhắc tới `ChoPhepGiaoDichAsync`
/// (3 tệp: khai báo + DTO + comment) ⇒ 🔑 một phép kiểm **`grep` thô** sẽ **XANH** trong khi sản phẩm **hỏng**.
/// 📌 Đúng loại *"cổng xanh vì lý do sai"* mà cả dự án đang chống — nên phép quét ở đây **BỎ DÒNG CHÚ THÍCH**
/// trước khi đếm (và có ca riêng chứng minh nó thật sự bỏ).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **GIỚI HẠN — NÓI THẲNG:** đây là **quét VĂN BẢN**, không phải phân tích IL. Nó chứng minh
/// *"lời gọi CÓ MẶT trong tệp"*, **không** chứng minh *"mọi nhánh đều đi qua nó"*.
/// 🔒 Phần chứng minh **hành vi** nằm ở chỗ khác và là bằng chứng **mạnh hơn**:
/// <list type="number">
///   <item>`tools/probe-khach-hang.ps1` — HTTP **thật** (token thật): khách `Blocked` ⇒ `403` + `0005`,
///     **và** DB **không** có đơn/lượt giữ chỗ nào cho khoá đó;</item>
///   <item>`tools/kiem-e2e.ps1` — ca HTML **thật** `/DatHang` (không phải unit test): khách `Blocked` ⇒
///     trang lỗi `0005`, `OrdOrders` **0** dòng.</item>
/// </list>
/// 📌 Vì vậy tệp này **không** phải "bảo vệ duy nhất" — nó là lưới **rẻ và chạy nhanh** (3,7s cho 1219 test)
/// để bắt đúng lỗi đã xảy ra **trước** khi phải chạy probe 15 giây.
/// </summary>
public class CuaKiemKhachBiChanTests
{
    /// <summary>Tệp **đường bán hàng** — mỗi tệp **PHẢI** gọi cổng. 📌 Thêm đường bán hàng mới ⇒ thêm vào đây.</summary>
    private static readonly string[] DuongBanHang =
    [
        "src/eCommerce.Application/Checkout/CheckoutAppService.cs",
        "src/eCommerce.Application/Sales/DonHangAppService.cs"
    ];

    /// <summary>Tệp chứa **định nghĩa** cổng — nó phải gọi đúng hợp đồng của module `Customers`.</summary>
    private const string TepCong = "src/eCommerce.Application/Sales/CuaKiemKhachGiaoDich.cs";

    /// <summary>Tên lời gọi **của cổng** (một chỗ duy nhất) — dấu hiệu một tệp đi qua cổng.</summary>
    private const string LoiGoiCong = "CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync";

    /// <summary>Tên method **gốc** của hợp đồng `ICustomerDirectory` (chỉ cổng được gọi).</summary>
    private const string LoiGoiHopDong = "ChoPhepGiaoDichAsync";

    [Fact]
    public void MOI_duong_ban_hang__PHAI_goi_cong_khach_bi_chan()
    {
        var goc = TimGocRepo();
        Assert.False(string.IsNullOrWhiteSpace(goc), "Không tìm được gốc repo (mốc `eCommerce.slnx`).");

        var thieu = new List<string>();

        foreach (var tepTuongDoi in DuongBanHang)
        {
            var duongDan = Path.Combine(goc, tepTuongDoi.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(duongDan),
                $"🚨 Không tìm thấy tệp `{tepTuongDoi}` — đường dẫn đã lệch (đổi tên/thư mục?).\n"
                + "📌 Nếu tệp đã được DI CHUYỂN thì cập nhật danh sách `DuongBanHang` — ⚠️ đừng xoá ca kiểm này.");

            var ma = BoChuThich(File.ReadAllText(duongDan));

            if (!ma.Contains(LoiGoiCong, StringComparison.Ordinal))
            {
                thieu.Add(tepTuongDoi);
            }
        }

        Assert.True(thieu.Count == 0,
            "🚨 **CỔNG KHÁCH BỊ CHẶN KHÔNG ĐƯỢC GỌI Ở ĐƯỜNG BÁN HÀNG** ⇒ khách `Blocked` lại đặt được hàng:\n  "
            + string.Join("\n  ", thieu)
            + "\n\n🔒 Đây ĐÚNG là lỗi đã xảy ra (17/10/2026): cổng được định nghĩa nhưng gọi 0 nơi."
            + "\n✅ Cách đúng: `await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_thuMucKhach, input.CustomerId);`"
            + "\n📌 Đặt **TRƯỚC** mọi tác dụng phụ (tính tiền / giữ chỗ / ghi đơn).");
    }

    [Fact]
    public void Cong__PHAI_goi_dung_hop_dong_cua_module_Customers()
    {
        var goc = TimGocRepo();
        var duongDan = Path.Combine(goc, TepCong.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(duongDan),
            $"🚨 Không tìm thấy tệp cổng `{TepCong}` — nếu nó bị xoá thì đường bán hàng **mất cổng**.");

        var ma = BoChuThich(File.ReadAllText(duongDan));

        // 🔑 **KHÔNG tự quyết định "khách bị chặn" ở đây** — phải hỏi module sở hữu dữ liệu (`ADR-12`).
        // 📌 Nếu cổng tự đọc `Customer`/`CusCustomers` thì: (a) hai nguồn sự thật về *"thế nào là bị chặn"*,
        // (b) mỗi lần đổi cấu trúc bảng khách sẽ vỡ miền bán hàng **mà không có lỗi biên dịch**.
        Assert.True(ma.Contains(LoiGoiHopDong, StringComparison.Ordinal),
            $"🚨 Cổng `{TepCong}` KHÔNG gọi `{LoiGoiHopDong}` ⇒ nó **luôn cho qua** (cổng rỗng).\n"
            + "🔒 Luật *\"khách bị chặn là gì\"* thuộc module `Customers` (`ADR-12`) — cổng chỉ được **hỏi**, không được **đoán**.");
    }

    [Fact]
    public void Cong_PHAI_di_TRUOC_tinh_tien_va_giu_cho()
    {
        var goc = TimGocRepo();
        var duongDan = Path.Combine(goc, DuongBanHang[0].Replace('/', Path.DirectorySeparatorChar));
        var ma = BoChuThich(File.ReadAllText(duongDan));

        var viTriCong = ma.IndexOf(LoiGoiCong, StringComparison.Ordinal);
        var viTriTinhGia = ma.IndexOf("_tinhGia.TinhGioAsync(", StringComparison.Ordinal);

        // 🚨 **NEO PHẢI ĐỦ CỤ THỂ — BÀI HỌC ĐO ĐƯỢC TỪ CHÍNH LẦN CHẠY ĐẦU CỦA TEST NÀY:**
        // bản đầu tôi dùng neo `GiuChoAsync` và test **ĐỎ**, báo *"cổng ở SAU bước giữ chỗ"* — ⚠️ **SAI**:
        // `IndexOf` khớp **chuỗi con** `TimGiuChoAsync` (hàm private chỉ ĐỌC, chạy trước cổng) chứ không phải
        // `_giuCho.GiuChoAsync` (hàm **GHI**, chạy sau). 📌 Đúng họ *"phép đo bất ngờ thường là phép đo SAI"*:
        // sản phẩm **đúng**, phép kiểm **sai**. ✅ Neo kèm tên biến (`_giuCho.`) để chỉ khớp lời gọi **ghi thật**.
        var viTriGiuCho = ma.IndexOf("_giuCho.GiuChoAsync(", StringComparison.Ordinal);

        // 🛡️ **LƯỚI AN TOÀN CHO CHÍNH PHÉP ĐO:** không thấy dấu hiệu nào ⇒ test **vô nghĩa**, phải ĐỎ.
        // 📌 Nếu không kiểm điều này, một lần đổi tên method sẽ làm `IndexOf` trả `-1` cho **cả hai** và phép
        // so sánh `-1 < -1` ⇒ **sai** ⇒ test xanh trong khi không đo gì (đúng bẫy "phép đo sai, không phải sản phẩm sai").
        Assert.True(viTriCong >= 0, $"Không thấy lời gọi cổng trong `{DuongBanHang[0]}` ⇒ phép đo VÔ NGHĨA.");
        Assert.True(viTriTinhGia >= 0, "Không thấy `TinhGioAsync` trong CheckoutAppService ⇒ phép đo VÔ NGHĨA (đổi tên method?).");
        Assert.True(viTriGiuCho >= 0, "Không thấy `GiuChoAsync` trong CheckoutAppService ⇒ phép đo VÔ NGHĨA (đổi tên method?).");

        // 🔒 **VÌ SAO THỨ TỰ QUAN TRỌNG TỚI MỨC PHẢI CÓ TEST:** checkout **GIỮ HÀNG TRONG KHO**.
        // 📌 Nếu cổng đặt SAU bước giữ chỗ: đơn không được tạo, nhưng **hàng vẫn bị giam** tới hết TTL (15 phút)
        // ⇒ cổng chặn đơn lại **làm hại kho** — một sửa lỗi sinh ra lỗi khác, rất khó thấy vì **không có lỗi nào**.
        Assert.True(viTriCong < viTriTinhGia,
            "🚨 Cổng khách bị chặn đang ở SAU bước TÍNH TIỀN ⇒ sổ giải trình đã bị ghi trước khi biết khách bị chặn.");
        Assert.True(viTriCong < viTriGiuCho,
            "🚨 Cổng khách bị chặn đang ở SAU bước GIỮ CHỖ ⇒ hàng bị giam 15 phút cho một khách không được mua.");
    }

    [Fact]
    public void Cong_PHAI_di_TRUOC_buoc_GHI_DON()
    {
        var goc = TimGocRepo();
        var duongDan = Path.Combine(goc, DuongBanHang[1].Replace('/', Path.DirectorySeparatorChar));
        var ma = BoChuThich(File.ReadAllText(duongDan));

        var viTriCong = ma.IndexOf(LoiGoiCong, StringComparison.Ordinal);
        // 📌 `new Order(` là **dấu hiệu WRITE** của đường tạo đơn — không phụ thuộc tên biến.
        var viTriGhiDon = ma.IndexOf("new Order(", StringComparison.Ordinal);

        Assert.True(viTriCong >= 0, $"Không thấy lời gọi cổng trong `{DuongBanHang[1]}` ⇒ phép đo VÔ NGHĨA.");
        Assert.True(viTriGhiDon >= 0, "Không thấy `new Order(` trong DonHangAppService ⇒ phép đo VÔ NGHĨA.");

        Assert.True(viTriCong < viTriGhiDon,
            "🚨 Cổng khách bị chặn đang ở SAU bước TẠO ĐƠN ⇒ đơn của khách bị chặn vẫn được ghi.");
    }

    // ── CA KIỂM CHÍNH PHÉP QUÉT (chống "cổng xanh vì lý do sai") ─────────────────────────────────

    [Fact]
    public void Phep_quet__THAY_loi_goi_that()
    {
        const string ma = """
            public class ViDu
            {
                public async Task ChayAsync() => await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_dm, id);
            }
            """;

        Assert.True(BoChuThich(ma).Contains(LoiGoiCong, StringComparison.Ordinal));
    }

    [Fact]
    public void Phep_quet__KHONG_bi_lua_boi_dong_CHU_THICH()
    {
        // 🚨 **CA QUAN TRỌNG NHẤT CỦA TỆP NÀY.** Lỗi thật 17/10/2026 đã "lọt" qua mọi `grep` vì trong mã **có**
        // câu chú thích nhắc tới `ChoPhepGiaoDichAsync`. 📌 Nếu phép quét không bỏ chú thích thì test này
        // **XANH** cho một tệp **KHÔNG hề gọi cổng** — đúng loại cổng vô dụng.
        const string ma = """
            public class ViDu
            {
                // TODO: cần gọi CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync ở đây (nợ CUS-001)
                /// <summary>Xem CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync.</summary>
                public Task ChayAsync() => Task.CompletedTask;
            }
            """;

        Assert.False(BoChuThich(ma).Contains(LoiGoiCong, StringComparison.Ordinal));
    }

    [Fact]
    public void Phep_quet__bo_ca_khoi_chu_thich_dai()
    {
        const string ma = """
            /*
                await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_dm, id);
            */
            public Task ChayAsync() => Task.CompletedTask;
            """;

        Assert.False(BoChuThich(ma).Contains(LoiGoiCong, StringComparison.Ordinal));
    }

    [Fact]
    public void Phep_quet__KHONG_bo_mat_ma_that_nam_SAU_chu_thich_cuoi_dong()
    {
        // 🔑 **Chiều ngược lại:** bỏ chú thích không được "ăn" luôn phần mã trên cùng dòng.
        const string ma = """
            var x = 1; // ghi chú
            await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_dm, id);
            """;

        Assert.True(BoChuThich(ma).Contains(LoiGoiCong, StringComparison.Ordinal));
    }

    // ── Lõi: BỎ CHÚ THÍCH (không phải "quét thô") ──────────────────────────────────────────────

    /// <summary>
    /// Bỏ **chú thích dòng** (`//`, `///`) và **khối** (`/* … */`) để phép quét **không** bị lừa.
    /// 📌 Giữ **độ dài dòng** (thay bằng chuỗi rỗng) để nếu cần báo số dòng thì vẫn đúng — cùng lý do đã ghi
    /// ở `ChannelVisibilityJoinGuardTests.LocChuThich`.
    /// </summary>
    private static string BoChuThich(string noiDung)
    {
        var cacDong = noiDung.Split('\n');
        var trongKhoi = false;

        for (var i = 0; i < cacDong.Length; i++)
        {
            var dong = cacDong[i];

            if (trongKhoi)
            {
                var ketThuc = dong.IndexOf("*/", StringComparison.Ordinal);

                if (ketThuc < 0)
                {
                    cacDong[i] = string.Empty;
                    continue;
                }

                // ⚠️ Phần **sau** `*/` là MÃ THẬT ⇒ phải giữ (bỏ đúng phần chú thích).
                dong = dong[(ketThuc + 2)..];
                trongKhoi = false;
            }

            var moKhoi = dong.IndexOf("/*", StringComparison.Ordinal);
            var chuThichDong = dong.IndexOf("//", StringComparison.Ordinal);

            if (moKhoi >= 0 && (chuThichDong < 0 || moKhoi < chuThichDong))
            {
                var ketThuc = dong.IndexOf("*/", moKhoi + 2, StringComparison.Ordinal);

                if (ketThuc < 0)
                {
                    dong = dong[..moKhoi];
                    trongKhoi = true;
                }
                else
                {
                    // Bỏ **đúng** đoạn `/* … */` (giữ phần trước và sau).
                    dong = dong[..moKhoi] + dong[(ketThuc + 2)..];
                }

                chuThichDong = dong.IndexOf("//", StringComparison.Ordinal);
            }

            if (chuThichDong >= 0)
            {
                dong = dong[..chuThichDong];
            }

            cacDong[i] = dong;
        }

        return string.Join('\n', cacDong);
    }

    /// <summary>Đi lên từ thư mục chạy test cho tới khi gặp `eCommerce.slnx` (dấu hiệu gốc repo).</summary>
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

        return string.Empty;
    }
}

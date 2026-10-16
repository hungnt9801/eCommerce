using eCommerce.Catalog;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🛡️ **LƯỚI AN TOÀN (BEST-EFFORT) CHỐNG `INNER JOIN` TRÊN BẢNG HIỂN THỊ KÊNH** (`CAT-003` `ADR-08`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **LỖI MÀ FILE NÀY TỒN TẠI ĐỂ CHẶN:**
///
/// `KeThua` **KHÔNG** phải *"trạng thái chờ xử lý"* — nó **LÀ trạng thái CUỐI CÙNG HỢP LỆ** cho **đa số** cặp
/// `(sản phẩm, kênh)`, và **vắng bản ghi CHÍNH LÀ** nó (`ADR-08`).
///
/// ⇒ Một `INNER JOIN` giữa **sản phẩm** và **bảng hiển thị kênh** sẽ **biên dịch được**, **chạy được**,
/// 🚨 **KHÔNG ném ngoại lệ nào**, và **LÀM MẤT** khỏi kênh đúng những sản phẩm **đang hiển thị bình thường**
/// ⇒ **storefront thiếu sản phẩm**, doanh thu giảm, **log không có gì** để lần ra.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **GIỚI HẠN CỦA LƯỚI NÀY — NÓI THẲNG, ĐỪNG ĐỌC THÀNH "ĐÃ AN TOÀN TUYỆT ĐỐI":**
///
/// Đây là **quét VĂN BẢN**, không phải **phân tích ngữ nghĩa**. Nó **KHÔNG THỂ** chắc chắn biết một `join`
/// trỏ tới **bảng nào** nếu người viết dùng tên biến **không gợi ý gì**.
///
/// 📌 **Bằng chứng đo được (tôi tự tìm ra bằng cách TIÊM một `INNER JOIN` giả để kiểm lưới — và lưới đã KHÔNG bắt):**
/// <code>
/// var a = await _sanPhamRepository.GetQueryableAsync();
/// var b = await _hienThiRepository.GetQueryableAsync();   // ← tên biến `b`, không gợi ý gì
/// var x = from p in a
///         join v in b on p.Id equals v.ProductId          // ⚠️ INNER JOIN — lưới ĐÃ bỏ qua
///         select p.Id;
/// </code>
/// 🔑 Và tôi còn tìm ra một lỗi **nghiêm trọng hơn** ở bản đầu: cửa sổ `Take(6)` dòng cho một `INNER JOIN`
/// **mượn** `into nhom` + `DefaultIfEmpty()` của phép `join` **KẾ TIẾP** trong cùng truy vấn ⇒ **cho qua**
/// một `INNER JOIN` thật. 🚨 Đó là **tệ hơn không có lưới**: nó tạo **cảm giác an toàn giả**.
/// ✅ Đã sửa: đoạn của một `join` nay **kết thúc** ở `join`/`select`/`group` **kế tiếp** (vì `DefaultIfEmpty()`
/// của **chính** phép join đó phải nằm **TRƯỚC** `select`).
///
/// 🔒 **VẬY CÁI GÌ BẢO VỆ THẬT?** Thứ tự tin cậy (từ mạnh nhất):
/// <list type="number">
///   <item>**Hợp đồng** `IChannelVisibilityResolver` — quy tắc `LEFT JOIN` nằm ở **MỘT** chỗ, đã có
///     **probe trên DB thật** chứng minh (`CAT-003` §24.5, §25);</item>
///   <item>**Test NÀY** — chặn **cách viết phổ biến** (tên bảng/biến gợi ý) ⇒ bắt được **đa số** ca thật,
///     và **quan trọng nhất**: nó bắt được **CHÍNH hợp đồng** (xem ca `Phep_quet_PHAI_thay_join_trong_HOP_DONG`);</item>
///   <item>🚨 **NỢ ĐÃ GHI:** một **interceptor runtime** kiểm **SQL THẬT** (`DefaultIfEmpty()` sinh ra
///     `LEFT JOIN` ⇒ `LEFT JOIN` **phải** có mặt khi truy vấn chạm bảng hiển thị) là lưới **chắc chắn**.
///     📌 Chưa làm — ghi ở `CAT-003` §25.7.</item>
/// </list>
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class ChannelVisibilityJoinGuardTests
{
    /// <summary>Tên bảng/kiểu mà một `join` tới nó **bắt buộc** phải là `LEFT JOIN`.</summary>
    private static readonly string[] BangHienThi =
    [
        "ProductChannelVisibility",
        "CatProductChannelVisibilities",
        "HienThiKenh"
    ];

    /// <summary>Tệp chứa **hợp đồng** — lưới này **phải** thấy `join` trong đó, nếu không nó vô nghĩa.</summary>
    private const string TepHopDong = "src/eCommerce.Application/Catalog/ChannelVisibilityResolver.cs";

    [Fact]
    public void MOI_join_toi_bang_hien_thi_kenh__PHAI_dung_DefaultIfEmpty()
    {
        var (viPham, soJoinDaKiem) = Quet(TimGocRepo());

        Assert.True(viPham.Count == 0,
            "🚨 CÓ `INNER JOIN` TỚI BẢNG HIỂN THỊ KÊNH ⇒ SẼ LÀM MẤT SẢN PHẨM **IM LẶNG**:\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ `KeThua` nghĩa là KHÔNG CÓ BẢN GHI (`ADR-08`), nên `INNER JOIN` loại đúng những sản phẩm"
            + "\n   ĐANG HIỂN THỊ BÌNH THƯỜNG. Không có ngoại lệ nào, không có log nào."
            + "\n🔒 Cách đúng: dùng `IChannelVisibilityResolver` (`TraMotAsync` / `TraNhieuAsync` / `LocSanPhamHienThiAsync`)."
            + "\n📌 Nếu BẮT BUỘC phải tự viết: `join … into nhom` + `from v in nhom.DefaultIfEmpty()`"
            + "\n   VÀ điều kiện kênh phải ở `where` với nhánh `null`: `where v == null || v.ChannelCode == maKenh`"
            + "\n   (đặt điều kiện kênh TRONG `join` cũng thành `INNER JOIN`).");

        Assert.True(soJoinDaKiem > 0,
            $"Phép quét không thấy `join` nào tới bảng hiển thị (soJoinDaKiem={soJoinDaKiem}) ⇒ "
            + "hoặc đã đổi cấu trúc thư mục, hoặc đổi tên bảng ⇒ test này XANH VÔ NGHĨA.");
    }

    [Fact]
    public void Phep_quet_PHAI_thay_join_trong_HOP_DONG__neu_khong_thi_luoi_vo_nghia()
    {
        // 🛡️ **LƯỚI AN TOÀN CHO CHÍNH LƯỚI AN TOÀN — ca này có GIÁ TRỊ NHẤT trong file.**
        //
        // 📌 Vì sao: phép quét dựa trên **tên bảng/tên biến** ⇒ nếu ai đó **đổi tên biến** trong hợp đồng thành
        // `b` (đúng như ca tiêm tôi đã thử), phép quét sẽ **không thấy** phép join nào ⇒ `soJoinDaKiem = 0` ⇒ **im lặng**.
        // 🔑 Ca này khẳng định phép quét **PHẢI** thấy ít nhất một `join` trong **tệp hợp đồng** — tức nếu hợp đồng
        // bị viết lại theo cách phép quét không nhận ra, test **ĐỎ** và buộc người sửa phải **cập nhật phép quét**
        // (hoặc tốt hơn: thêm interceptor runtime — xem nợ §25.7).
        var goc = TimGocRepo();
        Assert.False(string.IsNullOrWhiteSpace(goc), "Không tìm được gốc repo.");

        var duongDan = Path.Combine(goc, TepHopDong.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(duongDan), $"Không tìm thấy tệp hợp đồng `{TepHopDong}` — đường dẫn đã lệch.");

        var cacDong = LocChuThich(File.ReadAllText(duongDan));
        var soJoinTrongHopDong = CacCauJoin(cacDong).Count();

        Assert.True(soJoinTrongHopDong > 0,
            $"🚨 Phép quét KHÔNG thấy `join` nào trong `{TepHopDong}` — mà tệp đó **phải** có một `LEFT JOIN`.\n"
            + "⇒ Phép quét đã **mù** (nhiều khả năng vì ai đó đổi tên biến nguồn thành tên không gợi ý).\n"
            + "🔒 Hệ quả nếu bỏ qua: lưới an toàn **ngừng bảo vệ** mà **vẫn xanh** — đúng loại *\"cảm giác an toàn giả\"*.\n"
            + "📌 Cách sửa: mở rộng dấu hiệu nhận diện trong `Quet`, **hoặc** làm interceptor runtime (nợ §25.7).");
    }

    [Fact]
    public void Gia_tri_1_trong_resolver__PHAI_la_HienThi()
    {
        // 🔒 `ChannelVisibilityResolver.TraNhieuAsync` dùng **số** `1` cho *"kế thừa ⇒ `HienThi`"* thay vì tên enum
        // (vì provider dịch sang SQL). 📌 Rủi ro **có thật**: nếu ai đó chèn một giá trị **trước** `HienThi`
        // trong enum thì `1` **âm thầm** trỏ sang trạng thái khác ⇒ **ẩn/hiện sai toàn bộ storefront**.
        Assert.Equal(1, (int)TrangThaiHienThi.HienThi);
        Assert.Equal(0, (int)TrangThaiHienThi.KeThua);
    }

    [Fact]
    public void Tap_trang_thai_KHONG_hien_thi__gom_CA_An_VA_NgungBan()
    {
        // 🔒 Bất biến nghiệp vụ: *"hiển thị"* nghĩa là **KHÔNG** `An` **VÀ** **KHÔNG** `NgungBan`.
        // 📌 Test này tồn tại vì viết `!= An` mà **quên** `NgungBan` là lỗi **rất dễ** — hậu quả là **bán lại**
        // sản phẩm đã **NGỪNG BÁN** (trạng thái **hấp thụ**, cố ý không cho quay lại).
        foreach (var tt in new[] { TrangThaiHienThi.An, TrangThaiHienThi.NgungBan })
        {
            Assert.True(BiAn(tt), $"`{tt}` PHẢI nằm trong tập KHÔNG hiển thị.");
        }

        foreach (var tt in new[] { TrangThaiHienThi.HienThi, TrangThaiHienThi.KeThua })
        {
            Assert.False(BiAn(tt), $"`{tt}` KHÔNG được nằm trong tập bị loại.");
        }
    }

    // ── Lõi phép quét (tách ra để ca kiểm lưới dùng lại được) ───────────────────────────────────

    private static (List<string> ViPham, int SoJoin) Quet(string goc)
    {
        var viPham = new List<string>();
        var soJoinDaKiem = 0;

        if (string.IsNullOrWhiteSpace(goc))
        {
            return (viPham, soJoinDaKiem);
        }

        var thuMuc = new[]
        {
            Path.Combine(goc, "src"),
            Path.Combine(goc, "modules")
        }.Where(Directory.Exists).ToList();

        foreach (var tm in thuMuc)
        {
            foreach (var tep in Directory.EnumerateFiles(tm, "*.cs", SearchOption.AllDirectories))
            {
                if (LaTepBuild(tep))
                {
                    continue;
                }

                var cacDong = LocChuThich(File.ReadAllText(tep));

                foreach (var (dong, cau) in CacCauJoin(cacDong))
                {
                    if (!ChamBangHienThi(cau))
                    {
                        continue;
                    }

                    soJoinDaKiem++;

                    // 🔑 `LEFT JOIN` trong LINQ **bắt buộc** có `into <nhóm>` **VÀ** `DefaultIfEmpty()`.
                    // 📌 Chỉ có `into` mà **không** `DefaultIfEmpty()` thì nhóm rỗng **vẫn bị bỏ** ⇒ `INNER JOIN`.
                    var coInto = cau.Contains("into", StringComparison.Ordinal);
                    var coDefaultIfEmpty = cau.Contains("DefaultIfEmpty(", StringComparison.Ordinal);

                    if (!coInto || !coDefaultIfEmpty)
                    {
                        var mo = coInto
                            ? "[có `into` nhưng THIẾU `DefaultIfEmpty()`]"
                            : "[KHÔNG có `into` ⇒ INNER JOIN]";

                        viPham.Add($"{Path.GetRelativePath(goc, tep)}:{dong}  →  {Cat(cau, 140)}   {mo}");
                    }
                }
            }
        }

        return (viPham, soJoinDaKiem);
    }

    /// <summary>
    /// 🚨 **CẮT ĐOẠN CỦA TỪNG `join` — KHÔNG dùng cửa sổ N dòng cố định.**
    /// 📌 **Lỗi đã sửa:** cửa sổ `Take(6)` cho một `INNER JOIN` **mượn** `into nhom` + `DefaultIfEmpty()` của
    /// phép `join` **KẾ TIẾP** ⇒ **cho qua** `INNER JOIN` thật. 🔑 Đoạn của một `join` nay **kết thúc** ở
    /// `join`/`select`/`group` **kế tiếp** — vì `DefaultIfEmpty()` của **chính** phép join đó phải nằm **TRƯỚC** `select`.
    /// </summary>
    private static IEnumerable<(int Dong, string Cau)> CacCauJoin(string[] cacDong)
    {
        for (var i = 0; i < cacDong.Length; i++)
        {
            if (!cacDong[i].TrimStart().StartsWith("join ", StringComparison.Ordinal))
            {
                continue;
            }

            var cuoi = i + 1;

            while (cuoi < cacDong.Length)
            {
                var d = cacDong[cuoi].TrimStart();

                if (d.StartsWith("join ", StringComparison.Ordinal)
                    || d.StartsWith("select ", StringComparison.Ordinal)
                    || d.StartsWith("group ", StringComparison.Ordinal))
                {
                    break;
                }

                cuoi++;
            }

            yield return (i + 1, string.Join(" ", cacDong.Skip(i).Take(cuoi - i)).Trim());
        }
    }

    /// <summary>Dấu hiệu một câu `join` trỏ tới **bảng hiển thị kênh** (best-effort — xem giới hạn ở chú thích lớp).</summary>
    private static bool ChamBangHienThi(string cau)
        => BangHienThi.Any(b => cau.Contains(b, StringComparison.Ordinal))
        || cau.Contains("hienthi", StringComparison.OrdinalIgnoreCase)
        || cau.Contains("hien_thi", StringComparison.OrdinalIgnoreCase);

    private static bool BiAn(TrangThaiHienThi tt) => tt is TrangThaiHienThi.An or TrangThaiHienThi.NgungBan;

    private static string Cat(string s, int n) => s.Length <= n ? s : s[..n];

    /// <summary>Bỏ dòng chú thích `//` — 🚨 **bài học từ chính lượt này**: lần chạy đầu, phép quét báo vi phạm
    /// tại một dòng trong hợp đồng, nhưng đó là dòng **chú thích XML** minh hoạ cách **SAI** (thẻ `code`).
    /// 🔒 Giữ dòng trống để **số dòng báo lỗi vẫn đúng**.</summary>
    private static string[] LocChuThich(string noiDung)
        => noiDung
            .Split('\n')
            .Select(d => d.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : d)
            .ToArray();

    private static bool LaTepBuild(string duongDan)
        => duongDan.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || duongDan.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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

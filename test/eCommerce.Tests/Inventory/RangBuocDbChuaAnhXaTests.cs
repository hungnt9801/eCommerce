using System.Text.RegularExpressions;
using eCommerce.Kernel.Data;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🚨 **CƠ CHẾ (không phải quy tắc): mọi ràng buộc DB phải HOẶC được ÁNH XẠ, HOẶC được KHAI BÁO có LÝ DO.**
///
/// **VÌ SAO CẦN TEST NÀY — bài học đã trả giá:** quy tắc *"thêm ràng buộc mới ⇒ thêm dòng ánh xạ"* **đã được ghi**
/// (`handover` **Phụ lục B §B.5**) và **vẫn bị vi phạm ngay trong vòng làm ra nó**: ràng buộc
/// `IX_OrdOrders_TenantId_CheckoutIdempotencyKey` trả **mã chung** `eCommerce:Db:0001` thay vì mã nói rõ
/// *"đã có đơn cho lần checkout này"* (`inv-001` **§25.3**). ⇒ **Ghi quy tắc KHÔNG ĐỦ; cần cơ chế chặn ở CI.**
///
/// **CÁCH LÀM:** đọc `test/eCommerce.IntegrationTests/Assets/schema.sql` — **nguồn sự thật của DB** (sinh từ
/// migration bằng `dotnet ef migrations script`) — lấy ra **mọi `UNIQUE INDEX` + mọi `CHECK` có tên**, rồi đối chiếu:
///   • có trong <see cref="ViPhamRangBuocDb.CacRangBuocDaAnhXa"/> ⇒ **ĐÃ MAP** ✅;
///   • có trong `Data/RangBuocChuaAnhXa.txt` ⇒ **CHƯA MAP NHƯNG CÓ LÝ DO** ✅;
///   • **không** thuộc hai nhóm trên ⇒ 🚨 **ĐỎ**: người thêm ràng buộc **buộc phải chọn**: map, hoặc khai báo lý do.
///
/// 📌 **VÌ SAO ĐỌC `schema.sql` mà không đọc DB sống:** test đơn vị **không** có PostgreSQL; và `schema.sql` được
/// sinh lại **sau mỗi migration** (đã nằm trong quy trình) ⇒ nó là bản sao **đủ trung thực** cho mục đích này.
/// ⚠️ Nếu quên sinh lại `schema.sql` sau khi thêm ràng buộc thì test này **không thấy** ràng buộc mới —
/// nhưng `PostgreSqlSchemaTests` (integration) sẽ **ĐỎ** vì đếm index `NULLS NOT DISTINCT` lệch ⇒ **vẫn bị chặn**.
/// </summary>
public class RangBuocDbChuaAnhXaTests
{
    private static readonly string DuongDanSchema = TimSchemaSql();
    private static readonly string DuongDanDanhSachTrang = TimTheoDuongDanTuongDoi(
        Path.Combine("Data", "RangBuocChuaAnhXa.txt"));

    [Fact]
    public void MOI_rang_buoc_trong_schema_phai_DUOC_ANH_XA_hoac_CO_LY_DO()
    {
        var trongSchema = LayRangBuocTrongSchema();
        var daAnhXa = ViPhamRangBuocDb.CacRangBuocDaAnhXa.ToHashSet(StringComparer.Ordinal);
        var danhSachTrang = LayDanhSachTrang();

        Assert.True(trongSchema.Count > 30,
            $"Chỉ đọc được {trongSchema.Count} ràng buộc từ schema.sql ⇒ phép quét HỎNG (đường dẫn: {DuongDanSchema}).");

        var chuaXuLy = trongSchema
            .Where(x => !daAnhXa.Contains(x) && !danhSachTrang.ContainsKey(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(chuaXuLy.Count == 0,
            "🚨 RÀNG BUỘC DB CHƯA ĐƯỢC XỬ LÝ — mỗi cái phải HOẶC được ánh xạ, HOẶC được khai báo có lý do:\n  "
            + string.Join("\n  ", chuaXuLy)
            + "\n\n⇒ Chọn một:\n"
            + "  (a) thêm dòng vào `ViPhamRangBuocDb.TheoRangBuoc` — nếu ràng buộc chặn một hành vi HỢP LỆ VỀ NGHIỆP VỤ " 
            + "(retry/double-click/đồng thời) và client cần MÃ để phân biệt;\n"
            + $"  (b) thêm vào `{Path.GetFileName(DuongDanDanhSachTrang)}` theo định dạng `<TÊN>|<LÝ DO>`.\n"
            + "📌 Đừng thêm mà không có lý do: danh sách trắng này là chỗ DUY NHẤT nói rằng *'ca này chưa cần mã riêng'*.");
    }

    [Fact]
    public void Danh_sach_trang_KHONG_duoc_chua_rang_buoc_DA_ANH_XA__chong_de_rau_hai_noi()
    {
        // Nếu một ràng buộc vừa được ánh xạ vừa nằm trong danh sách trắng thì hai nguồn mâu thuẫn nhau
        // ⇒ người sau không biết cái nào đúng. (Đúng họ lỗi "hai nguồn sự thật" — `XC-003` F2.)
        var daAnhXa = ViPhamRangBuocDb.CacRangBuocDaAnhXa.ToHashSet(StringComparer.Ordinal);
        var trung = LayDanhSachTrang().Keys.Where(daAnhXa.Contains).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(trung.Count == 0,
            "Ràng buộc VỪA được ánh xạ VỪA nằm trong danh sách trắng:\n  " + string.Join("\n  ", trung));
    }

    [Fact]
    public void Danh_sach_trang_moi_dong_PHAI_co_ly_do__khong_duoc_de_trong()
    {
        // "Khai báo có lý do" mà lý do rỗng thì chỉ là **che** ràng buộc đi ⇒ đúng thứ cơ chế này ngăn.
        var thieuLyDo = LayDanhSachTrang(chiLayTen: false)
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(thieuLyDo.Count == 0,
            "Dòng trong danh sách trắng KHÔNG có lý do:\n  " + string.Join("\n  ", thieuLyDo));
    }

    [Fact]
    public void Danh_sach_trang_KHONG_duoc_chua_ten_LA__chong_rac_theo_thoi_gian()
    {
        // Tên trong danh sách trắng mà KHÔNG còn tồn tại trong schema ⇒ dòng chết, làm danh sách phình ra
        // và che mất việc ràng buộc đã bị đổi tên (khi đó cần ánh xạ lại).
        var trongSchema = LayRangBuocTrongSchema();
        var rac = LayDanhSachTrang().Keys
            .Where(x => !trongSchema.Contains(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(rac.Count == 0,
            "Danh sách trắng có tên KHÔNG còn trong schema (dòng chết):\n  " + string.Join("\n  ", rac));
    }

    // ── Đọc dữ liệu ──────────────────────────────────────────────────────────

    /// <summary>Mọi `UNIQUE INDEX` (trừ `PK_`) và mọi `CHECK` **có tên** trong `schema.sql`.</summary>
    private static HashSet<string> LayRangBuocTrongSchema()
    {
        var sql = File.ReadAllText(DuongDanSchema);

        var duyNhat = Regex.Matches(sql, "CREATE UNIQUE INDEX \"([A-Za-z0-9_]+)\"")
            .Select(m => m.Groups[1].Value)
            .Where(x => !x.StartsWith("PK_", StringComparison.Ordinal));

        var check = Regex.Matches(sql, "CONSTRAINT \"([A-Za-z0-9_]+)\" CHECK")
            .Select(m => m.Groups[1].Value);

        return duyNhat.Concat(check).ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> LayDanhSachTrang(bool chiLayTen = true)
    {
        var ket = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var dong in File.ReadAllLines(DuongDanDanhSachTrang))
        {
            if (string.IsNullOrWhiteSpace(dong) || dong.StartsWith('#'))
            {
                continue;
            }

            var phan = dong.Split('|', 2);

            if (phan.Length == 0 || string.IsNullOrWhiteSpace(phan[0]))
            {
                continue;
            }

            ket[phan[0].Trim()] = phan.Length > 1 ? phan[1].Trim() : string.Empty;
        }

        return ket;
    }

    // ── Tìm đường dẫn (test chạy từ `bin/…` nên phải đi ngược lên gốc repo) ──

    private static string TimSchemaSql()
        => TimTheoDuongDanTuongDoi(Path.Combine("test", "eCommerce.IntegrationTests", "Assets", "schema.sql"));

    private static string TimTheoDuongDanTuongDoi(string duongDanTuongDoi)
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            var ungVien = Path.Combine(thuMuc.FullName, duongDanTuongDoi);

            if (File.Exists(ungVien))
            {
                return ungVien;
            }

            // Mốc nhận biết gốc repo — nếu không thấy thì tiếp tục đi lên.
            thuMuc = thuMuc.Parent;
        }

        throw new FileNotFoundException(
            $"Không tìm thấy '{duongDanTuongDoi}' khi đi ngược từ '{AppContext.BaseDirectory}'. " +
            "Test này cần gốc repo (chạy `dotnet test` từ trong repo).");
    }
}

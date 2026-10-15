using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// Nút **cây danh mục** (`CatCategories`) — `CAT-002` **D1/D2/D3**.
///
/// 🚨 **BA QUYẾT ĐỊNH QUAN TRỌNG NHẤT CỦA LỚP NÀY:**
///
/// 1. **`Path` là `Adjacency List` + đường dẫn VẬT CHẤT HOÁ** (**D1**): lưu sẵn `/guid/guid/` để lấy cả cây
///    bằng **một** truy vấn `LIKE 'path%'` thay vì đệ quy. Đánh đổi: **di chuyển nhánh là thao tác GHI ĐẮT**
///    (phải dựng lại `Path` cho toàn bộ cây con) — xem `CategoryTreeService`.
///
/// 2. **`Path` chứa `Guid`, KHÔNG chứa slug/tên** (**D2**): slug **đổi được** (SEO) ⇒ nếu nằm trong `Path`
///    thì mỗi lần đổi tên là mọi `Path` con hỏng; `Guid` **bất biến** ⇒ `Path` chỉ đổi khi **di chuyển nhánh**.
///    Hệ quả: `Path` **không đọc được bằng mắt** ⇒ **BẮT BUỘC** có test khẳng định `Path` khớp cây thật.
///
/// 3. **KHÔNG có navigation `Children`** (**D3**): mỗi nút là **một aggregate riêng**. Nếu nạp cả cây vào
///    một aggregate thì hai người sửa **hai nhánh khác nhau** vẫn **tranh chấp ghi** nhau. Nhờ vậy thao tác
///    cây **bắt buộc** đi qua `CategoryTreeService` (nơi có khoá phân tán + kiểm tra chu trình).
///
/// ⚠️ **BẤT BIẾN KHÔNG ĐƯỢC DB BẢO VỆ:** *"cây không có chu trình"* (`CAT-002` §8.1 #1) — PostgreSQL **không**
/// có ràng buộc "cây không chu trình". Guard ở tầng domain + `CategoryTreeService` là **thứ duy nhất** chặn;
/// mất nó ⇒ chu trình **im lặng** (đọc cây sẽ treo/lặp vô hạn). Đây là lý do lớp này kiểm tra rất chặt.
/// </summary>
public class Category : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ sâu tối đa (`CAT-002` **§8.1 #3** + giả định #1: cây ≤ 5 cấp). Gốc = 0.</summary>
    public const int DoCaoToiDa = 5;

    /// <summary>Độ dài `Path` tối đa — khớp `HasMaxLength(1024)` ở DbContext.</summary>
    public const int DoDaiPathToiDa = 1024;

    /// <summary>Ngưỡng tổ hợp biến thể mặc định khi nút **không** ghi đè (`CAT-002` D6).</summary>
    public const int NguongToHopMacDinh = 200;

    public Guid? TenantId { get; protected set; }

    /// <summary>Nút cha; `NULL` = gốc. ⚠️ FK ở DB là **`Restrict`** (`CAT-002` D9) — xoá cha không xoá con.</summary>
    public Guid? ParentId { get; protected set; }

    /// <summary>Mã nghiệp vụ, **duy nhất trong tenant** — dùng cho import/báo cáo.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Phần SEO. ⚠️ Chi tiết SEO thuộc `CAT-003`; ở đây chỉ giữ chỗ.</summary>
    public string? Slug { get; set; }

    /// <summary>
    /// 🚨 **Đường dẫn vật chất hoá** dạng `/guid1/guid2/` (**có** `/` đầu **và** cuối).
    /// ⚠️ **CHỈ `CategoryTreeService` được ghi** (`CAT-002` BR-001) — setter là `protected` để không ai
    /// "tiện tay" sửa từ app service rồi làm `Path` lệch cây thật **mà không có lỗi biên dịch**.
    /// </summary>
    public string Path { get; protected set; } = default!;

    /// <summary>Số tổ tiên; gốc = 0. ⚠️ **Phải luôn khớp `Path`** (`CAT-002` BR-002).</summary>
    public int Level { get; protected set; }

    /// <summary>Thứ tự hiển thị trong nút cha. ⚠️ `int`, **cấm** `double` (`CAT-002` D11).</summary>
    public int SortOrder { get; set; }

    /// <summary>Ngừng dùng thay cho **xoá** (`CAT-002` **D9**) — nút còn con/sản phẩm thì **không** xoá được.</summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>Ghi đè ngưỡng tổ hợp theo nút; `NULL` = dùng mặc định (`CAT-002` D6).</summary>
    public int? MaxVariantCombinations { get; set; }

    protected Category() { }

    private Category(Guid id, Guid? parentId, string code, string name, string path, int level) : base(id)
    {
        ParentId = parentId;
        Code = code;
        Name = name;
        Path = path;
        Level = level;
        KiemTraBatBien();
    }

    /// <summary>
    /// Tạo nút danh mục (gốc hoặc con). 🚨 **Ném lỗi** khi vượt <see cref="DoCaoToiDa"/>
    /// (`eCommerce:Catalog:0024`) — và ở DB còn có `CHECK (Level <= 5)` làm **chốt cuối**.
    /// </summary>
    public static Category Tao(Guid id, Category? cha, string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name));
        }

        var ma = code.Trim();
        if (ma.Length > 64)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaDanhMucKhongHopLe)
                .WithData("truong", "Category.Code")
                .WithData("doDai", ma.Length);
        }

        var duongDan = TinhPath(cha?.Path, id);
        var cap = TinhLevel(duongDan);

        if (cap > DoCaoToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VuotDoSauDanhMuc)
                .WithData("cap", cap)
                .WithData("toiDa", DoCaoToiDa)
                .WithData("cha", cha?.Code ?? "(gốc)");
        }

        return new Category(id, cha?.Id, ma, name.Trim(), duongDan, cap);
    }

    /// <summary>
    /// Đổi nút cha (dùng bởi `CategoryTreeService` khi **di chuyển nhánh**).
    /// ⚠️ **KHÔNG** kiểm tra chu trình ở đây — việc đó cần nhìn thấy **cả cây con** nên thuộc `CategoryTreeService`
    /// (khoá phân tán + kiểm tra trong cùng transaction, `CAT-002` D8). Lớp này chỉ dựng lại `Path`/`Level`
    /// **của chính nút** và kiểm tra độ sâu của nút.
    /// </summary>
    public void DoiCha(Category? chaMoi)
    {
        var duongDan = TinhPath(chaMoi?.Path, Id);
        var cap = TinhLevel(duongDan);

        if (cap > DoCaoToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VuotDoSauDanhMuc)
                .WithData("cap", cap)
                .WithData("toiDa", DoCaoToiDa)
                .WithData("cha", chaMoi?.Code ?? "(gốc)");
        }

        ParentId = chaMoi?.Id;
        Path = duongDan;
        Level = cap;
        KiemTraBatBien();
    }

    public void DoiTen(string ten)
    {
        if (string.IsNullOrWhiteSpace(ten))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(ten));
        }

        Name = ten.Trim();
    }

    /// <summary>Ngừng dùng (thay cho xoá — `CAT-002` D9).</summary>
    public void NgungDung() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void KichHoat() => IsActive = true;

    public void DatThuTu(int thuTu) => SortOrder = thuTu;

    /// <summary>
    /// 🚨 **CHỈ `CategoryTreeService` được gọi** — dựng lại `Path`/`Level` cho **hậu duệ** khi cả nhánh
    /// được di chuyển. Không dùng cho thao tác thường (thao tác thường đi qua <see cref="DoiCha"/>).
    /// Vì sao vẫn để `public`: `CategoryTreeService` ở **cùng assembly** nhưng **khác lớp**; và bất biến
    /// `Path` khớp `Level` vẫn được <see cref="KiemTraBatBien"/> kiểm tra **ngay trong** hàm này ⇒ gọi sai
    /// sẽ **NÉM** chứ không im lặng.
    /// </summary>
    public void DatLaiPath(string pathMoi, int capMoi)
    {
        Path = pathMoi;
        Level = capMoi;
        KiemTraBatBien();
    }

    /// <summary>
    /// 🚨 **Kiểm tra `Path` khớp `Level`** (`CAT-002` §12 dòng cuối): nếu lệch thì đây là **LỖI LẬP TRÌNH**
    /// ⇒ **NÉM `InvalidOperationException`** để **lộ ra ở CI**, **KHÔNG** tự sửa im lặng.
    /// Vì sao phải ồn ào: `Path` lệch cây thật làm mọi truy vấn cây con trả **sai và thiếu** — mà **không**
    /// có exception nào ở tầng DB, nên nếu ở đây im lặng thì lỗi chỉ hiện ra dưới dạng "menu storefront thiếu mục".
    /// </summary>
    public void KiemTraBatBien()
    {
        if (string.IsNullOrEmpty(Path) || !Path.StartsWith('/') || !Path.EndsWith('/'))
        {
            throw new InvalidOperationException(
                $"`Path` của danh mục {Id} phải bắt đầu và kết thúc bằng '/' (đang là '{Path}').");
        }

        if (!Path.EndsWith($"/{Id}/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"`Path` của danh mục {Id} phải KẾT THÚC bằng chính Id của nó (đang là '{Path}').");
        }

        if (Path.Length > DoDaiPathToiDa)
        {
            throw new InvalidOperationException(
                $"`Path` của danh mục {Id} dài {Path.Length} > {DoDaiPathToiDa}.");
        }

        var capTheoPath = TinhLevel(Path);

        if (capTheoPath != Level)
        {
            throw new InvalidOperationException(
                $"`Path` và `Level` KHÔNG khớp ở danh mục {Id}: Path='{Path}' ⇒ Level phải là {capTheoPath}, " +
                $"nhưng đang là {Level}. Đây là lỗi lập trình — Path chỉ được sinh bởi CategoryTreeService.");
        }

        if (Level < 0 || Level > DoCaoToiDa)
        {
            throw new InvalidOperationException(
                $"`Level` của danh mục {Id} là {Level}, ngoài khoảng 0..{DoCaoToiDa}.");
        }
    }

    /// <summary>
    /// Sinh `Path` cho một nút: `Path` của cha + `Id` của chính nó. Nút gốc ⇒ `/{id}/`.
    /// ⚠️ Hàm **thuần** ⇒ test được không cần DB (đây là "trái tim" của D1/D2).
    /// </summary>
    public static string TinhPath(string? pathCha, Guid id)
    {
        var tienTo = string.IsNullOrEmpty(pathCha) ? "/" : pathCha;

        if (!tienTo.EndsWith('/'))
        {
            tienTo += "/";
        }

        return $"{tienTo}{id}/";
    }

    /// <summary>
    /// `Level` suy ra từ `Path`: số tổ tiên = số dấu `/` trừ 2 (`/{id}/` ⇒ 0; `/{a}/{b}/` ⇒ 1).
    /// ⚠️ Chỉ dùng cho `Path` **đúng định dạng**; dùng cho chuỗi khác thì kết quả vô nghĩa
    /// (đó là lý do <see cref="KiemTraBatBien"/> kiểm tra định dạng TRƯỚC).
    /// </summary>
    public static int TinhLevel(string path)
        => path.Count(c => c == '/') - 2;

    /// <summary>
    /// `ungVien` có phải **tổ tiên** của `pathCon` không (so theo **tiền tố** `Path`).
    /// 🚨 Dùng để **chặn chu trình**: không được di chuyển một nút vào chính **cây con** của nó.
    /// </summary>
    public static bool LaToTienCua(string pathToTien, string pathCon)
        => pathCon.Length > pathToTien.Length
           && pathCon.StartsWith(pathToTien, StringComparison.Ordinal);

    /// <summary>
    /// Đổi tiền tố `Path` khi **cả cây con** được di chuyển: `/A/B/` → `/A/C/B/`.
    /// ⚠️ Trả về `null` nếu `path` không nằm trong nhánh `pathCu` (không phải hậu duệ) ⇒ người gọi **phải**
    /// xử lý `null` thay vì âm thầm dựng ra đường dẫn rác.
    /// </summary>
    public static string? DoiTienToPath(string path, string pathCu, string pathMoi)
    {
        if (!path.StartsWith(pathCu, StringComparison.Ordinal))
        {
            return null;
        }

        return string.Concat(pathMoi, path.AsSpan(pathCu.Length));
    }
}

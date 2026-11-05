using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Catalog;

/// <summary>Kết quả tra hiển thị của **một** sản phẩm trên **một** kênh.</summary>
/// <param name="TrangThai">Trạng thái **hiệu lực** — 🔒 đã áp quy tắc *"vắng bản ghi ⇒ `HienThi`"*.</param>
/// <param name="CoBanGhiTuongMinh">`true` nếu có bản ghi **tường minh**; `false` nếu **kế thừa** (vắng bản ghi).
/// 📌 Trường này **không** thừa: UI cần phân biệt *"kế thừa"* với *"đã đặt tường minh = `HienThi`"* để hiện
/// nhãn **"Kế thừa"** (`CAT-003` §A2) — nếu chỉ trả `TrangThai` thì **không** phân biệt được.</param>
/// <param name="LyDo">Lý do (chỉ có khi bản ghi tường minh ở trạng thái `An`).</param>
public readonly record struct KetQuaHienThiKenh(
    TrangThaiHienThi TrangThai,
    bool CoBanGhiTuongMinh,
    string? LyDo);

/// <summary>
/// 🚨 **TRA HIỂN THỊ THEO KÊNH — ĐIỂM DUY NHẤT BIẾT QUY TẮC `LEFT JOIN` + `COALESCE`** (`CAT-003` `ADR-08`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO HỢP ĐỒNG NÀY TỒN TẠI — đây là nợ NGUY HIỂM NHẤT của `CAT-003` (§23.1, §24.7 #1):**
///
/// `KeThua` **KHÔNG** phải *"trạng thái chờ xử lý"* — nó **LÀ trạng thái CUỐI CÙNG HỢP LỆ** cho **đa số** cặp
/// `(sản phẩm, kênh)`, và **vắng bản ghi CHÍNH LÀ** nó (`ADR-08`).
///
/// 🚨 **HỆ QUẢ:** người viết **truy vấn storefront đầu tiên** rất dễ viết
/// <c>from p in SanPham join v in HienThiKenh on p.Id equals v.ProductId</c> — **`INNER JOIN`**. Nó **biên dịch
/// được**, **chạy được**, **không ném ngoại lệ nào**, và **làm MẤT** khỏi kênh đúng những sản phẩm **đang hiển
/// thị bình thường** (vì chúng **không có** bản ghi). 📌 Kết quả: **storefront thiếu sản phẩm**, doanh thu
/// giảm, và **không có gì trong log** để lần ra.
///
/// 🔒 **CÁCH CHẶN:** gom quy tắc vào **MỘT** hợp đồng, để (a) chỉ có **một** chỗ phải đúng, và (b) mọi chỗ
/// khác **không** phải tự viết `JOIN`. ➕ Kèm **test quét mã nguồn** (`ChannelVisibilityJoinGuardTests`) chặn
/// việc viết `JOIN` trực tiếp ra ngoài hợp đồng này.
///
/// 📌 **VÌ SAO Ở `Application`, KHÔNG ở `Domain`:** đây là **truy vấn đọc tổng hợp** (nhiều bảng), không phải
/// bất biến nghiệp vụ. 🔑 Bất biến (**bảng chuyển trạng thái**) đã ở `ProductChannelVisibility.ChuyenSang`
/// (tầng Domain) — hai việc **khác nhau**, và tách đúng chỗ là điều làm cả hai kiểm được.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public interface IChannelVisibilityResolver
{
    /// <summary>
    /// Hiển thị **hiệu lực** của **một** sản phẩm trên **một** kênh.
    /// 🔒 **Vắng bản ghi ⇒ `HienThi`** (kế thừa) — **KHÔNG** trả `KeThua` cho người gọi: `KeThua` là trạng thái
    /// **lưu trữ**, còn đây là trạng thái **hiệu lực**. 📌 Trả `KeThua` ra ngoài sẽ buộc **mọi** chỗ gọi tự
    /// nhớ quy tắc *"`KeThua` nghĩa là `HienThi`"* — và chỗ nào quên thì **ẩn nhầm** sản phẩm.
    /// </summary>
    Task<KetQuaHienThiKenh> TraMotAsync(Guid productId, string channelCode);

    /// <summary>
    /// 🚨 **TRA HÀNG LOẠT** — trạng thái hiệu lực của **mọi** sản phẩm trong `productIds` trên **một** kênh.
    /// 🔒 **Sản phẩm KHÔNG có bản ghi VẪN CÓ mặt trong kết quả** với `HienThi` + `CoBanGhiTuongMinh = false`.
    /// 📌 Đây **chính là** hành vi của `LEFT JOIN`: nếu hàm này **bỏ sót** sản phẩm không có bản ghi thì nó
    /// **chính là** `INNER JOIN` đội lốt — và đó là lỗi mà hợp đồng này tồn tại để chặn.
    ///
    /// ⚠️ **VÌ SAO CÓ HÀM HÀNG LOẠT (không chỉ hàm một):** một trang storefront cần trạng thái của **N** sản phẩm;
    /// gọi hàm một trong vòng lặp là **N truy vấn** (N+1) ⇒ với 100 sản phẩm/trang là 100 vòng khứ hồi DB.
    /// 🔑 Và quan trọng hơn: hàm hàng loạt **buộc** người viết phải xử lý ca *"không có bản ghi"* **một lần**,
    /// thay vì **N** lần ở **N** chỗ.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, KetQuaHienThiKenh>> TraNhieuAsync(
        IReadOnlyCollection<Guid> productIds,
        string channelCode);

    /// <summary>
    /// 🚨 **LỌC DANH SÁCH SẢN PHẨM THEO KÊNH** — trả về **chỉ** những sản phẩm **hiển thị** trên kênh đó
    /// (tức `TrangThai != An` **và** `!= NgungBan`), **GIỮ NGUYÊN THỨ TỰ** đầu vào.
    ///
    /// 🔑 **VÌ SAO CẦN HÀM NÀY DÙ ĐÃ CÓ `TraNhieuAsync`:** đây là **ý định nghiệp vụ** phổ biến nhất
    /// (*"kênh này đang bán gì"*), và nếu để mỗi chỗ gọi tự `Where(...)`, thì mỗi chỗ có thể **quên một** trạng
    /// thái (quên `NgungBan` là lỗi **rất** dễ) ⇒ **bán lại** một sản phẩm đã **ngừng bán**.
    /// 📌 Gom thành **một** định nghĩa *"hiển thị"* ⇒ chỉ có **một** chỗ đúng.
    /// </summary>
    Task<IReadOnlyList<Guid>> LocSanPhamHienThiAsync(
        IReadOnlyCollection<Guid> productIds,
        string channelCode);
}

/// <summary>
/// Hiện thực `IChannelVisibilityResolver`. Xem chú thích ở **hợp đồng** để biết vì sao nó tồn tại.
///
/// 🔒 **KỸ THUẬT `LEFT JOIN` TRONG LINQ — và vì sao tôi chọn cách này:**
/// <code>
/// var truyVan = from p in SanPham
///               join v in HienThiKenh on p.Id equals v.ProductId into nhom
///               from v in nhom.DefaultIfEmpty()      // 🔑 ĐÂY LÀ LEFT JOIN
///               where v == null || v.ChannelCode == maKenh
///               select new { p.Id, TrangThai = v == null ? 1 : v.TrangThai, ... };
/// </code>
/// 📌 `join … into nhom` + `from v in nhom.DefaultIfEmpty()` là **`LEFT JOIN`** trong cú pháp LINQ.
/// ⚠️ **CẠM BẪY:** `where v.ChannelCode == maKenh` **SAI** — nó loại luôn các dòng `v == null` (**chính** những
/// sản phẩm cần giữ!) ⇒ **hoá ra** `INNER JOIN` ⇒ **mất sản phẩm im lặng**. 🔑 Phải viết
/// <c>v == null || v.ChannelCode == maKenh</c> (**so sánh `null` TRƯỚC**).
/// 📌 Vì lý do đó tôi **tách điều kiện kênh ra khỏi `join`**: đặt nó trong `join` sẽ **loại** dòng không khớp
/// ⇒ lại thành `INNER JOIN`; phải để trong `where` với **nhánh `null`**.
/// </summary>
public class ChannelVisibilityResolver : IChannelVisibilityResolver, ITransientDependency
{
    private readonly IRepository<Product, Guid> _sanPhamRepository;
    private readonly IRepository<ProductChannelVisibility, Guid> _hienThiRepository;
    private readonly IAsyncQueryableExecuter _thucThi;

    public ChannelVisibilityResolver(
        IRepository<Product, Guid> sanPhamRepository,
        IRepository<ProductChannelVisibility, Guid> hienThiRepository,
        IAsyncQueryableExecuter thucThi)
    {
        _sanPhamRepository = sanPhamRepository;
        _hienThiRepository = hienThiRepository;
        _thucThi = thucThi;
    }

    /// <inheritdoc />
    public async Task<KetQuaHienThiKenh> TraMotAsync(Guid productId, string channelCode)
    {
        var ds = await TraNhieuAsync([productId], channelCode);

        return ds.TryGetValue(productId, out var kq)
            ? kq
            // 📌 Sản phẩm **không tồn tại** (đã xoá cứng, hoặc `Guid` sai) ⇒ `KeThua` + **không** có bản ghi.
            // ⚠️ **KHÔNG** ném: hàm này là **tra cứu**, và chỗ gọi thường đang hiển thị một danh sách —
            // ném ở đây sẽ làm **hỏng cả trang** vì một `Guid` lạ. 🔑 Chỗ nào **cần** phân biệt *"không tồn tại"*
            // thì phải hỏi repository sản phẩm (như `HienThiKenhAppService` đã làm ⇒ `0010`).
            : new KetQuaHienThiKenh(TrangThaiHienThi.HienThi, CoBanGhiTuongMinh: false, LyDo: null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, KetQuaHienThiKenh>> TraNhieuAsync(
        IReadOnlyCollection<Guid> productIds,
        string channelCode)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, KetQuaHienThiKenh>();
        }

        var maKenh = channelCode?.Trim() ?? string.Empty;
        var dsId = productIds.Distinct().ToList();

        var truyVanSanPham = await _sanPhamRepository.GetQueryableAsync();
        var truyVanHienThi = await _hienThiRepository.GetQueryableAsync();

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🔑 **LEFT JOIN** — `join … into nhom` + `from v in nhom.DefaultIfEmpty()`.
        // 🚨 Điều kiện kênh **KHÔNG** đặt trong `join` (sẽ thành `INNER JOIN`), mà ở `where` với **nhánh `null`**.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        var truyVan = from p in truyVanSanPham
                      where dsId.Contains(p.Id)
                      join v in truyVanHienThi on p.Id equals v.ProductId into nhom
                      from v in nhom.DefaultIfEmpty()
                      where v == null || v.ChannelCode == maKenh
                      select new
                      {
                          ProductId = p.Id,
                          // 🔒 `v == null` ⇒ **kế thừa** ⇒ hiệu lực là `HienThi` (`ADR-08`).
                          // 📌 Dùng số (`1`) thay vì tên enum vì provider dịch sang SQL; đổi lại phải **đồng bộ**
                          // với `TrangThaiHienThi.HienThi` — và `ChannelVisibilityResolverTests` khoá điều đó.
                          TrangThai = v == null ? 1 : (int)v.TrangThai,
                          CoBanGhi = v != null,
                          LyDo = v == null ? null : v.LyDo
                      };

        var dong = await _thucThi.ToListAsync(truyVan);

        // 📌 **GOM THEO `ProductId`** — một sản phẩm có thể có **nhiều** dòng nếu dữ liệu bẩn (unique index
        // `(TenantId, ProductId, ChannelCode)` **đã** cấm, nhưng dữ liệu cũ/nhập tay có thể có).
        // 🔑 Chọn **bản ghi TƯỜNG MINH** khi có (bỏ dòng kế thừa) — vì bản ghi tường minh **luôn** thắng.
        // ⚠️ Nếu chỉ `ToDictionary` thì dữ liệu bẩn sẽ làm **NÉM `ArgumentException`** ⇒ **hỏng cả trang**.
        var ketQua = new Dictionary<Guid, KetQuaHienThiKenh>(dong.Count);

        foreach (var d in dong)
        {
            if (ketQua.TryGetValue(d.ProductId, out var cu) && cu.CoBanGhiTuongMinh && !d.CoBanGhi)
            {
                continue;
            }

            ketQua[d.ProductId] = new KetQuaHienThiKenh(
                (TrangThaiHienThi)d.TrangThai, d.CoBanGhi, d.LyDo);
        }

        return ketQua;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> LocSanPhamHienThiAsync(
        IReadOnlyCollection<Guid> productIds,
        string channelCode)
    {
        var trangThai = await TraNhieuAsync(productIds, channelCode);

        // 🔒 **MỘT định nghĩa "hiển thị"** — `An` và `NgungBan` đều **KHÔNG** hiển thị.
        // 📌 Viết `!= An` mà quên `NgungBan` là lỗi **rất** dễ ⇒ **bán lại** sản phẩm đã ngừng bán.
        // 🔑 Và **giữ nguyên THỨ TỰ đầu vào** (trả theo `productIds`, không theo thứ tự DB): chỗ gọi đã sắp
        // xếp theo tiêu chí của nó (giá, mới nhất…), đổi thứ tự ở đây sẽ **phá** sắp xếp đó **âm thầm**.
        return productIds
            .Distinct()
            .Where(id => trangThai.TryGetValue(id, out var kq)
                      && kq.TrangThai is not (TrangThaiHienThi.An or TrangThaiHienThi.NgungBan))
            .ToList();
    }
}

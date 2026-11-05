using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **Dịch vụ miền quản lý CÂY danh mục** (`CAT-002` **D3/D8/D9**) — nơi **DUY NHẤT** được ghi `Path`/`Level`.
///
/// 🚨 **VÌ SAO PHẢI CÓ LỚP NÀY (không cho app service sửa `ParentId` trực tiếp):**
/// `CAT-002` §8.1 **#1** nói rõ bất biến *"cây danh mục KHÔNG có chu trình"* **KHÔNG được DB bảo vệ**
/// (PostgreSQL không có ràng buộc "cây không chu trình"). Nghĩa là nếu hai người **đồng thời** di chuyển
/// hai nhánh (A→B và B→A) mà không có khoá, ta tạo ra **chu trình** và **KHÔNG có lỗi nào** ở tầng DB —
/// hậu quả hiện ra sau đó dưới dạng *"menu storefront treo/thiếu mục"*, rất khó truy.
/// Vì vậy **mọi** thao tác cây ở đây đi theo một khuôn:
///   1. **Giành khoá phân tán theo TOÀN TENANT** (`D8`: khoá theo tenant, không theo nút — tranh chấp
///      chấp nhận được vì di chuyển cây rất thao tác ít).
///   2. **Kiểm tra chu trình + độ sâu** trong **cùng transaction** với thao tác ghi.
///   3. Dựng lại `Path`/`Level` cho **toàn bộ cây con** (đắt, nhưng đúng — `D1`).
///
/// 📌 **Điểm quan trọng về khoá:** phạm vi khoá **bao NGOÀI** Unit of Work (không nằm trong transaction)
/// để khoá được nhả **sau** khi transaction kết thúc (`CAT-002` §12 + `XC-003` §12).
/// </summary>
public class CategoryTreeService : DomainService
{
    /// <summary>Thời gian chờ giành khoá (giây) — `CAT-002` §8.2 đề xuất TTL **30 s** cho khoá cây.</summary>
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<ProductCategory, Guid> _lienKetRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public CategoryTreeService(
        IRepository<Category, Guid> categoryRepository,
        IRepository<ProductCategory, Guid> lienKetRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _categoryRepository = categoryRepository;
        _lienKetRepository = lienKetRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Tạo nút danh mục (gốc khi <paramref name="chaId"/> `null`, ngược lại là nút con).
    ///
    /// 📌 **Không cần khoá cây:** nút mới là **lá** và `Path` của nó suy ra từ cha **đang tồn tại**
    /// ⇒ không thể tạo chu trình. (Trùng `Code` do **unique index** `(TenantId, Code)` chặn — hàng rào cuối.)
    /// </summary>
    public async Task<Category> TaoNutAsync(Guid id, Guid? chaId, string code, string name)
    {
        Category? cha = null;

        if (chaId is not null)
        {
            cha = await _categoryRepository.FindAsync(chaId.Value)
                ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                    .WithData("lyDo", "Nút cha không tồn tại.")
                    .WithData("chaId", chaId);
        }

        var nut = Category.Tao(id, cha, code, name);

        await _categoryRepository.InsertAsync(nut, autoSave: true);

        Logger.LogInformation(
            "Đã tạo danh mục {Code} (cấp {Cap}, cha {Cha}).", nut.Code, nut.Level, cha?.Code ?? "(gốc)");

        return nut;
    }

    /// <summary>
    /// **Di chuyển một nhánh** sang nút cha mới (`chaMoiId = null` ⇒ thành nút gốc).
    ///
    /// 🚨 **ĐÂY LÀ THAO TÁC NGUY HIỂM NHẤT CỦA `CAT-002`** — nó vừa có thể tạo **chu trình** (nếu đích nằm
    /// trong chính cây con), vừa phải dựng lại `Path` cho **mọi** hậu duệ. Cả hai đều **không** được DB bảo vệ.
    /// </summary>
    public async Task DiChuyenNhanhAsync(Guid nutId, Guid? chaMoiId)
    {
        await using var khoa = await GiaNhKhoaCayAsync();

        var nut = await _categoryRepository.FindAsync(nutId)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Không tìm thấy danh mục cần di chuyển.")
                .WithData("nutId", nutId);

        if (chaMoiId == nutId)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhDanhMuc)
                .WithData("lyDo", "Không thể di chuyển một nút vào CHÍNH NÓ.")
                .WithData("nut", nut.Code);
        }

        Category? chaMoi = null;

        if (chaMoiId is not null)
        {
            chaMoi = await _categoryRepository.FindAsync(chaMoiId.Value)
                ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                    .WithData("lyDo", "Nút cha đích không tồn tại.")
                    .WithData("chaMoiId", chaMoiId);

            // 🚨 CHỐNG CHU TRÌNH: đích không được nằm trong chính cây con của nút đang di chuyển.
            // Nhận biết bằng TIỀN TỐ `Path` (đây là lý do `Path` tồn tại — D1): hậu duệ có `Path` bắt đầu
            // bằng `Path` của tổ tiên.
            if (Category.LaToTienCua(nut.Path, chaMoi.Path))
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhDanhMuc)
                    .WithData("lyDo", "Nút đích nằm trong CHÍNH cây con của nút đang di chuyển ⇒ sẽ tạo chu trình.")
                    .WithData("nut", nut.Code)
                    .WithData("chaMoi", chaMoi.Code);
            }
        }

        // Nạp TOÀN BỘ cây con (kể cả chính nút) bằng MỘT truy vấn tiền tố `Path` — đây là lợi ích của D1.
        var cayCon = await _categoryRepository.GetListAsync(x => x.Path.StartsWith(nut.Path));

        if (cayCon.Count == 0)
        {
            // Không thể xảy ra (nút phải nằm trong kết quả) ⇒ lỗi lập trình, phải ồn ào.
            throw new InvalidOperationException(
                $"Truy vấn cây con không trả về chính nút {nut.Id} (Path='{nut.Path}').");
        }

        var capCaoNhatTrongCayCon = cayCon.Max(x => x.Level);
        var chieuCaoCayCon = capCaoNhatTrongCayCon - nut.Level;
        var capMoiCuaNut = (chaMoi?.Level ?? -1) + 1;

        if (capMoiCuaNut + chieuCaoCayCon > Category.DoCaoToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VuotDoSauDanhMuc)
                .WithData("lyDo", "Di chuyển sẽ làm cây con VƯỢT độ sâu tối đa.")
                .WithData("capMoiCuaNut", capMoiCuaNut)
                .WithData("chieuCaoCayCon", chieuCaoCayCon)
                .WithData("toiDa", Category.DoCaoToiDa);
        }

        var pathCu = nut.Path;

        // `DoiCha` dựng lại Path/Level CỦA CHÍNH NÚT (và kiểm tra độ sâu của nút).
        nut.DoiCha(chaMoi);
        var pathMoi = nut.Path;

        var soHauDue = 0;

        foreach (var hauDue in cayCon.Where(x => x.Id != nut.Id))
        {
            var pathHauDueMoi = Category.DoiTienToPath(hauDue.Path, pathCu, pathMoi)
                ?? throw new InvalidOperationException(
                    $"Hậu duệ {hauDue.Id} (Path='{hauDue.Path}') không nằm trong nhánh '{pathCu}' " +
                    "dù đã được nạp bằng truy vấn tiền tố. Đây là lỗi lập trình.");

            // ⚠️ `Level` phải dịch CÙNG một lượng với nút gốc của nhánh; KHÔNG tính lại từ `Path` ở đây
            // để tránh hai nguồn sự thật (chính `KiemTraBatBiên()` sẽ bắt nếu lệch).
            hauDue.DatLaiPath(pathHauDueMoi, hauDue.Level + (capMoiCuaNut - nut.Level));

            await _categoryRepository.UpdateAsync(hauDue, autoSave: false);
            soHauDue++;
        }

        await _categoryRepository.UpdateAsync(nut, autoSave: true);

        Logger.LogInformation(
            "Đã di chuyển nhánh {Code}: '{PathCu}' → '{PathMoi}' (cập nhật {SoHauDue} hậu duệ).",
            nut.Code, pathCu, pathMoi, soHauDue);
    }

    /// <summary>
    /// **Xoá MỀM** một nút — `CAT-002` **D9**: **CẤM** xoá cứng (xoá cứng một nút sẽ kéo theo con qua
    /// `Cascade`, hoặc để lại con **mồ côi** trỏ vào cha không còn) và **cấm** xoá nút còn nút con.
    ///
    /// ⚠️ **CHƯA kiểm tra "còn sản phẩm"**: bảng liên kết `CatProductCategories` **chưa được tạo** trong
    /// lát cắt này ⇒ điều kiện đó sẽ bổ sung khi có bảng (`CAT-002` §12: xoá danh mục còn sản phẩm ⇒ `0021`).
    /// </summary>
    public async Task XoaMemAsync(Guid nutId)
    {
        await using var khoa = await GiaNhKhoaCayAsync();

        var nut = await _categoryRepository.FindAsync(nutId)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Không tìm thấy danh mục cần xoá.")
                .WithData("nutId", nutId);

        var soNutCon = await _categoryRepository.CountAsync(x => x.ParentId == nutId);

        if (soNutCon > 0)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Danh mục còn NÚT CON ⇒ không xoá được (chỉ có thể NGỪNG DÙNG).")
                .WithData("nut", nut.Code)
                .WithData("soNutCon", soNutCon);
        }

        // 🚨 Bổ sung 15/10/2026 (lát cắt 3 — khi bảng liên kết `CatProductCategories` ra đời):
        // XOÁ danh mục còn SẢN PHẨM sẽ làm sản phẩm **mất khỏi mọi danh mục** ⇒ storefront không hiển thị nữa,
        // **không có lỗi nào**. `CAT-002` §12 yêu cầu ném `0021`. (Trước lát cắt 3, điều kiện này **chưa làm được**
        // vì bảng liên kết chưa tồn tại — đã ghi rõ ở §16.3.)
        var soSanPham = await _lienKetRepository.CountAsync(x => x.CategoryId == nutId);

        if (soSanPham > 0)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Danh mục còn SẢN PHẨM ⇒ không xoá được (chỉ có thể NGỪNG DÙNG).")
                .WithData("nut", nut.Code)
                .WithData("soSanPham", soSanPham);
        }

        // Xoá MỀM (`IsDeleted`) — dữ liệu vẫn tra được cho lịch sử/đối soát.
        await _categoryRepository.DeleteAsync(nut, autoSave: true);

        Logger.LogInformation("Đã xoá MỀM danh mục {Code}.", nut.Code);
    }

    /// <summary>
    /// Giành **khoá toàn tenant cho cây danh mục** (`CAT-002` D8). Không giành được ⇒ **NÉM**
    /// `eCommerce:Lock:0001` (`XC-003` D10: **không** bỏ qua im lặng) — mã **dùng chung**, không phải mã riêng
    /// của Catalog (bài học 15/10/2026, xem sổ đăng ký mã lỗi).
    /// </summary>
    private async Task<IAsyncDisposable> GiaNhKhoaCayAsync()
    {
        var khoa = await _distributedLock.TryAcquireAsync(
            eCommerceConsts.LockKeys.CategoryTree(_currentTenant.Id),
            TimeSpan.FromSeconds(ChoKhoaToiDaGiay));

        if (khoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", eCommerceConsts.LockKeys.CategoryTree(_currentTenant.Id))
                .WithData("choGiay", ChoKhoaToiDaGiay);
        }

        return khoa;
    }
}

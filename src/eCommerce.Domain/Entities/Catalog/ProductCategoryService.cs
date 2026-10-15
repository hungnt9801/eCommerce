using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace eCommerce.Catalog;

/// <summary>
/// Dịch vụ miền **gán sản phẩm ↔ danh mục** (`CAT-002` BR-005/006/007, §9.2 `AssignProductsAsync`).
///
/// 🚨 **BA BẤT BIẾN Ở ĐÂY — và mức độ bảo vệ của từng cái KHÁC NHAU:**
///
/// | Bất biến | Ai bảo vệ | Nếu bỏ |
/// |---|---|---|
/// | Mỗi cặp (sản phẩm, danh mục) chỉ gán **một lần** | unique `(TenantId, ProductId, CategoryId)` **NULLS NOT DISTINCT** ⇒ ✅ **DB chặn** | chèn trùng ⇒ cùng sản phẩm hiện 2 lần trong 1 danh mục |
/// | **Tối đa MỘT** danh mục **chính** / sản phẩm | unique **có filter** `(TenantId, ProductId) WHERE IsPrimary` ⇒ ✅ **DB chặn** | hai breadcrumb/URL SEO cho cùng sản phẩm |
/// | **Không** gán vào danh mục **đã ngừng dùng** | ⚠️ **CHỈ tầng này** (phải đọc `IsActive`) ⇒ DB **KHÔNG** chặn được | sản phẩm nằm trong danh mục khách **không nhìn thấy** ⇒ sản phẩm "mất tích" **im lặng** trên storefront |
///
/// ⚠️ **ĐIỂM CHƯA HOÀN THIỆN (ghi rõ, không giấu):** `CAT-002` §8.2 **không** quy định khoá phân tán cho thao
/// tác gán danh mục ⇒ hai request **đồng thời** cùng đặt "danh mục chính" cho một sản phẩm sẽ để **DB chặn**
/// request thua bằng `DbUpdateException` (lỗi kỹ thuật, **không** phải mã nghiệp vụ). Đây **cùng họ** với vấn đề
/// ánh xạ lỗi unique đã ghi ở `cat-001` §17.4 — cần một `IExceptionSubscriber` ở tầng EFCore mới có mã đẹp.
/// </summary>
public class ProductCategoryService : DomainService
{
    private readonly IRepository<ProductCategory, Guid> _lienKetRepository;
    private readonly IRepository<Product, Guid> _sanPhamRepository;
    private readonly IRepository<Category, Guid> _danhMucRepository;

    public ProductCategoryService(
        IRepository<ProductCategory, Guid> lienKetRepository,
        IRepository<Product, Guid> sanPhamRepository,
        IRepository<Category, Guid> danhMucRepository)
    {
        _lienKetRepository = lienKetRepository;
        _sanPhamRepository = sanPhamRepository;
        _danhMucRepository = danhMucRepository;
    }

    /// <summary>
    /// Gán sản phẩm vào danh mục. <paramref name="laDanhMucChinh"/> = `true` ⇒ **hạ** danh mục chính cũ trước
    /// (để không đụng unique index có filter trong cùng một transaction).
    /// </summary>
    public async Task<ProductCategory> GanAsync(Guid productId, Guid categoryId, bool laDanhMucChinh = false)
    {
        // Sản phẩm phải tồn tại (FK sẽ chặn, nhưng kiểm trước cho mã lỗi rõ ràng).
        if (!await _sanPhamRepository.AnyAsync(x => x.Id == productId))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("productId", productId);
        }

        var danhMuc = await _danhMucRepository.FindAsync(categoryId)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Danh mục không tồn tại.")
                .WithData("categoryId", categoryId);

        // 🚨 Bất biến CHỈ tầng này bảo vệ được (DB không chặn): KHÔNG gán vào danh mục đã ngừng dùng.
        if (!danhMuc.IsActive)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Danh mục đã NGỪNG DÙNG ⇒ không gán sản phẩm vào (CAT-002 §12).")
                .WithData("danhMuc", danhMuc.Code);
        }

        var daCo = await _lienKetRepository.FirstOrDefaultAsync(
            x => x.ProductId == productId && x.CategoryId == categoryId);

        if (daCo is not null)
        {
            // Gán lại là **idempotent** (double-click không tạo trùng) — nhưng vẫn cập nhật cờ chính nếu đổi.
            if (laDanhMucChinh && !daCo.IsPrimary)
            {
                await HaDanhMucChinhCuAsync(productId, categoryId);
                daCo.DatLamChinh();
                await _lienKetRepository.UpdateAsync(daCo, autoSave: true);
            }

            return daCo;
        }

        if (laDanhMucChinh)
        {
            await HaDanhMucChinhCuAsync(productId, categoryId);
        }

        var lienKet = ProductCategory.Tao(GuidGenerator.Create(), productId, categoryId, laDanhMucChinh);

        await _lienKetRepository.InsertAsync(lienKet, autoSave: true);

        Logger.LogInformation(
            "Đã gán sản phẩm {ProductId} vào danh mục {Code} (chính: {Chinh}).",
            productId, danhMuc.Code, laDanhMucChinh);

        return lienKet;
    }

    /// <summary>Bỏ gán. ⚠️ Trả về `false` khi **không có** liên kết (không ném lỗi — bỏ gán là idempotent).</summary>
    public async Task<bool> BoGanAsync(Guid productId, Guid categoryId)
    {
        var lienKet = await _lienKetRepository.FirstOrDefaultAsync(
            x => x.ProductId == productId && x.CategoryId == categoryId);

        if (lienKet is null)
        {
            return false;
        }

        await _lienKetRepository.DeleteAsync(lienKet, autoSave: true);

        Logger.LogInformation("Đã bỏ gán sản phẩm {ProductId} khỏi danh mục {CategoryId}.", productId, categoryId);

        return true;
    }

    /// <summary>
    /// Đặt danh mục **chính** cho sản phẩm: **hạ** danh mục chính cũ rồi đặt cái mới.
    /// ⚠️ Hai request **đồng thời** có thể cùng qua bước "hạ" ⇒ DB chặn request thua (xem ghi chú ở lớp).
    /// </summary>
    public async Task<Guid?> DatDanhMucChinhAsync(Guid productId, Guid categoryId)
    {
        var cu = await HaDanhMucChinhCuAsync(productId, categoryId);

        var lienKet = await _lienKetRepository.FirstOrDefaultAsync(
            x => x.ProductId == productId && x.CategoryId == categoryId);

        if (lienKet is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc)
                .WithData("lyDo", "Sản phẩm CHƯA được gán vào danh mục này ⇒ không thể đặt làm danh mục chính.")
                .WithData("productId", productId)
                .WithData("categoryId", categoryId);
        }

        lienKet.DatLamChinh();
        await _lienKetRepository.UpdateAsync(lienKet, autoSave: true);

        Logger.LogInformation("Đã đặt danh mục {CategoryId} làm CHÍNH cho sản phẩm {ProductId}.", categoryId, productId);

        return cu;
    }

    /// <summary>Hạ cờ "chính" của liên kết cũ (nếu có). Trả về `CategoryId` cũ để người gọi biết đã đổi cái nào.</summary>
    private async Task<Guid?> HaDanhMucChinhCuAsync(Guid productId, Guid truDanhMucId)
    {
        var cu = await _lienKetRepository.FirstOrDefaultAsync(
            x => x.ProductId == productId && x.IsPrimary && x.CategoryId != truDanhMucId);

        if (cu is null)
        {
            return null;
        }

        cu.BoChinh();
        await _lienKetRepository.UpdateAsync(cu, autoSave: true);

        return cu.CategoryId;
    }
}

using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Catalog;

/// <summary>
/// App service **thuộc tính + sinh tổ hợp biến thể** (`CAT-002` lát cắt 4).
///
/// 🚨🚨 **BÀI HỌC THẬT (15/10/2026) — LỖI THIẾT KẾ DO TÔI GÂY RA, ĐƯỢC E2E BẮT:**
/// Ban đầu `SelectValueAsync` **THAY THẾ** giá trị cũ của cùng thuộc tính, kèm lập luận *"Đỏ + Xanh là vô nghĩa"*.
/// **Lập luận đó SAI**: `CatProductAttributeSelections` (và unique index `(TenantId, ProductId, AttributeId,
/// AttributeValueId)` của nó) cho phép **NHIỀU giá trị cho một thuộc tính** — đó chính là **bảng màu/size mà
/// người bán chọn** để hệ thống sinh **tổ hợp** (Đỏ-M, Đỏ-L, Xanh-M, Xanh-L). Với luật "mỗi thuộc tính một giá trị"
/// thì tích Descartes **luôn = 1** ⇒ **tính năng sinh tổ hợp mất hết ý nghĩa**, mà **không có lỗi nào**:
/// API vẫn trả `200`, vẫn tạo được 1 biến thể ⇒ nhìn như "chạy đúng".
/// ⇒ Đã sửa: chọn là **THÊM** giá trị (idempotent); bỏ một giá trị cụ thể qua `UnselectValueAsync`.
/// </summary>
[Authorize(eCommerceConsts.Permissions.CatalogAttributes.Default)]
public class CatalogAttributeAppService : ApplicationService, ICatalogAttributeAppService
{
    private readonly IRepository<CatalogAttribute, Guid> _thuocTinhRepository;
    private readonly IRepository<CatalogAttributeValue, Guid> _giaTriRepository;
    private readonly IRepository<ProductAttributeSelection, Guid> _luaChonRepository;
    private readonly IRepository<Product, Guid> _sanPhamRepository;
    private readonly VariantGenerationService _variantGenerationService;

    public CatalogAttributeAppService(
        IRepository<CatalogAttribute, Guid> thuocTinhRepository,
        IRepository<CatalogAttributeValue, Guid> giaTriRepository,
        IRepository<ProductAttributeSelection, Guid> luaChonRepository,
        IRepository<Product, Guid> sanPhamRepository,
        VariantGenerationService variantGenerationService)
    {
        _thuocTinhRepository = thuocTinhRepository;
        _giaTriRepository = giaTriRepository;
        _luaChonRepository = luaChonRepository;
        _sanPhamRepository = sanPhamRepository;
        _variantGenerationService = variantGenerationService;
    }

    public async Task<List<CatalogAttributeDto>> GetListAsync()
    {
        var thuocTinh = await _thuocTinhRepository.GetListAsync();
        var giaTri = await _giaTriRepository.GetListAsync();

        var theoThuocTinh = giaTri.GroupBy(x => x.AttributeId).ToDictionary(g => g.Key, g => g.ToList());

        return thuocTinh
            .OrderBy(x => x.Code)
            .Select(tt => ToDto(tt, theoThuocTinh.TryGetValue(tt.Id, out var ds) ? ds : []))
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task<CatalogAttributeDto> CreateAsync(CreateCatalogAttributeDto input)
    {
        var ma = input.Code.Trim();

        if (await _thuocTinhRepository.AnyAsync(x => x.Code == ma))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaDanhMucKhongHopLe)
                .WithData("lyDo", "Mã thuộc tính đã tồn tại trong tenant.")
                .WithData("code", ma);
        }

        var thuocTinh = new CatalogAttribute(GuidGenerator.Create(), ma, input.Name, input.Usage);

        await _thuocTinhRepository.InsertAsync(thuocTinh, autoSave: true);

        Logger.LogInformation("Đã tạo thuộc tính {Code} ({Usage}).", thuocTinh.Code, thuocTinh.Usage);

        return ToDto(thuocTinh, []);
    }

    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task<CatalogAttributeValueDto> AddValueAsync(Guid attributeId, AddAttributeValueDto input)
    {
        var thuocTinh = await _thuocTinhRepository.GetAsync(attributeId);

        var giaTriMoi = input.Value.Trim();

        var daCo = await _giaTriRepository.FirstOrDefaultAsync(
            x => x.AttributeId == attributeId && x.Value == giaTriMoi);

        if (daCo is not null)
        {
            // Idempotent: thêm lại cùng giá trị ⇒ trả về giá trị đang có (không tạo trùng).
            return ToDto(daCo);
        }

        var giaTri = new CatalogAttributeValue(GuidGenerator.Create(), thuocTinh.Id, giaTriMoi)
        {
            SortOrder = input.SortOrder
        };

        await _giaTriRepository.InsertAsync(giaTri, autoSave: true);

        return ToDto(giaTri);
    }

    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task SelectValueAsync(Guid productId, SelectAttributeValueDto input)
    {
        _ = await _sanPhamRepository.GetAsync(productId);

        var giaTri = await _giaTriRepository.GetAsync(input.AttributeValueId);
        var thuocTinh = await _thuocTinhRepository.GetAsync(giaTri.AttributeId);

        // 🚨 Chỉ thuộc tính `Variant` mới tham gia sinh tổ hợp (BR-008) — chọn `Specification` ở đây
        // sẽ tạo lựa chọn **không bao giờ được dùng**, âm thầm.
        if (thuocTinh.Usage != CatalogAttributeUsage.Variant)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("lyDo", "Chỉ thuộc tính Usage = Variant mới được chọn để sinh tổ hợp (BR-008).")
                .WithData("thuocTinh", thuocTinh.Code)
                .WithData("usage", thuocTinh.Usage.ToString());
        }

        // ✅ CHỌN = **THÊM** giá trị vào bảng giá trị của sản phẩm (nhiều giá trị / thuộc tính là HỢP LỆ —
        // đó là các trục của tích Descartes). Idempotent: chọn lại giá trị đã có ⇒ không tạo trùng.
        var daCo = await _luaChonRepository.FirstOrDefaultAsync(
            x => x.ProductId == productId
                 && x.AttributeId == giaTri.AttributeId
                 && x.AttributeValueId == giaTri.Id);

        if (daCo is not null)
        {
            return;
        }

        await _luaChonRepository.InsertAsync(
            new ProductAttributeSelection(GuidGenerator.Create(), productId, giaTri.AttributeId, giaTri.Id)
            {
                SortOrder = input.SortOrder
            },
            autoSave: true);

        Logger.LogInformation(
            "Sản phẩm {ProductId} chọn thêm {ThuocTinh} = {GiaTri}.",
            productId, thuocTinh.Code, giaTri.Value);
    }

    /// <summary>Bỏ **một giá trị cụ thể** khỏi lựa chọn của sản phẩm.</summary>
    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task UnselectValueAsync(Guid productId, Guid attributeValueId)
    {
        var cu = await _luaChonRepository.GetListAsync(
            x => x.ProductId == productId && x.AttributeValueId == attributeValueId);

        foreach (var c in cu)
        {
            await _luaChonRepository.DeleteAsync(c, autoSave: true);
        }
    }

    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task UnselectAsync(Guid productId, Guid attributeId)
    {
        var cu = await _luaChonRepository.GetListAsync(
            x => x.ProductId == productId && x.AttributeId == attributeId);

        foreach (var c in cu)
        {
            await _luaChonRepository.DeleteAsync(c, autoSave: true);
        }
    }

    public async Task<List<ProductAttributeSelectionDto>> GetSelectionsAsync(Guid productId)
    {
        _ = await _sanPhamRepository.GetAsync(productId);

        var luaChon = await _luaChonRepository.GetListAsync(x => x.ProductId == productId);
        var thuocTinh = await _thuocTinhRepository.GetListAsync();
        var giaTri = await _giaTriRepository.GetListAsync();

        var tenThuocTinh = thuocTinh.ToDictionary(x => x.Id, x => x.Code);
        var tenGiaTri = giaTri.ToDictionary(x => x.Id, x => x.Value);

        return luaChon
            .Select(x => new ProductAttributeSelectionDto
            {
                AttributeId = x.AttributeId,
                AttributeCode = tenThuocTinh.TryGetValue(x.AttributeId, out var c) ? c : "?",
                AttributeValueId = x.AttributeValueId,
                Value = tenGiaTri.TryGetValue(x.AttributeValueId, out var v) ? v : "?"
            })
            .OrderBy(x => x.AttributeCode)
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.CatalogAttributes.Manage)]
    public async Task<GenerateVariantsResultDto> GenerateVariantsAsync(Guid productId, GenerateVariantsDto input)
    {
        var ketQua = await _variantGenerationService.SinhToHopAsync(
            productId, input.DefaultPrice, input.MaxCombinations);

        return new GenerateVariantsResultDto
        {
            Created = ketQua.SoTaoMoi,
            Existing = ketQua.SoDaCo,
            Skus = ketQua.DanhSachSku.ToList()
        };
    }

    private static CatalogAttributeDto ToDto(CatalogAttribute tt, IReadOnlyCollection<CatalogAttributeValue> giaTri) => new()
    {
        Id = tt.Id,
        Code = tt.Code,
        Name = tt.Name,
        Usage = tt.Usage.ToString(),
        IsActive = tt.IsActive,
        Values = giaTri
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList()
    };

    private static CatalogAttributeValueDto ToDto(CatalogAttributeValue gt) => new()
    {
        Id = gt.Id,
        AttributeId = gt.AttributeId,
        Value = gt.Value,
        SortOrder = gt.SortOrder,
        IsActive = gt.IsActive
    };
}

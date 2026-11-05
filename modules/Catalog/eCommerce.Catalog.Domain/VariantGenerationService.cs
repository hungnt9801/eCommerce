using System.Text;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>Kết quả sinh tổ hợp (`CAT-002` §4.1 + BR-012).</summary>
/// <param name="SoTaoMoi">Số biến thể **mới** được tạo.</param>
/// <param name="SoDaCo">Số tổ hợp **đã tồn tại** và **đang bán** (giữ nguyên `Id`/`Sku` — BR-013).</param>
/// <param name="DanhSachSku">SKU của các biến thể **mới** (để phát `ProductVariantCreatedEto` sau này).</param>
/// <param name="SoNgungBan">Số biến thể **NGỪNG BÁN** vì tổ hợp không còn trong tập mong muốn (**BR-012** — **không** xoá).</param>
/// <param name="SoKichHoatLai">Số biến thể **mở bán lại** vì tổ hợp đã quay lại (giữ nguyên `Id`/`Sku`).</param>
public sealed record KetQuaSinhToHop(
    int SoTaoMoi,
    int SoDaCo,
    IReadOnlyList<string> DanhSachSku,
    int SoNgungBan = 0,
    int SoKichHoatLai = 0);

/// <summary>
/// Dịch vụ miền **sinh tổ hợp biến thể** (`CAT-002` D5/D6/D7, §4.1, BR-009→016).
///
/// 🚨 **TRÌNH TỰ BẮT BUỘC (không được đổi thứ tự):**
///   1. **Giành khoá** `catalog:variant-combination:{tenantId}:{productId}` (BR-015; TTL 60 s theo §8.2).
///      Khoá theo **sản phẩm** (không theo tenant) để hai sản phẩm khác nhau vẫn sinh **song song**.
///   2. Nạp giá trị đã chọn ⇒ **đếm tổ hợp TRƯỚC khi ghi** và chặn nếu vượt ngưỡng (D6 ⇒ `Catalog:0009`).
///   3. **Đối chiếu `AttributeHash`** đã có ⇒ tổ hợp nào đã tồn tại thì **GIỮ NGUYÊN `Id`/`Sku`** (BR-013).
///   4. Ghi biến thể mới + `ProductVariantAttribute` **trong CÙNG transaction** (BR-014).
///
/// ⚠️ **Điều KHÔNG làm ở đây (ghi rõ, không giấu):**
///   • **Chưa** phát `ProductVariantCreatedEto` — phải **phân loại** *thông báo* hay *mang dữ liệu* trước
///     (`coding-conventions` §6.2); và `F8` (outbox) vẫn hoãn theo `XC-002`.
///   • **Chưa** xử lý ca **giảm** tập thuộc tính (biến thể cũ không còn tổ hợp tương ứng ⇒ chỉ `IsActive = false`,
///     BR-012) — trả về `SoDaCo` là các tổ hợp còn khớp.
/// </summary>
public class VariantGenerationService : DomainService
{
    /// <summary>TTL khoá theo §8.2 (khoá theo **sản phẩm**).</summary>
    public const int ChoKhoaToiDaGiay = 60;

    private readonly IRepository<Product, Guid> _sanPhamRepository;
    private readonly IRepository<ProductVariant, Guid> _bienTheRepository;
    private readonly IRepository<ProductAttributeSelection, Guid> _luaChonRepository;
    private readonly IRepository<CatalogAttribute, Guid> _thuocTinhRepository;
    private readonly IRepository<CatalogAttributeValue, Guid> _giaTriRepository;
    private readonly IRepository<ProductVariantAttribute, Guid> _bienTheThuocTinhRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public VariantGenerationService(
        IRepository<Product, Guid> sanPhamRepository,
        IRepository<ProductVariant, Guid> bienTheRepository,
        IRepository<ProductAttributeSelection, Guid> luaChonRepository,
        IRepository<CatalogAttribute, Guid> thuocTinhRepository,
        IRepository<CatalogAttributeValue, Guid> giaTriRepository,
        IRepository<ProductVariantAttribute, Guid> bienTheThuocTinhRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _sanPhamRepository = sanPhamRepository;
        _bienTheRepository = bienTheRepository;
        _luaChonRepository = luaChonRepository;
        _thuocTinhRepository = thuocTinhRepository;
        _giaTriRepository = giaTriRepository;
        _bienTheThuocTinhRepository = bienTheThuocTinhRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Sinh (hoặc đối chiếu lại) tổ hợp biến thể cho một sản phẩm.
    /// </summary>
    /// <param name="productId">Sản phẩm cần sinh.</param>
    /// <param name="giaMacDinh">
    /// Giá gán cho biến thể **mới**. 🚨 **BẮT BUỘC** khi sản phẩm **chưa có** biến thể nào — nếu không,
    /// biến thể sinh ra sẽ có **giá 0** và **bán được với giá 0** (lỗi im lặng tốn tiền). Khi sản phẩm đã có
    /// biến thể, `null` ⇒ kế thừa giá của biến thể **đang hoạt động rẻ nhất**.
    /// </param>
    /// <param name="nguongToiDa">Ngưỡng tổ hợp (`MaxVariantCombinations`) — `null` ⇒ dùng mặc định 200.</param>
    public async Task<KetQuaSinhToHop> SinhToHopAsync(
        Guid productId, decimal? giaMacDinh = null, int? nguongToiDa = null)
    {
        await using var khoa = await GiaNhKhoaAsync(productId);

        var sanPham = await _sanPhamRepository.FindAsync(productId)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("productId", productId);

        // Chỉ thuộc tính `Variant` tham gia (BR-008) — `Specification` chỉ mô tả.
        var thuocTinhVariant = await _thuocTinhRepository.GetListAsync(
            x => x.Usage == CatalogAttributeUsage.Variant && x.IsActive);

        var idThuocTinhVariant = thuocTinhVariant.Select(x => x.Id).ToHashSet();

        var luaChon = (await _luaChonRepository.GetListAsync(x => x.ProductId == productId))
            .Where(x => idThuocTinhVariant.Contains(x.AttributeId))
            .ToList();

        // Nhóm theo thuộc tính ⇒ mỗi thuộc tính một trục của tích Descartes.
        var truc = luaChon
            .GroupBy(x => x.AttributeId)
            .Select(g => (
                AttributeId: g.Key,
                AttributeValueIds: (IReadOnlyList<Guid>)g.Select(x => x.AttributeValueId).Distinct().ToList()))
            .ToList();

        var nguong = nguongToiDa ?? VariantCombinationGenerator.NguongToHopMacDinh;

        var toHop = VariantCombinationGenerator.Sinh(truc, nguong);

        if (toHop.Count == 0)
        {
            // Sản phẩm đơn giản (không có thuộc tính `Variant`) ⇒ **không phải lỗi** (`CAT-002` §12).
            Logger.LogInformation(
                "Sản phẩm {Code} không có thuộc tính 'Variant' nào ⇒ không sinh tổ hợp.", sanPham.Code);

            return new KetQuaSinhToHop(0, 0, Array.Empty<string>());
        }

        var bienTheHienCo = await _bienTheRepository.GetListAsync(x => x.ProductId == productId);

        // 🚨 ĐỐI CHIẾU bằng hàm THUẦN (BR-012 + BR-013) — quyết định tạo mới / giữ nguyên / NGỪNG BÁN / mở bán lại.
        var doiChieu = VariantCombinationGenerator.DoiChieu(
            toHop.Select(x => x.AttributeHash).ToList(),
            bienTheHienCo
                .Where(x => x.AttributeHash is not null)
                .Select(x => (Hash: x.AttributeHash!, x.IsActive))
                .ToList());

        var theoHash = bienTheHienCo
            .Where(x => x.AttributeHash is not null)
            .GroupBy(x => x.AttributeHash!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var canTao = doiChieu.TaoMoi.ToHashSet(StringComparer.Ordinal);

        var gia = giaMacDinh ?? bienTheHienCo
            .Where(x => x.IsActive)
            .OrderBy(x => x.Price)
            .Select(x => (decimal?)x.Price)
            .FirstOrDefault();

        if (bienTheHienCo.Count == 0 && (gia is null || gia <= 0m))
        {
            // 🚨 Biến thể sinh ra mà **giá 0** thì **bán được với giá 0** ⇒ mất tiền **im lặng**.
            // Đây là quyết định BỔ SUNG của tôi (tài liệu `CAT-002` **không** nói về giá khi sinh tổ hợp) —
            // ghi rõ ở `cat-002` §19.3.
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe)
                .WithData("lyDo", "Sản phẩm chưa có biến thể nào ⇒ BẮT BUỘC truyền giá mặc định > 0 " +
                                  "(nếu không, biến thể sẽ có giá 0 và BÁN ĐƯỢC với giá 0).")
                .WithData("productId", productId);
        }

        var giaDung = gia ?? 0m;

        var maSkuDaDung = (await _bienTheRepository.GetListAsync())
            .Select(x => x.Sku)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var skuMoi = new List<string>();
        var tatCaGiaTri = await _giaTriRepository.GetListAsync();

        // ── 1. Tạo biến thể MỚI cho tổ hợp chưa có (BR-013 chỉ áp cho tổ hợp ĐÃ có) ──
        foreach (var th in toHop.Where(x => canTao.Contains(x.AttributeHash)))
        {
            var maSku = SinhSku(sanPham.Code, th, thuocTinhVariant, tatCaGiaTri);

            if (!maSkuDaDung.Add(maSku))
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuDaTonTai)
                    .WithData("sku", maSku)
                    .WithData("lyDo", "SKU sinh ra từ tổ hợp đã tồn tại (trong tenant).");
            }

            var bienThe = sanPham.AddVariant(GuidGenerator.Create(), maSku, giaDung);

            bienThe.GanToHop(th.AttributeHash);

            await _bienTheRepository.InsertAsync(bienThe, autoSave: false);

            // Ghi luôn các cặp (thuộc tính, giá trị) của biến thể để storefront dựng bộ chọn.
            foreach (var (attributeId, attributeValueId) in th.GiaTri)
            {
                await _bienTheThuocTinhRepository.InsertAsync(
                    new ProductVariantAttribute(GuidGenerator.Create(), bienThe.Id, attributeId, attributeValueId),
                    autoSave: false);
            }

            skuMoi.Add(maSku);
        }

        // ── 2. NGỪNG BÁN biến thể mồ côi (BR-012: **KHÔNG** xoá — biến thể có thể đã phát sinh giao dịch) ──
        foreach (var hash in doiChieu.NgungBan)
        {
            var bienThe = theoHash[hash];
            bienThe.NgungBan();
            await _bienTheRepository.UpdateAsync(bienThe, autoSave: false);
        }

        // ── 3. MỞ BÁN LẠI biến thể cũ khi tổ hợp quay lại (giữ nguyên `Id`/`Sku`) ──
        foreach (var hash in doiChieu.KichHoatLai)
        {
            var bienThe = theoHash[hash];
            bienThe.MoBanLai();
            await _bienTheRepository.UpdateAsync(bienThe, autoSave: false);
        }

        if (skuMoi.Count > 0 || doiChieu.NgungBan.Count > 0 || doiChieu.KichHoatLai.Count > 0)
        {
            await _sanPhamRepository.UpdateAsync(sanPham, autoSave: true);
        }

        Logger.LogInformation(
            "Sinh tổ hợp cho {Code}: tạo mới {Moi}, đang bán {DangBan}, NGỪNG BÁN {Ngung}, mở bán lại {Lai} (ngưỡng {Nguong}).",
            sanPham.Code, skuMoi.Count, doiChieu.GiuNguyen.Count,
            doiChieu.NgungBan.Count, doiChieu.KichHoatLai.Count, nguong);

        return new KetQuaSinhToHop(
            skuMoi.Count, doiChieu.GiuNguyen.Count, skuMoi,
            doiChieu.NgungBan.Count, doiChieu.KichHoatLai.Count);
    }

    /// <summary>
    /// Sinh **SKU** từ tổ hợp: `{mã sản phẩm}-{giá trị 1}-{giá trị 2}…`, chuẩn hoá về chữ HOA/số/`-`.
    /// ⚠️ SKU **bất biến sau khi phát sinh giao dịch** (`CAT-001` D2) nên quy tắc sinh phải **ổn định**:
    /// đầu vào là (mã sản phẩm, giá trị) **đã sắp theo `AttributeId`** ⇒ cùng tổ hợp luôn cho cùng SKU.
    /// </summary>
    private static string SinhSku(
        string maSanPham,
        VariantCombinationGenerator.ToHop toHop,
        IReadOnlyList<CatalogAttribute> thuocTinh,
        IReadOnlyList<CatalogAttributeValue> giaTri)
    {
        var theoId = giaTri.ToDictionary(x => x.Id, x => x.Value);

        var phan = toHop.GiaTri
            .OrderBy(x => x.AttributeId)
            .Select(x => ChuanHoa(theoId.TryGetValue(x.AttributeValueId, out var v) ? v : "?"));

        var sku = $"{ChuanHoa(maSanPham)}-{string.Join('-', phan)}";

        if (sku.Length <= Product.DoDaiSkuToiDa)
        {
            return sku;
        }

        // ⚠️ Vượt 64 ký tự ⇒ cắt bớt nhưng **phải ổn định** (cùng tổ hợp ⇒ cùng SKU). Dùng 8 ký tự đầu
        // của hash tổ hợp (hash đã ổn định theo BR-016) làm hậu tố phân biệt.
        var duoi = toHop.AttributeHash[..8].ToUpperInvariant();
        var giu = Product.DoDaiSkuToiDa - duoi.Length - 1;

        return $"{sku[..giu]}-{duoi}";
    }

    private static string ChuanHoa(string s)
    {
        var sb = new StringBuilder(s.Length);

        foreach (var c in s.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c) && c <= 127)
            {
                sb.Append(c);
            }
            else if (c is ' ' or '-' or '_' or '.')
            {
                sb.Append('-');
            }
        }

        return sb.Length == 0 ? "X" : sb.ToString();
    }

    private async Task<IAsyncDisposable> GiaNhKhoaAsync(Guid productId)
    {
        var ten = eCommerceConsts.LockKeys.VariantCombination(_currentTenant.Id, productId);

        var khoa = await _distributedLock.TryAcquireAsync(ten, TimeSpan.FromSeconds(ChoKhoaToiDaGiay));

        if (khoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", ten)
                .WithData("choGiay", ChoKhoaToiDaGiay);
        }

        return khoa;
    }
}

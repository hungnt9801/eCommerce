using eCommerce.Catalog;
using eCommerce.Orders;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace eCommerce.DbMigrator;

/// <summary>
/// **BACKFILL CATALOG — WP-03 mục 5 + 6** (`handover-n3`): chuyển mô hình cũ
/// (`CatProductVariants`, mỗi biến thể là một hàng bán được) sang mô hình Nhanh (D-07):
///
/// <list type="number">
///   <item>mỗi **biến thể** → **một sản phẩm con** (`ParentId` = sản phẩm cha cũ);</item>
///   <item>sản phẩm con có **một ĐVT gốc** mang **SKU/barcode/giá** của biến thể;</item>
///   <item>mỗi lần chuyển được ghi vào **bảng đối chiếu** `CatVariantMaps` (`VariantId → ProductId`);</item>
///   <item>**`OrdOrderLines.ProductId`** được gán theo `Sku` (mục 6) — giữ nguyên `Sku`/`ProductVariantId` cũ.</item>
/// </list>
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔒 **BỐN LUẬT CỦA LẦN CHẠY (mỗi luật là một lỗi đã biết trước, không phải phòng xa):**
/// <list type="number">
///   <item>**IDEMPOTENT** — biến thể đã có dòng trong `CatVariantMaps` thì **bỏ qua**. Chạy lần hai cho **cùng
///     kết quả** (điều kiện xanh của phiên `N3`), không tạo sản phẩm con thứ hai.</item>
///   <item>**KHÔNG BAO GIỜ GHI ĐÈ** — nếu mã sản phẩm (= SKU) đã tồn tại, hoặc barcode đã dùng ở ĐVT khác, hoặc
///     cha không tồn tại ⇒ **DỪNG, không ghi gì** và liệt kê xung đột. Ghi đè/đổi mã tự động sẽ làm **mất**
///     dữ liệu thật của người dùng (sản phẩm trùng mã do họ tạo tay) mà không có dấu vết.</item>
///   <item>**ĐỐI SOÁT TRƯỚC KHI GHI** — đếm `đã chuyển + sẽ tạo` phải **bằng** tổng số biến thể; lệch ⇒ dừng.
///     Đây là *"n variant → n child"* của kế hoạch, kiểm **trước** khi mở transaction.</item>
///   <item>**MỘT TRANSACTION** — mọi insert (sản phẩm con + ĐVT + bản đồ) và update (dòng đơn) nằm trong **một**
///     giao dịch: lỗi giữa đường thì **không** để lại nửa danh mục đã chuyển (không có trạng thái nửa vời).</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** tên ĐVT gốc của sản phẩm con là hằng số <see cref="TenDonViMacDinh"/> vì
/// `CatProductVariants` **không có** thông tin đơn vị tính (mô hình cũ không có khái niệm ĐVT). Người dùng
/// đổi tên/giá theo ĐVT sau khi chuyển. 📌 Không suy đoán tên ĐVT từ `AttributeHash` — đó là tổ hợp thuộc
/// tính, không phải đơn vị.
/// </summary>
public class BackfillCatalogService : ITransientDependency
{
    /// <summary>Tên ĐVT gốc của sản phẩm con khi dữ liệu cũ không có đơn vị tính nào để chép.</summary>
    public const string TenDonViMacDinh = "Cái";

    private const int DoDaiTenSanPhamToiDa = 512;

    private readonly IRepository<Product, Guid> _sanPham;
    private readonly IRepository<ProductVariant, Guid> _bienThe;
    private readonly IRepository<ProductUnit, Guid> _donVi;
    private readonly IRepository<ProductVariantMap, Guid> _banDo;
    private readonly IRepository<OrderLine, Guid> _dongDon;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public BackfillCatalogService(
        IRepository<Product, Guid> sanPham,
        IRepository<ProductVariant, Guid> bienThe,
        IRepository<ProductUnit, Guid> donVi,
        IRepository<ProductVariantMap, Guid> banDo,
        IRepository<OrderLine, Guid> dongDon,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _sanPham = sanPham;
        _bienThe = bienThe;
        _donVi = donVi;
        _banDo = banDo;
        _dongDon = dongDon;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Chạy backfill. <paramref name="dryRun"/> = <c>true</c> ⇒ **chỉ đếm và in**, không mở transaction ghi.
    /// Trả về số đo để tầng gọi (script/CI) khẳng định được kết quả thay vì đọc log.
    /// </summary>
    public async Task<BackfillCatalogResult> ChayAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[BACKFILL-CATALOG] dryRun={dryRun}");

        // ── ① Đọc hiện trạng ────────────────────────────────────────────────────────────────────────
        var dsBienThe = await _bienThe.GetListAsync(cancellationToken: cancellationToken);
        var dsBanDo = await _banDo.GetListAsync(cancellationToken: cancellationToken);
        var dsSanPham = await _sanPham.GetListAsync(cancellationToken: cancellationToken);
        var dsDonVi = await _donVi.GetListAsync(cancellationToken: cancellationToken);
        var dsDongDon = await _dongDon.GetListAsync(x => x.ProductId == null, cancellationToken: cancellationToken);

        var daBanDo = dsBanDo.Select(x => x.VariantId).ToHashSet();
        var sanPhamTheoId = dsSanPham.ToDictionary(x => x.Id);

        var maDaDung = dsSanPham.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var barcodeDaDung = dsDonVi
            .Where(x => x.Barcode is not null)
            .Select(x => x.Barcode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ── ② Dựng danh sách việc (KHÔNG ghi gì) ───────────────────────────────────────────────────
        var xungDot = new List<string>();
        var canTao = new List<ViecChuyen>();

        foreach (var bienThe in dsBienThe)
        {
            if (daBanDo.Contains(bienThe.Id))
            {
                continue;   // ③ IDEMPOTENT: đã chuyển ở lần trước.
            }

            if (!sanPhamTheoId.TryGetValue(bienThe.ProductId, out var cha))
            {
                xungDot.Add($"variant '{bienThe.Sku}' (id {bienThe.Id}): sản phẩm cha {bienThe.ProductId} KHÔNG tồn tại");
                continue;
            }

            if (maDaDung.Contains(bienThe.Sku))
            {
                xungDot.Add($"variant '{bienThe.Sku}': mã sản phẩm '{bienThe.Sku}' đã tồn tại ⇒ không ghi đè");
                continue;
            }

            var barcode = ProductUnit.ChuanHoaBarcode(bienThe.Barcode);

            if (barcode is not null && barcodeDaDung.Contains(barcode))
            {
                xungDot.Add($"variant '{bienThe.Sku}': barcode '{barcode}' đã được ĐVT khác dùng (unique toàn tenant)");
                continue;
            }

            canTao.Add(new ViecChuyen(bienThe, cha, barcode));
            maDaDung.Add(bienThe.Sku);

            if (barcode is not null)
            {
                barcodeDaDung.Add(barcode);
            }
        }

        // ── ③ ĐỐI SOÁT TRƯỚC KHI GHI: "n variant → n child" ───────────────────────────────────────
        var tong = dsBienThe.Count;
        var daCo = daBanDo.Count;
        var seTao = canTao.Count;

        Console.WriteLine(
            $"[BACKFILL-CATALOG] variants={tong} alreadyMapped={daCo} toCreate={seTao} conflicts={xungDot.Count}");

        if (daCo + seTao + xungDot.Count != tong)
        {
            xungDot.Add($"ĐỐI SOÁT LỆCH: alreadyMapped({daCo}) + toCreate({seTao}) + conflicts({xungDot.Count}) != variants({tong})");
        }

        foreach (var loi in xungDot)
        {
            Console.WriteLine($"[BACKFILL-CATALOG] CONFLICT: {loi}");
        }

        if (xungDot.Count > 0)
        {
            Console.WriteLine("[BACKFILL-CATALOG] ABORT — KHÔNG ghi gì (xem CONFLICT ở trên).");

            return new BackfillCatalogResult(tong, daCo, 0, dsDongDon.Count, 0, xungDot, dryRun, false);
        }

        // ── ④ Dòng đơn: gán `ProductId` theo `Sku` (mục 6) — tính cả sản phẩm con SẼ tạo ──────────
        var sanPhamTheoMa = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var sp in dsSanPham)
        {
            sanPhamTheoMa[sp.Code] = sp.Id;
        }

        // ⚠️ Sinh **Id sản phẩm con + Id ĐVT** NGAY tại đây (không phải lúc ghi) để bước tính dòng đơn và bước
        // ghi dùng **CÙNG một id**. Sinh hai lần thì "đã tính" khác "đã ghi" — dòng đơn trỏ vào sản phẩm không
        // tồn tại, và **không có lỗi nào** (FK không kiểm vì `ProductId` bên Sales là cột thường).
        for (var i = 0; i < canTao.Count; i++)
        {
            canTao[i] = canTao[i] with { ProductId = _guidGenerator.Create(), UnitId = _guidGenerator.Create() };
            sanPhamTheoMa[canTao[i].BienThe.Sku] = canTao[i].ProductId;
        }

        var dongDonCanGan = new List<(OrderLine Dong, Guid ProductId)>();

        foreach (var dong in dsDongDon)
        {
            if (sanPhamTheoMa.TryGetValue(dong.Sku, out var productId))
            {
                dongDonCanGan.Add((dong, productId));
            }
        }

        Console.WriteLine(
            $"[BACKFILL-CATALOG] orderLines={dsDongDon.Count} toLink={dongDonCanGan.Count}");

        // ── ⑤ Ghi (một transaction) hoặc kết thúc ở dry-run ────────────────────────────────────────
        if (!dryRun && (canTao.Count > 0 || dongDonCanGan.Count > 0))
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

            foreach (var viec in canTao)
            {
                var con = TaoSanPhamCon(viec);

                await _sanPham.InsertAsync(con, autoSave: false);

                await _banDo.InsertAsync(
                    new ProductVariantMap(
                        _guidGenerator.Create(),
                        viec.BienThe.Id,
                        con.Id,
                        viec.UnitId,
                        viec.BienThe.Sku),
                    autoSave: false);
            }

            foreach (var (dong, productId) in dongDonCanGan)
            {
                dong.ProductId = productId;

                await _dongDon.UpdateAsync(dong, autoSave: false);
            }

            await uow.CompleteAsync();
        }

        var ghiChu = dryRun ? "(DRY-RUN: KHONG ghi gi)" : "(da ghi)";

        Console.WriteLine($"[BACKFILL-CATALOG] RESULT: {tong} variant -> {seTao} child {ghiChu}");
        Console.WriteLine($"[BACKFILL-CATALOG] ORDERLINE: {dsDongDon.Count} unlinked -> {dongDonCanGan.Count} linked");
        Console.WriteLine("[BACKFILL-CATALOG] OK");

        return new BackfillCatalogResult(
            tong, daCo, seTao, dsDongDon.Count, dongDonCanGan.Count, [], dryRun, true);
    }

    /// <summary>
    /// Dựng **sản phẩm con** từ một biến thể: `Code` = SKU (khoá đối chiếu cũ), ĐVT gốc mang barcode/giá,
    /// `ParentId` = sản phẩm cha, và các trường thuế/khối lượng **chép từ cha/biến thể** (không tự chế số).
    /// </summary>
    private static Product TaoSanPhamCon(ViecChuyen viec)
    {
        var ten = viec.Cha.Name + " — " + viec.BienThe.Sku;

        var con = new Product(viec.ProductId, viec.BienThe.Sku, ten.Length <= DoDaiTenSanPhamToiDa ? ten : ten[..DoDaiTenSanPhamToiDa])
        {
            Description = viec.Cha.Description,
            BrandId = viec.Cha.BrandId,

            // 🔒 Thuế **chép nguyên** từ cha: bịa/để trống sẽ làm hoá đơn của sản phẩm con sai thuế.
            VatMode = viec.Cha.VatMode,
            WeightGram = viec.BienThe.WeightInGrams
        };

        // 🚨 Đi qua GUARD của domain (khoảng -100..100) — không gán thẳng thuộc tính.
        con.DatVatPercent(viec.Cha.VatPercent);
        con.DatCha(viec.Cha.Id);

        con.AddUnit(
            viec.UnitId,
            TenDonViMacDinh,
            conversionFactor: 1m,
            salePrice: viec.BienThe.Price,
            isBase: true,
            barcode: viec.Barcode,
            oldPrice: viec.BienThe.CompareAtPrice);

        return con;
    }

    /// <summary>Một việc chuyển: biến thể + cha + (id sản phẩm con/ĐVT sinh sẵn để tính trước dòng đơn).</summary>
    private sealed record ViecChuyen(
        ProductVariant BienThe,
        Product Cha,
        string? Barcode,
        Guid ProductId = default,
        Guid UnitId = default);
}

/// <summary>Số đo của một lần backfill — để script/CI khẳng định, không phải đọc log bằng mắt.</summary>
/// <param name="TongBienThe">Số biến thể đọc được (`CatProductVariants`).</param>
/// <param name="DaBanDo">Số biến thể đã có dòng đối chiếu từ lần chạy trước.</param>
/// <param name="SeTao">Số sản phẩm con tạo/đã tạo trong lần này.</param>
/// <param name="TongDongDon">Số dòng đơn còn `ProductId = null`.</param>
/// <param name="SeGanDon">Số dòng đơn được gán `ProductId`.</param>
/// <param name="XungDot">Danh sách xung đột (rỗng = chạy được).</param>
/// <param name="DryRun">Lần chạy này có ghi hay không.</param>
/// <param name="ThanhCong"><c>false</c> = đã DỪNG vì xung đột, **không ghi gì**.</param>
public sealed record BackfillCatalogResult(
    int TongBienThe,
    int DaBanDo,
    int SeTao,
    int TongDongDon,
    int SeGanDon,
    IReadOnlyList<string> XungDot,
    bool DryRun,
    bool ThanhCong);

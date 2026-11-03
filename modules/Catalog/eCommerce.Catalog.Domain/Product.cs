using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// Sản phẩm (aggregate root). Giá nằm ở <see cref="ProductVariant"/> vì cùng một sản phẩm
/// có nhiều biến thể với giá và tồn kho riêng.
///
/// 🚨 **MÁY TRẠNG THÁI Ở TẦNG DOMAIN** (`CAT-001` **D5** — đóng **F3**):
/// trước đây `IsPublished` là `bool` có `set` công khai ⇒ **không có chỗ nào** chặn chuyển trạng thái sai.
/// Nay `Status` chỉ đổi được qua <see cref="Publish"/>/<see cref="Unpublish"/>/<see cref="Archive"/>,
/// và **chuyển sai ⇒ NÉM LỖI** (quy ước: máy trạng thái không được im lặng bỏ qua).
/// </summary>
public class Product : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>SKU: 1..64 ký tự, chỉ chữ/số và <c>. _ -</c> (`CAT-001` BR-002).</summary>
    public const int DoDaiSkuToiDa = eCommerceConsts.CatalogLimits.SkuToiDa;

    /// <summary>Mã sản phẩm: 1..64 ký tự (khớp `HasMaxLength(64)` ở DbContext).</summary>
    public const int DoDaiMaToiDa = eCommerceConsts.CatalogLimits.MaToiDa;

    private static readonly Regex KhuonSku = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    public Guid? TenantId { get; protected set; }

    /// <summary>Mã nội bộ, duy nhất trong tenant. ⚠️ **Bất biến sau khi publish** (`CAT-001` D2).</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>
    /// 🚨 **Khoá định danh CÔNG KHAI của sản phẩm trên URL** (`CAT-003` ADR-03/ADR-05).
    ///
    /// ⚠️ **`protected set`** — cố ý **KHÔNG** cho gán thẳng từ ngoài: đổi slug **phải** đi qua
    /// <see cref="DatSlug"/> để (a) **chuẩn hoá**, (b) **phát sự kiện `ProductSlugChangedEto` kèm `OldSlug`**
    /// ⇒ `WEB-002` giữ được **301** (`CAT-003` **ADR-05**). Nếu cho `set` công khai thì đường gán tắt **không phát
    /// sự kiện** ⇒ link cũ **404** và **không có lỗi nào** nổi lên (đúng họ lỗi im lặng `XC-003` F1).
    /// </summary>
    public string? Slug { get; protected set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    // ── WP-03 (02/11/2026) — mô hình Nhanh (`03 §3.1`, D-07) ─────────────────────────────────────────
    // 📌 Tất cả là **CỘT MỚI NULLABLE/CÓ MẶC ĐỊNH** ⇒ migration additive, dữ liệu cũ không phải sửa.

    /// <summary>
    /// Loại sản phẩm (10 loại của Nhanh). Mặc định <see cref="ProductType.Standard"/> để sản phẩm cũ
    /// (mọi sản phẩm đang có đều là "thường") **không phải backfill** ngay ở bước này.
    /// ⚠️ Chưa có máy trạng thái cho việc đổi loại: đổi loại là thao tác quản trị, và luật chi tiết
    /// (IMEI bắt buộc serial, combo bắt buộc thành phần…) thuộc nửa sau của WP-03.
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Standard;

    /// <summary>
    /// Sản phẩm **CHA** (cha–con của Nhanh, thay cho mô hình "variant là nơi giữ tồn/giá" — D-07).
    /// Cha **không có tồn**, con mới bán được. 🔒 Luật *"cha không bán được"* thi hành ở
    /// `ProductAppService.PublishAsync` (phải hỏi repository *"có con không"* — aggregate không giữ collection con),
    /// mã lỗi `0047`. Chu trình nhiều mức: <see cref="CaySanPham.KiemTraKhongChuTrinh"/> (mã `0048`).
    /// 🔒 Chỉ đổi qua <see cref="DatCha"/> để chặn tự làm cha của chính mình.
    /// </summary>
    public Guid? ParentId { get; protected set; }

    /// <summary>
    /// % VAT: `-1` không chịu thuế · `-2` không kê khai · `-100` thuế 0% · `0..100` là % thường.
    /// `null` = **chưa khai**. 🚨 Chỉ đổi qua <see cref="DatVatPercent"/> (guard khoảng) + `CHECK` ở DB.
    /// </summary>
    public decimal? VatPercent { get; protected set; }

    /// <summary>Giá đã gồm hay chưa gồm VAT (xem <see cref="VatMode"/>). Mặc định **chưa gồm** như Nhanh.</summary>
    public VatMode VatMode { get; set; } = VatMode.Excluded;

    /// <summary>Khối lượng (gram) — dùng cho phí vận chuyển; ưu tiên hơn khối lượng của biến thể cũ.</summary>
    public int? WeightGram { get; set; }

    /// <summary>Số tháng bảo hành (0 = không bảo hành).</summary>
    public int? WarrantyMonths { get; set; }

    /// <summary>Số ngày lưu kho tối đa trước khi cảnh báo (hàng theo lô/hạn dùng).</summary>
    public int? StorageTimeDays { get; set; }

    /// <summary>Cờ "hàng hot" — chỉ phục vụ hiển thị/sắp xếp, không phải luật nghiệp vụ.</summary>
    public bool IsHot { get; set; }

    /// <summary>Cờ "hàng mới" — chỉ phục vụ hiển thị/sắp xếp, không phải luật nghiệp vụ.</summary>
    public bool IsNew { get; set; }

    /// <summary>Mã sản phẩm ở hệ thống ngoài (sàn/kho cũ) — dùng đối chiếu khi nhập dữ liệu.</summary>
    public string? ExternalId { get; set; }

    private readonly List<ProductUnit> _units = new();

    /// <summary>
    /// Các **đơn vị tính** của sản phẩm (1..n) — nơi giữ giá bán thay cho `ProductVariant` (D-07).
    /// 🚨 Đúng **một** đơn vị là gốc (`IsBase`), và đơn vị **đầu tiên** tự thành gốc nếu chưa có.
    /// </summary>
    public IReadOnlyCollection<ProductUnit> Units => _units.AsReadOnly();

    /// <summary>
    /// Vòng đời hiển thị. 🚨 Chỉ đổi qua <see cref="Publish"/>/<see cref="Unpublish"/>/<see cref="Archive"/>
    /// ⇒ không có đường nào "set thẳng" bỏ qua guard.
    /// </summary>
    public ProductStatus Status { get; protected set; } = ProductStatus.Draft;

    /// <summary>
    /// ⚠️ **Property DẪN XUẤT, chỉ-đọc** (`CAT-001` D5): giữ tên cũ để không phá code/DTO đang dùng,
    /// nhưng **không còn `set`** ⇒ mọi thay đổi phải đi qua máy trạng thái.
    /// </summary>
    public bool IsPublished => Status == ProductStatus.Published;

    private readonly List<ProductVariant> _variants = new();

    /// <summary>
    /// Biến thể — mỗi biến thể có SKU riêng và là đơn vị mà sàn TMĐT tham chiếu.
    /// </summary>
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    protected Product() { }

    public Product(Guid id, string code, string name) : base(id)
    {
        Code = ChuanHoaMa(code);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>
    /// 🚨 **ĐẶT SLUG — ĐƯỜNG DUY NHẤT** (`CAT-003` **ADR-05**).
    ///
    /// **VÌ SAO SLUG BẤT BIẾN MẶC ĐỊNH (không sinh lại khi đổi `Name`):** mọi lần sửa tên sản phẩm mà **tự động**
    /// đổi slug sẽ **phá URL đã được Google index** và **link đã chia sẻ** ⇒ mất SEO **âm thầm** (không lỗi,
    /// không log). ⇒ Đổi slug là **hành động TƯỜNG MINH** của người dùng.
    ///
    /// 🔑 **Phát <see cref="ProductSlugChangedEto"/> kèm `OldSlug`** — trả lời **Q3 của `WEB-002`**:
    /// *"ETO có mang `OldSlug` không?"* ⇒ **CÓ**. Không có nó thì `WEB-002` **không** dựng được bảng 301
    /// (`WEB-002` **BR-005**) ⇒ khách vào link cũ nhận **404** thay vì được chuyển hướng.
    ///
    /// 🔒 **Không phát sự kiện khi slug KHÔNG đổi** (idempotent): nếu phát, `WEB-002` sẽ ghi một dòng 301
    /// *"từ A đến A"* mỗi lần gọi lặp ⇒ bảng chuyển hướng đầy **dòng vô nghĩa** và **vòng lặp 301**.
    /// </summary>
    /// <param name="slugMoi">Slug đã sinh/chọn (chuỗi rỗng hoặc `null` = **chưa có URL công khai**).</param>
    /// <returns>
    /// <c>null</c> nếu slug **KHÔNG đổi** (chỗ gọi **không** ghi DB, **không** phát sự kiện);
    /// ngược lại trả **slug CŨ** để app service phát `ProductSlugChangedEto` kèm `OldSlug`.
    /// </returns>
    /// <remarks>
    /// ⚠️ **VÌ SAO TRẢ `string?` MÀ KHÔNG TỰ PHÁT SỰ KIỆN Ở ĐÂY (layering):** ETO nằm ở
    /// `eCommerce.Application.Contracts` mà tầng `Domain` **không được** tham chiếu (bất biến #2/#3 của dự án).
    /// Khuôn có sẵn của repo là như vậy: `Product.Publish()` **không** phát `ProductPublishedEto` —
    /// `ProductAppService.PublishAsync` phát (`ProductAppService.cs:262`).
    /// 📌 Hệ quả CẦN BIẾT: nếu có đường ghi slug **không** đi qua app service thì sự kiện **không** được phát
    /// ⇒ `WEB-002` không dựng được 301. Vì vậy `Slug` có `protected set` (**không** gán thẳng từ ngoài),
    /// và test `ProductSlugTests` khoá lại hợp đồng "trả về slug cũ".
    /// </remarks>
    public string? DatSlug(string? slugMoi)
    {
        var chuanHoa = string.IsNullOrWhiteSpace(slugMoi) ? null : slugMoi.Trim();

        if (chuanHoa is { Length: > SinhSlug.DoDaiToiDa })
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(slugMoi))
                .WithData("doDai", chuanHoa.Length)
                .WithData("toiDa", SinhSlug.DoDaiToiDa)
                .WithData("lyDo", "Slug vượt độ dài tối đa ⇒ sẽ bị DB cắt hoặc vỡ `canonical`.");
        }

        // 🔒 So sánh **phân biệt hoa/thường** (`Ordinal`): slug là **khoá URL**; `"SP-A"` và `"sp-a"` là hai
        // khoá khác nhau ở tầng DB (index `NULLS NOT DISTINCT` không gộp hoa/thường) ⇒ coi là "không đổi" sẽ
        // khiến DB giữ một giá trị mà hàm này tưởng đã đổi ⇒ lệch giữa bộ nhớ và DB.
        if (string.Equals(Slug, chuanHoa, StringComparison.Ordinal))
        {
            return null;
        }

        var slugCu = Slug;
        Slug = chuanHoa;

        return slugCu;
    }

    /// <summary>
    /// Đổi mã sản phẩm — **CHỈ khi còn Nháp** (`CAT-001` **D2**).
    /// 🚨 Vì sao cấm sau khi publish: `Code` là khoá đối chiếu đã ra ngoài (báo cáo, đối soát, listing sàn).
    /// Đổi nó sau khi đã bán ⇒ **báo cáo cũ trỏ sai** và **không có lỗi nào**.
    /// </summary>
    public void DoiMa(string maMoi)
    {
        if (Status != ProductStatus.Draft)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DaPhatSinhGiaoDich)
                .WithData("hanhDong", nameof(DoiMa))
                .WithData("status", Status.ToString())
                .WithData("lyDo", "Mã sản phẩm bất biến sau khi publish (CAT-001 D2).");
        }

        Code = ChuanHoaMa(maMoi);
    }

    /// <summary>
    /// Thêm biến thể. Kiểm tra **trong phạm vi sản phẩm**; việc chống trùng **toàn tenant** do
    /// app service + unique index DB lo (`CAT-001` D3/D6 — xem ghi chú ở <see cref="ProductVariant"/>).
    /// </summary>
    public ProductVariant AddVariant(Guid id, string sku, decimal price, string? barcode = null)
    {
        var skuChuanHoa = ChuanHoaSku(sku);

        if (_variants.Any(x => string.Equals(x.Sku, skuChuanHoa, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuDaTonTai)
                .WithData("sku", skuChuanHoa)
                .WithData("phamVi", $"sản phẩm '{Code}'");
        }

        KiemTraGia(price);

        var variant = new ProductVariant(id, skuChuanHoa, price, barcode);
        _variants.Add(variant);
        return variant;
    }

    /// <summary>
    /// Gán sản phẩm cha (`03 §3.1`). 🔒 Chặn **tự làm cha của chính mình** ngay tại aggregate; chu trình
    /// nhiều mức (`A → B → A`) cần đọc cả chuỗi tổ tiên nên nằm ở <see cref="CaySanPham.KiemTraKhongChuTrinh"/>
    /// (app service gọi trước khi gọi hàm này — WP-03 nửa sau).
    /// ⚠️ `null` = sản phẩm độc lập (hợp lệ, idempotent).
    /// </summary>
    public void DatCha(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SanPhamChaTrungChinhNo)
                .WithData("productId", Id);
        }

        ParentId = parentId;
    }

    /// <summary>
    /// Gán % VAT — **ĐƯỜNG DUY NHẤT** ghi `VatPercent` (`03 §3.1`: `-1`/`-2`/`-100` có nghĩa đặc biệt).
    ///
    /// 🚨 VÌ SAO PHẢI GUARD Ở ĐÂY: `-101` hay `101` vẫn là `decimal` hợp lệ với compiler và với DB kiểu
    /// `numeric(5,2)`; chỉ `CHECK` của DB mới chặn. Không kiểm ở domain ⇒ người dùng nhận lỗi DB khó hiểu
    /// và **transaction đã mở**, còn nếu ai bỏ `CHECK` thì **im lặng** ghi ra hoá đơn sai thuế.
    /// </summary>
    public void DatVatPercent(decimal? phanTram)
    {
        if (phanTram is < -100m or > 100m)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VatPercentKhongHopLe)
                .WithData("vatPercent", phanTram);
        }

        VatPercent = phanTram;
    }

    /// <summary>
    /// Thêm **đơn vị tính** (D-07: nơi giữ giá bán thay cho `ProductVariant`).
    ///
    /// 🔒 Ba bất biến kiểm **trong phạm vi một sản phẩm** (DB giữ hàng rào cuối toàn tenant — xem mapping):
    /// <list type="number">
    ///   <item>Tên ĐVT **không trùng** (không phân biệt hoa/thường) — "thùng" và "Thùng" là một;</item>
    ///   <item>Barcode **không trùng** trong cùng sản phẩm;</item>
    ///   <item>**Đúng một** đơn vị gốc: đơn vị đầu tiên tự thành gốc; xin thêm gốc lần hai ⇒ **NÉM LỖI**.</item>
    /// </list>
    /// 📌 Vì sao đơn vị đầu tiên **tự** thành gốc: người dùng tạo sản phẩm một ĐVT là ca phổ biến nhất;
    /// bắt họ gửi `isBase` cho mọi ca sẽ sinh lỗi "quên cờ" chứ không thêm thông tin.
    /// </summary>
    public ProductUnit AddUnit(
        Guid id,
        string name,
        decimal conversionFactor,
        decimal salePrice,
        bool isBase = false,
        string? barcode = null,
        decimal? importPrice = null,
        decimal? wholesalePrice = null,
        decimal? oldPrice = null)
    {
        var ten = ProductUnit.ChuanHoaTen(name);

        if (_units.Any(x => string.Equals(x.Name, ten, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe)
                .WithData("truong", nameof(name))
                .WithData("tenDonVi", ten)
                .WithData("lyDo", "Tên đơn vị tính đã tồn tại trong sản phẩm này.");
        }

        var maVach = ProductUnit.ChuanHoaBarcode(barcode);

        if (maVach is not null &&
            _units.Any(x => string.Equals(x.Barcode, maVach, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.BarcodeDaTonTai)
                .WithData("barcode", maVach)
                .WithData("phamVi", $"sản phẩm '{Code}'");
        }

        if (isBase && _units.Any(x => x.IsBase))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.NhieuHonMotDonViGoc)
                .WithData("productId", Id)
                .WithData("lyDo", "Sản phẩm đã có đơn vị gốc.");
        }

        var laGoc = isBase || _units.Count == 0;

        var donVi = new ProductUnit(id, Id, ten, conversionFactor, laGoc, salePrice, maVach)
        {
            ImportPrice = importPrice,
            WholesalePrice = wholesalePrice,
            OldPrice = oldPrice
        };

        _units.Add(donVi);
        return donVi;
    }

    /// <summary>
    /// Bật hiển thị. 🚨 **NÉM LỖI** nếu: sản phẩm đã Lưu trữ, hoặc **không có nguồn bán nào**
    /// (`CAT-001` **D9**; nới ở WP-03 nửa sau) — sản phẩm không có gì để bán **không publish được**, vì
    /// publish sẽ chỉ đẩy một trang rỗng ra storefront và bắt `Pricing`/`Inventory` xử lý ca rỗng ở mọi nơi.
    ///
    /// 🆕 **WP-03 nửa sau (02/11/2026) — ĐIỀU KIỆN "CÓ GÌ ĐỂ BÁN" ĐỔI THEO MÔ HÌNH MỚI (D-07):**
    /// <list type="bullet">
    ///   <item>**Trước:** bắt buộc có **biến thể đang hoạt động** (`ProductVariant`) — nơi duy nhất giữ giá.</item>
    ///   <item>**Nay:** đủ khi có **ĐVT gốc** (`ProductUnit.IsBase`) **HOẶC** còn biến thể cũ đang hoạt động.
    ///     📌 Nếu vẫn đòi biến thể thì **mọi** sản phẩm tạo qua `POST /api/v1/products` (mô hình Nhanh: 3 ĐVT,
    ///     0 biến thể) **vĩnh viễn không publish được** — đã ghi thành nợ bắt buộc ở `handover-n2` §2.8.</item>
    /// </list>
    /// 🔒 Vẫn **giữ mã lỗi cũ** `0003`: client đã đối soát theo mã này; nghĩa của nó chỉ **rộng hơn**
    /// (*"không có nguồn bán"* thay vì *"không có biến thể"*), không đổi số (D-17).
    /// </summary>
    public void Publish()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", Status.ToString())
                .WithData("den", ProductStatus.Published.ToString())
                .WithData("lyDo", "Sản phẩm đã Lưu trữ — không publish lại được.");
        }

        if (Status == ProductStatus.Published)
        {
            // Idempotent: publish lại là vô hại (worker/retry gọi lặp). KHÔNG ném lỗi ở đây vì
            // đây không phải "chuyển sai" mà là "đã ở đúng trạng thái".
            return;
        }

        var coDonViGoc = _units.Any(u => u.IsBase);
        var coBienTheDangBan = _variants.Any(v => v.IsActive);

        if (!coDonViGoc && !coBienTheDangBan)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongCoBienTheHoatDong)
                .WithData("ma", Code)
                .WithData("soBienThe", _variants.Count)
                .WithData("soDonVi", _units.Count)
                .WithData("lyDo", "Không có ĐVT gốc và cũng không có biến thể đang hoạt động ⇒ không có gì để bán.");
        }

        Status = ProductStatus.Published;
    }

    /// <summary>Ngừng bán (giữ dữ liệu, có thể publish lại). Chuyển sai ⇒ **NÉM LỖI**.</summary>
    public void Unpublish()
    {
        if (Status == ProductStatus.Published)
        {
            Status = ProductStatus.Unpublished;
            return;
        }

        if (Status == ProductStatus.Unpublished)
        {
            return; // idempotent
        }

        throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe)
            .WithData("tu", Status.ToString())
            .WithData("den", ProductStatus.Unpublished.ToString());
    }

    /// <summary>
    /// Lưu trữ — **trạng thái CUỐI**, không quay lại được (`CAT-001` D5).
    /// Dùng thay cho xoá cứng (`CAT-001` **D4**/F7: xoá cứng phá lịch sử đã tham chiếu).
    /// </summary>
    public void Archive()
    {
        if (Status == ProductStatus.Archived)
        {
            return; // idempotent
        }

        Status = ProductStatus.Archived;
    }

    /// <summary>
    /// Chuẩn hoá + kiểm tra mã sản phẩm.
    /// ⚠️ Dùng <c>MaSanPhamKhongHopLe</c> (**0012**) — **KHÔNG** dùng `SkuKhongHopLe` (0008) vì đó là mã của **SKU**;
    /// dùng lẫn mã ⇒ client/đối soát không phân biệt được lỗi ở `Code` hay ở `SKU` (bài học 15/10/2026:
    /// hai tài liệu từng gán trùng dải mã — xem sổ đăng ký ở `eCommerceConsts.ErrorCodes.Catalog`).
    /// </summary>
    public static string ChuanHoaMa(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaSanPhamKhongHopLe)
                .WithData("truong", "Code")
                .WithData("lyDo", "rỗng");
        }

        var s = ma.Trim();

        if (s.Length > DoDaiMaToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaSanPhamKhongHopLe)
                .WithData("truong", "Code")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiMaToiDa);
        }

        return s;
    }

    /// <summary>
    /// Chuẩn hoá + kiểm tra SKU (`CAT-001` **BR-002**).
    ///
    /// ⚠️ **GHI RÕ ĐIỀU CHƯA LÀM (không giả vờ):** hiện chỉ **trim** + kiểm ký tự, **chưa** chuẩn hoá
    /// hoa/thường. Muốn coi `sku-a` và `SKU-A` là **một** thì unique index phải là **functional index**
    /// (`UNIQUE (TenantId, UPPER(Sku))`) — việc đó cần migration riêng và **chưa** làm.
    /// Vì vậy chống trùng trong bộ nhớ ở đây so khớp **không phân biệt hoa/thường**, còn DB thì **có**
    /// phân biệt ⇒ **DB vẫn là hàng rào cuối** nhưng hai SKU khác hoa/thường vẫn có thể cùng tồn tại.
    /// </summary>
    public static string ChuanHoaSku(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe)
                .WithData("lyDo", "rỗng");
        }

        var s = sku.Trim();

        if (s.Length > DoDaiSkuToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe)
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiSkuToiDa);
        }

        if (!KhuonSku.IsMatch(s))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe)
                .WithData("sku", s)
                .WithData("lyDo", "chỉ cho phép chữ, số và . _ -");
        }

        return s;
    }

    /// <summary>Giá phải **≥ 0** (`CAT-001` BR-011). So sánh `decimal` — **cấm** `double` (quy ước #1).</summary>
    public static void KiemTraGia(decimal gia)
    {
        if (gia < 0m)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe)
                .WithData("gia", gia);
        }
    }
}

/// <summary>Biến thể sản phẩm — đơn vị bán thật, gắn với SKU và giá.</summary>
public class ProductVariant : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>
    /// SKU nội bộ; dùng làm khoá đối chiếu với <c>ChannelListing.ExternalSku</c>.
    /// ⚠️ **Bất biến sau khi phát sinh giao dịch** (`CAT-001` **D2**) — xem app service.
    /// 🚨 **Chống trùng SKU toàn tenant do DB bảo đảm** (unique `(TenantId, Sku)`), không phải lớp này
    /// (`CAT-001` **D3/F6**: kiểm tra trong bộ nhớ của một sản phẩm **yếu hơn** ràng buộc DB).
    /// </summary>
    public string Sku { get; protected set; } = default!;

    public string? Barcode { get; set; }

    /// <summary>
    /// Giá niêm yết (nguồn sự thật).
    /// ⚠️ <c>decimal</c>, không phải <c>double</c> — xem quy tắc trong <c>eCommerceDomainModule</c>.
    /// 🚨 **CẤM** so sánh `==` trên trường tiền (quy ước #1/#2).
    /// </summary>
    public decimal Price { get; protected set; }

    public decimal? CompareAtPrice { get; set; }

    /// <summary>Khối lượng (gram) để tính phí vận chuyển.</summary>
    public int? WeightInGrams { get; set; }

    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// 🚨 **Hash của TỔ HỢP thuộc tính** (`CAT-002` D7/BR-010/011) — `NULL` cho biến thể **không** sinh từ
    /// tổ hợp (sản phẩm đơn giản, hoặc biến thể do người dùng tự tạo).
    ///
    /// Đây là **khoá đối chiếu** để biết tổ hợp nào **đã tồn tại** khi sinh lại (BR-013: **giữ nguyên**
    /// `Id`/`Sku` cũ, **cấm** xoá-rồi-tạo-lại) ⇒ hash sinh **lệch** sẽ khiến hệ thống **tạo trùng** hoặc
    /// **coi hai tổ hợp khác nhau là một** (bán sai hàng). ⚠️ **CHỈ** sinh bằng
    /// `VariantCombinationGenerator.TinhAttributeHash` (BR-011).
    ///
    /// `(TenantId, ProductId, AttributeHash) WHERE AttributeHash IS NOT NULL` là **hàng rào cuối** ở DB.
    /// </summary>
    public string? AttributeHash { get; protected set; }

    protected ProductVariant() { }

    public ProductVariant(Guid id, string sku, decimal price, string? barcode = null) : base(id)
    {
        Sku = sku;
        Price = price;
        Barcode = barcode;
    }

    /// <summary>
    /// Đổi giá niêm yết. Trả về <c>true</c> nếu giá **thực sự đổi** (để phát `VariantPriceChangedEto`
    /// **chỉ khi có thay đổi**, tránh sự kiện rác làm đối soát nhiễu). So sánh `decimal` bằng
    /// `<`/`>`, **không** dùng `==` (quy ước #2).
    /// </summary>
    public bool DoiGia(decimal giaMoi)
    {
        Product.KiemTraGia(giaMoi);

        // 🚨 Quy ước #2 CẤM so tiền bằng `==`. Ở đây ta cần **phát hiện THAY ĐỔI** trên một giá trị
        // `decimal` đã lưu (không phải "hai khoản tiền có bằng nhau về nghiệp vụ không"), nên dùng
        // `decimal.Compare` — so sánh **thứ tự**, tường minh rằng đây không phải phép so bằng nghiệp vụ.
        var coDoi = decimal.Compare(Price, giaMoi) != 0;

        if (coDoi)
        {
            Price = giaMoi;
        }

        return coDoi;
    }

    /// <summary>
    /// Gắn hash tổ hợp cho biến thể. ⚠️ Kiểm tra **hình dạng** hash (64 ký tự hex thường) để không ai
    /// "tiện tay" truyền chuỗi khác vào — hash sai hình dạng vẫn **chạy** nhưng phá bất biến đối chiếu (BR-010).
    /// </summary>
    public void GanToHop(string attributeHash)
    {
        if (string.IsNullOrWhiteSpace(attributeHash) || attributeHash.Length != 64)
        {
            throw new ArgumentException(
                "AttributeHash phải là chuỗi 64 ký tự hex (SHA-256 dạng thường). " +
                "Dùng `VariantCombinationGenerator.TinhAttributeHash` (CAT-002 BR-011).",
                nameof(attributeHash));
        }

        AttributeHash = attributeHash;
    }

    /// <summary>Ngừng bán biến thể này (không xoá — giữ lịch sử tham chiếu).</summary>
    public void NgungBan() => IsActive = false;

    /// <summary>Mở bán lại.</summary>
    public void MoBanLai() => IsActive = true;
}

using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **QUẢN LÝ ẢNH SẢN PHẨM** (hiện thực `IProductMediaAppService`, `CAT-003`).
///
/// 🔐 `[Authorize]` cấp **CLASS** với `Products.ManageMedia`.
/// ⚠️ **KHÔNG** phải bảo mật dữ liệu duy nhất: **cùng** lớp này còn kiểm *"ảnh có thuộc sản phẩm này không"*
/// (xem <see cref="KiemAnhThuocSanPham"/>) — thiếu kiểm đó thì **người có quyền** trên tenant vẫn có thể
/// đặt ảnh chính cho **sản phẩm khác** bằng cách đoán `Id` ⇒ **sửa dữ liệu của sản phẩm không liên quan**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **THỨ TỰ "GHI TỆP TRƯỚC, GHI DB SAU" — VÀ VÌ SAO ĐÓ LÀ THỨ TỰ ĐÚNG**
///
/// Hai việc ở **hai hệ thống khác nhau** (kho tệp + PostgreSQL) ⇒ **không thể** nguyên tử nếu không có
/// giao dịch phân tán. Vậy phải chọn **kiểu hỏng nào dễ sống hơn**:
/// <list type="bullet">
///   <item><b>Ghi tệp TRƯỚC, DB SAU</b> (đã chọn): DB hỏng ⇒ còn **tệp mồ côi** — tốn dung lượng, **dọn được**
///     bằng job, và **không ai thấy hỏng**;</item>
///   <item><b>Ghi DB TRƯỚC, tệp SAU</b>: tệp hỏng ⇒ có **dòng DB trỏ tới tệp không tồn tại** ⇒ storefront
///     hiện **ảnh vỡ**, `GetAnhAsync` trả `404`, và **không có cách nào** biết ảnh nào hỏng nếu không thử từng cái.
///     📌 `CAT-003` §237 ghi rõ: *"không tồn tại — storefront hiển thị **ảnh vỡ**, tệ hơn nhiều so với vài MB
///     rác dọn được bằng job"*.</item>
/// </list>
/// 🔒 **Cộng thêm một lớp:** nếu `SaveChanges` **NÉM** (va ràng buộc `0204`, mất kết nối, timeout), tôi
/// **xoá luôn tệp vừa ghi** trong `catch` — để **giảm** tệp mồ côi trong ca phổ biến nhất. ⚠️ **Không** dựa
/// vào đó: lỗi tiến trình (mất điện, `kill -9`) vẫn để lại tệp mồ côi ⇒ **vẫn cần** job dọn (§20.7).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
[Authorize(eCommerceConsts.Permissions.Products.ManageMedia)]
public class ProductMediaAppService : ApplicationService, IProductMediaAppService
{
    private readonly IRepository<ProductMedia, Guid> _mediaRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IMediaStorage _khoTep;

    public ProductMediaAppService(
        IRepository<ProductMedia, Guid> mediaRepository,
        IRepository<Product, Guid> productRepository,
        IMediaStorage khoTep)
    {
        _mediaRepository = mediaRepository;
        _productRepository = productRepository;
        _khoTep = khoTep;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<ProductMediaDto>> GetDanhSachAsync(LocAnhInput input)
    {
        await KiemSanPhamTonTaiAsync(input.ProductId);

        var truyVan = (await _mediaRepository.GetQueryableAsync())
            .Where(m => m.ProductId == input.ProductId);

        var tong = await AsyncExecuter.CountAsync(truyVan);

        var ds = await AsyncExecuter.ToListAsync(
            truyVan.OrderBy(m => m.ThuTu).ThenBy(m => m.CreationTime)
                   .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<ProductMediaDto>(tong, ds.Select(TaoDto).ToList());
    }

    /// <inheritdoc />
    public async Task<ProductMediaDto> PostTaiAnhAsync(Guid productId, TaiAnhInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        await KiemSanPhamTonTaiAsync(productId);

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **THỨ TỰ BỐN PHÉP KIỂM LÀ MỘT PHẦN CỦA ĐẶC TẢ — đổi thứ tự là tạo lỗ hổng hoặc rác.**
        //
        // ① TRẦN DUNG LƯỢNG **TRƯỚC** MAGIC BYTES.
        //    📌 Vì sao: người dùng có thể gửi tệp **vài GB**. Kiểm magic bytes trước (chỉ cần 12 byte đầu)
        //    **nghe** rẻ hơn, NHƯNG bytes đã **nằm trong RAM** từ lúc model binding (mảng `byte[]` của DTO)
        //    ⇒ thiệt hại đã xảy ra. 🔒 Vì vậy **ở đường HTTP** phải chặn theo `Content-Length` **trước khi**
        //    đọc thân request (xem `CatMediaController`), còn ở đây kiểm lại `input.NoiDung.Length` để
        //    **không** phụ thuộc vào một tầng duy nhất (lớp này còn được gọi từ test/job/seed).
        //
        // ② MAGIC BYTES (`ADR-13`) — **TRƯỚC** khi chạm vào kho tệp.
        //    📌 Tệp không phải ảnh thì **không** được ghi vào kho: ghi rồi xoá là **hai** thao tác I/O và
        //    có cửa sổ mà tệp xấu **tồn tại** trên đĩa.
        //
        // ③ TRẦN SỐ ẢNH — **TRƯỚC** khi ghi tệp.
        //    🔒 Vượt trần mà vẫn ghi tệp ⇒ **tệp mồ côi** (tệp có thật, **không** dòng DB nào trỏ tới
        //    ⇒ **không ai biết để dọn**). 📌 Đếm trước rồi mới ghi ⇒ vượt trần là **0 tệp, 0 dòng**.
        //
        // ④ TẠO ENTITY **SAU** khi ghi tệp thành công ⇒ entity chỉ tồn tại khi bytes **chắc chắn** có.
        // ══════════════════════════════════════════════════════════════════════════════════════════

        // ① TRẦN DUNG LƯỢNG
        if (input.NoiDung is null || input.NoiDung.Length == 0)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh)
                .WithData("lyDo", "Tệp rỗng (0 byte) không thể là ảnh.")
                .WithData("toiDaByte", eCommerceConsts.CatalogLimits.AnhToiDaByte);
        }

        if (input.NoiDung.LongLength > eCommerceConsts.CatalogLimits.AnhToiDaByte)
        {
            // 📌 Cùng mã `0206` như *"sai định dạng"* (theo `CAT-003` §6.3 — client xử lý giống nhau:
            // *"chọn tệp khác"*). 🔑 Nhưng **`WithData` phân biệt được hai nguyên nhân** khi điều tra:
            // `toiDaByte` **có mặt** ⇒ vượt dung lượng (vấn đề **quy trình**); **không** có ⇒ sai định dạng
            // (**có thể** là ý đồ tấn công). 📌 Đây là lý do tôi **không** tạo hằng số alias cho dung lượng
            // (xem chú thích ở `eCommerceConsts.ErrorCodes.Catalog.AnhQuaKichThuoc` đã bị bỏ).
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh)
                .WithData("kichThuocByte", input.NoiDung.LongLength)
                .WithData("toiDaByte", eCommerceConsts.CatalogLimits.AnhToiDaByte)
                .WithData("lyDo", "Ảnh vượt trần dung lượng cho phép.");
        }

        // ② MAGIC BYTES — 🔒 `NhanDangAnh` **không ném**, trả `null` cho tệp lạ ⇒ chỗ này tự chọn mã lỗi.
        var dinhDang = NhanDangAnh.Tu(input.NoiDung);

        if (dinhDang is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh)
                .WithData("kichThuocByte", input.NoiDung.LongLength)
                .WithData("lyDo",
                    "Nội dung tệp KHÔNG khớp chữ ký ảnh nào (JPEG/PNG/GIF/WebP). "
                    + "Đuôi tệp và Content-Type do client khai KHÔNG được tin (ADR-13).");

            // 📌 Ghi chú vận hành: mã này tăng vọt ⇒ **soi nhật ký bảo mật**. Đây là chỉ dấu của việc
            // **đổi đuôi/tên tệp** để đưa tệp không phải ảnh vào kho — xem giới hạn `polyglot` ở `NhanDangAnh`.
        }

        // ③ TRẦN SỐ ẢNH
        var truyVan = await _mediaRepository.GetQueryableAsync();
        var soAnhHienCo = await AsyncExecuter.CountAsync(truyVan.Where(m => m.ProductId == productId));

        if (soAnhHienCo >= eCommerceConsts.CatalogLimits.SoAnhToiDaMoiSanPham)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DaCoAnhChinh)
                .WithData("soAnh", soAnhHienCo)
                .WithData("toiDa", eCommerceConsts.CatalogLimits.SoAnhToiDaMoiSanPham)
                .WithData("lyDo",
                    $"Sản phẩm đã có {soAnhHienCo} ảnh (trần {eCommerceConsts.CatalogLimits.SoAnhToiDaMoiSanPham}) "
                    + "⇒ xoá bớt ảnh trước khi thêm.");
        }

        // ④ GHI TỆP **TRƯỚC**, DB **SAU** (xem chú thích lớp).
        var ketQua = await _khoTep.LuuAsync(
            input.NoiDung, dinhDang.Value, CurrentTenant.Id, productId);

        try
        {
            var media = new ProductMedia(
                GuidGenerator.Create(),
                productId,
                ketQua.KhoaLuuTru,
                dinhDang.Value,
                input.NoiDung.LongLength,
                input.Alt,
                thuTu: soAnhHienCo);

            // 🚨 **ẢNH ĐẦU TIÊN TỰ ĐỘNG THÀNH ẢNH CHÍNH** (§Basic Flow bước 4).
            // 📌 Nếu không: sản phẩm **không có** ảnh chính cho tới khi ai đó bấm thêm một lần nữa — và
            // `Publish` (đòi ≥ 1 ảnh chính) sẽ **chặn** vì một lý do **không ai đoán được** từ thông báo.
            // 🔒 Nhưng nếu `Publish` đòi `Alt` cho **mọi** media thì tự đặt ảnh chính khi **thiếu `Alt`**
            // sẽ **NÉM** ngay tại đây (xem `ProductMedia.DatLaAnhChinh`) — và đó là **đúng**: người dùng
            // phải nhập `Alt`, không được im lặng tạo ảnh chính thiếu mô tả.
            var nenDatLamAnhChinh = input.LaAnhChinh || soAnhHienCo == 0;

            if (nenDatLamAnhChinh)
            {
                if (soAnhHienCo > 0)
                {
                    // 📌 Bo co anh chinh cu TRUOC, trong CUNG transaction (xem `PostDatAnhChinhAsync`).
                    await BoCoAnhChinhCuAsync(productId, truyVan);
                }

                media.DatLaAnhChinh(true);
            }

            await _mediaRepository.InsertAsync(media, autoSave: true);

            Logger.LogInformation(
                "DA TAI ANH SAN PHAM: sanPham={SanPhamId} · dinhDang={DinhDang} · {SoByte} byte · anhChinh={AnhChinh}. [MEDIA-TAI-LEN]",
                productId, dinhDang.Value, input.NoiDung.LongLength, media.LaAnhChinh);

            return TaoDto(media);
        }
        catch
        {
            // 🔒 **Giảm tệp mồ côi trong ca phổ biến nhất** (va ràng buộc / mất kết nối / timeout).
            // ⚠️ **KHÔNG** thay thế job dọn: lỗi tiến trình (`kill -9`, mất điện) vẫn để lại tệp mồ côi.
            // 📌 Nuốt lỗi xoá tệp là **có chủ ý**: exception **gốc** mới là thứ cần báo cho người dùng;
            // lỗi dọn tệp chỉ ghi log (nếu ném ở đây, người dùng nhận thông báo **sai** về nguyên nhân).
            try
            {
                await _khoTep.XoaAsync(ketQua.KhoaLuuTru);
            }
            catch (Exception exDonDep)
            {
                Logger.LogWarning(exDonDep,
                    "KHONG XOA DUOC tep vua ghi sau khi tao media that bai (se la RAC can don): khoa={Khoa}. [MEDIA-RAC]",
                    ketQua.KhoaLuuTru);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductMediaDto> PutAnhAsync(Guid id, SuaAnhInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var media = await LayAnhAsync(id);

        // ⚠️ **KHÔNG** cho sửa nội dung tệp qua đường này — xem chú thích `IProductMediaAppService`.
        media.DatAlt(input.Alt);
        media.DatThuTu(input.ThuTu);

        await _mediaRepository.UpdateAsync(media, autoSave: true);

        return TaoDto(media);
    }

    /// <inheritdoc />
    public async Task<ProductMediaDto> PostDatAnhChinhAsync(Guid id)
    {
        var media = await LayAnhAsync(id);

        if (media.LaAnhChinh)
        {
            // 🔒 **NO-OP** (không phải lỗi): gọi lại là retry/double-click ⇒ phải vô hại.
            // 📌 Cùng khuôn `ReconRunMismatch.DanhDauDaXuLy`: gọi lại **đúng** trạng thái ⇒ không ném.
            return TaoDto(media);
        }

        // 🔒 Bỏ cờ cũ **trước** — trong **cùng** transaction (UoW của ABP bọc cả method).
        // 📌 Nếu làm ngược (đặt cờ mới trước) thì unique index có bộ lọc ở DB sẽ **chặn ngay** ⇒ người dùng
        // nhận `0204` cho một thao tác **hoàn toàn hợp lệ** — và thông báo *"đã có ảnh chính"* **đúng**
        // nhưng **vô ích** vì họ **đang** đổi ảnh chính.
        var truyVan = await _mediaRepository.GetQueryableAsync();
        await BoCoAnhChinhCuAsync(media.ProductId, truyVan);

        media.DatLaAnhChinh(true);
        await _mediaRepository.UpdateAsync(media, autoSave: true);

        Logger.LogInformation(
            "DA DOI ANH CHINH: media={MediaId} · sanPham={SanPhamId}. [MEDIA-DOI-ANH-CHINH]",
            media.Id, media.ProductId);

        return TaoDto(media);
    }

    /// <inheritdoc />
    public async Task XoaAnhAsync(Guid id)
    {
        var media = await LayAnhAsync(id);

        // ⚠️ `ADR-11`: xoá **MỀM** (`FullAudited` — `DeleteAsync` của ABP đánh dấu `IsDeleted`).
        // 🔒 **KHÔNG** gọi `_khoTep.XoaAsync` ở đây: URL ảnh **đã** phát ra storefront/sàn/CDN; xoá bytes
        // ngay ⇒ **ảnh vỡ** ở nơi chưa kịp cập nhật, và **không có log** nào để lần ra.
        // 📌 Hệ quả **phải biết**: ảnh đã xoá **vẫn** phục vụ được qua `GetAnhAsync` (nếu không lọc `IsDeleted`)
        // — và đó là **chủ ý** (`ADR-11`: *"có cửa sổ hoàn tác + dọn có kiểm soát"*).
        await _mediaRepository.DeleteAsync(media, autoSave: true);

        Logger.LogInformation(
            "DA XOA MEM ANH (bytes GIU LAI theo ADR-11): media={MediaId} · khoa={Khoa}. [MEDIA-XOA-MEM]",
            media.Id, media.KhoaLuuTru);
    }

    // ── Hàm dùng chung ────────────────────────────────────────────────────────────────────────────

    /// <summary>Lấy ảnh hoặc **NÉM** `0210` (⚠️ **KHÔNG** im lặng trả `null` — quy ước #11).</summary>
    private async Task<ProductMedia> LayAnhAsync(Guid id)
    {
        var media = await _mediaRepository.FindAsync(id);

        if (media is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh)
                .WithData("mediaId", id);
        }

        return media;
    }

    /// <summary>
    /// 🚨 **Bỏ cờ ảnh chính của các ảnh KHÁC trong cùng sản phẩm.**
    /// 🔒 Điều kiện `m.Id != truId` là **quan trọng**: nếu bỏ sót, hàm sẽ **xoá cờ vừa đặt** khi ảnh đó
    /// **đã** là ảnh chính (ca gọi lại) ⇒ ảnh chính **biến mất im lặng** và `Publish` chặn vì *"thiếu ảnh chính"*.
    /// 📌 Dùng `UpdateAsync` từng dòng thay vì `ExecuteUpdateAsync`: số ảnh **≤ 50** (có trần) nên chi phí
    /// không đáng kể, và `UpdateAsync` chạy trong **cùng UoW** với thao tác gọi nó.
    /// </summary>
    private async Task BoCoAnhChinhCuAsync(Guid productId, IQueryable<ProductMedia> truyVan)
    {
        var anhChinhCu = await AsyncExecuter.ToListAsync(
            truyVan.Where(m => m.ProductId == productId && m.LaAnhChinh));

        foreach (var cu in anhChinhCu)
        {
            cu.DatLaAnhChinh(false);
            await _mediaRepository.UpdateAsync(cu, autoSave: false);
        }
    }

    /// <summary>
    /// 🚨 Kiểm sản phẩm **tồn tại** — và qua đó **chặn `productId` bịa**.
    /// 📌 Vì sao **cần** dù FK đã có: FK chỉ chặn được **sau khi** ghi (và chỉ khi ghi DB) — còn ở đây
    /// sản phẩm không tồn tại thì ta **không ghi tệp**, tránh **tệp mồ côi** do một `Guid` gõ sai.
    /// 🔒 Và không có kiểm này thì người dùng nhận `500` (lỗi FK) thay vì **`404` có mã**.
    /// </summary>
    private async Task KiemSanPhamTonTaiAsync(Guid productId)
    {
        if (!await _productRepository.AnyAsync(p => p.Id == productId))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("productId", productId);
        }
    }

    private static ProductMediaDto TaoDto(ProductMedia m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        // 🔒 URL do **MÁY CHỦ** dựng từ `Id` (không từ khoá lưu trữ) ⇒ client không dò được tệp của người khác.
        Url = $"/CatMedia/{m.Id}",
        Alt = m.Alt,
        ThuTu = m.ThuTu,
        LaAnhChinh = m.LaAnhChinh,
        KichThuocByte = m.KichThuocByte,
        DinhDang = m.DinhDangDaKiem.ToString(),
        // 🔒 `Content-Type` suy từ định dạng **đã kiểm magic bytes** — không từ tên tệp.
        ContentType = NhanDangAnh.Mime(m.DinhDangDaKiem),
        CreationTime = m.CreationTime
    };
}

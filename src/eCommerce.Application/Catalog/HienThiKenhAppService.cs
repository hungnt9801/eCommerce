using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ChannelListingEntity = eCommerce.ChannelHub.Channels.ChannelListing;

namespace eCommerce.Catalog.Dtos;

/// <summary>
/// 🚨 **QUẢN LÝ HIỂN THỊ SẢN PHẨM THEO KÊNH** — hiện thực `IHienThiKenhAppService` (`CAT-003` `ADR-08`).
///
/// **BỐN QUYẾT ĐỊNH CỦA LỚP NÀY (đọc trước khi sửa):**
/// <list type="number">
///   <item>🔑 **`TrangThai = KeThua` ⇒ XOÁ bản ghi** (không lưu giá trị `KeThua`) — xem chú thích ở
///     `DatHienThiKenhInput`. 📌 Nếu lưu, bảng có **hai** cách biểu diễn cùng một trạng thái ⇒ mọi truy vấn
///     `LEFT JOIN` phải xử lý **cả hai**, và chỗ nào quên thì **sai im lặng**;</item>
///   <item>🚨 **GUARD `0207` — kênh phải tồn tại VÀ đang hoạt động** (qua `IChannelDirectory`). 🔒 Kiểm **trước**
///     khi ghi: nếu chỉ dựa vào UI thì **API** và **job** vẫn ghi được cấu hình cho kênh **không có thật**
///     ⇒ bản ghi mồ côi, và truy vấn hiển thị sau đó **không** giải thích được;</item>
///   <item>🚨 **GUARD `0209` — còn `ChannelListing` ở `Status = Synced` thì KHÔNG ngừng bán.** 📌 Đây là bảo vệ
///     **TIỀN và UY TÍN**: đánh dấu *"ngừng bán"* trên hệ thống **trong khi** sàn **vẫn đang bán** ⇒ khách vẫn
///     đặt được nhưng hệ thống **không** còn coi sản phẩm thuộc kênh ⇒ **bán quá tồn** ⇒ phải **huỷ đơn của khách**.
///     🔒 Và thông báo phải kèm **DANH SÁCH** listing còn lại (không chỉ nói *"còn listing"*) — người vận hành
///     cần biết **gỡ cái nào**;</item>
///   <item>🔑 **MỌI thay đổi đi qua `ProductChannelVisibility.ChuyenSang`** — **không** có đường nào ghi trạng thái
///     trực tiếp. 📌 Đây là lý do bảng chuyển được đặt ở **entity** (`ADR-08`): một đường ghi duy nhất thì luật
///     chỉ có **một chỗ**, và không đường ghi thứ hai nào (job, seed, API mới) **bỏ qua** được nó.</item>
/// </list>
///
/// 🔐 `[Authorize]` cấp **CLASS** với `Products.Edit` — xem chú thích ở `IHienThiKenhAppService`.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Products.Edit)]
public class HienThiKenhAppService : ApplicationService, IHienThiKenhAppService
{
    /// <summary>Trạng thái listing **đang đồng bộ** trên sàn — khớp `ChannelListingStatus.Synced`.
    /// 📌 Hằng số **cục bộ** vì enum đó ở assembly khác; ⚠️ nếu `ChannelHub` đổi số thì phải đổi **cả đây**
    /// (đã ghi ở nợ `CAT-003` §24.7).</summary>
    private const int ListingDangDongBo = 2;

    private readonly IRepository<ProductChannelVisibility, Guid> _hienThiRepository;
    private readonly IRepository<Product, Guid> _sanPhamRepository;
    private readonly IRepository<ChannelListingEntity, Guid> _listingRepository;
    private readonly IChannelDirectory _kenh;

    public HienThiKenhAppService(
        IRepository<ProductChannelVisibility, Guid> hienThiRepository,
        IRepository<Product, Guid> sanPhamRepository,
        IRepository<ChannelListingEntity, Guid> listingRepository,
        IChannelDirectory kenh)
    {
        _hienThiRepository = hienThiRepository;
        _sanPhamRepository = sanPhamRepository;
        _listingRepository = listingRepository;
        _kenh = kenh;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<HienThiKenhDto>> GetDanhSachAsync(LocHienThiKenhInput input)
    {
        var truyVan = (await _hienThiRepository.GetQueryableAsync())
            .Where(v => v.ProductId == input.ProductId);

        var tong = await AsyncExecuter.CountAsync(truyVan);

        var ds = await AsyncExecuter.ToListAsync(
            truyVan.OrderBy(v => v.ChannelCode)
                   .Skip(input.SkipCount).Take(input.MaxResultCount));

        // 📌 Nạp tên kênh **một lượt** cho cả trang (không gọi trong vòng lặp): với 50 dòng thì N+1 là 50 truy vấn
        // cho **một** màn hình — đủ để màn hình chậm thấy rõ khi có nhiều tenant.
        var dsDto = new List<HienThiKenhDto>(ds.Count);

        foreach (var v in ds)
        {
            dsDto.Add(await TaoDtoAsync(v));
        }

        return new PagedResultDto<HienThiKenhDto>(tong, dsDto);
    }

    /// <inheritdoc />
    public async Task<HienThiKenhDto?> PostDatAsync(Guid productId, DatHienThiKenhInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var maKenh = input.ChannelCode?.Trim() ?? string.Empty;

        // ── GUARD ① KÊNH PHẢI TỒN TẠI VÀ ĐANG HOẠT ĐỘNG (`0207`) ─────────────────────────────────
        // 🔒 Kiểm **TRƯỚC** khi đọc/ghi bất cứ thứ gì: một `ChannelCode` sai thì **không** có việc gì khác
        // đáng làm, và kiểm sớm giữ cho thông báo lỗi **đúng nguyên nhân** (thay vì ném `0010` *"không tìm thấy
        // sản phẩm"* chỉ vì thứ tự kiểm).
        if (!await _kenh.KenhHoatDongAsync(maKenh))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KenhKhongHoatDong)
                .WithData("kenh", maKenh)
                .WithData("lyDo",
                    "Kênh KHÔNG tồn tại hoặc đã NGỪNG hoạt động ⇒ chọn kênh đang hoạt động.");
        }

        var sanPham = await _sanPhamRepository.FindAsync(productId);

        if (sanPham is null)
        {
            // 🔒 NÉM `0010` (không im lặng trả `null` — quy ước #11), và **KHÔNG** ghi gì.
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("sanPhamId", productId);
        }

        // ── QUY ƯỚC: `KeThua` ⇒ XOÁ bản ghi (quay về kế thừa) ──────────────────────────────────
        if (string.Equals(input.TrangThai, nameof(TrangThaiHienThi.KeThua), StringComparison.OrdinalIgnoreCase))
        {
            return await XoaDeQuayVeKeThuaAsync(productId, maKenh);
        }

        // ── Parse trạng thái đích — 🔒 nhận **TÊN ENUM**, sai tên ⇒ `0205` ──────────────────────
        if (!Enum.TryParse<TrangThaiHienThi>(input.TrangThai, ignoreCase: true, out var dich)
            || dich == TrangThaiHienThi.KeThua
            || dich == TrangThaiHienThi.Loi)
        {
            // 📌 `KeThua` đã xử lý ở trên; `Loi` **không bao giờ** là đích (nó là *"đích khi guard chặn"*).
            // 🔒 Và **KHÔNG** dùng `Enum.TryParse` một mình: nó **chấp nhận cả SỐ** (`"4"` ⇒ `Loi`) ⇒ phải chặn
            // tường minh hai giá trị không hợp lệ làm **đích**.
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe)
                .WithData("trangThai", input.TrangThai)
                .WithData("lyDo",
                    "Trạng thái đích phải là một trong: HienThi, An, NgungBan, KeThua "
                    + "(KeThua = xoá bản ghi để quay về kế thừa; Loi KHÔNG bao giờ là đích).");
        }

        var hienCo = await TimBanGhiAsync(productId, maKenh);

        // ── GUARD ② CÒN LISTING ĐANG ĐỒNG BỘ ⇒ KHÔNG NGỪNG BÁN (`0209`) ──────────────────────────
        if (dich == TrangThaiHienThi.NgungBan)
        {
            await KiemKhongConListingDongBoAsync(productId, maKenh);
        }

        var sanPhamDaPublish = sanPham.Status == ProductStatus.Published;

        if (hienCo is null)
        {
            var moi = new ProductChannelVisibility(GuidGenerator.Create(), productId, maKenh);

            // 🔑 **LUÔN** qua `ChuyenSang` (kể cả bản ghi vừa tạo — nó bắt đầu ở `KeThua`) ⇒ guard chạy **một chỗ**.
            moi.ChuyenSang(dich, sanPhamDaPublish, input.LyDo);

            await _hienThiRepository.InsertAsync(moi, autoSave: true);

            Logger.LogInformation(
                "DAT HIEN THI KENH: sanPham={SanPhamId} · kenh={Kenh} · trangThai={TrangThai} [HTK-DAT]",
                productId, maKenh, moi.TrangThai);

            return await TaoDtoAsync(moi);
        }

        hienCo.ChuyenSang(dich, sanPhamDaPublish, input.LyDo);
        await _hienThiRepository.UpdateAsync(hienCo, autoSave: true);

        Logger.LogInformation(
            "DOI HIEN THI KENH: sanPham={SanPhamId} · kenh={Kenh} · trangThai={TrangThai} [HTK-DOI]",
            productId, maKenh, hienCo.TrangThai);

        return await TaoDtoAsync(hienCo);
    }

    // ── Hàm dùng chung ────────────────────────────────────────────────────────────────────────────

    /// <summary>Xoá bản ghi để quay về `KeThua`. 🔒 **Idempotent**: chưa có bản ghi ⇒ trả `null`, **không ném**.</summary>
    private async Task<HienThiKenhDto?> XoaDeQuayVeKeThuaAsync(Guid productId, string maKenh)
    {
        var hienCo = await TimBanGhiAsync(productId, maKenh);

        if (hienCo is null)
        {
            // 📌 Gọi lại là retry/double-click ⇒ phải vô hại (cùng khuôn `ReconRunMismatch.DanhDauDaXuLy`).
            // 🚨 VÀ **KHÔNG** kiểm guard `NgungBan` ở đây: `KeThua` nghĩa là *"xoá bản ghi"*, mà xoá bản ghi
            // **không** phải một chuyển dịch ⇒ bảng chuyển **không** áp. ⚠️ Nếu áp, người dùng **không bao giờ**
            // gỡ được một cấu hình đã `NgungBan` — đó là **bế tắc**, và nó **phá** ý nghĩa của `ADR-08`
            // (*"vắng bản ghi là trạng thái hợp lệ"*).
            return null;
        }

        await _hienThiRepository.DeleteAsync(hienCo, autoSave: true);

        Logger.LogInformation(
            "XOA HIEN THI KENH (quay ve KeThua): sanPham={SanPhamId} · kenh={Kenh} [HTK-XOA]",
            productId, maKenh);

        return null;
    }

    private async Task<ProductChannelVisibility?> TimBanGhiAsync(Guid productId, string maKenh)
        => await _hienThiRepository.FirstOrDefaultAsync(
            v => v.ProductId == productId && v.ChannelCode == maKenh);

    /// <summary>
    /// 🚨 **GUARD `0209`** — NÉM nếu còn listing ở trạng thái **đang đồng bộ**.
    /// 🔒 **BẮT BUỘC kèm DANH SÁCH** listing còn lại: một thông báo *"còn listing"* **không** cho người vận hành
    /// biết **gỡ cái nào** ⇒ họ phải tự đi tìm, và với hàng trăm SKU thì đó là **bế tắc**.
    /// </summary>
    private async Task KiemKhongConListingDongBoAsync(Guid productId, string maKenh)
    {
        var truyVan = await _listingRepository.GetQueryableAsync();

        // 📌 Phạm vi kiểm: **mọi biến thể của sản phẩm này** trên kênh đó. 🔒 Vì `ChannelListing` trỏ tới
        // `ProductVariantId` (không phải `ProductId`), phải qua danh sách biến thể — nếu chỉ so `ProductId`
        // thì guard **luôn** thấy *"không còn listing"* và **mất tác dụng im lặng**.
        var truyVanBienThe = await _sanPhamRepository.GetQueryableAsync();

        var idBienThe = await AsyncExecuter.ToListAsync(
            truyVanBienThe.Where(p => p.Id == productId)
                          .SelectMany(p => p.Variants)
                          .Select(v => v.Id));

        if (idBienThe.Count == 0)
        {
            // 📌 Sản phẩm chưa có biến thể ⇒ **không thể** có listing ⇒ cho qua.
            // ⚠️ Nhưng **không** im lặng: ghi log để biết guard đã bị *"đi vòng"* vì lý do dữ liệu.
            Logger.LogInformation(
                "KIEM LISTING DONG BO: sanPham={SanPhamId} CHUA co bien the ⇒ khong the co listing. [HTK-KHONG-BIEN-THE]",
                productId);
            return;
        }

        var conLai = await AsyncExecuter.ToListAsync(
            truyVan.Where(l => l.ChannelCode == maKenh
                            && l.Status == ListingDangDongBo
                            && idBienThe.Contains(l.ProductVariantId))
                   .Select(l => new { l.Id, l.InternalSku }));

        if (conLai.Count == 0)
        {
            return;
        }

        throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ConLienKetDongBo)
            .WithData("kenh", maKenh)
            .WithData("soListing", conLai.Count)
            .WithData("listing", conLai.Select(l => $"{l.Id}:{l.InternalSku}").ToArray())
            .WithData("lyDo",
                $"Còn {conLai.Count} liên kết ĐANG ĐỒNG BỘ trên kênh '{maKenh}' ⇒ "
                + "gỡ (delist) hết các liên kết đó TRƯỚC khi ngừng bán, nếu không sàn vẫn bán được "
                + "trong khi hệ thống không còn phân bổ tồn ⇒ BÁN QUÁ TỒN.");
    }

    private async Task<HienThiKenhDto> TaoDtoAsync(ProductChannelVisibility v) => new()
    {
        Id = v.Id,
        ProductId = v.ProductId,
        ChannelCode = v.ChannelCode,
        TenKenh = await _kenh.TenKenhAsync(v.ChannelCode),
        TrangThai = v.TrangThai.ToString(),
        LyDo = v.LyDo,
        CreationTime = v.CreationTime,
        LastModificationTime = v.LastModificationTime,
        KenhConHoatDong = await _kenh.KenhHoatDongAsync(v.ChannelCode)
    };
}

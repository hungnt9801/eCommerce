using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **BỘ TRA GIÁ** — nạp ứng viên từ DB rồi giao **toàn bộ luật** cho `GiaCoSoResolver` (hàm THUẦN).
///
/// 📌 **PHÂN TÁCH TRÁCH NHIỆM** (`PRC-002` **ADR-04**): lớp này **chỉ** làm I/O + đọc **chính sách**;
/// nó **KHÔNG** chứa luật chọn giá. Nhờ vậy luật tiền test được **không cần PostgreSQL**, và không trộn I/O
/// với luật tiền — đúng tiền lệ `TaxRateResolver` (thuần) + `TaxRateResolverService` (nạp).
///
/// 🚨 **THỨ TỰ CẤP KHÔNG ĐƯỢC ĐẢO** (`PRC-001` **D7**): `Shop(1) → Kênh(2) → Bảng giá(3) → Niêm yết(4)`.
/// Lớp này nạp **tất cả** ứng viên của **cả 4 cấp** rồi mới giao đi — **KHÔNG** tự chọn ở đây. Nếu chỗ này
/// "tiện tay" chọn trước thì luật chọn bị **chia làm hai nơi** ⇒ hai đường tính giá khác nhau (giỏ hàng vs
/// checkout) và **không ai biết đường nào đúng**.
/// </summary>
[Authorize(PricingPermissions.Default)]
public class PriceResolverAppService : ApplicationService, IPriceResolverAppService
{
    private readonly IRepository<ChannelPrice, Guid> _giaTheoKenhRepository;
    private readonly IRepository<PriceList, Guid> _bangGiaRepository;
    private readonly IRepository<PriceListItem, Guid> _dongGiaRepository;

    public PriceResolverAppService(
        IRepository<ChannelPrice, Guid> giaTheoKenhRepository,
        IRepository<PriceList, Guid> bangGiaRepository,
        IRepository<PriceListItem, Guid> dongGiaRepository)
    {
        _giaTheoKenhRepository = giaTheoKenhRepository;
        _bangGiaRepository = bangGiaRepository;
        _dongGiaRepository = dongGiaRepository;
    }

    public async Task<ResolvedPriceDto> ResolveAsync(ResolvePriceInput input)
    {
        var ketQua = await TraMotAsync(input);
        return ketQua;
    }

    public async Task<List<ResolvedPriceDto>> ResolveManyAsync(ResolveManyInput input)
    {
        var ketQua = new List<ResolvedPriceDto>(input.Items.Count);

        foreach (var item in input.Items)
        {
            ketQua.Add(await TraMotAsync(item));
        }

        return ketQua;
    }

    private async Task<ResolvedPriceDto> TraMotAsync(ResolvePriceInput input)
    {
        var sku = input.Sku.Trim();
        var kenh = input.ChannelCode.Trim();

        // ⚠️ Đồng hồ đọc **Ở ĐÂY** (tầng app), KHÔNG trong hàm thuần (`PRC-002` ADR-04: pipeline cấm `DateTime.Now`).
        var at = input.At ?? Clock.Now;

        var ungVien = new List<UngVienGia>();

        // ── Cấp 1 + 2: `ChannelPrice` (có hiệu lực tại `at`) ────────────────────────────────────
        var giaTheoKenh = await _giaTheoKenhRepository.GetListAsync(
            x => x.ChannelCode == kenh && x.Sku == sku);

        var giaTheoKenhHieuLuc = giaTheoKenh.Where(x => x.CoHieuLucTai(at)).ToList();

        // Cấp 1: khớp CẢ kênh VÀ shop. Cấp 2: chỉ khớp kênh.
        foreach (var gp in giaTheoKenhHieuLuc.Where(x => x.ChannelShopId == input.ChannelShopId && x.LaTheoShop))
        {
            ungVien.Add(new UngVienGia(
                CapGia.GiaTheoShop, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
        }

        foreach (var gp in giaTheoKenhHieuLuc.Where(x => !x.LaTheoShop))
        {
            ungVien.Add(new UngVienGia(
                CapGia.GiaTheoKenh, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
        }

        // Cấp 3: `PriceListItem` của bảng giá **đang hiệu lực** (khớp kênh: `All` hoặc đúng kênh).
        var bangGiaHieuLuc = (await _bangGiaRepository.GetListAsync(x => x.Status == PriceListStatus.Active))
            .Where(x => x.CoHieuLucTai(at))
            .Where(x => x.Scope == PriceListScope.All
                        || (x.Scope == PriceListScope.Channel && x.ChannelCode == kenh)
                        || (x.Scope == PriceListScope.ChannelShop && x.ChannelCode == kenh
                            && x.ChannelShopId == input.ChannelShopId))
            .ToList();

        if (bangGiaHieuLuc.Count > 0)
        {
            var idBangGia = bangGiaHieuLuc.Select(x => x.Id).ToList();

            var dongGia = await _dongGiaRepository.GetListAsync(
                x => idBangGia.Contains(x.PriceListId) && x.Sku == sku);

            var uuTienTheoBang = bangGiaHieuLuc.ToDictionary(x => x.Id, x => (x.Priority, x.Code));

            foreach (var dong in dongGia)
            {
                var (uuTien, maBangGia) = uuTienTheoBang[dong.PriceListId];

                ungVien.Add(new UngVienGia(
                    CapGia.BangGia, dong.Amount, uuTien, $"PriceList:{maBangGia}#{dong.Sku}"));
            }
        }

        // ── Cấp 4: giá niêm yết ────────────────────────────────────────────────────────────────
        // 📌 Ở lát cắt này Pricing **không** đọc Catalog (Q1) ⇒ giá niêm yết do **bên gọi** truyền khi cần.
        // Hiện chưa có tham số đó trong DTO ⇒ cấp 4 **chưa có ứng viên** nào; ghi rõ ở `prc-001` §17.
        // (Không tự bịa giá niêm yết — bịa là **bán sai giá** mà không ai biết.)

        // Loại tiền: dùng loại tiền của **bảng giá Active** nếu có, ngược lại `VND` (mặc định tenant).
        var loaiTien = bangGiaHieuLuc.Count > 0 ? bangGiaHieuLuc[0].Currency : Currency.Vnd;

        var ket = GiaCoSoResolver.Giai(ungVien, loaiTien, ChinhSachThieuGia.NemLoi, sku, kenh, at);

        Logger.LogInformation(
            "Tra giá SKU {Sku} kênh {Kenh} tại {At}: {Amount} (cấp {Cap}, nguồn {Nguon}, {SoUngVien} ứng viên).",
            sku, kenh, at, ket.Amount.Amount, ket.Cap, ket.Nguon, ket.TatCaUngVien.Count);

        return new ResolvedPriceDto
        {
            Sku = sku,
            ChannelCode = kenh,
            Amount = ket.Amount.Amount,
            CurrencyCode = ket.Amount.Currency.Code,
            WinningLevel = ket.Cap.ToString(),
            Source = ket.Nguon,
            AllCandidates = ket.TatCaUngVien
                .Select(x => new ResolvedPriceCandidateDto
                {
                    Level = x.Cap.ToString(),
                    Amount = x.Amount.Amount,
                    Priority = x.Priority,
                    Source = x.Nguon
                })
                .ToList()
        };
    }
}

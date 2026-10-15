using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **BỘ TRA GIÁ** — nạp ứng viên **theo LÔ** rồi giao **toàn bộ luật** cho `GiaCoSoResolver` (hàm THUẦN).
///
/// 📌 **PHÂN TÁCH TRÁCH NHIỆM** (`PRC-002` **ADR-04**): lớp này **chỉ** làm I/O + đọc chính sách;
/// **KHÔNG** chứa luật chọn giá (tiền lệ `TaxRateResolver` thuần + `TaxRateResolverService` nạp).
///
/// 🚨 **NFR-002 — SỐ TRUY VẤN ≤ 4 CHO MỘT LẦN RESOLVE (kể cả giỏ nhiều dòng):**
/// phiên bản đầu lặp từng dòng và truy vấn **trong vòng lặp** ⇒ **3 truy vấn × N dòng** (N+1). Giỏ 50 dòng
/// thành ~150 truy vấn ⇒ **checkout chậm theo số dòng** mà vẫn "chạy đúng".
/// Nay: nạp **đúng 3 truy vấn cho CẢ LÔ** rồi gom trong bộ nhớ.
/// 🔍 **Đo được:** log có **marker** `NAP-LO-BAT-DAU` / `NAP-LO-KET-THUC` ⇒ **đếm được số lệnh EF thật**
/// (`Executed DbCommand`) nằm giữa hai marker (xem `prc-001` §19.2 — bằng chứng đo, không tự khai).
///
/// ⚠️ **Đồng hồ đọc Ở TẦNG NÀY** (`Clock.Now`) — hàm thuần **cấm** đọc đồng hồ (`PRC-002` ADR-04).
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
        => (await TraTheLoAsync([input]))[0];

    public async Task<List<ResolvedPriceDto>> ResolveManyAsync(ResolveManyInput input)
        => await TraTheLoAsync(input.Items);

    /// <summary>Tra giá cho **cả lô** trong **3 truy vấn** (NFR-002), rồi chạy hàm thuần cho từng dòng.</summary>
    private async Task<List<ResolvedPriceDto>> TraTheLoAsync(IReadOnlyList<ResolvePriceInput> inputs)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var at = inputs[0].At ?? Clock.Now;

        var tapSku = inputs.Select(x => x.Sku.Trim()).Distinct(StringComparer.Ordinal).ToList();

        Logger.LogInformation("NAP-LO-BAT-DAU: {SoDong} dòng, {SoSku} SKU khác nhau.", inputs.Count, tapSku.Count);

        // ── TRUY VẤN 1: mọi `ChannelPrice` của TẬP SKU (lọc kênh/shop/hiệu lực trong bộ nhớ) ──
        var giaTheoKenh = await _giaTheoKenhRepository.GetListAsync(x => tapSku.Contains(x.Sku));

        // ── TRUY VẤN 2: mọi bảng giá đang `Active` ──
        var bangGiaHieuLuc = (await _bangGiaRepository.GetListAsync(x => x.Status == PriceListStatus.Active))
            .Where(x => x.CoHieuLucTai(at))
            .ToList();

        var idBangGia = bangGiaHieuLuc.Select(x => x.Id).ToList();

        // ── TRUY VẤN 3: mọi dòng giá của TẬP SKU trong các bảng giá đang hiệu lực ──
        var dongGia = idBangGia.Count == 0
            ? []
            : await _dongGiaRepository.GetListAsync(x => idBangGia.Contains(x.PriceListId) && tapSku.Contains(x.Sku));

        Logger.LogInformation(
            "NAP-LO-KET-THUC: đã nạp {SoGiaKenh} giá theo kênh, {SoBangGia} bảng giá, {SoDongGia} dòng giá.",
            giaTheoKenh.Count, bangGiaHieuLuc.Count, dongGia.Count);

        var ketQua = new List<ResolvedPriceDto>(inputs.Count);

        foreach (var input in inputs)
        {
            ketQua.Add(TraMotDong(input, at, giaTheoKenh, bangGiaHieuLuc, dongGia));
        }

        return ketQua;
    }

    private ResolvedPriceDto TraMotDong(
        ResolvePriceInput input,
        DateTime at,
        IReadOnlyList<ChannelPrice> giaTheoKenh,
        IReadOnlyList<PriceList> bangGiaHieuLuc,
        IReadOnlyList<PriceListItem> dongGia)
    {
        var sku = input.Sku.Trim();
        var kenh = input.ChannelCode.Trim();

        var ungVien = new List<UngVienGia>();

        // ── Cấp 1 + 2: `ChannelPrice` (lọc theo SKU + kênh + shop + hiệu lực) ──
        foreach (var gp in giaTheoKenh.Where(x => x.Sku == sku && x.ChannelCode == kenh && x.CoHieuLucTai(at)))
        {
            if (gp.LaTheoShop && gp.ChannelShopId == input.ChannelShopId)
            {
                ungVien.Add(new UngVienGia(CapGia.GiaTheoShop, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
            }
            else if (!gp.LaTheoShop)
            {
                ungVien.Add(new UngVienGia(CapGia.GiaTheoKenh, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
            }
        }

        // ── Cấp 3: `PriceListItem` của bảng giá đang hiệu lực, khớp phạm vi kênh ──
        var bangGiaKhop = bangGiaHieuLuc
            .Where(x => x.Scope == PriceListScope.All
                        || (x.Scope == PriceListScope.Channel && x.ChannelCode == kenh)
                        || (x.Scope == PriceListScope.ChannelShop && x.ChannelCode == kenh
                            && x.ChannelShopId == input.ChannelShopId))
            .ToDictionary(x => x.Id, x => (x.Priority, x.Code));

        foreach (var dong in dongGia.Where(x => x.Sku == sku && bangGiaKhop.ContainsKey(x.PriceListId)))
        {
            var (uuTien, maBangGia) = bangGiaKhop[dong.PriceListId];

            ungVien.Add(new UngVienGia(CapGia.BangGia, dong.Amount, uuTien, $"PriceList:{maBangGia}#{dong.Sku}"));
        }

        // ── Cấp 4: **giá niêm yết do BÊN GỌI truyền vào** ─────────────────────────────────────────
        // 🚨 Pricing **KHÔNG** đọc Catalog (`PRC-001` §15.1 **Q1**) ⇒ giá niêm yết **phải** do bên gọi cung cấp
        // (`SAL-002`/API lấy từ Catalog). **CẤM tự bịa** một giá niêm yết: bịa ⇒ **bán sai giá** mà **không ai biết**.
        var loaiTienNiemYet = input.ListPriceCurrency is null
            ? LoaiTienMacDinh(bangGiaHieuLuc)
            : Currency.FromCode(input.ListPriceCurrency);

        if (input.ListPrice is { } giaNiemYet)
        {
            ungVien.Add(new UngVienGia(
                CapGia.GiaNiemYet, Money.Cua(giaNiemYet, loaiTienNiemYet), 0, "ListPrice:caller"));
        }

        var ket = GiaCoSoResolver.Giai(ungVien, loaiTienNiemYet, ChinhSachThieuGia.NemLoi, sku, kenh, at);

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

    /// <summary>
    /// Loại tiền dùng khi bên gọi **không** khai: lấy loại tiền của **bảng giá đang hiệu lực** (nguồn sự thật
    /// gần nhất); không có bảng giá nào ⇒ `VND` (mặc định tenant — ghi rõ ở `prc-001` §19.4).
    /// </summary>
    private static Currency LoaiTienMacDinh(IReadOnlyList<PriceList> bangGiaHieuLuc)
        => bangGiaHieuLuc.Count > 0 ? bangGiaHieuLuc[0].Currency : Currency.Vnd;
}

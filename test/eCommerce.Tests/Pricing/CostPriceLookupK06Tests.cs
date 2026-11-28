using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Tests.Pricing.HaTang;
using Volo.Abp.Application.Services;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — CA `K-06` TẦNG ĐỌC GIÁ VỐN ĐÃ DUYỆT** (`D-320` vi; `PRC-001-arch` §3.1/§5.5).
///
/// Phủ `K06-01` (giá vốn `0` **có** giá vốn), `K06-02` (không có dòng đã duyệt ⇒ `null` + **CẤM 0**),
/// `K06-03` (dòng **chưa duyệt** không được dùng) qua `CostPriceLookupAdapter` — cổng đọc thật mà Reporting
/// (`REP-002`) sẽ dùng. Đồng thời neo `BR-PRC-001-013`: port đọc giá vốn **KHÔNG** phải app service (không lộ endpoint).
/// </summary>
public class CostPriceLookupK06Tests
{
    private static readonly DateOnly Ngay1 = new(2026, 1, 1);
    private static readonly DateOnly Ngay2 = new(2026, 1, 2);

    private static CostPrice Tao(
        decimal tien,
        DateOnly tu,
        DateOnly? den = null,
        bool duyet = true,
        string sku = "SKU-1")
    {
        var giaVon = new CostPrice(Guid.NewGuid(), sku, Money.Vnd(tien), tu, den);

        if (duyet)
        {
            giaVon.Duyet(Guid.NewGuid(), new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));
        }

        return giaVon;
    }

    private static CostPriceLookupAdapter Dung(params CostPrice[] duLieu)
    {
        var repo = new RepoGia<CostPrice>();
        foreach (var dong in duLieu)
        {
            repo.Them(dong);
        }

        return new CostPriceLookupAdapter(repo);
    }

    /// <summary>K06-01 — dòng đã duyệt có số tiền `0` **vẫn là** "có giá vốn" (`CoGiaVon = true`).</summary>
    [Fact]
    public async Task K06_01_gia_von_0__co_gia_von_khong_phai_thieu()
    {
        var adapter = Dung(Tao(0m, Ngay1));

        var got = await adapter.GetApprovedCostAsync("SKU-1", Ngay1);

        Assert.NotNull(got);
        Assert.Equal(0m, got!.AmountValue);
        Assert.True(got.CoGiaVon);
    }

    /// <summary>K06-02 — KHÔNG có dòng đã duyệt hiệu lực ⇒ `null` (bên gọi đặt `CostMissing`; **CẤM 0**).</summary>
    [Fact]
    public async Task K06_02_khong_co_dong_da_duyet__tra_null()
    {
        var adapter = Dung();

        var got = await adapter.GetApprovedCostAsync("SKU-1", Ngay1);

        Assert.Null(got);
    }

    /// <summary>K06-02 — dòng đã duyệt nằm NGOÀI khoảng hiệu lực tại ngày hỏi ⇒ `null`.</summary>
    [Fact]
    public async Task K06_02_dong_ngoai_hieu_luc__tra_null()
    {
        var adapter = Dung(Tao(100_000m, Ngay2));

        var got = await adapter.GetApprovedCostAsync("SKU-1", Ngay1);

        Assert.Null(got);
    }

    /// <summary>K06-03 — dòng **CHƯA duyệt** hiệu lực tại ngày ⇒ **không** được dùng ⇒ `null`.</summary>
    [Fact]
    public async Task K06_03_dong_chua_duyet__tra_null()
    {
        var adapter = Dung(Tao(100_000m, Ngay1, duyet: false));

        var got = await adapter.GetApprovedCostAsync("SKU-1", Ngay1);

        Assert.Null(got);
    }

    /// <summary>K06-02 — bản lô (job báo cáo) trả MỘT DTO/đầu vào, thiếu ⇒ `AmountValue = null` (không bỏ dòng).</summary>
    [Fact]
    public async Task K06_02_lo_thieu_dong__van_tra_dto_voi_AmountValue_null()
    {
        var adapter = Dung(Tao(100_000m, Ngay1));

        var got = await adapter.GetApprovedCostManyAsync(new GetApprovedCostManyInput
        {
            Items =
            [
                new CostLookupItemDto { Sku = "SKU-1", At = Ngay1 },
                new CostLookupItemDto { Sku = "SKU-KHONG-CO", At = Ngay1 },
            ],
        });

        Assert.Equal(2, got.Count);
        Assert.Equal(100_000m, got[0].AmountValue);
        Assert.Null(got[1].AmountValue);
        Assert.False(got[1].CoGiaVon);
    }

    /// <summary>
    /// BR-PRC-001-013 — cổng đọc giá vốn **KHÔNG** phải `IApplicationService` ⇒ không thành endpoint công khai
    /// (giá vốn là biên lợi nhuận — `CostPrice.cs` chú thích lớp).
    /// </summary>
    [Fact]
    public void BR_013_port_doc_gia_von_KHONG_phai_app_service()
    {
        Assert.False(typeof(IApplicationService).IsAssignableFrom(typeof(ICostPriceLookup)));
        Assert.False(typeof(IApplicationService).IsAssignableFrom(typeof(CostPriceLookupAdapter)));
    }
}

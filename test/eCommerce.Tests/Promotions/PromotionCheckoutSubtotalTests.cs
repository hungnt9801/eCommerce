using eCommerce.Application.Checkout;
using eCommerce.Application.Contracts.Checkout;
using eCommerce.Application.Contracts.Inventory;
using eCommerce.Customers;
using eCommerce.Inventory;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Promotions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`TC-73`** (`D-291` ② P2 #2, SRS §6.1 quy tắc 9) — `CheckoutAppService` truyền `Subtotal` =
/// `GioTinhTienDto.Subtotal` (=`tongTruocGiam` của **pipeline giá**) và `Currency` = `CurrencyCode` của giỏ,
/// **KHÔNG** `Σ ListPrice × Quantity` và **KHÔNG** hard-code `"VND"`. Lượt DÒ dùng cờ additive
/// `KhongGhiSoGiaiTrinh = true` ⇒ không ghi sổ giải trình lần hai; lượt tính chính thức (`false`) mới ghi.
///
/// 🚨 Ca "kênh có giá bán < `ListPrice`": giỏ `ListPrice 100.000` nhưng pipeline trả `Subtotal 80.000`;
/// engine phải nhận **80.000** (nếu mã nguồn lại `Sum(ListPrice)` ⇒ **ĐỎ**).
/// </summary>
public class PromotionCheckoutSubtotalTests
{
    private sealed class TinhGiaGia : IPriceCalculationAppService
    {
        public GioTinhTienDto KetQua { get; set; } = new();

        /// <summary>Các lần `TinhGioAsync` — khẳng định cờ dò và `OrderDiscount` của lượt chính thức.</summary>
        public List<TinhGioInput> Lenh { get; } = new();

        public Task<GioTinhTienDto> TinhGioAsync(TinhGioInput input)
        {
            Lenh.Add(input);
            return Task.FromResult(KetQua);
        }

        public Task<GioTinhTienDto> GetSoGiaiTrinhAsync(string idempotencyKey)
            => throw new BusinessException(PricingErrorCodes.KhongTimThayGia);

        public Task<List<PriceCalculationRecordDto>> GetLichSuGiaiTrinhAsync(
            DateTime? tuNgay = null, DateTime? denNgay = null, int toiDa = 50)
            => Task.FromResult(new List<PriceCalculationRecordDto>());
    }

    private sealed class GiuChoGia : IStockReservationAppService
    {
        public int SoLanGiuCho { get; private set; }

        public Task<StockReservationDto> GiuChoAsync(GiuChoInput input)
        {
            SoLanGiuCho++;
            return Task.FromResult(new StockReservationDto
            {
                Id = Guid.NewGuid(),
                WarehouseId = input.WarehouseId,
                Sku = input.Sku,
                Quantity = input.Quantity,
                Status = nameof(ReservationStatus.Held),
                ExpiresAtUtc = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc)
            });
        }

        public Task<List<StockReservationDto>> GetDanhSachAsync(ReservationStatus? status = null)
            => Task.FromResult(new List<StockReservationDto>());

        public Task<StockReservationDto> GetAsync(Guid id) => throw new NotSupportedException();
        public Task<StockReservationDto> NhaChoAsync(Guid id, DateTime? at = null) => throw new NotSupportedException();
        public Task<KetQuaNhaChoHetHanDto> NhaChoHetHanAsync(DateTime? at = null) => throw new NotSupportedException();
        public Task<KetQuaChotDto> ChotTheoNguonAsync(string referenceId, DateTime? at = null) => throw new NotSupportedException();
    }

    private sealed class KhachGia : ICustomerDirectory
    {
        public Task<CustomerDto?> TimTheoIdAsync(Guid customerId) => throw new NotSupportedException();
        public Task<CustomerDto?> TimTheoTaiKhoanAsync(Guid identityUserId) => throw new NotSupportedException();
        public Task<bool> ChoPhepGiaoDichAsync(Guid? customerId) => Task.FromResult(true);
    }

    private sealed class KhuyenMaiGia : IPromotionEngine
    {
        public PromotionEvaluationRequest? YeuCau { get; private set; }
        public PromotionEvaluationResult KetQua { get; set; } = new(
            false, 0m, "USD", Array.Empty<PromotionBreakdownItem>(), null, 0, 0);

        public Task<PromotionEvaluationResult> EvaluateAsync(
            PromotionEvaluationRequest request, CancellationToken cancellationToken = default)
        {
            YeuCau = request;
            return Task.FromResult(KetQua);
        }
    }

    private static CheckoutAppService Service(
        TinhGiaGia tinhGia, GiuChoGia giuCho, KhuyenMaiGia khuyenMai)
    {
        var sut = new CheckoutAppService(
            tinhGia, giuCho, new KhachGia(), khuyenMai,
            new SettingGia(new Dictionary<string, string?>()));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
        return sut;
    }

    private static CheckoutInput Gio() => new()
    {
        ChannelCode = "web",
        CustomerId = null,
        WarehouseId = Guid.NewGuid(),
        Items = new List<DongCheckoutDto>
        {
            new() { Sku = "SKU-1", Quantity = 1, ListPrice = 100_000m }
        },
        OrderDiscount = 0m,
        IdempotencyKey = "checkout-tc73",
        At = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task TC73_Subtotal_va_Currency_lay_tu_pipeline_gia__khong_Sum_ListPrice()
    {
        var tinhGia = new TinhGiaGia
        {
            // Kênh có giá bán < ListPrice: pipeline (bảng giá/kênh) ra 80.000 dù ListPrice 100.000.
            KetQua = new GioTinhTienDto { CurrencyCode = "USD", Subtotal = 80_000m, Total = 80_000m }
        };
        var khuyenMai = new KhuyenMaiGia
        {
            KetQua = new PromotionEvaluationResult(true, 5_000m, "USD", Array.Empty<PromotionBreakdownItem>(), null, 0, 0)
        };

        var kq = await Service(tinhGia, new GiuChoGia(), khuyenMai).TinhVaGiuChoAsync(Gio());

        Assert.NotNull(khuyenMai.YeuCau);
        Assert.Equal(80_000m, khuyenMai.YeuCau!.Subtotal);          // pipeline, KHÔNG `Σ ListPrice`
        Assert.NotEqual(100_000m, khuyenMai.YeuCau.Subtotal);       // chứng minh 0 `Sum(ListPrice)`
        Assert.Equal("USD", khuyenMai.YeuCau.Currency);             // không hard-code "VND"
        Assert.False(kq.LaKetQuaDaCo);
    }

    [Fact]
    public async Task TC73_luot_do_dung_co_KhongGhiSoGiaiTrinh__luot_chinh_thuc_moi_ghi()
    {
        var tinhGia = new TinhGiaGia
        {
            KetQua = new GioTinhTienDto { CurrencyCode = "VND", Subtotal = 80_000m, Total = 80_000m }
        };
        var khuyenMai = new KhuyenMaiGia
        {
            KetQua = new PromotionEvaluationResult(true, 5_000m, "VND", Array.Empty<PromotionBreakdownItem>(), null, 0, 0)
        };

        await Service(tinhGia, new GiuChoGia(), khuyenMai).TinhVaGiuChoAsync(Gio());

        Assert.Equal(2, tinhGia.Lenh.Count);                        // dò + chính thức
        Assert.True(tinhGia.Lenh[0].KhongGhiSoGiaiTrinh);           // lượt DÒ: KHÔNG ghi sổ
        Assert.False(tinhGia.Lenh[1].KhongGhiSoGiaiTrinh);          // lượt CHÍNH THỨC: ghi sổ
        Assert.Equal(5_000m, tinhGia.Lenh[1].OrderDiscount);        // giảm giá engine vào lượt chính thức
    }
}

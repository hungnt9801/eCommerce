using eCommerce.Application.Contracts.Checkout;
using eCommerce.Application.Contracts.Inventory;
using eCommerce.Application.Sales;
using eCommerce.Customers;
using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Settings;

namespace eCommerce.Application.Checkout;

/// <summary>
/// 🚨 **CHECKOUT MỘT BƯỚC** (`SAL-002`) — **tính tiền RỒI giữ chỗ**, tất cả trong **một transaction**.
///
/// **THỨ TỰ VÀ LÝ DO:**
///   0. 🚨 **CỔNG KHÁCH BỊ CHẶN** (`CUS-001` `ADR-04`) — gọi `ICustomerDirectory.ChoPhepGiaoDichAsync` **trước
///      mọi tác dụng phụ**. Khách `Blocked` ⇒ `eCommerce:Customers:0005`, **không** giữ chỗ, **không** ghi sổ giá.
///   1. **Tra sổ giải trình theo `IdempotencyKey` TRƯỚC** ⇒ nếu đã có ⇒ **trả lại kết quả cũ** (kèm lượt giữ chỗ cũ)
///      và **thoát ngay**, **không** tính lại, **không** giữ chỗ thêm. 📌 Đây là điều kiện để *"khách bấm Đặt hàng
///      hai lần"* và *"client retry sau timeout"* **không** tạo hai lượt giữ chỗ / hai lần trừ kho.
///   2. **Tính tiền** (`Pricing` pipeline) — hàm này **đã ghi sổ giải trình**; nếu giá thiếu ⇒ `Pricing:0004`.
///   3. **Giữ chỗ TỪNG dòng** theo `referenceId = Checkout:{khoá}`.
///   4. Nếu **bất kỳ** bước nào ném ⇒ **transaction rollback toàn bộ** ⇒ **không** lượt giữ chỗ nào, **và cả**
///      bản ghi sổ giải trình cũng biến mất ⇒ **fail-closed** (khách **không** thấy "đã đặt" khi hàng chưa được giữ).
///
/// ⚠️ **GHI RÕ GIỚI HẠN (không che):**
///   • **Một kho cho cả đơn** (`WarehouseId` của input). Chọn kho theo SKU/tồn khả dụng là việc của **`INV-003`**
///     (phân bổ tồn theo kênh) — hiện **chưa** làm.
///   • **Chưa tạo đơn hàng**: lát cắt này **giữ chỗ + tính tiền**. Việc sinh `Order` + `OrderLine` + chuyển
///     giữ chỗ thành xuất kho là **`SAL-001`/`SAL-003`** (lát cắt sau).
///   • Gọi **app service** khác (`PriceCalculationAppService`) thay vì gọi thẳng dịch vụ miền: chấp nhận được vì
///     **cùng một transaction của request** (ABP `UnitOfWork` bao ngoài); nếu sau này cần tái dùng ngoài HTTP thì
///     phải tách thành dịch vụ miền.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class CheckoutAppService : ApplicationService, ICheckoutAppService
{
    private readonly IPriceCalculationAppService _tinhGia;
    private readonly IStockReservationAppService _giuCho;

    /// <summary>🚨 **CỔNG KHÁCH BỊ CHẶN** (`CUS-001` `ADR-04`, nợ §16.4 #4 đóng ở `S3`) — hợp đồng **CHỈ ĐỌC**
    /// của module `Customers` (`ADR-12`). 📌 Trước `S3` cổng này **tồn tại nhưng không ai gọi** ⇒ khách `Blocked`
    /// **vẫn đặt được hàng** ở đường thật.</summary>
    private readonly ICustomerDirectory _thuMucKhach;

    /// <summary>🆕 `PRO-002` D6 — engine khuyến mãi **in-process** (nguồn giảm giá cấp đơn DUY NHẤT).</summary>
    private readonly IPromotionEngine _khuyenMai;

    /// <summary>🆕 `PRO-002` — đọc cờ `Promotions:Enabled` (cờ tắt ⇒ KHÔNG gọi engine, `DiscountTotal = 0`).</summary>
    private readonly ISettingProvider _settings;

    public CheckoutAppService(
        IPriceCalculationAppService tinhGia,
        IStockReservationAppService giuCho,
        ICustomerDirectory thuMucKhach,
        IPromotionEngine khuyenMai,
        ISettingProvider settings)
    {
        _tinhGia = tinhGia;
        _giuCho = giuCho;
        _thuMucKhach = thuMucKhach;
        _khuyenMai = khuyenMai;
        _settings = settings;
    }

    public async Task<KetQuaCheckoutDto> TinhVaGiuChoAsync(CheckoutInput input)
    {
        // ── 🚨 `PRO-002` D6 / `D-143` (2): `OrderDiscount` do CLIENT gửi PHẢI = 0 ─────────────────────
        // 🔑 `DiscountTotal` của đơn **chỉ** đến từ engine khuyến mãi. Nhận giảm giá tay từ client là
        // **nguồn giảm giá thứ hai** ⇒ `TinhTienDon` phân bổ sai ⇒ thuế/doanh thu sai IMP LẶNG.
        // 📌 Kiểm **TRƯỚC MỌI tác dụng phụ** (giữ chỗ/tính tiền) ⇒ lỗi ⇒ **0 đơn, 0 lượt giữ chỗ**.
        if (input.OrderDiscount != 0m)
        {
            throw new BusinessException(SalesErrorCodes.KhongNhanGiamGiaNhapTay)
                .WithData("lyDo", "OrderDiscount phải do engine khuyến mãi tính — client gửi ≠ 0 bị từ chối.");
        }

        // ── ⓪ CỔNG KHÁCH BỊ CHẶN — TRƯỚC MỌI TÁC DỤNG PHỤ (`CUS-001` §28) ─────────────────────────
        // 🔑 **VÌ SAO LÀ BƯỚC ĐẦU TIÊN, KHÔNG PHẢI BƯỚC SAU `ThuKetQuaCuAsync`:** bước ① có thể **trả lại**
        // một kết quả cũ ⇒ nếu đặt cổng sau nó, khách vừa bị chặn mà **vẫn nhận được** *"đã giữ chỗ"* của lần
        // trước ⇒ đọc như *"vẫn mua được"*. 📌 Và nếu đặt cổng **sau** bước ③, hàng đã bị **giữ** rồi mới
        // bị chặn ⇒ đơn không tạo nhưng **kho vẫn bị giam** tới hết TTL (15 phút) — cổng chặn đơn lại
        // **làm hại kho**, đúng loại *"sửa một chỗ, hỏng chỗ khác"*.
        // ⚠️ Cố ý **KHÔNG** bọc `try/catch`: `0005` phải nổi lên thành lỗi nghiệp vụ cho bên gọi
        // (controller `DatHang` bắt `BusinessException` để hiện mã — xem quy ước #7).
        await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_thuMucKhach, input.CustomerId);

        if (input.Items.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.SoLuongPhaiDuong)
                .WithData("lyDo", "Giỏ phải có ít nhất một dòng.");
        }

        var khoa = input.IdempotencyKey.Trim();
        var thamChieu = ThamChieu(khoa);

        // ── ① IDEMPOTENT: đã có sổ giải trình cho khoá này ⇒ TRẢ LẠI, không ghi gì thêm ──────────────
        var daCo = await ThuKetQuaCuAsync(khoa, thamChieu);

        if (daCo is not null)
        {
            Logger.LogInformation("CHECKOUT IDEMPOTENT: khoá {Khoa} đã có ⇒ trả lại kết quả cũ.", khoa);
            return daCo;
        }

        // Kiểm TRƯỚC khi tính tiền: nếu nguồn này đã giữ chỗ mà CHƯA có sổ giải trình (trường hợp dở dang),
        // phải DỪNG thay vì giữ chỗ chồng lên — nếu không, `Reserved` tăng gấp đôi.
        var giuChoCu = await TimGiuChoAsync(thamChieu);

        if (giuChoCu.Count > 0)
        {
            throw new BusinessException(InventoryErrorCodes.DaGiuChoNguonNay)
                .WithData("idempotencyKey", khoa)
                .WithData("soLuotGiuCho", giuChoCu.Count)
                .WithData("lyDo",
                    "Nguồn này ĐÃ giữ chỗ nhưng KHÔNG có sổ giải trình tính giá ⇒ dữ liệu dở dang, từ chối chạy tiếp.");
        }

        // ── ② TÍNH TIỀN (pipeline Pricing; hàm này GHI SỔ GIẢI TRÌNH) ────────────────────────────────
        var at = input.At ?? Clock.Now;

        // 🆕 `PRO-002` D6 — giảm giá cấp đơn **chỉ** đến từ engine (nếu bật cờ). Engine CHỈ ĐỌC.
        var giamGiaKhuyenMai = 0m;
        var batKhuyenMai = !string.Equals(
            (await _settings.GetOrNullAsync(PromotionContractConsts.EnabledSettingName))?.Trim(),
            "false",
            StringComparison.OrdinalIgnoreCase);

        // Dựng dòng giỏ MỘT LẦN cho cả lượt dò giá lẫn lượt tính chính thức (cùng cửa `TinhGioAsync`).
        var itemTinhGia = input.Items.Select(x => new DongGioInput
        {
            Sku = x.Sku,
            Quantity = x.Quantity,
            TaxRate = x.TaxRate,
            ListPrice = x.ListPrice
        }).ToList();

        if (batKhuyenMai)
        {
            // `D-291` ② P2 #2 — `Subtotal` (== `tongTruocGiam`) và `Currency` PHẢI lấy từ **pipeline giá**;
            // KHÔNG `Sum(ListPrice × Quantity)` (ListPrice chỉ là giá niêm yết fallback) và KHÔNG hard-code
            // `"VND"` (đơn ngoại tệ sẽ lệch `Money` — `XC-001`). Lượt DÒ dùng cờ additive
            // `KhongGhiSoGiaiTrinh = true` ⇒ KHÔNG ghi sổ giải trình (lượt tính chính thức bên dưới mới ghi,
            // `ADR-13` giữ).
            var giaTruoc = await _tinhGia.TinhGioAsync(new TinhGioInput
            {
                ChannelCode = input.ChannelCode,
                ChannelShopId = input.ChannelShopId,
                OrderDiscount = 0m,
                OrderFee = input.OrderFee,
                At = at,
                IdempotencyKey = khoa,
                KhongGhiSoGiaiTrinh = true,
                Items = itemTinhGia
            });

            var kqKhuyenMai = await _khuyenMai.EvaluateAsync(new PromotionEvaluationRequest(
                CheckoutSessionId: Guid.Empty, // engine KHÔNG dùng; khoá idempotency thuộc bước ghi của Sales
                Lines: input.Items
                    .Select(x => new PromotionCartLine(x.Sku, x.Quantity, x.ListPrice ?? 0m))
                    .ToList(),
                ChannelCode: input.ChannelCode,
                Currency: giaTruoc.CurrencyCode,
                Subtotal: giaTruoc.Subtotal,
                CustomerId: input.CustomerId,
                CustomerGroupId: null, // Q26 fail-closed: Sales chưa nạp nhóm khách
                IsFirstOrder: null,     // Q26 fail-closed
                VoucherCode: null,
                Mode: PromotionApplyMode.AutoApply));

            giamGiaKhuyenMai = kqKhuyenMai.Applied ? kqKhuyenMai.DiscountTotal : 0m;
        }

        var tien = await _tinhGia.TinhGioAsync(new TinhGioInput
        {
            ChannelCode = input.ChannelCode,
            ChannelShopId = input.ChannelShopId,
            OrderDiscount = giamGiaKhuyenMai,
            OrderFee = input.OrderFee,
            At = at,
            IdempotencyKey = khoa,
            Items = itemTinhGia
        });

        // ── ③ GIỮ CHỖ TỪNG DÒNG (cùng transaction) ─────────────────────────────────────────────────
        var idGiuCho = new List<Guid>(input.Items.Count);
        var hetHanSomNhat = DateTime.MaxValue;

        foreach (var dong in input.Items)
        {
            var gc = await _giuCho.GiuChoAsync(new GiuChoInput
            {
                WarehouseId = input.WarehouseId,
                Sku = dong.Sku,
                Quantity = dong.Quantity,
                TtlPhut = input.TtlPhut,
                ReferenceType = nameof(Contracts.Checkout.CheckoutInput),
                ReferenceId = thamChieu,
                // 🚨 `INV-003`: kênh của checkout được chuyển xuống `StockReservationService` để **áp hạn mức theo kênh**
                // (chính sách "cho phép chọn": `Inv.ChannelLimit.*`, `web-001` §93–§94).
                ChannelCode = input.ChannelCode,
                At = at
            });

            idGiuCho.Add(gc.Id);
            hetHanSomNhat = gc.ExpiresAtUtc < hetHanSomNhat ? gc.ExpiresAtUtc : hetHanSomNhat;
        }

        Logger.LogInformation(
            "CHECKOUT OK: khoá {Khoa} · {SoDong} dòng · tổng {Tong} · giữ chỗ đến {HetHan}.",
            khoa, input.Items.Count, tien.Total, hetHanSomNhat);

        return new KetQuaCheckoutDto
        {
            Tien = tien,
            ReservationIds = idGiuCho,
            ExpiresAtUtc = hetHanSomNhat,
            LaKetQuaDaCo = false
        };
    }

    public async Task<KetQuaCheckoutDto> GetTheoKhoaAsync(string idempotencyKey)
    {
        var khoa = (idempotencyKey ?? string.Empty).Trim();
        var thamChieu = ThamChieu(khoa);

        var ketQua = await ThuKetQuaCuAsync(khoa, thamChieu);

        return ketQua
            ?? throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Chưa có kết quả checkout nào cho khoá này.")
                .WithData("idempotencyKey", khoa);
    }

    /// <summary>Lấy kết quả đã có (nếu có): **sổ giải trình** + **các lượt giữ chỗ** theo cùng nguồn.</summary>
    private async Task<KetQuaCheckoutDto?> ThuKetQuaCuAsync(string khoa, string thamChieu)
    {
        GioTinhTienDto tien;

        try
        {
            tien = await _tinhGia.GetSoGiaiTrinhAsync(khoa);
        }
        catch (BusinessException)
        {
            return null;   // chưa từng tính ⇒ chưa có gì để trả lại
        }

        var giuCho = await TimGiuChoAsync(thamChieu);

        var hetHan = giuCho.Count > 0 ? giuCho.Min(x => x.ExpiresAtUtc) : default;

        return new KetQuaCheckoutDto
        {
            Tien = tien,
            ReservationIds = giuCho.Select(x => x.Id).ToList(),
            ExpiresAtUtc = hetHan,
            LaKetQuaDaCo = true
        };
    }

    private async Task<List<StockReservationDto>> TimGiuChoAsync(string thamChieu)
    {
        var tatCa = await _giuCho.GetDanhSachAsync(ReservationStatus.Held);

        return tatCa.Where(x => x.ReferenceId == thamChieu).ToList();
    }

    /// <summary>Nguồn giữ chỗ **ổn định** theo khoá idempotency — cùng khoá ⇒ cùng nguồn ⇒ tìm lại được.</summary>
    private static string ThamChieu(string khoa) => $"Checkout:{khoa}";
}

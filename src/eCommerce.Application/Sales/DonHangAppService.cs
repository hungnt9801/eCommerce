using eCommerce.Application.Contracts.Sales;
using eCommerce.Customers;
using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🚨 **TẠO ĐƠN HÀNG TỪ CHECKOUT** (`SAL-001`) — nguồn số tiền là **SỔ GIẢI TRÌNH**, **KHÔNG** phải tính lại.
///
/// **TRÌNH TỰ:**
///   0. 🚨 **CỔNG KHÁCH BỊ CHẶN** (`CUS-001` `ADR-04`) — `eCommerce:Customers:0005`, **trước** mọi thao tác ghi.
///   1. Đọc **sổ giải trình** theo `CheckoutIdempotencyKey` ⇒ **số tiền đã báo khách** (bằng chứng).
///   2. Đọc **lượt giữ chỗ** theo cùng nguồn (`Checkout:{khoá}`) ⇒ **hàng đã được giữ**; không có ⇒ **NÉM**
///      (đơn mà không giữ được hàng là **đơn không có hàng** ⇒ sẽ phải xin lỗi khách).
///   3. Tạo `Order` + `OrderLine` với **đúng** đơn giá **đã làm tròn** từ sổ (`UnitPrice`) ⇒ rồi `SetDiscount`,
///      `SetTotals`, `Recalculate()`.
///   4. 🚨 **CHỐT KIỂM TRA (tripwire):** `order.GrandTotal` **PHẢI** bằng `tien.Total` của sổ giải trình.
///      Lệch ⇒ **NÉM**, **không** lưu đơn. 📌 Đây là **lưới an toàn rẻ nhất** cho loại lỗi tệ nhất của miền bán hàng:
///      *đơn ghi một số, khách thấy một số khác*. Nếu ai đó sửa công thức ở một trong hai nơi, tripwire **nổ ngay**
///      thay vì âm thầm lệch vài đồng trên mỗi đơn.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Orders.Default)]
public class DonHangAppService : ApplicationService, IDonHangAppService
{
    private readonly IRepository<Order, Guid> _donRepository;
    private readonly IRepository<OrderLine, Guid> _dongDonRepository;
    private readonly IRepository<StockReservation, Guid> _giuChoRepository;
    private readonly IPriceCalculationAppService _tinhGia;
    private readonly ChotGiuChoService _chotGiuCho;
    private readonly StockReservationService _giuChoService;
    private readonly eCommerce.Application.Contracts.Inventory.IStockDocumentAppService _phieuKho;

    /// <summary>🚨 **CỔNG KHÁCH BỊ CHẶN** — cùng cổng với checkout (`CuaKiemKhachGiaoDich`), 📌 vì đây là
    /// **đường vào thứ hai**: `TaoTuCheckoutAsync` là API công khai cho module/đối tác, ⚠️ nên *"checkout đã
    /// kiểm"* **KHÔNG** suy ra *"đường tạo đơn đã kiểm"*. Kiểm ở **cả hai** biên là chủ ý, không phải trùng lặp.</summary>
    private readonly ICustomerDirectory _thuMucKhach;

    public DonHangAppService(
        IRepository<Order, Guid> donRepository,
        IRepository<OrderLine, Guid> dongDonRepository,
        IRepository<StockReservation, Guid> giuChoRepository,
        IPriceCalculationAppService tinhGia,
        ChotGiuChoService chotGiuCho,
        StockReservationService giuChoService,
        eCommerce.Application.Contracts.Inventory.IStockDocumentAppService phieuKho,
        ICustomerDirectory thuMucKhach)
    {
        _donRepository = donRepository;
        _dongDonRepository = dongDonRepository;
        _giuChoRepository = giuChoRepository;
        _tinhGia = tinhGia;
        _chotGiuCho = chotGiuCho;
        _giuChoService = giuChoService;
        _phieuKho = phieuKho;
        _thuMucKhach = thuMucKhach;
    }

    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<DonHangDto> TaoTuCheckoutAsync(TaoDonTuCheckoutInput input)
    {
        // ── ⓪ CỔNG KHÁCH BỊ CHẶN (`CUS-001` `ADR-04` / §28) — TRƯỚC mọi tác dụng phụ ───────────────
        // 📌 Đặt TRƯỚC bước ① vì bước ① chỉ là phép ĐỌC, nhưng bước ③ **GHI ĐƠN**: khách bị chặn phải bị
        // chặn ở đây, và **không** được nhận một `DonHangDto` trông như đơn hợp lệ.
        await CuaKiemKhachGiaoDich.DamBaoDuocGiaoDichAsync(_thuMucKhach, input.CustomerId);

        var khoa = input.CheckoutIdempotencyKey.Trim();
        var thamChieu = $"Checkout:{khoa}";

        // ① SỐ TIỀN từ SỔ GIẢI TRÌNH (không tính lại). Không có ⇒ `Pricing:0004`.
        var tien = await _tinhGia.GetSoGiaiTrinhAsync(khoa);

        // ② Phải CÓ hàng đang giữ cho nguồn này.
        var giuCho = (await _giuChoRepository.GetListAsync(x => x.ReferenceId == thamChieu))
            .Where(x => x.Status == ReservationStatus.Held)
            .ToList();

        if (giuCho.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("idempotencyKey", khoa)
                .WithData("lyDo", "Không còn lượt giữ chỗ nào cho checkout này ⇒ KHÔNG tạo đơn " +
                                  "(đơn mà không có hàng là đơn không giao được).");
        }

        // ③ Tạo đơn với ĐÚNG đơn giá đã chốt trong sổ giải trình.
        var don = new Order(GuidGenerator.Create(), input.Number, input.ChannelCode)
        {
            ChannelShopId = input.ChannelShopId,
            CustomerId = input.CustomerId,
            Note = input.Note,
            Currency = tien.CurrencyCode,
            // 🔗 Lưu khoá để: (a) xác nhận đơn biết chốt lượt giữ chỗ nào, (b) unique index chặn HAI đơn
            // cho cùng một checkout, (c) truy vết ngược về bằng chứng giá khi có khiếu nại.
            CheckoutIdempotencyKey = khoa
        };

        await _donRepository.InsertAsync(don, autoSave: false);

        foreach (var dong in tien.Lines)
        {
            var dongDon = don.AddLine(GuidGenerator.Create(), dong.Sku, dong.Quantity, dong.UnitPrice);
            await _dongDonRepository.InsertAsync(dongDon, autoSave: false);
        }

        don.SetDiscount(tien.TotalDiscount);
        don.SetTotals(tien.TotalTax, tien.TotalFee);
        don.Recalculate();

        // ④ 🚨 TRIPWIRE: đơn PHẢI khớp TỪNG ĐỒNG với số đã báo khách.
        if (don.GrandTotal != tien.Total)
        {
            throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                .WithData("grandTotalDon", don.GrandTotal)
                .WithData("totalSoGiaiTrinh", tien.Total)
                .WithData("chenhLech", don.GrandTotal - tien.Total)
                .WithData("idempotencyKey", khoa)
                .WithData("lyDo",
                    "Số tiền trên đơn KHÁC số tiền trong sổ giải trình ⇒ KHÔNG lưu đơn " +
                    "(khách đã thấy một số, đơn không được ghi số khác).");
        }

        await _donRepository.UpdateAsync(don, autoSave: true);

        Logger.LogInformation(
            "TẠO ĐƠN {Number} từ checkout {Khoa}: tổng {Tong} (ĐÃ ĐỐI CHIẾU sổ giải trình: khớp), " +
            "{SoDong} dòng, {SoGiuCho} lượt giữ chỗ.",
            don.Number, khoa, don.GrandTotal, tien.Lines.Count, giuCho.Count);

        return ToDto(don, tien.Lines.Count, tien.Total, khoa);
    }

    /// <summary>
    /// 🚨 **XÁC NHẬN ĐƠN** ⇒ **chốt giữ chỗ** ⇒ **xuất kho** (`SAL-003`) ⇒ `Confirmed`.
    /// 📌 Đây là bước **duy nhất** biến *"hàng đang giữ"* thành *"hàng đã bán"*. Trước bước này **không** xuất kho:
    /// khách còn có thể huỷ, và huỷ thì chỉ cần **nhả chỗ** (`INV-002`) — **không** phải tạo chứng từ đảo.
    /// **NÉM** nếu: đơn không ở `Pending`, **hoặc** lượt giữ chỗ **đã quá hạn** (`0031` — *hàng đã được trả về
    /// cho người khác*; xác nhận tiếp là **bán hai lần**).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<DonHangDto> XacNhanAsync(Guid id, DateTime? at = null)
    {
        var don = await _donRepository.GetAsync(id);

        if (don.Status != OrderStatus.Pending)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("number", don.Number)
                .WithData("status", don.Status.ToString())
                .WithData("lyDo", "Chỉ đơn ở trạng thái `Pending` mới xác nhận được.");
        }

        if (string.IsNullOrWhiteSpace(don.CheckoutIdempotencyKey))
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("number", don.Number)
                .WithData("lyDo", "Đơn KHÔNG có khoá checkout ⇒ không biết chốt lượt giữ chỗ nào.");
        }

        var thoiDiem = at ?? Clock.Now;

        // Chốt TOÀN BỘ lượt giữ chỗ của nguồn ⇒ `OnHand` và `Reserved` cùng giảm (bất biến `SAL-003`).
        var soChot = await _chotGiuCho.ChotTheoNguonAsync($"Checkout:{don.CheckoutIdempotencyKey}", thoiDiem);

        if (soChot == 0)
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("number", don.Number)
                .WithData("idempotencyKey", don.CheckoutIdempotencyKey)
                .WithData("lyDo", "Không còn lượt giữ chỗ nào để chốt ⇒ KHÔNG xác nhận (đơn không có hàng).");
        }

        don.SetStatus(OrderStatus.Confirmed);
        await _donRepository.UpdateAsync(don, autoSave: true);

        Logger.LogInformation("XÁC NHẬN ĐƠN {Number}: đã chốt {So} lượt giữ chỗ ⇒ hàng ra khỏi kho.",
            don.Number, soChot);

        return ToDto(don, (int)await _dongDonRepository.CountAsync(x => x.OrderId == id),
            don.GrandTotal, don.CheckoutIdempotencyKey ?? "(không có)");
    }

    /// <summary>
    /// 🚨 **HUỶ ĐƠN** ⇒ **NHẢ CHỖ NGAY** (đường **ĐỐI XỨNG** với <see cref="XacNhanAsync"/>).
    /// 📌 **VÌ SAO PHẢI NHẢ NGAY (không chờ job hết TTL):** hàng phải trở lại kho **ngay** để người khác mua được.
    /// Chờ job nghĩa là hàng nằm chết thêm tới 15 phút **một cách vô ích** — và với đơn bị huỷ thì **không có lý do gì**
    /// để giữ tiếp. 📌 Đây cũng là điều kiện để **không** phải tạo chứng từ đảo: chưa xuất kho thì chỉ cần **nhả chỗ**.
    /// **NÉM** nếu đơn **đã ghi sổ xuất kho** (`Confirmed`) — khi đó phải **trả hàng** (chứng từ nhập) và **đảo tiền**,
    /// **không** phải huỷ.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<DonHangDto> HuyDonAsync(Guid id, DateTime? at = null)
    {
        var don = await _donRepository.GetAsync(id);

        if (don.Status == OrderStatus.Cancelled)
        {
            // Idempotent: huỷ hai lần ⇒ trả trạng thái hiện tại, KHÔNG nhả chỗ lần nữa (nhả hai lần sẽ bị `0030`).
            return ToDto(don, (int)await _dongDonRepository.CountAsync(x => x.OrderId == id),
                don.GrandTotal, don.CheckoutIdempotencyKey ?? "(không có)");
        }

        if (don.Status != OrderStatus.Pending)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("number", don.Number)
                .WithData("status", don.Status.ToString())
                .WithData("lyDo",
                    "Chỉ đơn `Pending` mới huỷ được. Đơn đã xác nhận (đã xuất kho) thì phải TRẢ HÀNG " +
                    "(chứng từ nhập) — KHÔNG xoá bút toán sổ cái (append-only).");
        }

        var soNha = string.IsNullOrWhiteSpace(don.CheckoutIdempotencyKey)
            ? 0
            : await _giuChoService.NhaTheoNguonAsync(
                $"Checkout:{don.CheckoutIdempotencyKey}", at ?? Clock.Now, ReservationStatus.Released);

        don.SetStatus(OrderStatus.Cancelled);
        await _donRepository.UpdateAsync(don, autoSave: true);

        Logger.LogInformation("HUỶ ĐƠN {Number}: đã nhả {So} lượt giữ chỗ ⇒ hàng về kho NGAY.", don.Number, soNha);

        return ToDto(don, (int)await _dongDonRepository.CountAsync(x => x.OrderId == id),
            don.GrandTotal, don.CheckoutIdempotencyKey ?? "(không có)");
    }

    /// <summary>
    /// 🚨 **TRẢ HÀNG** cho đơn đã xác nhận ⇒ **sinh phiếu NHẬP kho** (dùng lại `StockDocumentAppService` đã kiểm chứng)
    /// ⇒ hàng **về kho thật** (`OnHand` tăng) ⇒ `Returned`.
    /// 📌 **KHÔNG** xoá/đảo bút toán cũ: sổ cái **append-only** ⇒ trả hàng là **ghi bút toán MỚI**.
    /// **NÉM** nếu đơn ở `Pending` (chưa xuất kho ⇒ huỷ, không phải trả hàng) hoặc đã `Cancelled`/`Returned`.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<DonHangDto> TraHangAsync(Guid id, TraHangInput input)
    {
        var don = await _donRepository.GetAsync(id);

        var warehouseId = input.WarehouseId;
        var lines = input.Lines;
        var at = input.At;

        if (don.Status == OrderStatus.Returned)
        {
            // Idempotent: trả hàng hai lần ⇒ trả trạng thái hiện tại, KHÔNG nhập kho lần nữa (nhập hai lần = thừa hàng ảo).
            return ToDto(don, (int)await _dongDonRepository.CountAsync(x => x.OrderId == id),
                don.GrandTotal, don.CheckoutIdempotencyKey ?? "(không có)");
        }

        if (don.Status != OrderStatus.Confirmed && don.Status != OrderStatus.Shipped && don.Status != OrderStatus.Completed)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.InsufficientStock)
                .WithData("number", don.Number)
                .WithData("status", don.Status.ToString())
                .WithData("lyDo",
                    "Chỉ đơn ĐÃ XÁC NHẬN (đã xuất kho) mới trả hàng được. Đơn `Pending` thì HUỶ, không phải trả hàng.");
        }

        var dongDon = await _dongDonRepository.GetListAsync(x => x.OrderId == id);

        if (dongDon.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong).WithData("number", don.Number);
        }

        // ── 🚨 KIỂM **TÍNH HỢP LỆ CỦA ĐẦU VÀO TRƯỚC** mọi phép suy diễn khác ──────────────────────────────
        // 📌 **VÌ SAO PHẢI ĐẶT Ở ĐÂY (lỗi thứ tự phát hiện vòng 43):** khi khối này nằm **SAU** phần tính `yeuCau`,
        // một yêu cầu mà `lines` **chỉ toàn SKU lạ** cho ra `yeuCau` **rỗng** ⇒ rơi vào nhánh
        // `ChungTuThieuDong` (0011 *"chứng từ thiếu dòng"*) ⇒ mã `0041` **KHÔNG BAO GIỜ tới được** (mã chết)
        // và client nhận **chẩn đoán sai** (*"thiếu dòng"* thay vì *"SKU không thuộc đơn"*).
        // 📌 Nguyên tắc: **kiểm đầu vào trước, diễn giải sau.**
        if (lines is { Count: > 0 })
        {
            var skuDon = dongDon.Select(x => x.Sku).ToHashSet(StringComparer.Ordinal);
            var skuLa = lines.Select(l => l.Sku).Where(s => !skuDon.Contains(s)).Distinct(StringComparer.Ordinal).ToList();

            if (skuLa.Count > 0)
            {
                throw new BusinessException(InventoryErrorCodes.SkuKhongThuocDon)
                    .WithData("number", don.Number)
                    .WithData("skuKhongThuocDon", string.Join(", ", skuLa));
            }
        }

        // ── 🚨 GIỚI HẠN LUỸ KẾ: đã trả bao nhiêu (các lần TRƯỚC) + lần này ≤ đã mua ─────────────────────
        // 📌 Vì sao phải LUỸ KẾ chứ không chỉ kiểm lần này: khách trả 2 rồi trả 2 nữa (đơn mua 4) — nếu chỉ kiểm
        // "lần này ≤ 4" thì **cả hai lần đều hợp lệ** ⇒ **trả 4 mà nhập lại 8** ⇒ **thừa hàng ảo**.
        // Và vì sổ cái **append-only**, sửa lại phải **tạo chứng từ đảo** ⇒ đây là lỗi **đắt** để sửa.
        var maTienTo = $"TH-{don.Number}";

        var phieuTraCu = await _phieuKho.GetListAsync(StockDocumentType.CustomerReturn);
        var idPhieuTraCu = phieuTraCu.Where(x => x.Number.StartsWith(maTienTo, StringComparison.Ordinal))
            .Select(x => x.Id).ToList();

        var daTraTheoSku = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var idPhieu in idPhieuTraCu)
        {
            foreach (var d in await _phieuKho.GetLinesAsync(idPhieu))
            {
                daTraTheoSku[d.Sku] = daTraTheoSku.TryGetValue(d.Sku, out var cu) ? cu + d.Quantity : d.Quantity;
            }
        }

        // `lines` rỗng ⇒ trả **toàn bộ phần CÒN LẠI** (không phải "toàn bộ đơn" — nếu đã trả một phần thì chỉ trả nốt).
        var yeuCau = dongDon
            .Select(x => new
            {
                x.Sku,
                DaMua = x.Quantity,
                DaTra = daTraTheoSku.TryGetValue(x.Sku, out var t) ? t : 0,
                YeuCau = lines is { Count: > 0 }
                    ? lines.Where(l => l.Sku == x.Sku).Sum(l => l.Quantity)
                    : Math.Max(0, x.Quantity - (daTraTheoSku.TryGetValue(x.Sku, out var t2) ? t2 : 0))
            })
            .Where(x => x.YeuCau > 0)
            .ToList();

        var vuot = yeuCau.Where(x => x.DaTra + x.YeuCau > x.DaMua).ToList();

        if (vuot.Count > 0)
        {
            throw new BusinessException(InventoryErrorCodes.TraVuotSoDaMua)
                .WithData("number", don.Number)
                .WithData("lyDo", "Trả hàng VƯỢT số đã mua (tính LUỸ KẾ các lần trả trước).")
                .WithData("chiTiet", string.Join("; ", vuot.Select(x =>
                    $"{x.Sku}: đã mua {x.DaMua}, đã trả {x.DaTra}, lần này {x.YeuCau}")));
        }

        if (yeuCau.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong)
                .WithData("number", don.Number)
                .WithData("lyDo", "Không còn gì để trả (đã trả hết).");
        }


        var thoiDiem = at ?? Clock.Now;

        // 📌 Dùng LẠI dịch vụ kho: tạo phiếu nhập + ghi sổ ⇒ mọi bất biến của `INV-001` (chứng từ & ghi sổ hai bước,
        // khoá phân tán, `CHECK` DB) **được áp nguyên vẹn** thay vì viết một đường ghi tồn kho thứ hai.
        var phieu = await _phieuKho.CreateAsync(new eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto
        {
            // 🚨 **SỐ CHỨNG TỪ PHẢI KHÁC NHAU GIỮA CÁC LẦN TRẢ** — lỗi thật phát hiện vòng 41:
            // số cố định `TH-{đơn}` ⇒ lần trả **thứ hai** (trả một phần rồi trả nốt) **trùng số** ⇒
            // unique `(TenantId, Number)` chặn ⇒ **không trả nốt được** (`Inventory:0014`).
            // 📌 Đúng loại lỗi mà cơ chế ánh xạ ràng buộc (Phụ lục B) đã chỉ **đúng tên ràng buộc** — không còn `500`.
            Number = $"TH-{don.Number}-{idPhieuTraCu.Count + 1}",
            // 🚨 Dùng **loại chứng từ RIÊNG** (`CustomerReturn`) thay vì "mượn" `GoodsReceipt`:
            // sổ cái ghi biến động `CustomerReturn` ⇒ báo cáo *bán* và *trả* **tách bạch** (`inv-001` §31).
            Type = StockDocumentType.CustomerReturn,
            WarehouseId = warehouseId,
            OccurredAtUtc = thoiDiem,
            Note = $"Trả hàng cho đơn {don.Number}",
            Lines = yeuCau.Select(x => new eCommerce.Application.Contracts.Inventory.CreateStockDocumentLineDto
            {
                Sku = x.Sku,
                Quantity = x.YeuCau
            }).ToList()
        });

        var ketQua = await _phieuKho.GhiSoAsync(phieu.Id, thoiDiem);

        // 📌 **Trạng thái:** chỉ `Returned` khi **đã trả HẾT**; trả một phần thì **giữ `Confirmed`**.
        // ⚠️ **GHI NHẬN:** chưa có trạng thái `PartiallyReturned` ⇒ đơn trả một phần **trông như** đơn chưa trả gì.
        // Cần trạng thái/cờ riêng (lát cắt sau) — **không** tự bịa trạng thái mới ở đây vì nó đổi hợp đồng API.
        var sauKhiTra = yeuCau.ToDictionary(x => x.Sku, x => x.DaTra + x.YeuCau, StringComparer.Ordinal);
        var conLai = dongDon.Any(x => sauKhiTra[x.Sku] < x.Quantity);

        don.SetStatus(conLai ? OrderStatus.Confirmed : OrderStatus.Returned);
        await _donRepository.UpdateAsync(don, autoSave: true);

        Logger.LogInformation(
            "TRẢ HÀNG đơn {Number}: phiếu nhập {Phieu} đã ghi sổ ({SoButToan} bút toán) ⇒ HÀNG VỀ KHO.",
            don.Number, phieu.Number, ketQua.PostedEntries);

        return ToDto(don, dongDon.Count, don.GrandTotal, don.CheckoutIdempotencyKey ?? "(không có)");
    }

    /// <summary>
    /// 🚨 **PHÂN BỔ TỒN THEO KÊNH** (`INV-003` **bước 1 — NHÌN THẤY**): mỗi cặp **(kênh, SKU)** ⇒
    /// *đã bán* · *đang giữ* · *đã trả*.
    ///
    /// 📌 **Suy ra từ ĐƠN, không thêm cột:** `InvStockReservations` **không có** `ChannelCode`, nhưng mỗi lượt giữ chỗ
    /// trỏ tới `Checkout:{khoá}` và **đơn giữ chính khoá đó** (`CheckoutIdempotencyKey`) + `ChannelCode` ⇒ suy ra được
    /// **không cần migration**. 📌 Thêm cột/hạn mức cứng là lựa chọn **khác** — **cần người quyết** (xem tài liệu).
    /// </summary>
    public async Task<List<PhanBoKenhDto>> GetPhanBoTheoKenhAsync()
    {
        var donList = await _donRepository.GetListAsync();
        var dongList = await _dongDonRepository.GetListAsync();
        var giuChoList = await _giuChoRepository.GetListAsync(x => x.Status == ReservationStatus.Held);

        var dongTheoDon = dongList.GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.ToList());
        var giuChoTheoNguon = giuChoList
            .GroupBy(x => x.ReferenceId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var ket = new Dictionary<(string Kenh, string Sku), PhanBoKenhDto>();

        PhanBoKenhDto Lay(string kenh, string sku)
        {
            var khoa = (kenh, sku);

            if (!ket.TryGetValue(khoa, out var dto))
            {
                dto = new PhanBoKenhDto { ChannelCode = kenh, Sku = sku };
                ket[khoa] = dto;
            }

            return dto;
        }

        foreach (var don in donList)
        {
            var kenh = don.ChannelCode ?? "(không kênh)";

            if (!dongTheoDon.TryGetValue(don.Id, out var dong))
            {
                continue;
            }

            // "Đã bán" = đơn **đã rời khỏi** `Pending` và **không** bị `Cancelled` (tức đã xuất kho / đã trả một phần).
            var daBan = don.Status is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Completed or OrderStatus.Returned;

            foreach (var d in dong)
            {
                if (daBan)
                {
                    Lay(kenh, d.Sku).DaBan += d.Quantity;
                }
            }

            // "Đang giữ" = lượt giữ chỗ `Held` của **chính nguồn checkout của đơn này**.
            if (!string.IsNullOrWhiteSpace(don.CheckoutIdempotencyKey)
                && giuChoTheoNguon.TryGetValue($"Checkout:{don.CheckoutIdempotencyKey}", out var giuCho))
            {
                foreach (var gc in giuCho)
                {
                    var dongKenh = Lay(kenh, gc.Sku);
                    dongKenh.DangGiu += gc.Quantity;

                    // Tách theo kho — `WarehouseId` của **lượt giữ chỗ** (khác `Order`: đơn KHÔNG lưu kho).
                    var khoaKho = gc.WarehouseId.ToString();
                    dongKenh.DangGiuTheoKho[khoaKho] = dongKenh.DangGiuTheoKho.TryGetValue(khoaKho, out var daCo)
                        ? daCo + gc.Quantity
                        : gc.Quantity;
                }
            }

            // "Đã trả" = các dòng của **phiếu `CustomerReturn`** sinh từ đơn này (số phiếu có tiền tố `TH-{số đơn}`).
            foreach (var phieu in await _phieuKho.GetListAsync(StockDocumentType.CustomerReturn))
            {
                if (!phieu.Number.StartsWith($"TH-{don.Number}", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var d in await _phieuKho.GetLinesAsync(phieu.Id))
                {
                    Lay(kenh, d.Sku).DaTra += d.Quantity;
                }
            }
        }

        return ket.Values
            .OrderBy(x => x.ChannelCode, StringComparer.Ordinal)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<DonHangDto> GetAsync(Guid id)
    {
        var don = await _donRepository.GetAsync(id);
        var soDong = (int)await _dongDonRepository.CountAsync(x => x.OrderId == id);

        return ToDto(don, soDong, don.GrandTotal, "(xem log)");
    }

    private static DonHangDto ToDto(Order don, int soDong, decimal totalTuSo, string checkoutKey) => new()
    {
        Id = don.Id,
        Number = don.Number,
        ChannelCode = don.ChannelCode,
        Status = don.Status.ToString(),
        Currency = don.Currency,
        Subtotal = don.Subtotal,
        DiscountTotal = don.DiscountTotal,
        TaxTotal = don.TaxTotal,
        ShippingTotal = don.ShippingTotal,
        GrandTotal = don.GrandTotal,
        LineCount = soDong,
        TotalTuSoGiaiTrinh = totalTuSo,
        CheckoutIdempotencyKey = checkoutKey
    };
}

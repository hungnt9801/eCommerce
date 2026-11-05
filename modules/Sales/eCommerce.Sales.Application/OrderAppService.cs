using System.Linq.Expressions;
using eCommerce.Application.Contracts.Sales;
using eCommerce.Orders;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🆕 **`WP-05`** — **BỘ ĐỊNH NGHĨA 14 TAB** của màn DS đơn (`07 §WP-05` việc 6, pattern Nhanh *"tab có badge số"*).
///
/// 🚨 **VÌ SAO TAB LÀ DỮ LIỆU (không phải 14 chuỗi `if` rải trong page):**
/// ① **Tanh đếm và lọc phải CÙNG một định nghĩa** — nếu tab "Cần xử lý" đếm 7 mà bấm vào chỉ ra 5 dòng thì
/// người dùng **mất tin** vào badge (và badge là thứ duy nhất khiến họ nhìn danh sách).
/// ② Test được **không cần DB**: `Loc.Compile()` chạy trên đơn dựng tay ⇒ mỗi tab có ca *"thuộc/không thuộc"*.
///
/// 📌 **`Expression` (không phải `Func`)** để EF dịch được sang SQL ⇒ đếm/lọc ở **server** (20.000 đơn không thể
/// tải hết rồi lọc ở client).
/// </summary>
public static class OrderTabs
{
    /// <summary>Một tab: khoá URL · nhãn hiển thị · điều kiện (dịch được sang SQL).</summary>
    public sealed record Tab(string Key, string Label, Expression<Func<Order, bool>> Loc);

    /// <summary>
    /// **Trạng thái mà đơn KHÔNG tự đi tiếp** ⇒ nhân viên phải bấm gì đó.
    /// 📌 Vì sao loại `AwaitingPickup`/`OutForDelivery`: hàng đang trên đường, **HVC** cập nhật — không phải
    /// việc của shop. Ngược lại `DeliveryFailed` **phải** có người xử lý (giao lại/chuyển hoàn) ⇒ nằm trong tab.
    /// 📌 `Returning`/`ReturnConfirmed` **thuộc** tab vì kho phải đếm và nhận hàng hoàn.
    /// </summary>
    private static readonly FulfillmentStatus[] CanXuLy =
    [
        FulfillmentStatus.Pending,
        FulfillmentStatus.PendingConfirm,
        FulfillmentStatus.Confirming,
        FulfillmentStatus.OutOfStock,
        FulfillmentStatus.WarehouseSwitched,
        FulfillmentStatus.Confirmed,
        FulfillmentStatus.Packing,
        FulfillmentStatus.Packed,
        FulfillmentStatus.DeliveryFailed,
        FulfillmentStatus.Returning,
        FulfillmentStatus.ReturnConfirmed
    ];

    private static readonly FulfillmentStatus[] DaHuy =
    [
        FulfillmentStatus.CustomerCancelled,
        FulfillmentStatus.SystemCancelled,
        FulfillmentStatus.CarrierCancelled
    ];

    /// <summary>**ĐÚNG 14 tab**, thứ tự hiển thị cố định (Nhanh: tab nghiệp vụ trước, tab trạng thái sau).</summary>
    public static readonly IReadOnlyList<Tab> DanhSach =
    [
        new("all", "Tất cả", _ => true),

        new("can-xu-ly", "Cần xử lý",
            x => !x.IsCancelled && CanXuLy.Contains(x.FulfillmentStatus)),

        new("pending", "Mới", x => x.FulfillmentStatus == FulfillmentStatus.Pending),

        new("pending-confirm", "Chờ/Đang xác nhận",
            x => x.FulfillmentStatus == FulfillmentStatus.PendingConfirm
                 || x.FulfillmentStatus == FulfillmentStatus.Confirming),

        new("confirmed", "Đã xác nhận", x => x.FulfillmentStatus == FulfillmentStatus.Confirmed),

        new("out-of-stock", "Hết hàng / Đổi kho",
            x => x.FulfillmentStatus == FulfillmentStatus.OutOfStock
                 || x.FulfillmentStatus == FulfillmentStatus.WarehouseSwitched),

        new("packing", "Đóng gói",
            x => x.FulfillmentStatus == FulfillmentStatus.Packing
                 || x.FulfillmentStatus == FulfillmentStatus.Packed),

        new("awaiting-pickup", "Chờ thu gom", x => x.FulfillmentStatus == FulfillmentStatus.AwaitingPickup),

        new("shipping", "Đang chuyển", x => x.FulfillmentStatus == FulfillmentStatus.OutForDelivery),

        new("delivered", "Thành công",
            // 📌 Loại đơn **đang xin trả** (`ReturnRequested`/`ReturnApproved`/`PartiallyReturned`) và
            // `Returned` khỏi tab "Thành công": nếu không, cùng một đơn hiện ở **hai** tab và badge nói dối.
            // `ReturnRejected` (từ chối trả) **vẫn** là thành công — đơn đã giao xong, không có việc gì nữa.
            x => !x.IsCancelled
                 && x.FulfillmentStatus == FulfillmentStatus.Delivered
                 && (x.ReturnStatus == ReturnStatus.Open || x.ReturnStatus == ReturnStatus.ReturnRejected)),

        new("failed", "Thất bại", x => x.FulfillmentStatus == FulfillmentStatus.DeliveryFailed),

        new("returning", "Đang hoàn",
            // 📌 **HAI đường hoàn** phải nằm cùng một tab: (a) trục thực hiện (`Returning`/`Xác nhận hoàn`);
            // (b) trục kết thúc (`Yêu cầu trả`/`Đã duyệt trả`/`Trả một phần`) — đơn ĐÃ GIAO rồi mới xin trả
            // nên trục thực hiện vẫn `Delivered`. Bỏ nhánh (b) thì đơn xin trả **rơi khỏi mọi tab** ⇒
            // badge cộng lại **không bằng** "Tất cả".
            x => x.FulfillmentStatus == FulfillmentStatus.Returning
                 || x.FulfillmentStatus == FulfillmentStatus.ReturnConfirmed
                 || x.ReturnStatus == ReturnStatus.ReturnRequested
                 || x.ReturnStatus == ReturnStatus.ReturnApproved
                 || x.ReturnStatus == ReturnStatus.PartiallyReturned),

        new("returned", "Đã hoàn",
            x => x.FulfillmentStatus == FulfillmentStatus.Returned
                 || x.ReturnStatus == ReturnStatus.Returned),

        new("cancelled", "Đã huỷ", x => x.IsCancelled || DaHuy.Contains(x.FulfillmentStatus))
    ];

    /// <summary>Tìm tab theo khoá; **khoá lạ ⇒ `null`** (chỗ gọi báo lỗi `0011`, KHÔNG im lặng bỏ qua).</summary>
    public static Tab? Tim(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return DanhSach.FirstOrDefault(t => string.Equals(t.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 🆕 **`WP-05`** — **API ĐƠN HÀNG** cho Web Admin (`07 §WP-05` việc 6).
///
/// 🔐 Quyền: **đọc** = `eCommerce.Orders` (cấp class); **ghi** (chuyển trạng thái/huỷ) **KHÔNG** ở đây —
/// nằm ở <see cref="OrderStatusAppService"/> với **quyền riêng từng trục**. 📌 Tách vậy để màn DS/chi tiết
/// (chỉ đọc) **không** cấp kèm quyền ghi cho người chỉ cần xem.
///
/// 🚨 **KHÔNG** có method ghi trục tài chính (`BR-013`): tiền chỉ do `PaymentHub`/IPN đã xác thực chữ ký ghi.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Orders.Default)]
public class OrderAppService : ApplicationService, IOrderAppService
{
    private readonly IRepository<Order, Guid> _donHang;
    private readonly IRepository<OrderLine, Guid> _dongDon;

    /// <summary>
    /// 🆕 `WP-07` — **phạm vi dữ liệu** của người đang đăng nhập (`07 §WP-07`: DoD
    /// *"nhân viên bật 'chỉ xem dữ liệu của mình' chỉ thấy đơn mình tạo"*).
    /// 🚨 Lọc ở **server**: ẩn ở UI là **không lọc** (DevTools vẫn gọi được API — và đó là dữ liệu doanh thu).
    /// 📌 Dùng chung **một** hợp đồng với `Pos` (`Customers.IDataScopeResolver`) để hai màn không lệch nhau.
    /// </summary>
    private readonly eCommerce.Customers.IDataScopeResolver _phamVi;

    public OrderAppService(
        IRepository<Order, Guid> donHang,
        IRepository<OrderLine, Guid> dongDon,
        eCommerce.Customers.IDataScopeResolver phamVi)
    {
        _donHang = donHang;
        _dongDon = dongDon;
        _phamVi = phamVi;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<OrderListItemDto>> GetListAsync(OrderListInput input)
    {
        var q = await ApDungBoLocAsync(input);

        var tab = OrderTabs.Tim(input.Tab);

        if (tab is not null)
        {
            q = q.Where(tab.Loc);
        }
        else if (!string.IsNullOrWhiteSpace(input.Tab))
        {
            // 🚨 Khoá tab lạ: **NÉM**, không trả "tất cả" — trả tất cả thì người dùng tưởng tab đó rỗng/đầy
            // mà thực ra bộ lọc đã bị bỏ im lặng.
            throw new Volo.Abp.BusinessException(OrderErrorCodes.DuLieuDonKhongHopLe)
                .WithData("field", "tab").WithData("value", input.Tab)
                .WithData("lyDo", "Tab không tồn tại trong 14 tab của màn đơn hàng.");
        }

        var tong = await AsyncExecuter.CountAsync(q);

        // ⚠️ Sắp xếp **tất định** (ngày tạo giảm dần, rồi `Number`) để phân trang không lặp/mất dòng.
        // ⚠️ KHÔNG truyền `StringComparer` vào `ThenBy` trên `IQueryable` — EF không dịch được (bài học `WP-04`).
        var trang = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Number)
                .Skip(input.SkipCount).Take(input.PageSize));

        var ids = trang.Select(x => x.Id).ToList();

        // Đếm số dòng của **đúng trang này** (một truy vấn) — không nạp cả bảng dòng.
        var soDong = ids.Count == 0
            ? new Dictionary<Guid, int>()
            : (await _dongDon.GetListAsync(x => ids.Contains(x.OrderId)))
                .GroupBy(x => x.OrderId)
                .ToDictionary(g => g.Key, g => g.Count());

        return new PagedResultDto<OrderListItemDto>(
            tong,
            trang.Select(x => ToListItem(x, soDong.GetValueOrDefault(x.Id))).ToList());
    }

    /// <inheritdoc />
    public async Task<OrderTabCountsDto> GetTabCountsAsync(OrderListInput input)
    {
        var q = await ApDungBoLocAsync(input);

        var tabs = new List<OrderTabDto>(OrderTabs.DanhSach.Count);

        foreach (var tab in OrderTabs.DanhSach)
        {
            // 📌 14 lời đếm trên cột **đã có index** (`IX_OrdOrders_TenantId_FulfillmentStatus_CreationTime`).
            // Nếu thành điểm nóng, đích đúng là **read-model** của `WP-08` — không phải bỏ badge (badge là
            // thứ biến danh sách thành hàng đợi việc).
            tabs.Add(new OrderTabDto
            {
                Key = tab.Key,
                Label = tab.Label,
                Count = await AsyncExecuter.CountAsync(q.Where(tab.Loc))
            });
        }

        return new OrderTabCountsDto
        {
            Total = tabs.FirstOrDefault(t => t.Key == "all")?.Count ?? 0,
            Tabs = tabs
        };
    }

    /// <inheritdoc />
    public async Task<OrderDetailDto> GetAsync(Guid id)
    {
        var don = await _donHang.GetAsync(id);

        // 🆕 `WP-07` — kiểm phạm vi ở **đường đọc chi tiết** (không chỉ DS): nếu thiếu, đổi GUID trên URL
        // là xem được đơn ngoài phạm vi — đúng lỗi *"lọc ở client"* mà `07 §WP-07` cấm.
        await KiemPhamViAsync(don);

        var dong = await _dongDon.GetListAsync(x => x.OrderId == id);

        var dto = new OrderDetailDto();
        SaoChep(don, soDong: dong.Count, dto);

        dto.Currency = don.Currency;
        dto.Subtotal = don.Subtotal;
        dto.DiscountTotal = don.DiscountTotal;
        dto.TaxTotal = don.TaxTotal;
        dto.ShippingTotal = don.ShippingTotal;
        dto.ShippingFeeCustomer = don.ShippingFeeCustomer;
        dto.ShippingFeeActual = don.ShippingFeeActual;
        dto.WeightGram = don.WeightGram;
        dto.CustomerId = don.CustomerId;
        dto.SaleStaffId = don.SaleStaffId;
        dto.PackerId = don.PackerId;
        dto.ExternalOrderId = don.ExternalOrderId;
        dto.CheckoutIdempotencyKey = don.CheckoutIdempotencyKey;
        dto.Note = don.Note;
        dto.RowVersion = don.RowVersion;
        dto.FulfillmentVersion = don.FulfillmentVersion;
        dto.PaymentVersion = don.PaymentVersion;
        dto.ReconciliationVersion = don.ReconciliationVersion;
        dto.ClosureVersion = don.ClosureVersion;
        dto.CanCancel = !don.IsCancelled && OrderStateMachine.CoTheHuy(don.FulfillmentStatus);

        // 📌 Nút hợp lệ lấy từ **bảng cạnh của domain** — UI không chép tay danh sách (một định nghĩa).
        dto.AllowedTransitions = Enum.GetValues<FulfillmentStatus>()
            .Where(x => OrderStateMachine.CoCanh(OrderAxis.Fulfillment, (int)don.FulfillmentStatus, (int)x))
            .ToList();

        dto.Lines = dong
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Select(x => new OrderLineItemDto
            {
                Id = x.Id,
                Sku = x.Sku,
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                ReturnedQuantity = x.ReturnedQuantity,
                UnitPrice = x.UnitPrice,
                DiscountAmount = x.DiscountAmount,
                TaxAmount = x.TaxAmount,
                LineTotal = x.LineTotal
            })
            .ToList();

        return dto;
    }

    /// <summary>
    /// Áp **mọi bộ lọc trừ tab** — dùng chung cho DS và bộ đếm ⇒ hai bên **không thể lệch nhau**.
    /// </summary>
    private async Task<IQueryable<Order>> ApDungBoLocAsync(OrderListInput input)
    {
        var q = await _donHang.GetQueryableAsync();

        // 🆕 `WP-07` — **phạm vi dữ liệu** áp TRƯỚC mọi bộ lọc khác (một chỗ duy nhất, áp cho **cả** DS và
        // bộ đếm 14 tab ⇒ badge không thể đếm những đơn người dùng không được thấy).
        q = await ApDungPhamViAsync(q);

        if (!string.IsNullOrWhiteSpace(input.Q))
        {
            var tim = input.Q.Trim();
            q = q.Where(x => x.Number.Contains(tim)
                             || (x.TrackingNumber != null && x.TrackingNumber.Contains(tim))
                             || (x.ExternalOrderId != null && x.ExternalOrderId.Contains(tim)));
        }

        if (input.FulfillmentStatus is { } thucHien)
        {
            q = q.Where(x => x.FulfillmentStatus == thucHien);
        }

        if (input.Type is { } loai)
        {
            q = q.Where(x => x.Type == loai);
        }

        if (input.Source is { } nguon)
        {
            q = q.Where(x => x.Source == nguon);
        }

        if (input.WarehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        // 🆕 `WP-07` — tab "Đơn hàng" của màn chi tiết khách.
        if (input.CustomerId is { } khachId)
        {
            q = q.Where(x => x.CustomerId == khachId);
        }

        if (input.FromUtc is { } tu)
        {
            q = q.Where(x => x.CreationTime >= tu);
        }

        if (input.ToUtc is { } den)
        {
            q = q.Where(x => x.CreationTime <= den);
        }

        return q;
    }

    /// <summary>
    /// 🆕 `WP-07` — áp **phạm vi dữ liệu** lên truy vấn đơn (server-side).
    /// 📌 `Own` ⇒ `CreatorId == người đăng nhập`; `Store` ⇒ `WarehouseId == kho của cửa hàng`.
    /// ⚠️ Thiếu khoá để lọc (`Own` không có người dùng / `Store` chưa gắn kho) ⇒ **RỖNG**, **không** rơi về
    /// "tất cả": nới quyền im lặng khi cấu hình thiếu là lỗi bảo mật tệ nhất trong ba lựa chọn.
    /// 📌 Đơn do **sàn/kênh** tạo (`CreatorId = null`) **không** thuộc `Own` — đúng nghĩa *"dữ liệu của tôi"*.
    /// </summary>
    private async Task<IQueryable<Order>> ApDungPhamViAsync(IQueryable<Order> q)
    {
        var phamVi = await _phamVi.LayAsync();

        return phamVi.PhamVi switch
        {
            eCommerce.Customers.PhamViDuLieu.Own =>
                phamVi.NguoiDungId is { } nguoiDung ? q.Where(x => x.CreatorId == nguoiDung) : q.Where(_ => false),

            eCommerce.Customers.PhamViDuLieu.Store =>
                phamVi.KhoCuaHangId is { } kho ? q.Where(x => x.WarehouseId == kho) : q.Where(_ => false),

            _ => q
        };
    }

    /// <summary>Kiểm phạm vi cho **một** đơn (đường đọc chi tiết) — ném `0018` nếu ngoài phạm vi.</summary>
    private async Task KiemPhamViAsync(Order don)
    {
        var phamVi = await _phamVi.LayAsync();

        var ngoai = phamVi.PhamVi switch
        {
            eCommerce.Customers.PhamViDuLieu.Own =>
                phamVi.NguoiDungId is null || don.CreatorId != phamVi.NguoiDungId,
            eCommerce.Customers.PhamViDuLieu.Store =>
                phamVi.KhoCuaHangId is null || don.WarehouseId != phamVi.KhoCuaHangId,
            _ => false
        };

        if (ngoai)
        {
            throw new Volo.Abp.BusinessException(OrderErrorCodes.KhongTimThayDon)
                .WithData("orderId", don.Id)
                .WithData("phamVi", phamVi.PhamVi.ToString())
                .WithData("lyDo", "Đơn NGOÀI phạm vi dữ liệu của bạn.");
        }
    }

    private static OrderListItemDto ToListItem(Order don, int soDong)    {
        var dto = new OrderListItemDto();
        SaoChep(don, soDong, dto);
        return dto;
    }

    /// <summary>Phần chung của DS và chi tiết — **một** chỗ map nhãn (tránh hai bản lệch nhau).</summary>
    private static void SaoChep(Order don, int soDong, OrderListItemDto dto)
    {
        dto.Id = don.Id;
        dto.Number = don.Number;
        dto.ChannelCode = don.ChannelCode;
        dto.Type = don.Type;
        dto.Source = don.Source;
        dto.FulfillmentStatus = don.FulfillmentStatus;
        dto.PaymentStatus = don.PaymentStatus;
        dto.ReconciliationStatus = don.ReconciliationStatus;
        dto.ReturnStatus = don.ReturnStatus;
        dto.DerivedStatus = don.Status;
        dto.StatusLabel = OrderStateMachine.NhanHienThi(don.FulfillmentStatus, don.ReturnStatus, don.IsCancelled);
        dto.IsCancelled = don.IsCancelled;
        dto.GrandTotal = don.GrandTotal;
        dto.CodAmount = don.CodAmount;
        dto.LineCount = soDong;
        dto.WarehouseId = don.WarehouseId;
        dto.CarrierCode = don.CarrierCode;
        dto.TrackingNumber = don.TrackingNumber;
        dto.ReasonCode = don.ReasonCode;
        dto.CreationTime = don.CreationTime;
    }
}

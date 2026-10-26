using System.Reflection;
using eCommerce.Application.Contracts.Sales;
using eCommerce.Application.Sales;
using Microsoft.AspNetCore.Authorization;

namespace eCommerce.Tests.Domain;

/// <summary>
/// 🚨 **HỢP ĐỒNG QUYỀN CỦA API TRẠNG THÁI ĐƠN** (`SAL-003` §6.1/§9 — 24/10/2026).
///
/// 🔑 **VÌ SAO CẦN:** mỗi method là **một trục + một quyền**. Nếu ai đó đổi `[Authorize(StatusChange)]`
/// thành `[Authorize(Orders.Manage)]` (hoặc bỏ attribute), API vẫn **build xanh**, test nghiệp vụ vẫn xanh —
/// nhưng **người giao hàng có thể đối soát** hoặc **người chỉ xem có thể huỷ đơn**. 📌 Đây đúng loại lỗi
/// *"phân quyền sai mà không có triệu chứng"* mà `XC-004` TC-01/TC-02 sinh ra để chặn.
///
/// 🔒 Test khoá **cặp method ↔ permission** và **không có method ghi trục tài chính** (BR-013).
/// </summary>
public class OrderStatusApiHopDongTests
{
    public static TheoryData<string, string> CapQuyen => new()
    {
        { nameof(IOrderStatusAppService.ChangeFulfillmentStatusAsync), eCommerceConsts.Permissions.Orders.StatusChange },
        { nameof(IOrderStatusAppService.ChangeReconciliationStatusAsync), eCommerceConsts.Permissions.Orders.ReconcileManage },
        { nameof(IOrderStatusAppService.RequestReturnAsync), eCommerceConsts.Permissions.Orders.ReturnRequest },
        { nameof(IOrderStatusAppService.ApproveReturnAsync), eCommerceConsts.Permissions.Orders.ReturnApprove },
        { nameof(IOrderStatusAppService.CancelOrderAsync), eCommerceConsts.Permissions.Orders.Cancel },
        { nameof(IOrderStatusAppService.GetStatusHistoryAsync), eCommerceConsts.Permissions.Orders.HistoryView }
    };

    [Theory]
    [MemberData(nameof(CapQuyen))]
    public void Moi_method_PHAI_gan_DUNG_mot_permission(string tenMethod, string permission)
    {
        var method = typeof(OrderStatusAppService)
            .GetMethod(tenMethod, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(permission, attr!.Policy);
    }

    [Fact]
    public void Class_PHAI_co_Authorize_cap_CLASS()
    {
        // 🔒 Quy ước #7 / `XC-004` BR-001: thiếu `[Authorize]` cấp class ⇒ method nào quên attribute là
        // **công khai**. `BatBienPhanQuyenTests` quét toàn bộ app service; ca này nhắc riêng lớp mới.
        var attr = typeof(OrderStatusAppService).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void KHONG_co_method_ghi_truc_tai_chinh()
    {
        // 🔒 BR-013 / D11: trục tài chính CHỈ PaymentHub/IPN đã xác thực chữ ký ghi.
        var ten = typeof(IOrderStatusAppService).GetMethods().Select(x => x.Name).ToList();

        Assert.DoesNotContain("ChangePaymentStatusAsync", ten);
        Assert.DoesNotContain(ten, x => x.Contains("Payment", StringComparison.OrdinalIgnoreCase)
                                        && x.Contains("Change", StringComparison.OrdinalIgnoreCase));
    }
}

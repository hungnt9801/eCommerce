using System.Reflection;
using eCommerce.Application.Contracts.Pos;
using eCommerce.Application.Pos;
using Microsoft.AspNetCore.Authorization;

namespace eCommerce.Tests.Pos;

/// <summary>
/// 🚨 **HỢP ĐỒNG QUYỀN CỦA API POS** (`WP-06`).
///
/// 🔑 **VÌ SAO CẦN:** mỗi method là **một việc + một quyền**. Nếu ai đổi `[Authorize(Pos.Void)]` thành
/// `[Authorize(Pos.Sell)]` (hoặc bỏ attribute), API vẫn build xanh, test nghiệp vụ vẫn xanh — nhưng
/// **mọi thu ngân có thể HUỶ hoá đơn** (đảo doanh thu + đảo tồn kho) hoặc **tự kết ca** (chốt biên bản tiền).
/// 📌 Đây đúng loại lỗi *"phân quyền sai mà không có triệu chứng"* mà `XC-004` TC-01/TC-02 sinh ra để chặn.
/// 🛡️ Lưới an toàn thứ hai: `BatBienPhanQuyenTests.TC01` (mọi app service phải có `[Authorize]` cấp class).
/// </summary>
public class Wp06PosApiHopDongTests
{
    public static TheoryData<Type, string, string> CapQuyen => new()
    {
        { typeof(RetailBillAppService), nameof(RetailBillAppService.CreateAsync), eCommerceConsts.Permissions.Pos.Sell },
        { typeof(RetailBillAppService), nameof(RetailBillAppService.VoidAsync), eCommerceConsts.Permissions.Pos.Void },
        { typeof(CashSessionAppService), nameof(CashSessionAppService.OpenAsync), eCommerceConsts.Permissions.Pos.SessionManage },
        { typeof(CashSessionAppService), nameof(CashSessionAppService.CloseAsync), eCommerceConsts.Permissions.Pos.SessionManage },
        { typeof(RetailReturnAppService), nameof(RetailReturnAppService.CreateAsync), eCommerceConsts.Permissions.Pos.Return }
    };

    [Theory]
    [MemberData(nameof(CapQuyen))]
    public void Moi_method_GHI_PHAI_gan_DUNG_mot_permission(Type loai, string tenMethod, string permission)
    {
        var method = loai.GetMethod(tenMethod, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);

        // ⚠️ `GetCustomAttribute<AuthorizeAttribute>` (không phải `GetCustomAttributes`): policy của ABP nằm
        // trong `AuthorizeAttribute.Policy`; dùng `IAuthorizeData` sẽ bỏ sót ca attribute được kế thừa.
        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(permission, attr!.Policy);
    }

    [Theory]
    [InlineData(typeof(RetailBillAppService))]
    [InlineData(typeof(CashSessionAppService))]
    [InlineData(typeof(RetailReturnAppService))]
    public void Class_PHAI_co_Authorize_cap_CLASS(Type loai)
    {
        // 🔒 Quy ước #7 / `XC-004` BR-001: thiếu `[Authorize]` cấp class ⇒ method nào quên attribute là **công khai**.
        Assert.NotNull(loai.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void KHONG_co_duong_ghi_nao_bo_qua_domain_tien()
    {
        // 🔒 Bất biến: app service POS **không** được nhận `TotalPayment` từ client. Nếu DTO tạo hoá đơn có
        // trường đó thì một client sửa tổng tiền là xong (một dạng gian lận giá) — mà **không có lỗi nào**.
        var tenTruong = typeof(CreateRetailBillDto).GetProperties().Select(x => x.Name).ToList();

        Assert.DoesNotContain("TotalPayment", tenTruong);
        Assert.DoesNotContain("PaidAmount", tenTruong);
        Assert.DoesNotContain("DebtAmount", tenTruong);
    }
}

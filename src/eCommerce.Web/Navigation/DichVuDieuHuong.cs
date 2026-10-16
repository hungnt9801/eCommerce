using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace eCommerce.Web.Navigation;

/// <summary>
/// **MỘT CHỖ DUY NHẤT** trả lời *"menu nào hiện cho người đang đăng nhập"* (`WEB-001`).
///
/// 🔒 **VÌ SAO CHỈ MỘT CHỖ:** nếu mỗi view tự lọc (hoặc tệ hơn: mỗi view tự viết `if (IsGranted)`), thì
/// *"mục nào hiện cho ai"* có **N bản** và chúng **sẽ lệch nhau** — đúng họ lỗi *"hai nguồn sự thật"*
/// (`XC-003` F1/F2) đã trả giá nhiều lần. Một lớp ⇒ một quy tắc, một chỗ sửa, một chỗ test.
///
/// 📌 **Lõi quyết định** nằm ở <see cref="BoLocDieuHuong.Loc"/> (**hàm thuần, đã có unit test**);
/// lớp này **chỉ** làm phần I/O: lấy principal của người dùng hiện tại + gọi `IAuthorizationService`.
/// Đây đúng khuôn đã dùng ở `CAT-003` (`SinhSlug` thuần + app service làm I/O) và `SAL-005`
/// (`LuatWatermark` thuần + handler làm I/O).
/// </summary>
public class DichVuDieuHuong : ITransientDependency
{
    private readonly IAuthorizationService _kiemQuyen;
    private readonly ICurrentPrincipalAccessor _principal;

    public DichVuDieuHuong(IAuthorizationService kiemQuyen, ICurrentPrincipalAccessor principal)
    {
        _kiemQuyen = kiemQuyen;
        _principal = principal;
    }

    /// <summary>
    /// Menu cho **người dùng hiện tại** — đã lọc theo quyền, **giữ nguyên thứ tự** khai ở
    /// <see cref="MenuDieuHuong.TatCa"/>.
    /// ⚠️ Chưa đăng nhập ⇒ trả **rỗng** ngay (không cần gọi kho quyền): người chưa đăng nhập **không** được
    /// thấy menu quản trị. 📌 **Fail-closed** — xem <see cref="BoLocDieuHuong"/>.
    /// </summary>
    public IReadOnlyList<MucDieuHuong> LayMenuAsync()
    {
        if (_principal.Principal?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        return BoLocDieuHuong.Loc(MenuDieuHuong.TatCa, CoQuyen);
    }

    /// <summary>
    /// Gọi ABP **ĐỒNG BỘ** (`GetAwaiter().GetResult()`) có chủ ý — xem chú thích bên dưới.
    ///
    /// 🚨 **BẪY ĐÃ VẤP — NGUYÊN NHÂN GỐC CỦA "MENU BIẾN MẤT MÀ KHÔNG CÓ LỖI NÀO":**
    /// phiên bản đầu gọi `_kiemQuyen.AuthorizeAsync(null!, tenQuyen)` với suy nghĩ *"`null` = người dùng hiện tại"*.
    /// **SAI.** `Microsoft.AspNetCore.Authorization.IAuthorizationService.AuthorizeAsync(user, policyName)`
    /// kiểm quyền cho **principal được TRUYỀN VÀO**; `null` ⇒ **không có ai** ⇒ **mọi** policy **thất bại**.
    /// 📌 Hệ quả **im lặng tuyệt đối**: `BoLocDieuHuong.Loc` là **fail-closed** ⇒ ẩn **mọi** mục ⇒ menu **rỗng**;
    /// ViewComponent trả `HtmlString.Empty` (đúng thiết kế: "không có mục thì không render khung rỗng")
    /// ⇒ trang vẫn **`200`**, log **`0` lỗi**, và **menu biến mất**. Phải **in HTML thật** mới thấy.
    /// 🔑 Đây là *cùng một cái bẫy* mà chính tôi đã ghi trong `BoLocDieuHuong` (*"fail-closed ⇒ ẩn nhầm thì
    /// chỉ là không thấy"*) — và nó **che** một lỗi lập trình thành một **triệu chứng vô hại**.
    ///
        /// 🔒 **Cách sửa (đúng):** lấy **principal thật** qua `ICurrentPrincipalAccessor` của ABP
    /// (`Volo.Abp.Security.Claims`) — abstraction **đã được ABP đăng ký**, không cần `HttpContext`.
    /// ⚠️ **Hai lần vấp khi sửa (ghi lại vì cả hai đều là bài học):**
    ///   • `ICurrentUser` **KHÔNG** có thuộc tính `User` (chỉ có `Id`/`UserName`/`IsAuthenticated`...) ⇒ build đỏ;
    ///   • **Vì sao KHÔNG dùng `IHttpContextAccessor`:** nó kéo phụ thuộc HTTP vào lớp này và **trả `null`**
    ///     ở ngữ cảnh không có request (worker/test) — lại đúng một cái bẫy im lặng nữa.
    ///
    /// ⚠️ **Vì sao được phép gọi ĐỒNG BỘ ở đây:** đây là đường **RENDER** (không có `await` để nhường luồng),
    /// và cổng vào là **một** lần/1 mục cho **cả** menu (≤ 5 mục) với permission **đã cache trong tiến trình**.
    /// 📌 **Bài học đã trả giá (`CAT-003` §16.4):** nhét `GetAwaiter().GetResult()` vào **đường async**
    /// (như `ProductAppService`) là **chặn luồng** và nhiều provider DB **cấm** gọi đồng bộ — ở đó phải `await`.
    /// Ở **view** thì ngược lại. ⚠️ Nếu menu phình to (hàng chục mục) ⇒ **phải** chuyển ViewComponent sang
    /// `InvokeAsync` — ghi lại đây để người sau biết **điều kiện** của quyết định này.
    /// </summary>
    private bool CoQuyen(string tenQuyen)
    {
        var principal = _principal.Principal;

        if (principal is null)
        {
            // Không lấy được principal ⇒ **KHÔNG** kiểm được ⇒ fail-closed (ẩn). Xem `BoLocDieuHuong`.
            return false;
        }

        // `IAuthorizationService` của ABP nhận `policyName` = TÊN QUYỀN (ABP tự dựng policy theo tên quyền).
        var ketQua = _kiemQuyen.AuthorizeAsync(principal, tenQuyen).GetAwaiter().GetResult();

        return ketQua.Succeeded;
    }
}

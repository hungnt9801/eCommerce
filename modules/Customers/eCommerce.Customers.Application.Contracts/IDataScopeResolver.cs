namespace eCommerce.Customers;

/// <summary>
/// 🚨 **CỔNG HỢP ĐỒNG: PHẠM VI DỮ LIỆU CỦA NGƯỜI ĐANG ĐĂNG NHẬP** — `WP-07`
/// (`07 §WP-07`: *"Áp `data_scope` vào `Orders`/`RetailBills` (**lọc server-side, không tin client**)"*).
///
/// 🔑 **VÌ SAO LÀ INTERFACE Ở `Application.Contracts` (không phải một hàm trong app service):**
/// hai miền cần nó là **`Sales`** (DS đơn) và **`Pos`** (DS hoá đơn bán lẻ) — hai **module** khác nhau.
/// Nếu mỗi nơi tự đọc bảng `SetRoleDataScopes` thì:
/// ① có **hai định nghĩa** của *"phạm vi của tôi là gì"* (và chúng sẽ lệch — `XC-003` F2);
/// ② module `Sales`/`Pos` phải tham chiếu **entity** khách ⇒ vi phạm quy ước *"module sở hữu entity của mình"*.
/// 📌 Hợp đồng nằm ở đây, hiện thực ở `Customers.Application` (`DataScopeResolver`) — cùng khuôn
/// `ICustomerDirectory` (`ADR-12`).
///
/// 🔒 **CHỈ ĐỌC** — không có `Set*`: phạm vi do người có quyền cấu hình ở màn nhân viên, **không** do
/// đường đọc tự đổi.
/// </summary>
public interface IDataScopeResolver
{
    /// <summary>
    /// Phạm vi **hiệu lực** của người gọi. 📌 **KHÔNG NÉM** khi không xác định được (job nền, chưa cấu hình):
    /// trả `All` + lý do — vì *"không xác định"* không được biến thành *"không thấy gì"* (job/báo cáo sẽ
    /// **im lặng ra rỗng**, đúng loại lỗi tệ nhất). Ghi log ở tầng hiện thực khi rơi vào nhánh đó.
    /// </summary>
    Task<PhamViHienTai> LayAsync();
}

/// <summary>
/// Kết quả **đã chuẩn hoá** để tầng gọi dựng bộ lọc EF (mỗi miền biết cột của mình, hợp đồng **không** biết).
/// </summary>
/// <param name="PhamVi">Phạm vi hiệu lực (đã gộp theo luật "rộng nhất" — xem `D-46`).</param>
/// <param name="NguoiDungId">`AbpUsers.Id` của người gọi — dùng cho `Own` (`CreatorId == NguoiDungId`).</param>
/// <param name="NhanVienId">`SetStaffs.Id` tương ứng (nếu có) — 📌 để báo lỗi/ghi log *"nhân viên nào"*.</param>
/// <param name="KhoCuaHangId">Kho của cửa hàng mà nhân viên thuộc — dùng cho `Store`. `null` = chưa gắn kho.</param>
/// <param name="LyDo">Vì sao ra phạm vi này (ghi log/audit; 📌 giúp trả lời *"sao tôi không thấy đơn"*).</param>
public sealed record PhamViHienTai(
    PhamViDuLieu PhamVi,
    Guid? NguoiDungId,
    Guid? NhanVienId,
    Guid? KhoCuaHangId,
    string LyDo);

/// <summary>
/// 🚨 **CỔNG GHI SỔ ĐIỂM/CÔNG NỢ CỦA KHÁCH** — `WP-07` (mở lại nợ #6 của `handover-n7` §5).
///
/// **BỐI CẢNH (đo được ở `N7`):** POS **đã** ghi `PointsEarned/PointsUsed/DebtAmount` **trên hoá đơn**,
/// nhưng **chưa** cộng/trừ vào `Customer` — vì vậy đừng tin *"khách đã tích điểm"* (`handover-n7` §5 #6).
/// `WP-07` nối đúng một chỗ: sau khi chứng từ nguồn（hoá đơn/phiếu trả）đã ghi, gọi cổng này **trong cùng
/// transaction**.
///
/// 🔒 **VÌ SAO LÀ CỔNG (không cho POS `new Customer(...)`):**
/// <list type="number">
///   <item>module `Pos` **không** được đụng entity `Customer` (quy ước *"module sở hữu entity của mình"*);
///   <item>luật điểm/công nợ (không âm, không vượt hạn mức) phải nằm ở **một** chỗ (`NghiepVuKhach` +
///     `Customer.GhiNhanMuaHang`) — hai bản sao là hai nguồn sự thật;
///   <item>⚠️ **KHÔNG ghi sổ ở FE** (`handover-n7` §6): FE không giữ được transaction với chứng từ, nên
///     mất mạng giữa hai lời gọi là **hoá đơn có, điểm không**.
/// </list>
/// </summary>
public interface ICustomerLedger
{
    /// <summary>
    /// **GHI NHẬN BÁN HÀNG** — cộng tiền tích luỹ + điểm tích − điểm tiêu + nợ; xếp lại cấp khách.
    /// 📌 Idempotent **theo chứng từ** là trách nhiệm của **nơi gọi** (hoá đơn đã idempotent theo
    /// `ClientBillId` — `D-42`); cổng này **không** tự chống trùng để không tạo khoá thứ hai.
    /// </summary>
    /// <param name="customerId">`null` = khách vãng lai ⇒ **không làm gì** (và trả `false`).</param>
    /// <exception cref="Volo.Abp.BusinessException">`Customers:0018` (không đủ điểm) · `0019` (vượt hạn mức nợ)
    /// · `0005` (khách bị chặn). 📌 Ném ⇒ **UoW rollback** ⇒ hoá đơn không được lưu (đúng ý).</exception>
    Task<bool> GhiNhanBanHangAsync(Guid? customerId, decimal tienHang, decimal diemTich, decimal diemSuDung,
        decimal noTang, string nguon);

    /// <summary>
    /// **ĐẢO MỘT LẦN BÁN** (huỷ hoá đơn) — trừ tiền tích luỹ, đảo điểm, giảm nợ. 📌 Kẹp ở `0` (xem
    /// `Customer.DaoGhiNhanMuaHang`): huỷ là đường **sửa sai**, không được làm hỏng thêm.
    /// </summary>
    Task<bool> DaoBanHangAsync(Guid? customerId, decimal tienHang, decimal diemTich, decimal diemSuDung,
        decimal noGiam, string nguon);

    /// <summary>
    /// **GHI NHẬN TRẢ HÀNG** — trừ tiền tích luỹ theo giá trị hàng trả, trừ lại điểm đã tích của phần trả,
    /// giảm nợ của khách đúng phần `DebtReductionAmount` đã giảm trên hoá đơn.
    /// 🚨 **Ba số này phải khớp chứng từ**: trả hàng làm giảm **cả** doanh thu (`AccumulatedAmount`) **và**
    /// công nợ — nếu chỉ giảm một trong hai thì `DoiChieuSoDuDto` sẽ **không khớp** và đó là cảnh báo đúng.
    /// </summary>
    Task<bool> GhiNhanTraHangAsync(Guid? customerId, decimal tienHangTra, decimal diemThuHoi,
        decimal noGiam, string nguon);
}

using Volo.Abp;

namespace eCommerce.Catalog;

/// <summary>
/// **LUẬT THUẦN CỦA CÂY CHA–CON SẢN PHẨM** (WP-03 nửa sau, `03 §3.1`, D-07).
///
/// 🚨 VÌ SAO LÀ HÀM THUẦN (không phải method trên `Product`): câu hỏi *"gán cha này có tạo chu trình không"*
/// cần **cả chuỗi tổ tiên**, mà `Product` chỉ giữ `ParentId` của chính nó — đọc cả cây là việc của tầng
/// app service (repository). Tách luật ra khỏi nguồn dữ liệu để **test được bằng từ điển trong bộ nhớ**
/// (`ProductNhanhModelNuaCuoiTests`), đúng khuôn `GiaCoSoResolver`/`LuatDoiSoatThanhToan`.
///
/// ⚠️ **VÌ SAO PHẢI CHẶN:** `A → B → A` (hoặc `A → A` nhiều mức) làm mọi phép duyệt cây **lặp vô hạn**;
/// biểu hiện thật là **treo request** hoặc **tràn stack** ở đúng chỗ ít ai nghĩ tới (dựng cây danh mục,
/// tính giá theo nhóm cha, xuất Excel). `Product.DatCha` chỉ chặn được ca **một mức** (`0043`).
/// </summary>
public static class CaySanPham
{
    /// <summary>
    /// Chặn sâu tối đa của chuỗi cha — phòng dữ liệu đã có chu trình sẵn (import tay/SQL): vòng lặp dừng
    /// bằng **lỗi nghiệp vụ** thay vì treo. 64 mức là rộng hơn mọi cây thật (Nhanh dùng 2–3 mức).
    /// </summary>
    public const int DoSauToiDa = 64;

    /// <summary>
    /// Kiểm tra gán <paramref name="parentId"/> cho <paramref name="productId"/> có tạo chu trình không.
    ///
    /// 🔒 Đi **ngược lên** từ cha: nếu gặp lại chính sản phẩm (hoặc một nút đã gặp — dữ liệu cũ đã có vòng)
    /// ⇒ **NÉM** <see cref="eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon"/>.
    /// </summary>
    /// <param name="productId">Sản phẩm đang được gán cha.</param>
    /// <param name="parentId">Cha dự kiến (`null` = bỏ cha, luôn hợp lệ).</param>
    /// <param name="layCha">Hàm lấy `ParentId` hiện tại của một sản phẩm (`null` nếu không có/không tồn tại).</param>
    public static void KiemTraKhongChuTrinh(Guid productId, Guid? parentId, Func<Guid, Guid?> layCha)
    {
        if (parentId is null)
        {
            return;   // Bỏ cha = sản phẩm độc lập ⇒ không thể tạo chu trình.
        }

        // 🔒 Ca MỘT MỨC giữ **mã riêng của domain** (`0043`, `SanPhamChaTrungChinhNo`) thay vì nuốt vào `0048`:
        // client đã đối soát theo `0043` cho ca này, và `Product.DatCha` cũng ném đúng mã đó. Dùng hai mã cho
        // cùng một hành vi là đúng họ lỗi *"hai nguồn sự thật"* mà sổ đăng ký mã lỗi ngăn.
        if (parentId == productId)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SanPhamChaTrungChinhNo)
                .WithData("productId", productId);
        }

        var daGap = new HashSet<Guid>();
        Guid? hienTai = parentId;

        for (var buoc = 0; hienTai is { } id; buoc++)
        {
            if (id == productId)
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon)
                    .WithData("productId", productId)
                    .WithData("parentId", parentId)
                    .WithData("lyDo", "Cha dự kiến nằm trong cây con của chính sản phẩm này.");
            }

            if (!daGap.Add(id))
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon)
                    .WithData("productId", productId)
                    .WithData("parentId", parentId)
                    .WithData("nutLap", id)
                    .WithData("lyDo", "Dữ liệu HiỆN CÓ đã chứa chu trình ở phía trên — phải sửa dữ liệu trước.");
            }

            if (buoc >= DoSauToiDa)
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon)
                    .WithData("productId", productId)
                    .WithData("parentId", parentId)
                    .WithData("doSauToiDa", DoSauToiDa)
                    .WithData("lyDo", "Chuỗi cha dài quá giới hạn ⇒ nhiều khả năng có chu trình trong dữ liệu.");
            }

            hienTai = layCha(id);
        }
    }
}

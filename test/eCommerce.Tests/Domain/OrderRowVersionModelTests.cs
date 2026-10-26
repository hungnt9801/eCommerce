using eCommerce.EntityFrameworkCore;
using eCommerce.Orders;
using Microsoft.EntityFrameworkCore;

namespace eCommerce.Tests.Domain;

/// <summary>
/// 🚨 **`RowVersion` PHẢI LÀ CONCURRENCY TOKEN** (`SAL-003` D5/BR-010 — 24/10/2026).
///
/// 🔑 **VÌ SAO CẦN TEST NÀY:** khai `public long RowVersion` mà **quên** `.IsConcurrencyToken()` thì EF
/// **không** đưa nó vào `WHERE` ⇒ hai request cùng sửa một đơn: request sau **ghi đè** request trước
/// **im lặng** (lost update), đúng lỗi `BR-010` sinh ra để chặn. Build vẫn xanh, test nghiệp vụ vẫn xanh.
///
/// 📌 Test đọc **model của EF** (không cần DB): nếu ai xoá cấu hình, ca này đỏ ngay.
/// </summary>
public class OrderRowVersionModelTests
{
    [Fact]
    public void RowVersion_PHAI_la_concurrency_token()
    {
        var tuyChon = new DbContextOptionsBuilder<eCommerceDbContext>()
            // 📌 Chuỗi kết nối KHÔNG được mở — chỉ dựng model. Dùng host giả để không phụ thuộc DB.
            .UseNpgsql("Host=localhost;Port=5432;Database=khong_dung;Username=khong_dung;Password=khong_dung")
            .Options;

        using var db = new eCommerceDbContext(tuyChon);

        var kieu = db.Model.FindEntityType(typeof(Order));

        Assert.NotNull(kieu);

        var thuocTinh = kieu!.FindProperty(nameof(Order.RowVersion));

        Assert.NotNull(thuocTinh);
        Assert.True(
            thuocTinh!.IsConcurrencyToken,
            "🚨 `Order.RowVersion` KHÔNG còn là concurrency token ⇒ hai request cùng sửa một đơn sẽ " +
            "ghi đè nhau IM LẶNG (lost update) — đúng lỗi `BR-010` cần chặn.");
    }
}

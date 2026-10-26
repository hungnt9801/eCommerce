using System.Reflection;
using eCommerce.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **OUTBOX/INBOX PHẢI THẬT SỰ ĐƯỢC BẬT** (`XC-002` D1/D2, P0 #3 — 24/10/2026).
///
/// 🔑 **VÌ SAO CẦN TEST NÀY:** ABP bật outbox bằng **cấu hình ngầm**. Nếu ai đó xoá `IHasEventOutbox` khỏi
/// `eCommerceDbContext` (hoặc xoá `DbSet`), hệ thống **vẫn build, vẫn chạy, test nghiệp vụ vẫn xanh** — chỉ
/// khác là `AddToOutboxAsync` trả **false** và ABP **rơi im lặng** về publish trực tiếp. 📌 Đúng loại lỗi
/// *"có trên giấy, không có trong DB"* mà `S10` đã trả giá với 4/15 metric.
///
/// 🔒 Test này khoá **hình dạng tối thiểu** của cấu hình: 2 interface + 2 `DbSet` + 2 hàm model-builder.
/// ⚠️ Nó **không** chứng minh relay chạy (việc đó đo bằng `AbpEventOutbox` rỗng + `AbpEventInbox` có dòng
/// trong lần chạy probe; xem `XC-002` §16).
/// </summary>
public class OutboxInboxBatBuocTests
{
    [Fact]
    public void DbContext_PHAI_khai_IHasEventOutbox()
    {
        Assert.True(
            typeof(IHasEventOutbox).IsAssignableFrom(typeof(eCommerceDbContext)),
            "🚨 `eCommerceDbContext` KHÔNG còn implement `IHasEventOutbox` ⇒ ABP KHÔNG ghi outbox " +
            "(AddToOutboxAsync trả false) ⇒ mất event khi process chết giữa lúc publish.");
    }

    [Fact]
    public void DbContext_PHAI_khai_IHasEventInbox()
    {
        Assert.True(
            typeof(IHasEventInbox).IsAssignableFrom(typeof(eCommerceDbContext)),
            "🚨 `eCommerceDbContext` KHÔNG còn implement `IHasEventInbox` ⇒ consumer không ghi nhận sự kiện " +
            "đến ⇒ không có chống trùng ở phía nhận.");
    }

    [Theory]
    [InlineData(nameof(eCommerceDbContext.OutgoingEvents))]
    [InlineData(nameof(eCommerceDbContext.IncomingEvents))]
    public void DbContext_PHAI_co_DbSet_tuong_ung(string ten)
    {
        // 🚨 Quy ước #5: thiếu `DbSet` ⇒ `IRepository<T>` không được đăng ký ⇒ outbox/inbox **không ghi được**
        // dù interface đã khai (đúng lỗi Phase 0: "build xanh, migration xanh, chạy mới nổ").
        var thuocTinh = typeof(eCommerceDbContext).GetProperty(ten, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(thuocTinh);

        // 📌 `typeof(DbSet<>).IsAssignableFrom(typeof(DbSet<X>))` trả **false** (generic definition ≠ constructed)
        // ⇒ phải so **generic definition**. Đây đúng là bẫy của chính phép kiểm.
        var kieu = thuocTinh!.PropertyType;
        Assert.True(kieu.IsGenericType && kieu.GetGenericTypeDefinition() == typeof(DbSet<>),
            $"`{ten}` phải là `DbSet<T>` — hiện là `{kieu.Name}`.");
    }

    [Fact]
    public void Module_EF_PHAI_bat_ca_hai_chieu()
    {
        // 📌 Kiểm **văn bản** của module: đây là cấu hình DI (lambda), không có API phản chiếu nào đọc được
        // "đã Configure hay chưa". Một phép quét văn bản là **thô**, nhưng nó bắt đúng ca nguy hiểm nhất:
        // ai đó xoá một trong hai dòng `UseDbContext<eCommerceDbContext>()` ⇒ outbox **hoặc** inbox tắt im lặng.
        var duongDan = TimTepModule();
        var noiDung = File.ReadAllText(duongDan);

        Assert.Equal(2, Dem(noiDung, "UseDbContext<eCommerceDbContext>()"));
    }

    private static int Dem(string noiDung, string mau)
    {
        var dem = 0;
        var viTri = 0;

        while ((viTri = noiDung.IndexOf(mau, viTri, StringComparison.Ordinal)) >= 0)
        {
            dem++;
            viTri += mau.Length;
        }

        return dem;
    }

    private static string TimTepModule()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            var ungVien = Path.Combine(thuMuc.FullName, "src", "eCommerce.EntityFrameworkCore", "eCommerceEntityFrameworkCoreModule.cs");

            if (File.Exists(ungVien))
            {
                return ungVien;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new FileNotFoundException(
            "KHÔNG TÌM THẤY `eCommerceEntityFrameworkCoreModule.cs` ⇒ phép kiểm outbox/inbox VO NGHIA " +
            $"(bắt đầu từ `{AppContext.BaseDirectory}`).");
    }
}

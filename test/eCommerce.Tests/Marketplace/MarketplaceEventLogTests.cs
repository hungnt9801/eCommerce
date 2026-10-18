using System.Reflection;
using eCommerce.Marketplace;
using eCommerce.Orders;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Tests.Marketplace;

/// <summary>
/// 🚨 **NHẬT KÝ SỰ KIỆN ĐƠN SÀN** (`SAL-005` **D7**, `S6`) — bất biến ở tầng **entity + model**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **BA CÂU HỎI Ở ĐÂY (mỗi câu ứng với một cách "hỏng âm thầm" đã được ghi trong dự án):**
/// <list type="number">
///   <item>**APPEND-ONLY có thật không?** — một nhật ký **sửa được** thì **không phải nhật ký**: chỉ cần một
///     phương thức `SuaLyDo()` là dấu vết lịch sử **thành lời khai**. 🔒 Kiểm bằng **reflection** (không có
///     phương thức công khai nào trên entity), đúng khuôn `TC-20` của `SAL-005` §14.1;</item>
///   <item>**Guard phiên bản có ném không?** — `SourceVersion ≤ 0` là dấu hiệu **map sai** (map số dòng hoặc
///     `PlacedAtUtc` vào watermark). ⚠️ Nếu entity **im lặng nhận** thì nhánh C/D so với **mốc sai** và DLQ giả;</item>
///   <item>**Model EF có đúng thứ sẽ tạo ra ở DB không?** — 📌 đọc `GenerateCreateScript()` thay vì tin vào
///     `OnModelCreating`: 🔑 cái được tạo ở DB mới là thứ **thực sự** bảo vệ dữ liệu.</item>
/// </list>
///
/// 📌 Bất biến **sống trong PostgreSQL** (`NULLS NOT DISTINCT`, `CHECK`) được kiểm riêng ở
/// `test/eCommerce.IntegrationTests/MarketplaceEventLogTests.cs` — unit test **không** nhìn thấy chúng.
/// </summary>
public class MarketplaceEventLogTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static MarketplaceEventLog Tao(
        long? phienBan = 1760313600,
        string channelCode = "shopee",
        string externalOrderId = "SN-1",
        string payloadHash = Hash) => new(
            Guid.NewGuid(),
            channelCode,
            externalOrderId,
            payloadHash,
            MarketplaceIntakeOutcome.Applied,
            DateTime.UtcNow,
            phienBan);

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① GUARD `SourceVersion` — bắt MAP SAI ngay tại chỗ, không để nó thành "lịch sử"
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void Phien_ban_KHONG_duong__NEM(long phienBan)
    {
        // 📌 `0` là mốc THẬT (1970-01-01) nhưng **không thể** là `update_time` của một đơn sàn thật ⇒ chỉ xuất
        // hiện khi map sai (`default(long)`, số dòng, mốc rỗng). 🚨 Nếu nhận im lặng: watermark = 0 ⇒ **mọi**
        // bản cập nhật thật đều "mới hơn" ⇒ nhánh `ApDung` **luôn** chạy ⇒ bất biến watermark **mất tác dụng**.
        Assert.Throws<ArgumentOutOfRangeException>(() => Tao(phienBan));
    }

    [Fact]
    public void Phien_ban_NULL__duoc_phep__va_la_duong_duy_nhat_de_biet_thieu_watermark()
    {
        // 🔑 `null` KHÁC `0`: `null` = **không có** watermark (sự kiện từ connector chưa trả `update_time`).
        // 📌 Phân biệt được hai thứ này là điều kiện để **không** coi dữ liệu cũ là *"đã có watermark"*
        // rồi **chặn** mọi cập nhật.
        var ban = Tao(phienBan: null);

        Assert.Null(ban.SourceVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Channel_hoac_ma_don_san_RONG__NEM(string rong)
    {
        // 🔒 Một dòng nhật ký không có kênh/mã đơn sàn thì **không tra cứu được** ⇒ mất giá trị đúng lúc cần đọc.
        Assert.Throws<ArgumentException>(() => Tao(channelCode: rong));
        Assert.Throws<ArgumentException>(() => Tao(externalOrderId: rong));
        Assert.Throws<ArgumentException>(() => Tao(payloadHash: rong));
    }

    [Fact]
    public void Truong_dai_bi_CAT_chu_KHONG_nem()
    {
        // 📌 `FailureMessage` là chỗ **PII/payload** dễ lọt nhất (message của serializer chứa cả object).
        // 🔒 Cắt là **cố ý** (`XC-002` BR-021) — nhưng phải cắt **đúng** giới hạn, không cắt cụt tuỳ tiện.
        var ban = new MarketplaceEventLog(
            Guid.NewGuid(),
            "shopee",
            "SN-1",
            Hash,
            MarketplaceIntakeOutcome.Failed,
            DateTime.UtcNow,
            1760313600,
            failureCode: new string('c', 200),
            failureMessage: new string('m', 5000),
            correlationId: new string('k', 200));

        Assert.Equal(MarketplaceEventLog.DoDaiFailureMessage, ban.FailureMessage!.Length);
        Assert.Equal(MarketplaceEventLog.DoDaiFailureCode, ban.FailureCode!.Length);
        Assert.Equal(MarketplaceEventLog.DoDaiCorrelationId, ban.CorrelationId!.Length);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② APPEND-ONLY — kiểm bằng REFLECTION (`TC-20`)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void APPEND_ONLY__entity_KHONG_co_phuong_thuc_cong_khai_nao_lam_thay_doi_du_lieu()
    {
        var kieu = typeof(MarketplaceEventLog);

        // 📌 `DeclaredOnly`: chỉ xét phần **của entity này**; base class của ABP có sẵn vài phương thức
        // (`SetConcurrencyStamp`…) — chúng **không** mở đường sửa nội dung nghiệp vụ của một dòng nhật ký.
        var phuongThuc = kieu.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)   // bỏ getter/setter
            .Select(m => m.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(phuongThuc.Count == 0,
            "🚨 `MarketplaceEventLog` có phương thức công khai ⇒ nhật ký SỬA ĐƯỢC thì không phải nhật ký " +
            "(dấu vết lịch sử thành lời khai). Phương thức vi phạm: " + string.Join(", ", phuongThuc));

        var setterCongKhai = kieu.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(setterCongKhai.Count == 0,
            "🚨 `MarketplaceEventLog` có property với setter CÔNG KHAI ⇒ sửa được dấu vết từ bên ngoài. " +
            "Thuộc tính vi phạm: " + string.Join(", ", setterCongKhai));
    }

    [Fact]
    public void Ket_cuc_phai_giu_NGUYEN_gia_tri_so__vi_du_lieu_cu_da_ghi_theo_so()
    {
        // 🚨 Cột `Outcome` là `int` trong DB. Đổi/đảo giá trị số sẽ làm **dữ liệu cũ** đọc ra SAI NGHĨA
        // (ví dụ `Late` cũ thành `Conflict` mới) — và **không** có lỗi nào. 📌 Khoá lại hợp đồng bằng test.
        Assert.Equal(0, (int)MarketplaceIntakeOutcome.Applied);
        Assert.Equal(1, (int)MarketplaceIntakeOutcome.Duplicate);
        Assert.Equal(2, (int)MarketplaceIntakeOutcome.Late);
        Assert.Equal(3, (int)MarketplaceIntakeOutcome.Conflict);
        Assert.Equal(4, (int)MarketplaceIntakeOutcome.GapSuspected);
        Assert.Equal(5, (int)MarketplaceIntakeOutcome.Failed);
        Assert.Equal(6, (int)MarketplaceIntakeOutcome.ThieuWatermark);
        Assert.Equal(7, Enum.GetValues<MarketplaceIntakeOutcome>().Length);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ MODEL EF — thứ **thực sự** được tạo ở DB
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Model_phai_tao_bang_2_CHECK_va_index_unique_dung_ten()
    {
        // ⚠️ Dựng model KHÔNG cần kết nối (chỉ để provider biết phương ngữ).
        var options = new DbContextOptionsBuilder<EntityFrameworkCore.eCommerceDbContext>()
            .UseNpgsql("Host=khong-ket-noi;Database=khong-ket-noi")
            .Options;

        using var db = new EntityFrameworkCore.eCommerceDbContext(options);

        var sql = db.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE \"SalMarketplaceEventLogs\"", sql);
        Assert.Contains("CK_SalMktEventLogs_SourceVersion", sql);
        Assert.Contains("CK_SalMktEventLogs_PayloadHash_Hex", sql);

        // 🔑 Tên index **đích danh** (không phải tên EF tự sinh): EF sinh
        // `IX_SalMarketplaceEventLogs_…_SourceVersion_PayloadHash` = **94 ký tự** > 63 ⇒ PostgreSQL **CẮT**
        // ⇒ `PostgreSqlSchemaTests` (đối chiếu đích danh) không tìm thấy ⇒ mất lưới an toàn của
        // `NULLS NOT DISTINCT` **mà không ai biết**.
        Assert.Contains("IX_SalMktEventLogs_TenantId_Channel_Order_Ver_Hash_Outcome", sql);
        Assert.True("IX_SalMktEventLogs_TenantId_Channel_Order_Ver_Hash_Outcome".Length <= 63,
            "Tên index vượt 63 ký tự ⇒ PostgreSQL sẽ CẮT tên ⇒ test đối chiếu đích danh không tìm thấy.");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ④ ĐĂNG KÝ DI — thiếu marker là "chết âm thầm" (đúng họ lỗi Phase 0)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bo_ghi_nhat_ky_phai_dang_ky_duoc_theo_QUY_UOC_cua_ABP()
    {
        var kieu = typeof(MarketplaceEventLogWriter);

        var coMarker = kieu.GetInterfaces().Any(i =>
            i == typeof(ITransientDependency)
            || i == typeof(IScopedDependency)
            || i == typeof(ISingletonDependency));

        Assert.True(coMarker,
            "🚨 `MarketplaceEventLogWriter` THIẾU marker đăng ký DI ⇒ ABP không đăng ký nó ⇒ handler không " +
            "resolve được ⇒ **mọi sự kiện đơn sàn mất dấu vết** (lỗi chỉ lộ khi chạy thật).");
    }

    [Fact]
    public void Handler_PHAI_nhan_bo_ghi_nhat_ky__de_khong_the_quen_ghi_dau_vet()
    {
        // 🔒 Nếu bỏ tham số này đi thì handler **vẫn biên dịch** và mọi test cũ vẫn xanh — nhưng bảng nhật ký
        // **rỗng vĩnh viễn**, và đó chính là nợ §17.6 #3 quay lại. 📌 Khoá bằng reflection.
        var thamSo = typeof(MarketplaceOrderIntakeHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.Contains(typeof(MarketplaceEventLogWriter), thamSo);
    }
}

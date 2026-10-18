using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🚨 **NHẬT KÝ SỰ KIỆN ĐƠN SÀN — BẤT BIẾN SỐNG TRONG POSTGRESQL** (`SAL-005` **D7**, `S6`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI LÀ INTEGRATION TEST (không phải unit):** ba bất biến của bảng
/// `SalMarketplaceEventLogs` **chỉ** tồn tại trong PostgreSQL — unit test không nhìn thấy chúng:
/// <list type="number">
///   <item>**`NULLS NOT DISTINCT`** là thuộc tính **EF Core KHÔNG mô hình hoá** ⇒ `CreateIndex(unique: true)`
///     sinh ra index **KHÔNG** có mệnh đề đó, và ở chế độ `TenantId = NULL` (hiện nay) PostgreSQL coi
///     **mỗi `NULL` là KHÁC nhau** ⇒ index **mất tác dụng IM LẶNG** ⇒ **một sự kiện ghi được NHIỀU dòng**.
///     🔑 Hệ quả **không** dừng ở chuyện trùng dòng: mọi con số vận hành đọc từ bảng này
///     (*"sàn gửi lại bao nhiêu lần"*, *"có dual-path không"*, *"bao nhiêu xung đột"*) sẽ **sai dần**
///     — và bảng **chỉ tăng** nên sai số **chỉ lớn lên**, không có lỗi nào để lần ra;</item>
///   <item>**`CHECK` `SourceVersion > 0`** — epoch giây **luôn dương**; giá trị `0`/âm nghĩa là **map sai**
///     (ví dụ map số dòng hoặc `PlacedAtUtc` vào watermark). Entity đã chặn, nhưng **đường ghi tắt**
///     (script sửa dữ liệu, phát lại DLQ viết tay) thì **không** — hàng rào cuối phải là DB;</item>
///   <item>**`CHECK` `PayloadHash ~ '^[0-9a-f]{64}$'`** — hash HOA hoặc base64 **trông vẫn như hash**, nhưng
///     làm nhánh **C** (trùng) và **D** (xung đột) **so sai** ⇒ cùng một sự kiện bị coi là **xung đột**
///     ⇒ **DLQ giả** (`SAL-005` §15.3 giả định 2).</item>
/// </list>
///
/// 🔒 **Kiểm CẢ HAI CHIỀU ở mọi ca:** nếu chỉ kiểm "chặn được" thì một index **quá chặt** (chặn cả dữ liệu
/// hợp lệ — ví dụ chặn hai phiên bản khác nhau của cùng một đơn) cũng XANH, trong khi hệ quả thật là
/// **không ghi được cập nhật thứ hai nào** ⇒ mất cập nhật của sàn.
///
/// 📌 Mọi câu lệnh dùng **tham số**; mỗi ca **tự dọn** trong `finally` (nếu không, `kiem-db.ps1` nhóm ① sẽ đỏ
/// ở lượt sau — `tools/README-probe.md` §D).
/// </summary>
[Collection("postgres")]
public class MarketplaceEventLogTests
{
    /// <summary>Tiền tố dành riêng cho test — dọn theo tiền tố chứ **không** theo `Id` (để không sót ca chèn hỏng).</summary>
    private const string TienTo = "MKEV-TEST-";

    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly PostgreSqlSchemaFixture _fixture;

    public MarketplaceEventLogTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Chèn **thẳng** vào bảng (không qua entity) — 🔑 chính đường **ghi tắt** mà các `CHECK`/index phải chặn.
    /// </summary>
    private async Task ChenAsync(
        Guid? tenantId,
        string externalOrderId,
        long? sourceVersion,
        string payloadHash,
        int outcome = 0)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "SalMarketplaceEventLogs"
                ("Id","TenantId","ChannelCode","ChannelShopId","ExternalOrderId","SourceVersion","PayloadHash",
                 "Outcome","EventKind","MarketplaceStatusRaw","IsFromWebhook","SourceUpdatedAtUtc","ReceivedAtUtc",
                 "ExtraProperties","ConcurrencyStamp","CreationTime")
            values ($1, $2, 'shopee', null, $3, $4, $5, $6, 'Created', 'READY_TO_SHIP', false, now(), now(),
                    '{}', $7, now())
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(externalOrderId);
        cmd.Parameters.AddWithValue((object?)sourceVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue(payloadHash);
        cmd.Parameters.AddWithValue(outcome);
        cmd.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> DemAsync(string externalOrderId)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            """select count(*) from "SalMarketplaceEventLogs" where "ExternalOrderId" = $1""");

        cmd.Parameters.AddWithValue(externalOrderId);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task DonDepAsync()
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            """delete from "SalMarketplaceEventLogs" where "ExternalOrderId" like $1""");

        cmd.Parameters.AddWithValue(TienTo + "%");

        await cmd.ExecuteNonQueryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① `NULLS NOT DISTINCT` — MỘT SỰ KIỆN ⇒ MỘT DÒNG, kể cả khi `TenantId = NULL`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Cho_phep_KHAC_ket_cuc__de_ghi_duoc_dong_Duplicate()
    {
        var don = TienTo + "DON-04";

        await DonDepAsync();

        try
        {
            // 🚨 **PHÁT HIỆN ĐO ĐƯỢC Ở `S6` (`sal-005` §20.5 #1):** nếu khoá unique **không** có `Outcome` thì
            // dòng thứ hai dưới đây **KHÔNG BAO GIỜ** ghi được — nó trùng toàn bộ khoá với dòng `Applied`
            // ⇒ `Outcome = Duplicate` trở thành **giá trị chết**, và câu hỏi *"sàn có gửi lại không?"*
            // (mục đích của `D7`) **không trả lời được**. Có `Outcome` trong khoá ⇒ ghi được **một** dòng
            // đánh dấu lần giao lại, mà ca "cùng kết cục ghi hai lần" **vẫn** bị chặn (ca `Chan_trung_…` ở trên).
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA, outcome: 0);   // Applied
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA, outcome: 1);   // Duplicate

            Assert.Equal(2L, await DemAsync(don));
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Chan_trung_CUNG_su_kien_khi_TenantId_NULL()
    {
        var don = TienTo + "DON-01";

        await DonDepAsync();

        try
        {
            // 🔑 Ca NẶNG: `TenantId = NULL`. Nếu index thiếu `NULLS NOT DISTINCT` thì **hai dòng LỌT** ⇒
            // nhật ký nói *"sàn gửi 2 lần"* trong khi thực tế chỉ **một**.
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA);

            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Chan_trung_CA_KHI_phien_ban_NULL__khong_chi_khi_co_phien_ban()
    {
        var don = TienTo + "DON-NULLVER";

        await DonDepAsync();

        try
        {
            // 🚨 Ca tinh vi nhất: sự kiện **không mang watermark** (`SourceVersion IS NULL`, `Outcome = ThieuWatermark`).
            // Với `NULLS NOT DISTINCT`, `NULL` được coi là **BẰNG NHAU** ⇒ sự kiện giao lại **KHÔNG** được ghi
            // dòng thứ hai. 📌 Nếu thiếu mệnh đề đó thì mỗi lần giao lại ghi thêm một dòng `NULL` — và ca
            // "thiếu watermark" (nợ connector) sẽ **phình** bảng nhanh nhất, đúng lúc nó vô dụng nhất.
            await ChenAsync(null, don, sourceVersion: null, payloadHash: HashA, outcome: 6);

            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(null, don, sourceVersion: null, payloadHash: HashA, outcome: 6));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Cho_phep_KHAC_phien_ban__va_KHAC_hash__tren_CUNG_mot_don()
    {
        var don = TienTo + "DON-02";

        await DonDepAsync();

        try
        {
            // 🔒 **Chiều NGƯỢC** (chống index quá chặt): một đơn sàn **phải** có nhiều dòng — mỗi lần sàn cập nhật
            // (`update_time` mới) là một sự kiện **hợp lệ**. Nếu index chặn ca này thì nhật ký chỉ còn **một**
            // dòng cho mỗi đơn ⇒ chính tính năng `D7` **vô nghĩa** (không còn lịch sử để đọc).
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA);   // v1
            await ChenAsync(null, don, sourceVersion: 1760313605, payloadHash: HashB);   // v2 (cập nhật)
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashB);   // cùng v1, nội dung khác

            Assert.Equal(3L, await DemAsync(don));
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Tenant_KHAC_nhau_thi_cung_su_kien__duoc_phep()
    {
        var don = TienTo + "DON-03";

        await DonDepAsync();

        try
        {
            // 🔒 Cách ly tenant (`NFR-SAL-005-007`): hai tenant **cùng** `order_sn` là chuyện bình thường
            // (mã đơn của sàn không duy nhất toàn cầu). Nếu index gộp chúng lại thì tenant B **không ghi được**
            // dấu vết cho đơn của mình — và lỗi đó chỉ lộ ra khi **có tenant thứ hai**.
            await ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: HashA);
            await ChenAsync(Guid.NewGuid(), don, sourceVersion: 1760313600, payloadHash: HashA);
            await ChenAsync(Guid.NewGuid(), don, sourceVersion: 1760313600, payloadHash: HashA);

            Assert.Equal(3L, await DemAsync(don));
        }
        finally
        {
            await DonDepAsync();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② `CHECK` — bắt lỗi MAP SAI trước khi nó thành "lịch sử"
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task SourceVersion_KHONG_DUONG__bi_DB_chan_23514(long phienBan)
    {
        var don = TienTo + "DON-CK-VER";

        await DonDepAsync();

        try
        {
            // 📌 `0` là mốc **thật** (1970-01-01) nhưng **không thể** là `update_time` của một đơn sàn thật
            // ⇒ nó chỉ xuất hiện khi ai đó map **sai** (số dòng, `default(long)`, mốc rỗng).
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(null, don, sourceVersion: phienBan, payloadHash: HashA));

            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
            Assert.Equal("CK_SalMktEventLogs_SourceVersion", ex.ConstraintName);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Theory]
    [InlineData("AAAAAAAaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // có chữ HOA
    [InlineData("abc")]                                                              // quá ngắn
    [InlineData("Zaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // không phải hex
    public async Task PayloadHash_KHONG_phai_hex_thuong_64__bi_DB_chan_23514(string hashSai)
    {
        var don = TienTo + "DON-CK-HASH";

        await DonDepAsync();

        try
        {
            // 🚨 Hash **trông vẫn như hash** nhưng sai định dạng ⇒ so sánh ở nhánh C/D **sai** ⇒ cùng một
            // sự kiện bị coi là **xung đột** ⇒ DLQ giả. 📌 Đây đúng loại lỗi "không có triệu chứng" mà
            // `SAL-005` §15.3 giả định 2 đã cảnh báo.
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(null, don, sourceVersion: 1760313600, payloadHash: hashSai));

            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
            Assert.Equal("CK_SalMktEventLogs_PayloadHash_Hex", ex.ConstraintName);
        }
        finally
        {
            await DonDepAsync();
        }
    }
}

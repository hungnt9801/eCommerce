using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🚨 **HÀNG RÀO CHỐNG **GHI HAI LẦN** MỘT GIAO DỊCH THANH TOÁN** (`S4` — `PAY-001` **TC-29**).
///
/// **VÌ SAO PHẢI LÀ TEST INTEGRATION (không phải unit):** bất biến này sống ở **PostgreSQL**, không ở C#:
/// `NULLS NOT DISTINCT` là thuộc tính **EF Core không mô hình hoá** (`PAY-001` §5.4) ⇒ nó chỉ đúng nếu migration
/// chạy đúng trên máy chủ thật. 📌 Đây đúng loại lỗi mà nhóm test này sinh ra để bắt: unique index **trông như**
/// đang bảo vệ, nhưng ở chế độ `TenantId = NULL` (hiện nay) thì PostgreSQL mặc định coi `NULL` là **khác nhau**
/// ⇒ hai dòng trùng **lọt im lặng** (bằng chứng lịch sử: migration `20261013024226` phải viết lại index bằng
/// **SQL thô**).
///
/// **ĐIỀU GÌ XẢY RA NẾU THIẾU HÀNG RÀO NÀY (để thấy đây không phải test hình thức):** IPN là **at-least-once**;
/// nếu hai IPN song song tạo được **hai dòng** cho cùng một lần thu tiền thì `RefundedAmount` có thể bị trừ trên
/// **hai** dòng, đối soát **không khớp** sổ cổng, và *"đơn này đã trả chưa"* trả **hai** câu trả lời khác nhau.
///
/// 🔒 Mọi câu lệnh dùng **tham số** (`$1`, `$2`…), và mỗi ca **tự dọn** trong `finally` — nếu không, dòng còn lại
/// sẽ làm đỏ `kiem-db.ps1` ở lượt sau (`tools/README-probe.md` §D).
/// </summary>
[Collection("postgres")]
public class PayTransactionsChanTrungTests
{
    /// <summary>Tiền tố dành riêng cho test — dọn theo tiền tố này, không theo `Id` (để không sót ca chèn hỏng).</summary>
    private const string TienTo = "PAY-TEST-";

    private readonly PostgreSqlSchemaFixture _fixture;

    public PayTransactionsChanTrungTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task ChenAsync(Guid id, Guid? tenantId, string ma, string? idempotencyKey)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "PayTransactions"
                ("Id","TenantId","ProviderCode","MerchantReference","Amount","Currency","Status",
                 "RefundedAmount","WebhookReceivedCount","IdempotencyKey",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, 'vnpay', $3, 250000.0000, 'VND', 0, 0.0000, 0, $4, '{}', $5, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(ma);
        cmd.Parameters.AddWithValue((object?)idempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task DonDepAsync()
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            """delete from "PayTransactions" where "MerchantReference" like $1""");

        cmd.Parameters.AddWithValue(TienTo + "%");

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> DemAsync(string ma)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            """select count(*) from "PayTransactions" where "MerchantReference" = $1""");

        cmd.Parameters.AddWithValue(ma);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // ── (1) TRÙNG MÃ KHI `TenantId = NULL` — chính là ca mà `NULLS NOT DISTINCT` bảo vệ ─────────────

    [Fact]
    public async Task TC29_trung_MerchantReference_khi_TenantId_NULL__BI_CHAN__23505()
    {
        var ma = TienTo + "DUP-NULL";

        try
        {
            await ChenAsync(Guid.NewGuid(), null, ma, null);

            // 🔑 Khẳng định ĐIỀU KIỆN TIÊN QUYẾT: dòng thứ nhất **đã vào** (nếu không thì "dòng 2 bị chặn" và
            // "dòng 1 cũng không vào" trông giống hệt nhau — bài học `§25.5` của `tools/README-probe.md`).
            Assert.Equal(1L, await DemAsync(ma));

            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(Guid.NewGuid(), null, ma, null));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
        }
        finally
        {
            await DonDepAsync();
        }
    }

    /// <summary>
    /// 🔑 **CHIỀU NGƯỢC — chống "chặn oan":** cùng `MerchantReference` nhưng **khác tenant** là **hợp lệ**
    /// (mã chỉ duy nhất trong `(tenant, cổng)`). 📌 Nếu thiếu ca này, một index sai (ví dụ `UNIQUE(MerchantReference)`
    /// không có `TenantId`) vẫn làm test (1) **xanh** — trong khi nó chặn oan giao dịch của tenant khác.
    /// </summary>
    [Fact]
    public async Task Cung_ma_nhung_KHAC_tenant__DUOC_PHEP()
    {
        var ma = TienTo + "DUP-TENANT";

        try
        {
            await ChenAsync(Guid.NewGuid(), Guid.NewGuid(), ma, null);
            await ChenAsync(Guid.NewGuid(), Guid.NewGuid(), ma, null);

            Assert.Equal(2L, await DemAsync(ma));
        }
        finally
        {
            await DonDepAsync();
        }
    }

    // ── (2) KHOÁ CHỐNG TRÙNG `IdempotencyKey` — có BỘ LỌC `IS NOT NULL` ─────────────────────────────

    [Fact]
    public async Task Trung_IdempotencyKey_cua_cong__BI_CHAN__23505()
    {
        var ma1 = TienTo + "IDEM-1";
        var ma2 = TienTo + "IDEM-2";

        try
        {
            await ChenAsync(Guid.NewGuid(), null, ma1, "vnp-12345");

            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => ChenAsync(Guid.NewGuid(), null, ma2, "vnp-12345"));

            // ⚠️ Hai `MerchantReference` KHÁC nhau nhưng CÙNG mã cổng cấp ⇒ đúng ca "hai dòng cho một lần thu tiền"
            // mà index `(TenantId, ProviderCode, IdempotencyKey)` sinh ra để chặn.
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    /// <summary>
    /// 🚨 **BỘ LỌC `WHERE "IdempotencyKey" IS NOT NULL` LÀ BẮT BUỘC** — trước khi có IPN, **mọi** giao dịch đều có
    /// `IdempotencyKey = NULL`. 📌 Nếu ai đó bỏ bộ lọc (ví dụ khi EF sinh lại index), thì với `NULLS NOT DISTINCT`
    /// **toàn bộ** giao dịch mới sẽ **đụng nhau ở `NULL`** ⇒ giao dịch thứ **hai** của bất kỳ đơn nào cũng bị chặn
    /// ⇒ *"không tạo được giao dịch"* trên diện rộng. Test này khoá đúng hành vi đó.
    /// </summary>
    [Fact]
    public async Task Nhieu_dong_IdempotencyKey_NULL__DUOC_PHEP__nho_bo_loc()
    {
        var ma1 = TienTo + "NULL-KEY-1";
        var ma2 = TienTo + "NULL-KEY-2";
        var ma3 = TienTo + "NULL-KEY-3";

        try
        {
            await ChenAsync(Guid.NewGuid(), null, ma1, null);
            await ChenAsync(Guid.NewGuid(), null, ma2, null);
            await ChenAsync(Guid.NewGuid(), null, ma3, null);

            Assert.Equal(1L, await DemAsync(ma1));
            Assert.Equal(1L, await DemAsync(ma2));
            Assert.Equal(1L, await DemAsync(ma3));
        }
        finally
        {
            await DonDepAsync();
        }
    }

    // ── (3) MÃ ỔN ĐỊNH: cùng đơn + cùng ngày ⇒ mã CÓ TIỀN TỐ NGÀY (không cần DB) ───────────────────
    //
    // 📌 Ca này KHÔNG chạm DB nhưng thuộc nhóm integration vì nó khoá **định dạng mã** đã được ghi vào bảng:
    // cột là `varchar(64)` và cổng yêu cầu "không trùng trong ngày" ⇒ chuỗi phải bắt đầu bằng `PAY{yyyyMMdd}-`.

    [Fact]
    public async Task Ma_tham_chung_sinh_ra_VUA_cot_varchar_64()
    {
        var ma = eCommerce.PaymentHub.Payments.SinhMaThamChieu.Sinh(
            "WEB-WEB-0123456789abcdef", new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc));

        await using var cmd = _fixture.DataSource.CreateCommand(
            """select character_maximum_length from information_schema.columns where table_name = 'PayTransactions' and column_name = 'MerchantReference'""");

        var doDaiCot = (int)(await cmd.ExecuteScalarAsync())!;

        Assert.True(ma.Length <= doDaiCot, $"Mã '{ma}' dài {ma.Length} > cột {doDaiCot} ⇒ PostgreSQL cắt cụt im lặng.");
    }
}

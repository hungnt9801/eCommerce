using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// `PRO-001` / `W25-4` — phần **chỉ chứng minh được trên PostgreSQL 17 thật** mà `W25-2` để nợ:
/// <list type="bullet">
///   <item><b>`BR-06` / `D-305`</b> — lượt + bộ đếm đổi trong **cùng UoW**; rollback ⇒ lượt **vẫn** `Reserved`,
///     bộ đếm **không** đổi. Đây là cơ chế thật giữ "0 đơn / lượt vẫn giữ chỗ" khi hỏng giữa đường.</item>
///   <item><b>`D-295`</b> — trùng `(TenantId, PromotionId, CustomerId, UsageSequence)` ⇒ `23505`; **không**
///     `SAVEPOINT` thì lệnh kế trong transaction nhận `25P02`; **có** `SAVEPOINT` + `ROLLBACK TO SAVEPOINT` thì
///     transaction dùng tiếp được. Đây là cơ chế `UsageSequenceAllocator`/`VoucherRedemptionService` dựa vào —
///     nếu PostgreSQL không hành xử đúng vậy, retry 5 lần của `D-295` vô nghĩa.</item>
///   <item><b>`D-306`</b> — guard `UPDATE … WHERE "Status" = 0` của job `voucher-expiry` trả **0 dòng** khi lượt
///     không còn `Reserved` (không đổi gì) — bỏ qua là **bình thường**, không phải lỗi. Ca khẳng định nghĩa
///     "0 dòng" của job trên DB thật; phần log/metric đã phủ ở unit (`VoucherExpiryWorkerTests`).</item>
/// </list>
///
/// 🔒 Dùng chung fixture Testcontainers (`postgres:17-alpine`), schema từ `Assets/schema.sql`. Mọi câu lệnh
/// **tham số** — không nối chuỗi. `Oversell`/`lock in-process` KHÔNG dùng: đây là test hàng rào DB thật.
/// </summary>
[Collection("postgres")]
public class PostgreSqlVoucherWp25Tests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlVoucherWp25Tests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Ma() => "PRO-" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Lệnh dùng connection/tx cho trước, hoặc connection vay từ pool khi <paramref name="conn"/> null.</summary>
    private NpgsqlCommand TaoLenh(NpgsqlConnection? conn, NpgsqlTransaction? tx, string sql)
        => conn is null ? _fixture.DataSource.CreateCommand(sql) : new NpgsqlCommand(sql, conn, tx);

    // ── helper chèn dữ liệu (đúng cột thật của migration) ─────────────────────────────────────────

    private async Task<Guid> ChenPromotionAsync(NpgsqlConnection? conn, NpgsqlTransaction? tx,
        int? maxUsageTotal = null, int used = 0, int reserved = 0)
    {
        var id = Guid.NewGuid();
        await using var cmd = TaoLenh(conn, tx, """
            insert into "ProPromotions"
                ("Id","TenantId","Code","Name","Kind","Status","IsCumulative","Priority","ChannelScope",
                 "StartsAtUtc","EndsAtUtc","TreeVersion","MaxUsageTotal","UsedCount","ReservedCount",
                 "MaxUsagePerCustomer","DiscountKind","DiscountValue","MaxDiscountAmount","MinOrderAmount",
                 "Currency","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1,null,$2,'KM test','Voucher','Active',false,0,null,
                    now(),null,0,$3,$4,$5,
                    null,'FixedAmount',1000,null,null,
                    'VND','{}',$6,now(),false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(Ma());
        cmd.Parameters.AddWithValue((object?)maxUsageTotal ?? DBNull.Value);
        cmd.Parameters.AddWithValue(used);
        cmd.Parameters.AddWithValue(reserved);
        cmd.Parameters.AddWithValue("stamp-" + Ma());

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> ChenVoucherAsync(Guid promotionId, int maxUsage = 10, int used = 0, int reserved = 0)
    {
        var id = Guid.NewGuid();
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProVoucherCodes"
                ("Id","TenantId","PromotionId","Code","MaxUsage","UsedCount","ReservedCount","Status")
            values ($1,null,$2,$3,$4,$5,$6,'Issued')
            """);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue(Ma());
        cmd.Parameters.AddWithValue(maxUsage);
        cmd.Parameters.AddWithValue(used);
        cmd.Parameters.AddWithValue(reserved);

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Chèn một lượt. `status`: `0` Reserved (cần `ExpiresAtUtc`), `1` Applied (cần `AppliedAtUtc`),
    /// `2` Released / `3` Reversed (cần mốc + `ReasonCode`) — đúng `CHECK` của bảng.
    /// </summary>
    private async Task<Guid> ChenLuotAsync(
        NpgsqlConnection? conn, NpgsqlTransaction? tx,
        Guid promotionId, Guid? voucherId, Guid? orderId, int status, int usageSequence,
        Guid? customerId, string? idempotencyKey, DateTime? expiresAtUtc = null)
    {
        var id = Guid.NewGuid();
        await using var cmd = TaoLenh(conn, tx, """
            insert into "ProPromotionRedemptions"
                ("Id","TenantId","PromotionId","VoucherCodeId","OrderId","Status","ExpiresAtUtc","UsageSequence",
                 "ReasonCode","CustomerId","AppliedAtUtc","ReleasedAtUtc","ReversedAtUtc",
                 "IdempotencyKey","DiscountAmount","Currency","BreakdownJson","TreeVersion","EvaluatedAtUtc")
            values ($1,null,$2,$3,$4,$5,
                    case when $5 = 0 then coalesce($9::timestamptz, now() + interval '15 minutes') else null end,
                    $6,
                    case when $5 in (2,3) then 'TTL_EXPIRED' else null end,
                    $7,
                    case when $5 = 1 then now() else null end,
                    case when $5 = 2 then now() else null end,
                    case when $5 = 3 then now() else null end,
                    $8, 10000, 'VND', '{}'::jsonb, 1, now())
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue((object?)voucherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)orderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue(usageSequence);
        cmd.Parameters.AddWithValue((object?)customerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(idempotencyKey ?? Ma());
        cmd.Parameters.AddWithValue((object?)expiresAtUtc ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<long> DemAsync(string sql, params object?[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        for (var i = 0; i < thamSo.Length; i++)
        {
            cmd.Parameters.AddWithValue(thamSo[i]!);
        }

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  BR-06 / D-305 — rollback cùng UoW: lượt vẫn Reserved, bộ đếm không đổi
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BR06_rollback_cung_UoW__luot_van_Reserved_va_bo_dem_khong_doi()
    {
        // Mô phỏng ĐÚNG chuỗi lệnh `VoucherRedemptionService.ThucHienCommitAsync` (lượt → mã → promotion)
        // trong MỘT transaction rồi ROLLBACK: đây là cơ chế giữ "0 đơn / lượt vẫn Reserved" (§8.1 #7, D-305).
        var promotionId = await ChenPromotionAsync(null, null, maxUsageTotal: 10, used: 0, reserved: 1);
        var voucherId = await ChenVoucherAsync(promotionId, maxUsage: 5, used: 0, reserved: 1);
        var orderId = Guid.NewGuid();
        var redemptionId = await ChenLuotAsync(null, null, promotionId, voucherId, orderId: null,
            status: 0, usageSequence: 1, customerId: Guid.NewGuid(), idempotencyKey: Ma());

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var soLuot = await ChayAsync(conn, tx, """
            UPDATE "ProPromotionRedemptions"
               SET "Status" = 1, "OrderId" = $2, "AppliedAtUtc" = now()
             WHERE "Id" = $1 AND "Status" = 0 AND "ExpiresAtUtc" > now()
            """, redemptionId, orderId);
        var soMa = await ChayAsync(conn, tx, """
            UPDATE "ProVoucherCodes"
               SET "ReservedCount" = "ReservedCount" - 1, "UsedCount" = "UsedCount" + 1
             WHERE "Id" = $1 AND "ReservedCount" > 0
            """, voucherId);
        var soPromotion = await ChayAsync(conn, tx, """
            UPDATE "ProPromotions"
               SET "ReservedCount" = "ReservedCount" - 1, "UsedCount" = "UsedCount" + 1
             WHERE "Id" = $1 AND "ReservedCount" > 0
            """, promotionId);

        Assert.Equal(1, soLuot);
        Assert.Equal(1, soMa);
        Assert.Equal(1, soPromotion);

        await tx.RollbackAsync();

        // ✅ Rollback ⇒ mọi thứ như trước: lượt Reserved (Status 0), OrderId NULL, bộ đếm nguyên.
        var luot = await DongLuotAsync(redemptionId);
        Assert.Equal(0, luot.Status);
        Assert.Null(luot.OrderId);
        Assert.Null(luot.AppliedAtUtc);

        Assert.Equal(1L, await DemAsync(
            "select \"ReservedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
        Assert.Equal(0L, await DemAsync(
            "select \"UsedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
        Assert.Equal(1L, await DemAsync(
            "select \"ReservedCount\"::bigint from \"ProPromotions\" where \"Id\" = $1", promotionId));
        Assert.Equal(0L, await DemAsync(
            "select \"UsedCount\"::bigint from \"ProPromotions\" where \"Id\" = $1", promotionId));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  D-295 — UsageSequence: 23505 · 25P02 khi không SAVEPOINT · SAVEPOINT cứu transaction
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D295_trung_UsageSequence_cung_khach__bi_chan_23505()
    {
        var promotionId = await ChenPromotionAsync(null, null);
        var khachId = Guid.NewGuid();

        // Lượt còn hiệu lực (Status 0) giữ số 1 của khách.
        await ChenLuotAsync(null, null, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());

        // Hàng rào = unique `(TenantId, PromotionId, CustomerId, UsageSequence) WHERE "CustomerId" IS NOT NULL
        // AND "Status" <> 2` — trùng số còn hiệu lực ⇒ 23505 (cơ chế "thử số kế tiếp" của D-295).
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChenLuotAsync(
            null, null, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task D295_KHONG_savepoint__loi_23505_lam_transaction_ket_25P02()
    {
        // 🚨 Lý do BẮT BUỘC có SAVEPOINT quanh `INSERT` của allocator: trong PostgreSQL, một lỗi giữa transaction
        // đưa transaction sang trạng thái ABORTED ⇒ **mọi** lệnh sau nhận `25P02` ("current transaction is aborted").
        // Nếu `VoucherRedemptionService` thử lại mà không SAVEPOINT, lần thử 2 sẽ nổ `25P02` chứ không nhận `23505`.
        var promotionId = await ChenPromotionAsync(null, null);
        var khachId = Guid.NewGuid();

        await ChenLuotAsync(null, null, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var trung = await Assert.ThrowsAsync<PostgresException>(() => ChenLuotAsync(
            conn, tx, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma()));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, trung.SqlState);

        // Lệnh kế tiếp trong CÙNG transaction (không SAVEPOINT) ⇒ 25P02, KHÔNG phải 23505.
        var sau = await Assert.ThrowsAsync<PostgresException>(
            () => ChayAsync(conn, tx, "select 1"));
        Assert.Equal(PostgresErrorCodes.InFailedSqlTransaction, sau.SqlState);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task D295_CO_savepoint__rollback_to_savepoint_roi_transaction_dung_tiep_duoc()
    {
        // ✅ Đúng cơ chế `ChenLuotAsync` (`VoucherRedemptionService.cs:306-357`, `D-295`): SAVEPOINT → INSERT
        // → 23505 → ROLLBACK TO SAVEPOINT ⇒ transaction còn sống, chèn được số kế tiếp, commit sạch.
        var promotionId = await ChenPromotionAsync(null, null);
        var khachId = Guid.NewGuid();

        await ChenLuotAsync(null, null, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await ChayAsync(conn, tx, "SAVEPOINT sp_cap_so");

        var trung = await Assert.ThrowsAsync<PostgresException>(() => ChenLuotAsync(
            conn, tx, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 1, customerId: khachId, idempotencyKey: Ma()));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, trung.SqlState);

        await ChayAsync(conn, tx, "ROLLBACK TO SAVEPOINT sp_cap_so");

        // Transaction dùng tiếp được: đọc OK và INSERT số 2 (số kế tiếp) thành công.
        Assert.Equal(1L, await MotAsync(conn, tx, "select 1::bigint"));

        await ChenLuotAsync(conn, tx, promotionId, voucherId: null, orderId: null,
            status: 0, usageSequence: 2, customerId: khachId, idempotencyKey: Ma());

        await tx.CommitAsync();

        Assert.Equal(2L, await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"PromotionId\" = $1 and \"CustomerId\" = $2",
            promotionId, khachId));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  D-306 — guard `Status = 0` của job nhả TTL: 0 dòng khi không phải Reserved, không đổi gì
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D306_guard_Status_0__luot_Applied__0_dong_va_khong_doi_gi()
    {
        var promotionId = await ChenPromotionAsync(null, null, used: 1, reserved: 0);
        var voucherId = await ChenVoucherAsync(promotionId, maxUsage: 5, used: 1, reserved: 0);
        var redemptionId = await ChenLuotAsync(null, null, promotionId, voucherId, orderId: Guid.NewGuid(),
            status: 1, usageSequence: 1, customerId: Guid.NewGuid(), idempotencyKey: Ma());

        // ĐÚNG câu `UPDATE` của `VoucherExpiryWorker.CapNhatLoAsync` (Workers/VoucherExpiryWorker.cs:226-230).
        int soDong;
        await using (var conn = await _fixture.DataSource.OpenConnectionAsync())
        {
            soDong = await ChayAsync(conn, null, """
                UPDATE "ProPromotionRedemptions"
                   SET "Status" = 2, "ReleasedAtUtc" = now(), "ReasonCode" = 'TTL_EXPIRED'
                 WHERE "TenantId" IS NULL AND "Id" = $1 AND "Status" = 0
                """, redemptionId);
        }

        // 0 dòng = lượt không còn `Reserved` ⇒ job bỏ qua (không phải lỗi); KHÔNG được nhả lượt đã chốt.
        Assert.Equal(0, soDong);

        var luot = await DongLuotAsync(redemptionId);
        Assert.Equal(1, luot.Status);
        Assert.Null(luot.ReleasedAtUtc);
        Assert.Null(luot.ReasonCode);

        Assert.Equal(0L, await DemAsync(
            "select \"ReservedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
        Assert.Equal(1L, await DemAsync(
            "select \"UsedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
    }

    [Fact]
    public async Task D306_guard_Status_0__luot_Reserved_het_han__1_dong_va_giam_ReservedCount()
    {
        // Đối chứng: cùng câu `UPDATE`, lượt `Reserved` đã quá hạn ⇒ 1 dòng; job giảm `ReservedCount` (không đụng
        // `UsedCount`) — nếu guard quá rộng tay, ca trên (0 dòng) sẽ không còn phân biệt được.
        var promotionId = await ChenPromotionAsync(null, null, maxUsageTotal: 5, used: 0, reserved: 1);
        var voucherId = await ChenVoucherAsync(promotionId, maxUsage: 5, used: 0, reserved: 1);
        var redemptionId = await ChenLuotAsync(null, null, promotionId, voucherId, orderId: null,
            status: 0, usageSequence: 1, customerId: Guid.NewGuid(), idempotencyKey: Ma(),
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        int soDong;
        int soMa;
        int soPromotion;
        await using (var conn = await _fixture.DataSource.OpenConnectionAsync())
        {
            soDong = await ChayAsync(conn, null, """
                UPDATE "ProPromotionRedemptions"
                   SET "Status" = 2, "ReleasedAtUtc" = now(), "ReasonCode" = 'TTL_EXPIRED'
                 WHERE "TenantId" IS NULL AND "Id" = $1 AND "Status" = 0
                """, redemptionId);

            // Bước 2 của job: MÃ trước, PROMOTION sau (chỉ dòng có trần) — sắp `Id` (D-306, §8.2 #9).
            soMa = await ChayAsync(conn, null, """
                UPDATE "ProVoucherCodes"
                   SET "ReservedCount" = "ReservedCount" - 1
                 WHERE "TenantId" IS NULL AND "Id" = $1 AND "ReservedCount" > 0
                """, voucherId);
            soPromotion = await ChayAsync(conn, null, """
                UPDATE "ProPromotions"
                   SET "ReservedCount" = "ReservedCount" - 1
                 WHERE "TenantId" IS NULL AND "Id" = $1 AND "ReservedCount" > 0
                """, promotionId);
        }

        Assert.Equal(1, soDong);
        Assert.Equal(1, soMa);
        Assert.Equal(1, soPromotion);

        var luot = await DongLuotAsync(redemptionId);
        Assert.Equal(2, luot.Status);
        Assert.Equal("TTL_EXPIRED", luot.ReasonCode);

        Assert.Equal(0L, await DemAsync(
            "select \"ReservedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
        Assert.Equal(0L, await DemAsync(
            "select \"UsedCount\"::bigint from \"ProVoucherCodes\" where \"Id\" = $1", voucherId));
        Assert.Equal(0L, await DemAsync(
            "select \"ReservedCount\"::bigint from \"ProPromotions\" where \"Id\" = $1", promotionId));
    }

    // ── chạy lệnh tham số, trả số dòng ảnh hưởng (hoặc scalar) ───────────────────────────────────

    private static async Task<int> ChayAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql,
        params object?[] thamSo)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var t in thamSo)
        {
            cmd.Parameters.AddWithValue(t ?? DBNull.Value);
        }

        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> MotAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<(int Status, Guid? OrderId, DateTime? AppliedAtUtc, DateTime? ReleasedAtUtc, string? ReasonCode)>
        DongLuotAsync(Guid redemptionId)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            select "Status", "OrderId", "AppliedAtUtc", "ReleasedAtUtc", "ReasonCode"
            from "ProPromotionRedemptions" where "Id" = $1
            """);
        cmd.Parameters.AddWithValue(redemptionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return (
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }
}

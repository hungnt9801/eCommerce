using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — RÀNG BUỘC + BỀN VỮNG CỦA ĐỘNG CƠ GIÁ VỐN TRÊN POSTGRESQL THẬT**
/// (`D-320` vi; `PRC-001-arch` §4.2/§5.6; `K06-01`, `K06-08b`, `BR-PRC-001-008`, `BR-PRC-001-011`, `BR-PRC-001-012`).
///
/// 🔒 Vì sao phải chạy PostgreSQL thật: unique index `NULLS NOT DISTINCT` và tính **bền vững của sổ run qua
/// rollback UoW ngoài** chỉ đo được ở DB. Test này bổ sung cho `PostgreSqlSchemaTests` (đếm 116 index) bằng
/// **phép thử hành vi** đích danh trên `PrcCostPrices` + `PrcCostEngineRuns`.
///
/// 📌 Mọi câu lệnh **tham số hoá**; dữ liệu dùng `Guid` hậu tố để không đụng test khác trong collection.
/// </summary>
[Collection("postgres")]
public class PostgreSqlPricingCostEngineTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlPricingCostEngineTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Ma(string tienTo) => tienTo + "-" + Guid.NewGuid().ToString("N")[..8];

    // ── tiện ích chèn ──────────────────────────────────────────────────────────────────────────

    private async Task ChenCostPriceAsync(
        Guid? tenantId,
        string sku,
        decimal amount,
        DateOnly effectiveFrom,
        string? idempotencyKey = null,
        Guid? approvedBy = null)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "PrcCostPrices"
                ("Id","TenantId","Sku","AmountValue","CurrencyCode","EffectiveFrom",
                 "IdempotencyKey","NguonTinh","RunId","ComputedAtUtc",
                 "ApprovedBy","ApprovedAtUtc",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, 'VND', $5,
                    $6, null, null, null,
                    $7, $8,
                    '{}', $9, now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(sku);
        cmd.Parameters.AddWithValue(amount);
        cmd.Parameters.AddWithValue(effectiveFrom);
        cmd.Parameters.AddWithValue((object?)idempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)approvedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue(approvedBy is null ? DBNull.Value : DateTime.UtcNow);
        cmd.Parameters.AddWithValue("stamp-" + Guid.NewGuid().ToString("N")[..8]);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenRunAsync(
        Guid? tenantId,
        string runKey,
        int trangThai = 0,
        int attempt = 1)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "PrcCostEngineRuns"
                ("Id","TenantId","RunKey","FromDate","ToDate","Trigger","TrangThai","Attempt",
                 "StartedAtUtc","RowsUpserted","RowsSkippedApproved","RowsFlaggedMissing",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, date '2026-01-01', date '2026-01-01', 'Manual', $4, $5,
                    now(), 0, 0, 0,
                    '{}', $6, now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(runKey);
        cmd.Parameters.AddWithValue(trangThai);
        cmd.Parameters.AddWithValue(attempt);
        cmd.Parameters.AddWithValue("stamp-" + Guid.NewGuid().ToString("N")[..8]);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> DemAsync(string sql, params NpgsqlParameter[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        cmd.Parameters.AddRange(thamSo);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // ── BR-008 / K06-01: grain giá vốn + giá vốn 0 hợp lệ ──────────────────────────────────────

    /// <summary>
    /// BR-008 — **một giá vốn cho một SKU trong một kỳ** (`TenantId = NULL`): dòng thứ hai cùng
    /// `(Sku, EffectiveFrom)` bị **unique `NULLS NOT DISTINCT`** chặn (23505). Mất NND ⇒ lãi gộp không tất định.
    /// </summary>
    [Fact]
    public async Task BR_008_mot_SKU_mot_ky__ban_ghi_thu_hai_bi_chan_khi_TenantId_NULL()
    {
        var sku = Ma("GRAIN");
        var tu = new DateOnly(2026, 3, 1);

        await ChenCostPriceAsync(null, sku, 100_000m, tu);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenCostPriceAsync(null, sku, 120_000m, tu));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    /// <summary>BR-008 — cùng SKU nhưng **khác kỳ** thì hợp lệ (không chặn quá tay).</summary>
    [Fact]
    public async Task BR_008_cung_SKU_khac_ky__cho_phep()
    {
        var sku = Ma("GRAIN2");

        await ChenCostPriceAsync(null, sku, 100_000m, new DateOnly(2026, 3, 1));
        await ChenCostPriceAsync(null, sku, 120_000m, new DateOnly(2026, 3, 2));

        await using var cmd = _fixture.DataSource.CreateCommand(
            "select count(*) from \"PrcCostPrices\" where \"Sku\" = $1");
        cmd.Parameters.AddWithValue(sku);

        Assert.Equal(2L, (long)(await cmd.ExecuteScalarAsync())!);
    }

    /// <summary>K06-01 — giá vốn `0` hợp lệ ở DB (CHECK `AmountValue >= 0` cho qua).</summary>
    [Fact]
    public async Task K06_01_gia_von_0_hop_le_o_DB()
    {
        var sku = Ma("ZERO");

        await ChenCostPriceAsync(null, sku, 0m, new DateOnly(2026, 3, 1), approvedBy: Guid.NewGuid());

        await using var cmd = _fixture.DataSource.CreateCommand(
            "select \"AmountValue\" from \"PrcCostPrices\" where \"Sku\" = $1 and \"ApprovedBy\" is not null");
        cmd.Parameters.AddWithValue(sku);

        Assert.Equal(0m, (decimal)(await cmd.ExecuteScalarAsync())!);
    }

    /// <summary>K06-01 (phòng vệ chiều sâu) — giá vốn âm bị `CHECK` chặn (23514) dù có đường ghi tắt.</summary>
    [Fact]
    public async Task K06_01_gia_von_am_bi_CHECK_chan()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenCostPriceAsync(null, Ma("AM"), -1m, new DateOnly(2026, 3, 1)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    // ── BR-012: idempotency đường ghi (unique filtered NND) ─────────────────────────────────────

    /// <summary>BR-012 — hai dòng cùng `IdempotencyKey` (khi `TenantId = NULL`) bị **chặn** (23505).</summary>
    [Fact]
    public async Task BR_012_hai_dong_cung_IdempotencyKey_bi_chan()
    {
        var khoa = Ma("IDEM");

        await ChenCostPriceAsync(null, Ma("SKU"), 100_000m, new DateOnly(2026, 3, 1), khoa);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenCostPriceAsync(null, Ma("SKU"), 100_000m, new DateOnly(2026, 3, 1), khoa));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    /// <summary>BR-012 — `IdempotencyKey = NULL` (dòng cũ/do động cơ) **không** bị ràng buộc (partial index).</summary>
    [Fact]
    public async Task BR_012_IdempotencyKey_null__nhieu_dong_cho_phep()
    {
        var sku = Ma("NULLKEY");

        await ChenCostPriceAsync(null, sku, 100_000m, new DateOnly(2026, 3, 1));
        await ChenCostPriceAsync(null, sku, 120_000m, new DateOnly(2026, 3, 2));

        await using var cmd = _fixture.DataSource.CreateCommand(
            "select count(*) from \"PrcCostPrices\" where \"Sku\" = $1");
        cmd.Parameters.AddWithValue(sku);

        Assert.Equal(2L, (long)(await cmd.ExecuteScalarAsync())!);
    }

    // ── K06-08b: sổ run (unique RunKey NND + bền vững qua rollback UoW ngoài) ───────────────────

    /// <summary>K06-08b — hai lượt cùng `(TenantId = NULL, RunKey)` bị **chặn** (23505).</summary>
    [Fact]
    public async Task K06_08b_RunKey_trung_bi_chan_khi_TenantId_NULL()
    {
        var runKey = "cost:null:" + Ma("RUN");

        await ChenRunAsync(null, runKey);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenRunAsync(null, runKey));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    /// <summary>
    /// K06-08b — **sổ run BỀN VỮNG qua rollback UoW ngoài** (`D-324` ②): run được ghi/commit ở UoW **riêng
    /// trước** khi tính; khi UoW tính rollback (phần giá vốn dở bị bỏ) thì dòng run **vẫn còn** ở trạng thái
    /// `DangChay` ⇒ lượt sau thấy "đang chạy" và bị chặn thay vì chạy chồng.
    /// </summary>
    [Fact]
    public async Task K06_08b_run_ben_vung_qua_rollback_UoW_ngoai()
    {
        var runKey = "cost:null:" + Ma("DURABLE");
        var sku = Ma("ROLLBACK");

        // P1: ghi sổ run trong UoW RIÊNG rồi commit (autocommit ở đây = đã commit).
        await ChenRunAsync(null, runKey, trangThai: 0);

        // P2: tính + ghi giá vốn trong UoW riêng; lỗi ⇒ ROLLBACK (mô phỏng bằng transaction tường minh).
        await using (var conn = await _fixture.DataSource.OpenConnectionAsync())
        {
            await using var tx = await conn.BeginTransactionAsync();

            await using (var insert = conn.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = """
                    insert into "PrcCostPrices"
                        ("Id","TenantId","Sku","AmountValue","CurrencyCode","EffectiveFrom",
                         "NguonTinh","RunId","ComputedAtUtc",
                         "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
                    values ($1, null, $2, 100000, 'VND', date '2026-01-01',
                            1, $3, now(),
                            '{}', $4, now(), false)
                    """;
                insert.Parameters.AddWithValue(Guid.NewGuid());
                insert.Parameters.AddWithValue(sku);
                insert.Parameters.AddWithValue(Guid.NewGuid());
                insert.Parameters.AddWithValue("stamp-" + Guid.NewGuid().ToString("N")[..8]);

                await insert.ExecuteNonQueryAsync();
            }

            await tx.RollbackAsync();
        }

        // Sổ run CÒN (đã commit riêng) — trạng thái DangChay (0).
        await using (var cmd = _fixture.DataSource.CreateCommand(
                         "select \"TrangThai\" from \"PrcCostEngineRuns\" where \"RunKey\" = $1"))
        {
            cmd.Parameters.AddWithValue(runKey);
            Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);
        }

        // Phần giá vốn dở đã bị rollback ⇒ 0 dòng (không ghi nửa).
        await using (var cmd = _fixture.DataSource.CreateCommand(
                         "select count(*) from \"PrcCostPrices\" where \"Sku\" = $1"))
        {
            cmd.Parameters.AddWithValue(sku);
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }
    }

    /// <summary>K06-08b — lượt `Loi` retry trên **CÙNG dòng**: `Attempt++` về `DangChay`, vẫn MỘT dòng run.</summary>
    [Fact]
    public async Task K06_08b_loi_retry_attempt_moi_tren_cung_dong()
    {
        var runKey = "cost:null:" + Ma("RETRY");

        await ChenRunAsync(null, runKey, trangThai: 2 /* Loi */, attempt: 1);

        await using (var cmd = _fixture.DataSource.CreateCommand("""
                         update "PrcCostEngineRuns"
                         set "TrangThai" = 0, "Attempt" = "Attempt" + 1, "ErrorMessage" = null
                         where "RunKey" = $1
                         """))
        {
            cmd.Parameters.AddWithValue(runKey);
            Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
        }

        await using (var cmd = _fixture.DataSource.CreateCommand(
                         "select \"Attempt\", \"TrangThai\" from \"PrcCostEngineRuns\" where \"RunKey\" = $1"))
        {
            cmd.Parameters.AddWithValue(runKey);

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.Equal(0, reader.GetInt32(1));
        }

        await using (var cmd = _fixture.DataSource.CreateCommand(
                         "select count(*) from \"PrcCostEngineRuns\" where \"RunKey\" = $1"))
        {
            cmd.Parameters.AddWithValue(runKey);
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
    }
}

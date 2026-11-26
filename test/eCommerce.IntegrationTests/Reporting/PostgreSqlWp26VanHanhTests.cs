using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// `WP-26` — **RÀNG BUỘC DB CỦA VẬN HÀNH GĐ3** (`REP-001` §5.2–§5.5, §10.2).
///
/// 🚨 **4 ca `EXCLUDE` bắt buộc** (`D-171` ③): constraint đích danh tồn tại; hai kỳ `DaChot` chồng nhau ⇒
/// `23P01`; hai kỳ `DaChot` liền kề ⇒ nhận; `Mo` chồng `DaChot` ⇒ nhận; hai `Mo` chồng nhau ⇒ nhận.
/// 🔑 **VÌ SAO TEST TẦNG PostgreSQL THẬT:** `EXCLUDE`/`NULLS NOT DISTINCT`/partial là hành vi của **DB**, không
/// phải của EF model; chỉ chạy trên PostgreSQL thật mới chứng minh được.
/// </summary>
[Collection("postgres")]
public class PostgreSqlWp26VanHanhTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlWp26VanHanhTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> DemAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Chèn một kỳ. `status = 2` (`DaChot`) ⇒ phải có SnapshotHash/ClosedBy/ClosedAtUtc (CHECK).</summary>
    private async Task ChenKyAsync(Guid? tenantId, short status, string from, string to)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RepPeriodCloses"
                ("Id","TenantId","FromDate","ToDate","Status","SnapshotHash","CloseReason",
                 "ClosedBy","ClosedAtUtc","ReopenedBy","ReopenReason","ReopenedAtUtc","IdempotencyKey",
                 "CreationTime","CreatorId")
            values ($1,$2,$3::date,$4::date,$5,$6,'Lý do đóng kỳ đủ dài',$7,$8,
                    null,null,null,$9,now(),null)
            """);

        var id = Guid.NewGuid();
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(from);
        cmd.Parameters.AddWithValue(to);
        cmd.Parameters.AddWithValue(status);

        if (status == 2)
        {
            cmd.Parameters.AddWithValue(new string('a', 64));
            cmd.Parameters.AddWithValue(Guid.NewGuid());
            cmd.Parameters.AddWithValue(DateTime.UtcNow);
        }
        else
        {
            cmd.Parameters.AddWithValue(DBNull.Value);
            cmd.Parameters.AddWithValue(DBNull.Value);
            cmd.Parameters.AddWithValue(DBNull.Value);
        }

        cmd.Parameters.AddWithValue("ky-" + id);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenRunAsync(
        Guid? tenantId, Guid? requestedBy, string idempotencyKey, short status = 2)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RepRunLogs"
                ("Id","TenantId","JobName","Projection","Status","FromDate","ToDate","WindowsTotal",
                 "WindowsDone","CheckpointDate","RowsWritten","Reason","RequestedBy","StartedAtUtc",
                 "FinishedAtUtc","HostName","CorrelationId","LastErrorCode","IdempotencyKey",
                 "CreationTime","CreatorId")
            values ($1,$2,'Rebuild',0,$3,'2026-09-01'::date,'2026-09-30'::date,30,0,null,0,
                    'Lý do rebuild đủ dài',$4,null,null,null,null,null,$5,now(),null)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue((object?)requestedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue(idempotencyKey);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── EX-1: constraint tồn tại ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EX1_rang_buoc_EXCLUDE_ton_tai_voi_daterange_va_toan_tu_chong_nhau()
    {
        var co = await DemAsync("""
            select count(*) from pg_constraint
            where conname = 'EX_RepPeriodCloses_KyDaChot_KhongChong'
              and contype = 'x'
              and pg_get_constraintdef(oid) like '%EXCLUDE USING gist%'
              and pg_get_constraintdef(oid) like '%daterange%'
              and pg_get_constraintdef(oid) like '%&&%'
            """);

        Assert.Equal(1L, co);
    }

    // ── EX-2: hai kỳ DaChot chồng nhau ⇒ 23P01 ─────────────────────────────────────────────────────

    [Fact]
    public async Task EX2_hai_ky_DaChot_chong_nhau_nem_23P01()
    {
        var tenant = Guid.NewGuid();

        await ChenKyAsync(tenant, 2, "2026-09-01", "2026-09-30");

        var loi = await Assert.ThrowsAsync<PostgresException>(
            () => ChenKyAsync(tenant, 2, "2026-09-15", "2026-10-15"));

        Assert.Equal("23P01", loi.SqlState);
    }

    // ── EX-3: hai kỳ DaChot LIỀN KỀ ⇒ nhận ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EX3_hai_ky_DaChot_lien_ke_duoc_nhan()
    {
        var tenant = Guid.NewGuid();

        await ChenKyAsync(tenant, 2, "2026-09-01", "2026-09-30");
        await ChenKyAsync(tenant, 2, "2026-10-01", "2026-10-31");

        var dem = await DemAsync($"select count(*) from \"RepPeriodCloses\" where \"TenantId\" = '{tenant}'");
        Assert.Equal(2L, dem);
    }

    // ── EX-4: Mo chồng DaChot ⇒ nhận (khoá thứ hai khác nhau — kết quả ĐÃ ĐỊNH) ─────────────────────

    [Fact]
    public async Task EX4_Mo_chong_DaChot_duoc_nhan()
    {
        var tenant = Guid.NewGuid();

        await ChenKyAsync(tenant, 0, "2026-09-01", "2026-09-30");         // Mo
        await ChenKyAsync(tenant, 2, "2026-09-15", "2026-10-15");         // DaChot (chồng Mo)

        var dem = await DemAsync($"select count(*) from \"RepPeriodCloses\" where \"TenantId\" = '{tenant}'");
        Assert.Equal(2L, dem);
    }

    // ── EX-5: hai Mo chồng nhau ⇒ nhận (chứng minh CHỈ chặn DaChot) ────────────────────────────────

    [Fact]
    public async Task EX5_hai_Mo_chong_nhau_duoc_nhan()
    {
        var tenant = Guid.NewGuid();

        await ChenKyAsync(tenant, 0, "2026-09-01", "2026-09-30");
        await ChenKyAsync(tenant, 0, "2026-09-15", "2026-10-15");

        var dem = await DemAsync($"select count(*) from \"RepPeriodCloses\" where \"TenantId\" = '{tenant}'");
        Assert.Equal(2L, dem);
    }

    // ── 4 unique index NULLS NOT DISTINCT (đích danh) ──────────────────────────────────────────────

    [Theory]
    [InlineData("IX_RepRunLogs_TenantId_KhoaIdempotency")]
    [InlineData("UX_RepRunLogs_TenantId_Job_Projection_DangChay")]
    [InlineData("UX_RepConsistencyChecks_DangMo")]
    [InlineData("IX_RepPeriodCloses_TenantId_KhoaIdempotency")]
    public async Task Bon_index_NND_ton_tai(string ten)
    {
        var co = await DemAsync(
            "select count(*) from pg_indexes where schemaname = 'public' "
            + $"and indexname = '{ten}' and indexdef like '%NULLS NOT DISTINCT%'");

        Assert.Equal(1L, co);
    }

    // ── Dòng HỆ THỐNG (RequestedBy NULL) cùng khoá 2 lượt ⇒ 2 dòng, 0 `23505` ─────────────────────

    [Fact]
    public async Task DongHeThong_cung_khoa_hai_luot__hai_dong_khong_23505()
    {
        var tenant = Guid.NewGuid();

        await ChenRunAsync(tenant, null, "Report:AggregateRun:sys:ban-hang:run-1");
        await ChenRunAsync(tenant, null, "Report:AggregateRun:sys:ban-hang:run-1");

        var dem = await DemAsync($"select count(*) from \"RepRunLogs\" where \"TenantId\" = '{tenant}'");
        Assert.Equal(2L, dem);
    }

    [Fact]
    public async Task Rebuild_nguoi_dung_cung_khoa__lan_thu_hai_23505()
    {
        var tenant = Guid.NewGuid();
        var key = "Report:Rebuild:ban-hang:key-1";

        await ChenRunAsync(tenant, Guid.NewGuid(), key);

        var loi = await Assert.ThrowsAsync<PostgresException>(
            () => ChenRunAsync(tenant, Guid.NewGuid(), key));

        Assert.Equal("23505", loi.SqlState);
    }

    [Fact]
    public async Task UX_DangChay_chan_lan_chay_hoat_dong_thu_hai()
    {
        var tenant = Guid.NewGuid();

        await ChenRunAsync(tenant, null, "k-1", status: 0);   // Queued — lượt hoạt động ĐẦU TIÊN

        var loi = await Assert.ThrowsAsync<PostgresException>(
            () => ChenRunAsync(tenant, null, "k-2", status: 0));   // Queued thứ hai cùng (tenant, job, projection)

        Assert.Equal("23505", loi.SqlState);
    }
}

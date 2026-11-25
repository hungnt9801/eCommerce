using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// **`ADDR-001` — hạ tầng schema + hợp đồng giao dịch trên PostgreSQL 17 THẬT** (`W18-4` / `N80`).
///
/// 🎯 Bổ sung cho <see cref="PostgreSqlSchemaTests"/> (đếm/tên index) bằng **hành vi**:
///   • **TC-11**: lô nạp **nguyên tử** — cả đường **file lẫn job**: một lượt trong MỘT transaction gồm
///     `insert` đơn vị + `insert` dòng lô; lỗi giữa chừng ⇒ **ROLLBACK ⇒ 0 dòng** (NFR-007). Đây là hợp đồng
///     UoW mà `ImportDatasetAsync`/`AdministrativeUnitSyncWorker` dựa vào (`D-78` §5.1/§5.5).
///   • **TC-21(f) / TC-24(e)** (mức DB): dòng lô **TỪ CHỐI** ghi ở **transaction RIÊNG** và **commit TRƯỚC**
///     khi ném ⇒ **vẫn đọc được** sau rollback của lô; **0** đơn vị danh mục bị ghi.
///   • Ràng buộc CHECK của `AddrCatalogSyncRuns` (`Outcome ∈ {0,1,2}`, `SchemaVersion ∈ {1,2}`, `SourceHash`
///     hex 64) — hàng rào DB chống ghi dòng lô sai nghĩa.
///   • `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code` `NULLS NOT DISTINCT` — chống **một mã lạ** ghi
///     hai dòng trong **cùng một lượt** khi `TenantId = NULL` (`ADDR-002` `D-234`, hạ tầng schema `W18-2`).
///
/// 📌 Test tầng app service (gọi thẳng `ImportDatasetAsync`, khẳng định `requiresNew: true`) ở
/// `test/eCommerce.Tests/AddressSync/AdministrativeUnitAppServiceImportTests.cs`. Tệp này chứng minh **DB
/// thật** giữ đúng hợp đồng khi nhiều câu lệnh nằm chung/khác transaction — phần mà fake in-memory không thấy.
/// </summary>
[Collection("postgres")]
public class PostgreSqlAddressSyncTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlAddressSyncTests(PostgreSqlSchemaFixture fixture) => _fixture = fixture;

    // ── TC-11: lô nạp NGUYÊN TỬ trên DB thật ────────────────────────────────

    [Fact]
    public async Task TC11_duong_FILE_loi_giua_chung_thi_ROLLBACK_0_dong()
    {
        var dau = "TC11F-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            // MỘT transaction: insert 2 đơn vị + dòng lô `DaApDung` (đúng thứ tự ImportDatasetAsync).
            // 📌 `Code` là `varchar(5)`; marker nhận diện nằm ở `Name` (varchar 256) ⇒ mã ngắn 5 ký tự.
            var maA = Ma5();
            await ChenDonViAsync(conn, tx, maA, dau + "-A");
            await ChenDonViAsync(conn, tx, Ma5(), dau + "-B", parent: maA);
            await ChenDongLoAsync(conn, tx, dau, (int)CatalogSyncOutcomeMap.DaApDung);

            // Lỗi giữa chừng (mô phỏng bước sau ném) ⇒ ROLLBACK toàn bộ.
            await tx.RollbackAsync();
        }

        Assert.Equal(0L, await DemDonViAsync(conn, dau));
        Assert.Equal(0L, await DemDongLoAsync(conn, dau));
    }

    [Fact]
    public async Task TC11_duong_JOB_insert_roi_loi_TRUOC_update_thi_ROLLBACK_0_dong()
    {
        var dau = "TC11J-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            // Job: InsertMany (đơn vị mới) → (lỗi TRƯỚC UpdateMany) ⇒ cả hai không được áp.
            await ChenDonViAsync(conn, tx, Ma5(), dau + "-MOI");
            await ChenDongLoAsync(conn, tx, dau, (int)CatalogSyncOutcomeMap.DaApDung);

            await tx.RollbackAsync();
        }

        Assert.Equal(0L, await DemDonViAsync(conn, dau));
        Assert.Equal(0L, await DemDongLoAsync(conn, dau));
    }

    // ── TC-21(f) / TC-24(e): dòng TỪ CHỐI ở transaction RIÊNG ⇒ CÒN ĐỌC ĐƯỢC ──

    [Fact]
    public async Task TC21f_TC24e_dong_TU_CHOI_o_transaction_rieng_CON_DOC_DUOC_sau_rollback()
    {
        var dau = "TC21F-" + Guid.NewGuid().ToString("N")[..8];
        var hash = new string('a', 64);

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        // 1) Lượt lô THẤT BẠI (đổi cha) — transaction của lô bị rollback.
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenDonViAsync(conn, tx, Ma5(), dau + "-XA", parent: "01");
            await tx.RollbackAsync();
        }

        // 2) Dòng lô TỪ CHỐI ghi ở transaction RIÊNG, commit TRƯỚC khi ném (D-78 §5.5 bước 7).
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenDongLoAsync(
                conn, tx, dau, (int)CatalogSyncOutcomeMap.BiChan,
                errorCode: "eCommerce:Address:0008", sourceHash: hash);
            await tx.CommitAsync();
        }

        // 🔑 0 đơn vị danh mục được áp; dòng từ chối ĐỌC ĐƯỢC (BR-009).
        Assert.Equal(0L, await DemDonViAsync(conn, dau));

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "select \"Outcome\", \"ErrorCode\", \"SourceHash\" from \"AddrCatalogSyncRuns\" where \"SourceName\" = $1";
        cmd.Parameters.AddWithValue(dau);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Dòng lô TỪ CHỐI biến mất sau rollback ⇒ mất dấu vết (BR-009).");
        Assert.Equal(1, reader.GetInt32(0));                 // BiChan
        Assert.Equal("eCommerce:Address:0008", reader.GetString(1));
        Assert.Equal(hash, reader.GetString(2));
    }

    // ── Ràng buộc CHECK của sổ lô ───────────────────────────────────────────

    [Theory]
    [InlineData(3, 0, null)]                       // Outcome ngoài {0,1,2}
    [InlineData(0, 3, null)]                       // SchemaVersion ngoài {1,2}
    [InlineData(0, 0, "KHONG-PHAI-HEX")]           // SourceHash sai định dạng
    public async Task Check_AddrCatalogSyncRuns_chan_dong_lo_sai_nghia(int outcome, int schemaVersion, string? hash)
    {
        var dau = "TCCHK-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChenDongLoAsync(
            conn, null, dau, outcome, schemaVersion: schemaVersion, sourceHash: hash));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    // ── UX_AddrCarrierSyncRunUnmappedCodes: một mã lạ / một lượt (NND) ──────

    [Fact]
    public async Task UX_UnmappedCodes_chan_MA_LA_TRUNG_trong_CUNG_luot_khi_TenantId_NULL()
    {
        var dau = "UXU-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        var runId = Guid.NewGuid();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenLuotHangAsync(conn, tx, runId, dau);
            await ChenMaLaAsync(conn, tx, runId, "GHN-CODE-1", thu: "a");
            await tx.CommitAsync();
        }

        // Cùng lượt + cùng mã (Tenant NULL) ⇒ DB chặn (NULLS NOT DISTINCT).
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "insert into \"AddrCarrierSyncRunUnmappedCodes\" " +
                "(\"Id\",\"TenantId\",\"SyncRunId\",\"CarrierCode\",\"CarrierLevel\",\"CarrierAddressCode\"," +
                "\"CarrierAddressName\",\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\") " +
                "values ($1, null, $2, 'GHN', 'Phuong', 'GHN-CODE-1', 'Tên', '{}', $3, now())";
            cmd.Parameters.AddWithValue(Guid.NewGuid());
            cmd.Parameters.AddWithValue(runId);
            cmd.Parameters.AddWithValue("b");
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505

        // Đối chứng: mã KHÁC trong cùng lượt HỢP LỆ (index không quá chặt).
        await ChenMaLaAsync(conn, null, runId, "GHN-CODE-2", thu: "c");
    }

    [Fact]
    public async Task UX_UnmappedCodes_cho_phep_CUNG_ma_o_HAI_luot_khac_nhau()
    {
        var dau = "UXU2-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        var run1 = Guid.NewGuid();
        var run2 = Guid.NewGuid();

        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenLuotHangAsync(conn, tx, run1, dau + "-1");
            await ChenLuotHangAsync(conn, tx, run2, dau + "-2");
            await tx.CommitAsync();
        }

        await ChenMaLaAsync(conn, null, run1, "GHN-CODE-1", thu: "a");
        await ChenMaLaAsync(conn, null, run2, "GHN-CODE-1", thu: "b");   // khác lượt ⇒ được phép

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "select count(*) from \"AddrCarrierSyncRunUnmappedCodes\" where \"SyncRunId\" in ($1, $2)";
        cmd.Parameters.AddWithValue(run1);
        cmd.Parameters.AddWithValue(run2);

        Assert.Equal(2L, (long)(await cmd.ExecuteScalarAsync())!);
    }

    // ── D-92 / NFR-006: worker chạy ngữ cảnh HOST ⇒ 3 bảng đều TenantId NULL ──────────────────

    [Fact]
    public async Task TC19_moi_dong_ANH_XA_LO_DONG_CON_cua_luot_worker_deu_TenantId_NULL()
    {
        var dau = "TC19-" + Guid.NewGuid().ToString("N")[..8];
        var maAnhXa = "P1-" + Guid.NewGuid().ToString("N")[..8];
        var runId = Guid.NewGuid();

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        // Mô phỏng ghi của `CarrierAddressSyncWorker` (ngữ cảnh host ⇒ `tenantId: null`, `D-92`):
        // 1 ánh xạ + 1 dòng lô + 1 dòng con trong CÙNG một lượt.
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenAnhXaAsync(conn, tx, maAnhXa, dau);
            await ChenLuotHangAsync(conn, tx, runId, dau);
            await ChenMaLaAsync(conn, tx, runId, "P1", "a");
            await tx.CommitAsync();
        }

        // 🔒 Bất biến `D-92`: dữ liệu dùng chung ở host ⇒ **0** dòng ghi bởi lượt này có TenantId khác NULL.
        Assert.Equal(0L, await DemTenantKhacNullAsync(conn, maAnhXa, runId));
        Assert.Equal(1L, await DemAnhXaAsync(conn, maAnhXa));
        Assert.Equal(1L, await DemLoAsync(conn, runId));
        Assert.Equal(1L, await DemDongConAsync(conn, runId, "P1"));
    }

    // ── tiện ích chèn dữ liệu (cột lấy từ `Assets/schema.sql` sinh từ migration thật) ──────────

    private enum CatalogSyncOutcomeMap { DaApDung = 0, BiChan = 1, Loi = 2 }

    /// <summary>Mã 5 ký tự (cột `Code` là `varchar(5)`), đủ ngẫu nhiên để không đụng unique index.</summary>
    private static string Ma5() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static async Task ChenDonViAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string code, string name, string? parent = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            insert into "AddrAdministrativeUnits"
                ("Id","TenantId","SchemaVersion","Level","Code","Name","ParentCode","IsActive",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, 2, 2, $2, $3, $4, true, '{}', 's', now(), false)
            """;
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(code);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue((object?)parent ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ChenDongLoAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string sourceName, int outcome,
        int schemaVersion = 2, string? errorCode = null, string? sourceHash = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            insert into "AddrCatalogSyncRuns"
                ("Id","TenantId","SchemaVersion","Trigger","SourceName","SourceHash","StartedAtUtc","ImportedAtUtc",
                 "ImportedBy","Outcome","ErrorCode","ErrorType","SoThemMoi","SoDoiTen","SoMoLai","SoDong",
                 "ExtraProperties","ConcurrencyStamp","CreationTime")
            values ($1, null, $2, 1, $3, $4, now(), now(), 'user:qa', $5, $6, null, 0, 0, 0, 0,
                    '{}', 's', now())
            """;
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(schemaVersion);
        cmd.Parameters.AddWithValue(sourceName);
        cmd.Parameters.AddWithValue((object?)sourceHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue(outcome);
        cmd.Parameters.AddWithValue((object?)errorCode ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ChenLuotHangAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, Guid runId, string nguon)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            insert into "AddrCarrierSyncRuns"
                ("Id","TenantId","CarrierCode","Source","SourceHash","StartedAtUtc","FinishedAtUtc","TriggeredBy",
                 "Outcome","ErrorCode","ErrorType","SoMaNhan","SoCapNhatTen","SoDanhDauNghiNgo","SoCanNguoiAnhXa",
                 "MauCanNguoiAnhXa","ExtraProperties","ConcurrencyStamp","CreationTime")
            values ($1, null, 'GHN', $2, null, now(), now(), 'user:qa', 0, null, null, 0, 0, 0, 0,
                    null, '{}', 's', now())
            """;
        cmd.Parameters.AddWithValue(runId);
        cmd.Parameters.AddWithValue(nguon);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ChenMaLaAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, Guid runId, string ma, string thu)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            insert into "AddrCarrierSyncRunUnmappedCodes"
                ("Id","TenantId","SyncRunId","CarrierCode","CarrierLevel","CarrierAddressCode",
                 "CarrierAddressName","ExtraProperties","ConcurrencyStamp","CreationTime")
            values ($1, null, $2, 'GHN', 'Phuong', $3, 'Tên', '{}', $4, now())
            """;
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(runId);
        cmd.Parameters.AddWithValue(ma);
        cmd.Parameters.AddWithValue(thu);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> DemDonViAsync(NpgsqlConnection conn, string dau)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(*) from \"AddrAdministrativeUnits\" where \"Name\" like $1";
        cmd.Parameters.AddWithValue(dau + "%");
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> DemDongLoAsync(NpgsqlConnection conn, string sourceName)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(*) from \"AddrCatalogSyncRuns\" where \"SourceName\" = $1";
        cmd.Parameters.AddWithValue(sourceName);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ChenAnhXaAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string maHang, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            insert into "AddrCarrierMappings"
                ("Id","TenantId","CarrierCode","CarrierLevel","CarrierAddressCode","CarrierAddressName",
                 "OurAdministrativeUnitId","MatchMethod","Status","Source","SyncedAtUtc",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, 'GHN', 'Phuong', $2, $3, $4, 0, 1, 'test', now(), '{}', 's', now(), false)
            """;
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(maHang);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Đếm tổng số dòng của lượt (ánh xạ + lô + dòng con) có `TenantId IS NOT NULL`.</summary>
    private static async Task<long> DemTenantKhacNullAsync(
        NpgsqlConnection conn, string maHang, Guid runId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select
              (select count(*) from "AddrCarrierMappings"
                 where "CarrierAddressCode" = $1 and "TenantId" is not null)
            + (select count(*) from "AddrCarrierSyncRuns"
                 where "Id" = $2 and "TenantId" is not null)
            + (select count(*) from "AddrCarrierSyncRunUnmappedCodes"
                 where "SyncRunId" = $2 and "TenantId" is not null)
            """;
        cmd.Parameters.AddWithValue(maHang);
        cmd.Parameters.AddWithValue(runId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> DemAnhXaAsync(NpgsqlConnection conn, string maHang)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(*) from \"AddrCarrierMappings\" where \"CarrierAddressCode\" = $1";
        cmd.Parameters.AddWithValue(maHang);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> DemLoAsync(NpgsqlConnection conn, Guid runId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(*) from \"AddrCarrierSyncRuns\" where \"Id\" = $1";
        cmd.Parameters.AddWithValue(runId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> DemDongConAsync(NpgsqlConnection conn, Guid runId, string ma)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "select count(*) from \"AddrCarrierSyncRunUnmappedCodes\" where \"SyncRunId\" = $1 and \"CarrierAddressCode\" = $2";
        cmd.Parameters.AddWithValue(runId);
        cmd.Parameters.AddWithValue(ma);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}

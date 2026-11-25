using System.Diagnostics;
using Npgsql;
using Xunit.Abstractions;

namespace eCommerce.IntegrationTests;

/// <summary>
/// **`ADDR-001` NFR-001 — p95 tra xã của tỉnh lớn nhất, đường NGUỘI (chưa cache) &lt; 100 ms** trên
/// PostgreSQL 17 thật. Đo **câu truy vấn đúng hình dạng** mà `AdministrativeUnitAppService.LayConAsync` phát
/// (`SchemaVersion + ParentCode + IsActive`, sắp theo `Code`).
///
/// 📌 Đây là **số đo** (bằng chứng), dùng cùng fixture `postgres` với các test schema; KHÔNG phải cổng hiệu
/// năng của CI (máy CI tải khác nhau). Đường **NÓNG** (cache hit) đo ở
/// `test/eCommerce.Tests/AddressSync/AddressCatalogCacheTests.Nfr001_p95_tra_xa_NONG_cache_duoi_10ms`.
/// Cả hai số được ghi vào `docs/tests/ADDR-001-testplan.md`.
/// </summary>
[Collection("postgres")]
public class Nfr001AddressP95Tests
{
    private readonly PostgreSqlSchemaFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Nfr001AddressP95Tests(PostgreSqlSchemaFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Nfr001_p95_tra_xa_tinh_lon_nhat_NGUOI_DB_duoi_100ms()
    {
        const int soXa = 200;                       // xấp xỉ tỉnh nhiều xã/phường nhất (TP.HCM ~168)
        var tinh = Ma5();
        var dau = "NFR-" + Guid.NewGuid().ToString("N")[..6];

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();

        // Seed 1 tỉnh + `soXa` xã thuộc tỉnh đó (cột Code là varchar(5)).
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await ChenDonViAsync(conn, tx, tinh, dau + "-TINH", parent: null);
            for (var i = 0; i < soXa; i++)
            {
                await ChenDonViAsync(conn, tx, Ma5(), dau + "-XA-" + i, parent: tinh);
            }

            await tx.CommitAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select "Id" from "AddrAdministrativeUnits"
            where "TenantId" is null and "SchemaVersion" = 2 and "ParentCode" = $1 and "IsActive"
            order by "Code"
            """;
        cmd.Parameters.AddWithValue(tinh);
        await cmd.PrepareAsync();

        // 10 lượt khởi động rồi 100 lượt đo (đọc hết resultset mỗi lượt).
        for (var i = 0; i < 10; i++)
        {
            await DemAsync(cmd);
        }

        var mau = new List<double>(100);
        for (var i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var dem = await DemAsync(cmd);
            sw.Stop();
            Assert.Equal(soXa, dem);
            mau.Add(sw.Elapsed.TotalMilliseconds);
        }

        var p95 = P95(mau);
        _output.WriteLine(
            $"NFR-001 NGUỘI (DB thật, {soXa} xã × 100 lượt): p95 = {p95:F3} ms · max = {mau.Max():F3} ms");

        Assert.True(p95 < 100.0, $"p95 nguội {p95:F3} ms ≥ 100 ms (NFR-001).");
    }

    private static async Task<int> DemAsync(NpgsqlCommand cmd)
    {
        var dem = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dem++;
        }

        return dem;
    }

    private static double P95(List<double> mau)
    {
        mau.Sort();
        var chiSo = (int)Math.Ceiling(0.95 * mau.Count) - 1;
        return mau[Math.Clamp(chiSo, 0, mau.Count - 1)];
    }

    private static string Ma5() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static async Task ChenDonViAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string code, string name, string? parent)
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
}

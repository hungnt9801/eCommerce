namespace eCommerce.IntegrationTests;

/// <summary>
/// **`REP-001` — HÀNG RÀO DB CỦA 8 BẢNG READ-MODEL `Rpt*`** (`REP-001` §5 / §8.1 `BR-003` · `TC-20`).
///
/// 🎯 VÌ SAO PHẢI CHẠY TRÊN PostgreSQL THẬT: `TenantId` nullable + index unique `NULLS NOT DISTINCT` là
/// **hành vi của DB**, không phải của EF model — unit test reflection chỉ chứng minh `IMultiTenant`, KHÔNG
/// chứng minh index chặn trùng khi `TenantId = NULL` (họ lỗi `C4/F1`: PostgreSQL coi hai `NULL` là KHÁC
/// nhau nếu thiếu `NULLS NOT DISTINCT` ⇒ index mất tác dụng **im lặng**).
/// 📌 Đo **HAI CHIỀU**: index đích danh tồn tại + cột `TenantId` cho phép `NULL` (chạy một tenant ở host).
/// </summary>
[Collection("postgres")]
public class PostgreSqlWp26ReadModelNndTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlWp26ReadModelNndTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> DemAsync(string sql, params object[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);

        for (var i = 0; i < thamSo.Length; i++)
        {
            cmd.Parameters.AddWithValue(thamSo[i]);
        }

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public static TheoryData<string, string> BangVaIndex => new()
    {
        { "RptFactSalesLines", "IX_RptFactSalesLines_TenantId_Nguon_DongNguon" },
        { "RptAggDailySales", "IX_RptAggDailySales_TenantId_Ngay_Nguon" },
        { "RptFactInventorySnapshots", "IX_RptFactInventorySnapshots_TenantId_Ngay_Kho_Sku" },
        { "RptWatermarks", "IX_RptWatermarks_TenantId_Nguon" },
        { "RptDimDates", "IX_RptDimDates_TenantId_Ngay" },
        { "RptDimProducts", "IX_RptDimProducts_TenantId_Sku" },
        { "RptDimStores", "IX_RptDimStores_TenantId_Kho" },
        { "RptDimStaffs", "IX_RptDimStaffs_TenantId_NhanVien" }
    };

    [Theory]
    [MemberData(nameof(BangVaIndex))]
    public async Task TC20_index_unique_tenant_dung_NULLS_NOT_DISTINCT(string bang, string index)
    {
        var co = await DemAsync(
            "select count(*) from pg_indexes where schemaname = 'public' "
            + "and indexname = $1 and indexdef like '%NULLS NOT DISTINCT%'",
            index);

        Assert.Equal(1L, co);

        // 📌 Lưới an toàn: bảng tương ứng phải TỒN TẠI (nếu không, index có thể thuộc bảng khác).
        var bangCo = await DemAsync(
            "select count(*) from information_schema.tables where table_schema = 'public' and table_name = $1",
            bang);

        Assert.Equal(1L, bangCo);
    }

    [Theory]
    [MemberData(nameof(BangVaIndex))]
    public async Task TC20_cot_TenantId_cho_phep_NULL(string bang, string index)
    {
        _ = index;

        var nullable = await DemAsync(
            "select count(*) from information_schema.columns "
            + "where table_schema = 'public' and table_name = $1 and column_name = 'TenantId' and is_nullable = 'YES'",
            bang);

        Assert.Equal(1L, nullable);
    }

    /// <summary>
    /// **BR-003 / TC-06 (lớp DB)** — cùng một khoá nghiệp vụ ở **hai tenant** phải cùng tồn tại (index
    /// multi-tenant gồm `TenantId`, không chặn oan xuyên tenant). Đây là chiều NGƯỢC của `TC-18`.
    /// </summary>
    [Fact]
    public async Task TC06_hai_tenant_cung_khoa_nghiep_vu__cung_ton_tai()
    {
        var sourceLineId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await ChenDongSuKienAsync(a, sourceLineId);
        await ChenDongSuKienAsync(b, sourceLineId);

        var dem = await DemAsync(
            "select count(*) from \"RptFactSalesLines\" where \"SourceLineId\" = $1", sourceLineId);

        Assert.Equal(2L, dem);
    }

    private async Task ChenDongSuKienAsync(Guid tenantId, Guid sourceLineId)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RptFactSalesLines"
                ("Id","TenantId","BusinessDate","Nguon","DocumentId","SourceLineId","DocumentNumber",
                 "WarehouseId","StaffId","CustomerId","Sku","ProductName","Quantity","UnitPrice",
                 "LineDiscount","LineAmount","VatPercent","VatAmount","OccurredAtUtc","CreationTime","CreatorId")
            values ($1,$2,date '2026-09-15',0,$3,$4,'POS-TC06',
                    null,null,null,'SKU-TC06',null,1,10,0,10,null,0,now(),now(),null)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(tenantId);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(sourceLineId);

        await cmd.ExecuteNonQueryAsync();
    }
}

using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 **`WP-08` — HÀNG RÀO DB CỦA READ-MODEL BÁO CÁO** (`Rpt*`).
///
/// 🎯 **VÌ SAO CẦN TEST Ở TẦNG PostgreSQL THẬT (không đủ unit test):** DoD của `WP-08` là
/// *"job chạy lại 2 lần không nhân đôi dữ liệu"*. Tầng ứng dụng `DELETE` theo `(ngày, nguồn)` rồi `INSERT`,
/// nhưng **hai lượt chạy chồng nhau** hoặc một đường ghi tắt (SQL tay/backfill viết vội) vẫn chèn trùng được
/// ⇒ với `TenantId = NULL` (đang chạy MỘT tenant), nếu index thiếu `NULLS NOT DISTINCT` thì PostgreSQL coi
/// hai `NULL` là **KHÁC nhau** ⇒ index **mất tác dụng IM LẶNG** ⇒ **doanh thu nhân đôi** đúng những dòng bị
/// trùng, và không có lỗi nào để lần ra. 📌 Đây đúng họ lỗi `C4/F1` mà cả bộ test này tồn tại để chặn.
///
/// 🚨 Đo **HAI CHIỀU**: giá trị TRÙNG phải bị chặn · giá trị **KHÁC** (nguồn khác / dòng khác / tenant khác)
/// phải lọt — nếu chỉ đo một chiều thì một index "chặn tất cả" cũng làm test xanh.
/// </summary>
[Collection("postgres")]
public class PostgreSqlWp08BaoCaoTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlWp08BaoCaoTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> DemAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Chèn một dòng sự kiện doanh thu (tối thiểu các cột NOT NULL).</summary>
    private async Task ChenDongSuKienAsync(Guid? tenantId, int nguon, Guid sourceLineId, decimal lineAmount)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RptFactSalesLines"
                ("Id","TenantId","BusinessDate","Nguon","DocumentId","SourceLineId","DocumentNumber",
                 "WarehouseId","StaffId","CustomerId","Sku","ProductName","Quantity","UnitPrice",
                 "LineDiscount","LineAmount","VatPercent","VatAmount","OccurredAtUtc","CreationTime","CreatorId")
            values ($1, $2, date '2026-11-04', $3, $4, $5, 'POS-N8-TEST',
                    null, null, null, 'SKU-N8', null, 1, $6,
                    0, $6, null, 0, now(), now(), null)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(nguon);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(sourceLineId);
        cmd.Parameters.AddWithValue(lineAmount);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenDongTongHopAsync(Guid? tenantId, int nguon, decimal revenue)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RptAggDailySales"
                ("Id","TenantId","BusinessDate","Nguon","DocumentCount","LineCount","Quantity",
                 "LineAmountTotal","DocumentDiscountTotal","Revenue","VatTotal","AggregatedAtUtc",
                 "CreationTime","CreatorId")
            values ($1, $2, date '2026-11-04', $3, 1, 1, 1, $4, 0, $4, 0, now(), now(), null)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(nguon);
        cmd.Parameters.AddWithValue(revenue);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenMocAsync(Guid? tenantId, int nguon)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "RptWatermarks"
                ("Id","TenantId","Nguon","LastAggregatedDate","LastRunAtUtc","LastRowCount",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","CreatorId","IsDeleted")
            values ($1, $2, $3, date '2026-11-04', now(), 12, '{}', 'stamp-n8', now(), null, false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(nguon);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Chan_HAI_dong_su_kien_CUNG_dong_nguon_khi_TenantId_NULL()
    {
        // 🚨 Đây là ca "job chạy lại làm doanh thu nhân đôi": cùng một dòng chứng từ ghi hai lần.
        var dongNguon = Guid.NewGuid();

        await ChenDongSuKienAsync(null, 0, dongNguon, 100_000m);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDongSuKienAsync(null, 0, dongNguon, 100_000m));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task Cho_phep_CUNG_dong_nguon_nhung_KHAC_nguon__va_KHAC_tenant()
    {
        // 🔒 Chiều NGƯỢC LẠI: khoá là `(TenantId, Nguon, SourceLineId)` — một `Guid` trùng giữa hoá đơn
        // POS và đơn hàng (dữ liệu nhập tay/seed) **không** được chặn oan; và hai tenant có thể trùng.
        var dongNguon = Guid.NewGuid();

        await ChenDongSuKienAsync(null, 0, dongNguon, 10m);
        await ChenDongSuKienAsync(null, 1, dongNguon, 10m);
        await ChenDongSuKienAsync(Guid.NewGuid(), 0, dongNguon, 10m);

        var soDong = await DemAsync(
            $"select count(*) from \"RptFactSalesLines\" where \"SourceLineId\" = '{dongNguon}'");

        Assert.Equal(3L, soDong);
    }

    [Fact]
    public async Task Chan_HAI_dong_tong_hop_CUNG_ngay_CUNG_nguon_khi_TenantId_NULL()
    {
        await ChenDongTongHopAsync(null, 0, 50_000m);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDongTongHopAsync(null, 0, 50_000m));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task Chan_HAI_moc_CUNG_nguon_khi_TenantId_NULL()
    {
        // Hai mốc cho cùng nguồn ⇒ job đọc dòng nào cũng "đúng" ⇒ chạy lại từ đầu mỗi lượt (hoặc không bao
        // giờ tiến) mà không có lỗi nào.
        await ChenMocAsync(null, 2);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChenMocAsync(null, 2));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task CHECK_chan_nguon_TonKho_trong_bang_doanh_thu()
    {
        // 📌 `Nguon = 2` (`TonKho`) **không** phải nguồn doanh thu — nếu lọt qua DB thì báo cáo có một dòng
        // "bán từ tồn kho" không tồn tại và không ai biết vì sao tổng lệch.
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDongSuKienAsync(null, 2, Guid.NewGuid(), 1m));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    [Fact]
    public async Task CHECK_chan_doanh_thu_AM_trong_bang_tong_hop()
    {
        // Doanh thu âm = ghi sai dấu; DB chặn cả đường ghi tắt.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChenDongTongHopAsync(null, 1, -1m));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }
}

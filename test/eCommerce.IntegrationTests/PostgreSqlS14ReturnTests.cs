using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 **`S14` — HÀNG RÀO DB CHO PHIẾU TRẢ HÀNG** (`SAL-006`): mã phiếu · idempotency · một dòng đơn một lần ·
/// một lịch sử/phiên bản · trả vượt số đã mua.
/// 🚨 Đo **HAI CHIỀU**: giá trị SAI phải bị chặn, giá trị **HỢP LỆ** phải lọt (tenant khác · trả đủ · `returned = quantity`).
/// </summary>
[Collection("postgres")]
public class PostgreSqlS14ReturnTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlS14ReturnTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> DemAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<Guid> ChenPhieuAsync(Guid? tenantId, string soPhieu, string khoa)
    {
        var idPhieu = Guid.NewGuid();

        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "OrdReturns"
                ("Id","TenantId","OrderId","ReturnNumber","ChannelCode","Status","GoodsStatus",
                 "ReasonCode","ReasonText","WarehouseId","RefundMethod","RefundedAmount","RefundCurrency",
                 "RequestedByActorType","RequestedByActorId","IdempotencyKey","PayloadHash","Version",
                 "CorrelationId","ReceivedAtUtc","ClosedAtUtc",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, 'web', 0, 0, 'HANG_LOI', null, $5, 2, 0, 'VND',
                    'Staff', null, $6, repeat('a', 64), 0, '', null, null,
                    '{}', 'stamp-s14', now(), false)
            """);

        cmd.Parameters.AddWithValue(idPhieu);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(soPhieu);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(khoa);

        await cmd.ExecuteNonQueryAsync();

        return idPhieu;
    }

    [Fact]
    public async Task Chan_HAI_phieu_CUNG_ma_va_CUNG_khoa_idempotency_khi_TenantId_NULL()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];

        await ChenPhieuAsync(null, $"RT-{duoi}", $"K9S14-{duoi}");

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            (await Assert.ThrowsAsync<PostgresException>(
                () => ChenPhieuAsync(null, $"RT-{duoi}", $"K9S14-KHAC-{duoi}"))).SqlState);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            (await Assert.ThrowsAsync<PostgresException>(
                () => ChenPhieuAsync(null, $"RT-KHAC-{duoi}", $"K9S14-{duoi}"))).SqlState);
    }

    [Fact]
    public async Task Cho_phep_CUNG_ma_phieu_nhung_KHAC_tenant()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var soPhieu = $"RT-TENANT-{duoi}";

        await ChenPhieuAsync(Guid.NewGuid(), soPhieu, $"K9S14-A-{duoi}");
        await ChenPhieuAsync(Guid.NewGuid(), soPhieu, $"K9S14-B-{duoi}");

        var so = await DemAsync($"select count(*) from \"OrdReturns\" where \"ReturnNumber\" = '{soPhieu}'");

        Assert.Equal(2L, so);
    }

    [Fact]
    public async Task Chan_LICH_SU_khong_doi_trang_thai__va_cho_phep_khi_DOI()
    {
        // FK `OrdReturnHistory → OrdReturns` ⇒ phải có phiếu cha.
        var returnId = await ChenPhieuAsync(
            null, $"RT-HS-{Guid.NewGuid().ToString("N")[..6]}", $"K9S14-HS-{Guid.NewGuid().ToString("N")[..6]}");

        async Task ChenAsync(int tu, int den)
        {
            await using var cmd = _fixture.DataSource.CreateCommand("""
                insert into "OrdReturnHistory"
                    ("Id","TenantId","ReturnId","Axis","FromStatus","ToStatus","ActorType","ActorId",
                     "ReasonCode","ReasonText","CorrelationId","EventId","VersionAfter","OccurredAtUtc",
                     "CreationTime","IsDeleted")
                values ($1, null, $2, 'Lifecycle', $3, $4, 'Staff', null,
                        'DUYET', null, '', $5, 1, now(), now(), false)
                """);
            cmd.Parameters.AddWithValue(Guid.NewGuid());
            cmd.Parameters.AddWithValue(returnId);
            cmd.Parameters.AddWithValue(tu);
            cmd.Parameters.AddWithValue(den);
            cmd.Parameters.AddWithValue(Guid.NewGuid());
            await cmd.ExecuteNonQueryAsync();
        }

        Assert.Equal(
            PostgresErrorCodes.CheckViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => ChenAsync(1, 1))).SqlState);

        await ChenAsync(0, 1);

        var so = await DemAsync($"select count(*) from \"OrdReturnHistory\" where \"ReturnId\" = '{returnId}'");

        Assert.Equal(1L, so);
    }

    [Fact]
    public async Task Chan_dong_don_TRA_VUOT_so_da_mua__va_cho_phep_BANG_dung()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var orderId = Guid.NewGuid();

        // FK `OrdOrderLines → OrdOrders` ⇒ phải có đơn cha (dữ liệu tối thiểu, đúng các cột NOT NULL).
        await using (var don = _fixture.DataSource.CreateCommand("""
            insert into "OrdOrders"
                ("Id","TenantId","Number","ChannelCode","Status","PaymentStatus","FulfillmentStatus",
                 "ReconciliationStatus","ReturnStatus","IsCancelled","FulfillmentVersion","PaymentVersion",
                 "ReconciliationVersion","ClosureVersion","RowVersion","Currency","Subtotal","DiscountTotal",
                 "TaxTotal","ShippingTotal","GrandTotal","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, $2, 'web', 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 'VND', 0, 0, 0, 0, 0,
                    '{}', 'stamp-s14-don', now(), false)
            """))
        {
            don.Parameters.AddWithValue(orderId);
            don.Parameters.AddWithValue($"S14-DON-{duoi}");
            await don.ExecuteNonQueryAsync();
        }

        async Task ChenDongAsync(Guid id, int daMua, int daTra)
        {
            await using var cmd = _fixture.DataSource.CreateCommand("""
                insert into "OrdOrderLines"
                    ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount",
                     "ReturnedQuantity","CreationTime","IsDeleted")
                values ($1, null, $2, $3, $4, 100000, 0, 0, $5, now(), false)
                """);
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(orderId);
            cmd.Parameters.AddWithValue($"S14-{duoi}");
            cmd.Parameters.AddWithValue(daMua);
            cmd.Parameters.AddWithValue(daTra);
            await cmd.ExecuteNonQueryAsync();
        }

        Assert.Equal(
            PostgresErrorCodes.CheckViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => ChenDongAsync(Guid.NewGuid(), 3, 4))).SqlState);

        await ChenDongAsync(Guid.NewGuid(), 3, 3);   // trả đúng bằng số đã mua ⇒ hợp lệ

        var so = await DemAsync($"select count(*) from \"OrdOrderLines\" where \"OrderId\" = '{orderId}'");

        Assert.Equal(1L, so);
    }
}

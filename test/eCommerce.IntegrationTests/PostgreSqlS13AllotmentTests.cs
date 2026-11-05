using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 **`S13` — HÀNG RÀO DB CHO NGÂN SÁCH TỒN THEO KÊNH** (`INV-003`).
///
/// 🚨 Đo **HAI CHIỀU** ở mọi bất biến: giá trị SAI phải bị chặn; giá trị **HỢP LỆ** phải **LỌT**
/// (kênh khác · tenant khác · `Reserved = Allotted` · `Frozen` có đủ dấu vết · bút toán **không** khoá).
/// 📌 Một index quá chặt cũng là lỗi: nó chặn nghiệp vụ có thật (ví dụ kênh bán hết ngân sách).
/// </summary>
[Collection("postgres")]
public class PostgreSqlS13AllotmentTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlS13AllotmentTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> DemAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task ChenNganSachAsync(
        Guid? tenantId,
        Guid warehouseId,
        string sku,
        string kenh,
        int allotted,
        int reserved,
        int status = 0,
        DateTime? frozenAt = null,
        Guid? frozenBy = null,
        string? releaseReason = null)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "InvStockAllotments"
                ("Id","TenantId","WarehouseId","Sku","ChannelCode","AllottedQuantity","ReservedQuantity",
                 "Priority","Status","FrozenAtUtc","FrozenByUserId","ReleasedAtUtc","ReleaseReason",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, $5, $6, $7, 0, $8, $9, $10, null, $11, '{}', 'stamp-ns', now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(warehouseId);
        cmd.Parameters.AddWithValue(sku);
        cmd.Parameters.AddWithValue(kenh);
        cmd.Parameters.AddWithValue(allotted);
        cmd.Parameters.AddWithValue(reserved);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue((object?)frozenAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)frozenBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)releaseReason ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Ngan_sach_chan_HAI_dong_CUNG_kho_sku_kenh_khi_TenantId_NULL()
    {
        // 🚨 Mất `NULLS NOT DISTINCT` ⇒ ATP của một kênh có **hai** nguồn ⇒ *"còn bán được bao nhiêu"*
        // **không tất định**, và 📌 không có lỗi nào để lần ra.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();
        var sku = $"S13-NS-{duoi}";

        await ChenNganSachAsync(null, kho, sku, "shopee", 10, 0);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenNganSachAsync(null, kho, sku, "shopee", 5, 0));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task Ngan_sach_CHO_PHEP_cung_kho_sku_khac_KENH__va_cung_bo_ba_khac_TENANT()
    {
        // 🔒 Đối chứng: index **không** được biến thành "mỗi (kho, SKU) chỉ một ngân sách" —
        // mỗi kênh **phải** có ngân sách riêng (đó là toàn bộ mục đích `INV-003`).
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();
        var sku = $"S13-NS2-{duoi}";

        await ChenNganSachAsync(null, kho, sku, "shopee", 10, 0);
        await ChenNganSachAsync(null, kho, sku, "lazada", 10, 0);
        await ChenNganSachAsync(Guid.NewGuid(), kho, sku, "shopee", 10, 0);

        var so = await DemAsync($"select count(*) from \"InvStockAllotments\" where \"Sku\" = '{sku}'");

        Assert.Equal(3L, so);
    }

    [Fact]
    public async Task Ngan_sach_CHECK_chan_Reserved_VUOT_Allotted__va_cho_phep_BANG_nhau()
    {
        // 🚨 `Reserved <= Allotted` = **hàng rào cuối** cho ATP không âm (`BR-02`).
        // 📌 Chiều ngược: `Reserved = Allotted` (ATP = 0) là **hợp lệ** — kênh đã bán hết ngân sách.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenNganSachAsync(null, kho, $"S13-CK-{duoi}", "shopee", 5, 6));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514

        await ChenNganSachAsync(null, kho, $"S13-CK-OK-{duoi}", "shopee", 5, 5);

        var so = await DemAsync($"select count(*) from \"InvStockAllotments\" where \"Sku\" = 'S13-CK-OK-{duoi}'");

        Assert.Equal(1L, so);
    }

    [Fact]
    public async Task Ngan_sach_CHECK_chan_dong_bang_THIEU_dau_vet__va_cho_phep_khi_CO_du()
    {
        // 🚨 `BR-12`: `Frozen` mà thiếu `FrozenAtUtc`/`FrozenByUserId` ⇒ không trả lời được **ai/khi nào**
        // đã ngừng bán kênh. 📌 Chiều ngược: có đủ dấu vết thì **phải** lọt.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenNganSachAsync(null, kho, $"S13-FZ-{duoi}", "shopee", 5, 0, status: 1));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);

        await ChenNganSachAsync(
            null, kho, $"S13-FZ-OK-{duoi}", "shopee", 5, 0, status: 1,
            frozenAt: DateTime.UtcNow, frozenBy: Guid.NewGuid());

        var so = await DemAsync($"select count(*) from \"InvStockAllotments\" where \"Sku\" = 'S13-FZ-OK-{duoi}'");

        Assert.Equal(1L, so);
    }

    [Fact]
    public async Task Ngan_sach_CHECK_chan_NHA_thieu_ly_do__va_cho_phep_khi_co_ly_do()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenNganSachAsync(null, Guid.NewGuid(), $"S13-RL-{duoi}", "shopee", 0, 0, status: 2));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);

        await ChenNganSachAsync(
            null, Guid.NewGuid(), $"S13-RL-OK-{duoi}", "shopee", 0, 0, status: 2,
            releaseReason: "Kênh ngừng hợp tác (kiểm chứng)");

        var so = await DemAsync($"select count(*) from \"InvStockAllotments\" where \"Sku\" = 'S13-RL-OK-{duoi}'");

        Assert.Equal(1L, so);
    }

    [Fact]
    public async Task So_cai_ngan_sach_chan_hai_dong_CUNG_IdempotencyKey__nhung_KHONG_chan_dong_khong_khoa()
    {
        // 🚨 `BR-09` — khoá idempotency của biên vào là **hàng rào cuối** chống **nhân đôi ngân sách**
        // (import lại cùng tệp / retry). 📌 Chiều ngược QUAN TRỌNG: bút toán **không** có khoá (hệ thống/job
        // sinh) phải ghi được **nhiều** dòng — nếu unique không có bộ lọc `IS NOT NULL` thì toàn hệ thống
        // chỉ được phép có **một** dòng như vậy (đúng lỗi đã trả giá với `InvStockLedger`).
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();
        var nsId = Guid.NewGuid();
        var sku = $"S13-SC-{duoi}";
        var khoa = $"S13-IDEM-{duoi}";

        async Task ChenAsync(Guid id, string? idem)
        {
            await using var cmd = _fixture.DataSource.CreateCommand("""
                insert into "InvStockAllotmentLedgers"
                    ("Id","TenantId","StockAllotmentId","WarehouseId","Sku","ChannelCode","QuantityDelta",
                     "AllottedAfter","Reason","IdempotencyKey","OccurredAtUtc",
                     "CreationTime","IsDeleted")
                values ($1, null, $2, $3, $4, 'shopee', 5, 5, 0, $5, now(), now(), false)
                """);
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(nsId);
            cmd.Parameters.AddWithValue(kho);
            cmd.Parameters.AddWithValue(sku);
            cmd.Parameters.AddWithValue((object?)idem ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        await ChenAsync(Guid.NewGuid(), khoa);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChenAsync(Guid.NewGuid(), khoa));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);

        await ChenAsync(Guid.NewGuid(), null);
        await ChenAsync(Guid.NewGuid(), null);

        var soKhongKhoa = await DemAsync(
            $"select count(*) from \"InvStockAllotmentLedgers\" where \"Sku\" = '{sku}' and \"IdempotencyKey\" is null");

        Assert.Equal(2L, soKhongKhoa);
    }
}

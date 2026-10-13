using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// Kiểm chứng **schema thật** trên PostgreSQL 17 thật — chính những phép thử đã bắt được lỗi trong đợt
/// chạy thật, nay tự động hoá.
///
/// 🔒 Đây là **lưới an toàn chống tái phát**: mọi sửa đổi migration làm mất `NULLS NOT DISTINCT` hoặc
/// làm hỏng ràng buộc `EXCLUDE` sẽ **làm đỏ CI** thay vì âm thầm quay lại lỗi.
///
/// 📌 Mọi câu lệnh dùng **tham số** (`$1`, `$2`…) — không nối chuỗi, không phải escape.
/// </summary>
[Collection("postgres")]
public class PostgreSqlSchemaTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlSchemaTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    // ── tiện ích ────────────────────────────────────────────────────────────

    private async Task<long> DemAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string?> ChuoiAsync(string sql)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        return await cmd.ExecuteScalarAsync() as string;
    }

    /// <summary>Chèn một mức thuế suất. `ma` + `tenantId` là cặp bị ràng buộc unique.</summary>
    private async Task ChenMucThueAsync(Guid id, Guid? tenantId, string ma, decimal rate, string stamp)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "TaxRateSchedules"
                ("Id","TenantId","Code","Name","Rate","Kind","EffectiveFrom",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, $5, 1, date '2026-01-01', '{}', $6, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(ma);
        cmd.Parameters.AddWithValue("Mức thuế " + ma);
        cmd.Parameters.AddWithValue(rate);
        cmd.Parameters.AddWithValue(stamp);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Chèn một khung hiệu lực phân loại thuế. Ràng buộc EXCLUDE chống chồng khung.</summary>
    private async Task ChenPhanLoaiAsync(
        Guid id, Guid? tenantId, Guid targetId, DateOnly tu, DateOnly? den, string stamp)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "TaxClassifications"
                ("Id","TenantId","TargetType","TargetId","BaseRateCode","ReductionApplicable",
                 "EffectiveFrom","EffectiveTo","Status","Version",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, 0, $3, 'VAT10', true, $4, $5, 1, 1, '{}', $6, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(targetId);
        cmd.Parameters.AddWithValue(tu);
        cmd.Parameters.AddWithValue((object?)den ?? DBNull.Value);
        cmd.Parameters.AddWithValue(stamp);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── 1. Migration áp được trên PostgreSQL thật ───────────────────────────

    [Fact]
    public async Task Migration_ap_dung_duoc_va_co_du_bang()
    {
        var soBang = await DemAsync(
            "select count(*) from information_schema.tables where table_schema = 'public'");

        Assert.True(soBang > 40, $"Chỉ có {soBang} bảng — migration có vẻ KHÔNG chạy hết.");
    }

    [Fact]
    public async Task Extension_btree_gist_ton_tai()
    {
        // Bắt buộc cho ràng buộc EXCLUDE trên cột uuid + daterange.
        Assert.Equal(1L, await DemAsync("select count(*) from pg_extension where extname = 'btree_gist'"));
    }

    // ── 2. 🚨 HỒI QUY C4/F1 — unique index PHẢI chặn trùng khi TenantId NULL ─

    [Fact]
    public async Task F1_chan_trung_khi_TenantId_NULL()
    {
        // Đây ĐÚNG là lỗi đã xảy ra khi chạy thật: PostgreSQL coi NULL là DISTINCT
        // ⇒ 2 dòng cùng Code, cùng TenantId = NULL đều lọt vào DB.
        // Sau khi sửa bằng `NULLS NOT DISTINCT`, dòng thứ hai PHẢI bị chặn.
        await ChenMucThueAsync(Guid.NewGuid(), null, "F1-DUP", 0.08m, "f1-a");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenMucThueAsync(Guid.NewGuid(), null, "F1-DUP", 0.10m, "f1-b"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task F1_chan_trung_khi_TenantId_CO_gia_tri()
    {
        // Đối chứng: nhánh này vốn LUÔN đúng — giữ lại để bảo đảm bản sửa không làm hỏng nhánh có tenant.
        var tenant = Guid.NewGuid();

        await ChenMucThueAsync(Guid.NewGuid(), tenant, "F1-TENANT", 0.08m, "f1-c");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenMucThueAsync(Guid.NewGuid(), tenant, "F1-TENANT", 0.10m, "f1-d"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task F1_cho_phep_CUNG_ma_nhung_KHAC_tenant()
    {
        // Bổ sung: NULLS NOT DISTINCT KHÔNG được biến thành "cấm trùng mã toàn cục".
        var ma = "F1-KHAC-TENANT-" + Guid.NewGuid().ToString("N")[..6];

        await ChenMucThueAsync(Guid.NewGuid(), null, ma, 0.08m, "f1-e");
        await ChenMucThueAsync(Guid.NewGuid(), Guid.NewGuid(), ma, 0.08m, "f1-f");

        var soDong = await DemAsync(
            $"select count(*) from \"TaxRateSchedules\" where \"Code\" = '{ma}'");

        Assert.Equal(2L, soDong);
    }

    [Fact]
    public async Task F1_du_11_index_deu_dung_NULLS_NOT_DISTINCT()
    {
        // 🔒 BẤT BIẾN (ghi ở XC-004 Q6): mọi unique index trên entity IMultiTenant PHẢI có
        // `NULLS NOT DISTINCT`. Ai tạo migration mới mà quên ⇒ test này đỏ.
        Assert.Equal(11L, await DemAsync(
            "select count(*) from pg_indexes where schemaname = 'public' and indexdef like '%NULLS NOT DISTINCT%'"));

        // Kiểm tra ĐÍCH DANH để thông báo lỗi có ý nghĩa (tên index dài có thể bị PostgreSQL cắt ⇒ so theo tiền tố).
        string[] phaiCo =
        [
            "IX_TaxRateSchedules_TenantId_Code",
            "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr",
            "IX_CatProducts_TenantId_Code",
            "IX_CatProductVariants_TenantId_Sku",
            "IX_InvItems_TenantId_WarehouseId_Sku",
            "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId",
            "IX_OrdOrders_TenantId_Number",
            "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey",
            "IX_PayTransactions_TenantId_ProviderCode_MerchantReference",
            "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId",
            "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId"
        ];

        foreach (var ten in phaiCo)
        {
            var co = await DemAsync(
                $"select count(*) from pg_indexes where schemaname = 'public' and indexname like '{ten}%' and indexdef like '%NULLS NOT DISTINCT%'");

            Assert.True(co == 1L, $"Index '{ten}' KHÔNG có NULLS NOT DISTINCT ⇒ lỗi C4/F1 đã quay lại.");
        }
    }

    // ── 3. Ràng buộc EXCLUDE — chống chồng khung hiệu lực ──────────────────

    [Fact]
    public async Task Rang_buoc_EXCLUDE_ton_tai_voi_daterange_va_toan_tu_chong_nhau()
    {
        var dinhNghia = await ChuoiAsync(
            "select pg_get_constraintdef(oid) from pg_constraint where conname = 'EX_TaxClassifications_ChongChongKhung'");

        Assert.NotNull(dinhNghia);
        Assert.Contains("EXCLUDE USING gist", dinhNghia);
        Assert.Contains("daterange", dinhNghia);
        Assert.Contains("&&", dinhNghia);          // toán tử CHỒNG NHAU
        Assert.Contains("COALESCE", dinhNghia);    // TenantId NULL vẫn phải chống chồng
    }

    [Fact]
    public async Task EXCLUDE_chan_khung_thoi_gian_chong_nhau()
    {
        var targetId = Guid.NewGuid();

        await ChenPhanLoaiAsync(Guid.NewGuid(), null, targetId,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "ex-a");

        // Khung CHỒNG NHAU ⇒ phải bị chặn (23P01 exclusion_violation)
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenPhanLoaiAsync(Guid.NewGuid(), null, targetId,
                new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 31), "ex-b"));

        Assert.Equal(PostgresErrorCodes.ExclusionViolation, ex.SqlState);   // 23P01
    }

    [Fact]
    public async Task EXCLUDE_cho_phep_khung_thoi_gian_KHONG_chong_nhau()
    {
        var targetId = Guid.NewGuid();

        await ChenPhanLoaiAsync(Guid.NewGuid(), null, targetId,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "ex-c");

        // Khung LIỀN KỀ, không chồng ⇒ phải ĐƯỢC NHẬN
        await ChenPhanLoaiAsync(Guid.NewGuid(), null, targetId,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), "ex-d");

        var soDong = await DemAsync(
            $"select count(*) from \"TaxClassifications\" where \"TargetId\" = '{targetId}'");

        Assert.Equal(2L, soDong);
    }
}

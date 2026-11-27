using System.Text.RegularExpressions;
using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// `PRO-002` (`W24-4`) — hàng rào DB của **7 bảng `Pro*`** chỉ chứng minh được trên **PostgreSQL 17 thật**.
///
/// 🚨 VÌ SAO: EF Core **không** mô hình hoá `NULLS NOT DISTINCT` lẫn partial unique + `CHECK` phức hợp ⇒ migration
/// `Add_Promotion_Module` viết **SQL thô** cho 9 index và các `CHECK` ở §5.3 SRS. Nếu bước SQL thô bị mất/ghi sai,
/// PostgreSQL coi `NULL` là **DISTINCT** và `TenantId = NULL` (đang chạy 1 tenant) làm các hàng rào **mất tác dụng
/// IM LẶNG** ⇒ cộng dồn lượt (`UsedCount` +2), hai gốc cây, hai lượt cùng đơn… mà **không lỗi nào** để lần ra.
///
/// 📌 Phủ **phần DB** của:
///   • `TC-33` — `ProVoucherCodes` đúng **một** `CHECK` `UsedCount + ReservedCount &lt;= MaxUsage`; 4 partial unique
///     của `ProPromotionRedemptions` + unique **một gốc** `ProPromotionConditions` (đúng filter + `NULLS NOT DISTINCT`);
///     ghi vượt ⇒ `23514`; hai lượt `Reserved` (`OrderId` NULL) cùng mã **không** `23505`; gốc thứ hai ⇒ `23505`.
///   • `TC-34` — `ProPromotionMappings.TargetType` `Sku`/`Category`/`Brand` ⇒ `23514`; `Channel` ⇒ OK.
///   • `TC-42` — `ProPromotions.Kind = 'FlashSale'` ⇒ `23514`; `'Discount'` ⇒ OK.
///   • `TC-43` **phần DB** — 3 unique chống trùng lượt (theo đơn+voucher, theo đơn tự động, theo khách+thứ tự) chặn
///     bản ghi trùng ⇒ `23505`, và mỗi cái có **ca hợp lệ** lọt.
///
/// ⚠️ **NỢ (ghi ở testplan vòng 2, KHÔNG giả vờ phủ):** các tiểu-ca của `TC-43` cần **app service + UoW** — rollback
/// cả đơn khi `23505` (0 `Order`, 0 redemption, bộ đếm không đổi), `UsageSequence` 1→2 qua hai đơn, đua song song,
/// và `TC-43 (ix)` (unit `CustomerRequired`) — **không** biểu diễn được bằng raw SQL một dòng; do `qa` vòng 2 ghi
/// nợ. Ca `TC-33` "hai lượt `Reserved` cùng mã **không** `23505`" **được** phủ ở đây vì chỉ cần chứng minh index có
/// bộ lọc `"OrderId" IS NOT NULL`.
///
/// 🔒 Dùng chung fixture Testcontainers (`PostgreSqlSchemaFixture`, `postgres:17-alpine`), schema áp từ
/// `Assets/schema.sql` (**sinh lại từ chính migration** ở `W24-4`). Mọi câu lệnh **tham số** — không nối chuỗi.
/// </summary>
[Collection("postgres")]
public class PostgreSqlPromotionSchemaTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlPromotionSchemaTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    // ── tiện ích ─────────────────────────────────────────────────────────────

    private static string Ma() => "PRO-" + Guid.NewGuid().ToString("N")[..12];

    private async Task<long> DemAsync(string sql, params object?[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        for (var i = 0; i < thamSo.Length; i++)
        {
            cmd.Parameters.AddWithValue(thamSo[i]!);
        }

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string?> ChuoiAsync(string sql, params object?[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        for (var i = 0; i < thamSo.Length; i++)
        {
            cmd.Parameters.AddWithValue(thamSo[i]!);
        }

        return await cmd.ExecuteScalarAsync() as string;
    }

    private async Task<List<string>> CheckDefsAsync(string bang)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            select pg_get_constraintdef(c.oid)
            from pg_constraint c
            join pg_class t on t.oid = c.conrelid
            where t.relname = $1 and c.contype = 'c'
            """);
        cmd.Parameters.AddWithValue(bang);

        var ketQua = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ketQua.Add(reader.GetString(0));
        }

        return ketQua;
    }

    // ── helper chèn dữ liệu (đi đúng các cột thật của migration) ──────────────

    /// <summary>Chèn một `ProPromotions` với `Kind`/`DiscountKind` tuỳ biến (mặc định hợp lệ).</summary>
    private async Task<Guid> ChenPromotionAsync(
        Guid? tenantId, string kind = "Discount", string discountKind = "FixedAmount",
        decimal discountValue = 0m, decimal? maxDiscountAmount = null)
    {
        var id = Guid.NewGuid();
        var ma = Ma();

        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProPromotions"
                ("Id","TenantId","Code","Name","Kind","Status","IsCumulative","Priority","ChannelScope",
                 "StartsAtUtc","EndsAtUtc","TreeVersion","MaxUsageTotal","UsedCount","ReservedCount",
                 "MaxUsagePerCustomer","DiscountKind","DiscountValue","MaxDiscountAmount","MinOrderAmount",
                 "Currency","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1,$2,$3,'KM test',$4,'Draft',false,0,null,
                    now(),null,0,null,0,0,
                    null,$5,$6,$7,null,
                    'VND','{}',$8,now(),false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(ma);
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(discountKind);
        cmd.Parameters.AddWithValue(discountValue);
        cmd.Parameters.AddWithValue((object?)maxDiscountAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("stamp-" + ma);

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Chèn một `ProVoucherCodes` (mã của `promotionId`).</summary>
    private async Task<Guid> ChenVoucherAsync(
        Guid? tenantId, Guid promotionId, int maxUsage = 10, int used = 0, int reserved = 0)
    {
        var id = Guid.NewGuid();

        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProVoucherCodes"
                ("Id","TenantId","PromotionId","Code","MaxUsage","UsedCount","ReservedCount","Status")
            values ($1,$2,$3,$4,$5,$6,$7,'Issued')
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue(Ma());
        cmd.Parameters.AddWithValue(maxUsage);
        cmd.Parameters.AddWithValue(used);
        cmd.Parameters.AddWithValue(reserved);

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Chèn một `ProPromotionRedemptions`. Các cột thời điểm/`ReasonCode` **suy ra** từ `status` để luôn thoả
    /// `CHECK` của bảng: `0` Reserved ⇒ cần `ExpiresAtUtc`; `1` Applied ⇒ `AppliedAtUtc`; `2` Released ⇒
    /// `ReleasedAtUtc` + `ReasonCode`; `3` Reversed ⇒ `ReversedAtUtc` + `ReasonCode`.
    /// </summary>
    private async Task ChenLuotAsync(
        Guid? tenantId, Guid promotionId, Guid? voucherCodeId, Guid? orderId,
        int status, int usageSequence, Guid? customerId, string idempotencyKey)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProPromotionRedemptions"
                ("Id","TenantId","PromotionId","VoucherCodeId","OrderId","Status","ExpiresAtUtc","UsageSequence",
                 "ReasonCode","CustomerId","AppliedAtUtc","ReleasedAtUtc","ReversedAtUtc",
                 "IdempotencyKey","DiscountAmount","Currency","BreakdownJson","TreeVersion","EvaluatedAtUtc")
            values ($1,$2,$3,$4,$5,$6,
                    case when $6 = 0 then now() + interval '1 day' else null end,
                    $7,
                    case when $6 in (2,3) then 'TTL_EXPIRED' else null end,
                    $8,
                    case when $6 = 1 then now() else null end,
                    case when $6 = 2 then now() else null end,
                    case when $6 = 3 then now() else null end,
                    $9, 0, 'VND', '{}'::jsonb, 1, now())
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue((object?)voucherCodeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)orderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue(usageSequence);
        cmd.Parameters.AddWithValue((object?)customerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(idempotencyKey);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Chèn một node `ProPromotionConditions` là **gốc** (`ParentId = NULL`).</summary>
    private async Task ChenGocAsync(Guid? tenantId, Guid promotionId, int sortOrder)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProPromotionConditions"
                ("Id","TenantId","PromotionId","ParentId","IsGroup","InteractionType","RuleSystemName",
                 "ConfigJson","SortOrder","Depth")
            values ($1,$2,$3,null,true,'And',null,null,$4,1)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue(sortOrder);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenMappingAsync(Guid? tenantId, Guid promotionId, string targetType)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ProPromotionMappings"
                ("Id","TenantId","PromotionId","TargetType","TargetId","Mode")
            values ($1,$2,$3,$4,$5,'Include')
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(promotionId);
        cmd.Parameters.AddWithValue(targetType);
        cmd.Parameters.AddWithValue(Guid.NewGuid());

        await cmd.ExecuteNonQueryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-33 — CHECK `UsedCount + ReservedCount <= MaxUsage` + 4 partial unique +
    //         unique MỘT GỐC; hai lượt Reserved cùng mã KHÔNG 23505
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC33_ProVoucherCodes_dung_MOT_CHECK_cong_luot__khong_con_UsedCount_le_MaxUsage()
    {
        // 🚨 SRS §5.3: `CHECK` cũ `UsedCount <= MaxUsage` **phải bị THAY** bằng `UsedCount + ReservedCount <= MaxUsage`
        // — KHÔNG được tồn tại song song (nếu còn bản cũ thì lượt GIỮ không biểu diễn được, vỡ §8.1 #9).
        var defs = await CheckDefsAsync("ProVoucherCodes");

        // `pg_get_constraintdef` chuẩn hoá thành `... (("UsedCount" + "ReservedCount") <= "MaxUsage")` — nên so
        // theo *có mặt cả hai vế*, không bám dấu ngoặc.
        var moi = defs
            .Where(d => d.Contains("\"UsedCount\" + \"ReservedCount\"", StringComparison.Ordinal)
                        && d.Contains("<=", StringComparison.Ordinal)
                        && d.Contains("\"MaxUsage\"", StringComparison.Ordinal))
            .ToList();
        Assert.Single(moi);

        var cu = defs
            .Where(d => Regex.IsMatch(d, "\"UsedCount\"\\s*<=\\s*\"MaxUsage\""))
            .ToList();
        Assert.Empty(cu);
    }

    [Fact]
    public async Task TC33_ghi_vuot_luot_ma__UsedCount_cong_ReservedCount_vuot_MaxUsage__bi_chan_23514()
    {
        var promotionId = await ChenPromotionAsync(tenantId: null);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenVoucherAsync(null, promotionId, maxUsage: 1, used: 1, reserved: 1));   // 1 + 1 > 1

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    [Fact]
    public async Task TC33_bon_partial_index_redemptions_va_unique_mot_goc_conditions_dung_filter_va_NND()
    {
        // 4 partial unique của `ProPromotionRedemptions` + unique một gốc của `ProPromotionConditions`:
        // CẢ hai vế — `UNIQUE` + `NULLS NOT DISTINCT` + đúng bộ lọc. Thiếu bất kỳ vế nào ⇒ mất hàng rào IM LẶNG.
        var kyVong = new (string Ten, string[] Filter)[]
        {
            ("IX_ProPromRedemptions_Tenant_Prom_Order_Voucher",
                ["\"VoucherCodeId\" IS NOT NULL", "\"OrderId\" IS NOT NULL"]),
            ("IX_ProPromotionRedemptions_TenantId_OrderId_Voucher",
                ["\"OrderId\" IS NOT NULL", "\"VoucherCodeId\" IS NOT NULL"]),
            ("IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong",
                ["\"OrderId\" IS NOT NULL", "\"VoucherCodeId\" IS NULL"]),
            ("IX_ProPromRedemptions_Tenant_Prom_Cust_Seq",
                ["\"CustomerId\" IS NOT NULL", "\"Status\"", "2"]),
            ("IX_ProPromotionConditions_MotGoc",
                ["\"ParentId\" IS NULL"]),
        };

        foreach (var (ten, filter) in kyVong)
        {
            var dinhNghia = await ChuoiAsync(
                "select indexdef from pg_indexes where schemaname = 'public' and indexname = $1", ten);

            Assert.NotNull(dinhNghia);
            Assert.Contains("UNIQUE", dinhNghia);
            Assert.Contains("NULLS NOT DISTINCT", dinhNghia);
            Assert.Contains("WHERE", dinhNghia);
            foreach (var ve in filter)
            {
                Assert.Contains(ve, dinhNghia);
            }
        }
    }

    [Fact]
    public async Task TC33_hai_luot_RESERVED_OrderId_NULL_cung_ma_KHONG_23505()
    {
        // 🚨 Đây là ca chứng minh filter `"OrderId" IS NOT NULL` của index `(TenantId, OrderId)` là BẮT BUỘC:
        // hai lượt GIỮ cùng một mã (`OrderId = NULL`) là **hợp lệ** — nếu thiếu filter (và có `NULLS NOT DISTINCT`)
        // thì mỗi mã chỉ một người giữ cùng lúc được. `UsageSequence` cả hai = 1 (khách NULL ⇒ ngoài index theo khách).
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var voucherId = await ChenVoucherAsync(null, promotionId);

        await ChenLuotAsync(null, promotionId, voucherId, orderId: null, status: 0, usageSequence: 1, customerId: null, idempotencyKey: Ma());
        await ChenLuotAsync(null, promotionId, voucherId, orderId: null, status: 0, usageSequence: 1, customerId: null, idempotencyKey: Ma());

        var soLuot = await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"PromotionId\" = $1 and \"VoucherCodeId\" = $2 and \"OrderId\" is null",
            promotionId, voucherId);

        Assert.Equal(2L, soLuot);
    }

    [Fact]
    public async Task TC33_hai_goc_cung_PromotionId_bi_chan_23505__boi_index_mot_goc()
    {
        // 🚨 `D-159` · `BR-03`: **đúng MỘT gốc** mỗi cây. `SortOrder` KHÁC nhau để index `(TenantId, PromotionId,
        // ParentId, SortOrder)` KHÔNG chặn ⇒ chứng minh chính index **một gốc** `(TenantId, PromotionId) WHERE ParentId
        // IS NULL` bắt được. Thiếu nó ⇒ cây có hai gốc ⇒ duyệt không tất định.
        var promotionId = await ChenPromotionAsync(tenantId: null);

        await ChenGocAsync(null, promotionId, sortOrder: 0);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenGocAsync(null, promotionId, sortOrder: 1));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-34 — `ProPromotionMappings.TargetType` v1 chỉ `Channel`/`Shipping` (D-152)
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Sku")]
    [InlineData("Category")]
    [InlineData("Brand")]
    public async Task TC34_TargetType_Sku_Category_Brand_bi_chan_23514(string targetType)
    {
        var promotionId = await ChenPromotionAsync(tenantId: null);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenMappingAsync(null, promotionId, targetType));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    [Fact]
    public async Task TC34_TargetType_Channel_thi_DUOC_PHEP()
    {
        var promotionId = await ChenPromotionAsync(tenantId: null);

        await ChenMappingAsync(null, promotionId, "Channel");

        var so = await DemAsync(
            "select count(*) from \"ProPromotionMappings\" where \"PromotionId\" = $1", promotionId);

        Assert.Equal(1L, so);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-42 — `Kind = 'FlashSale'` bị chặn fail-closed (D-156)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC42_Kind_FlashSale_bi_chan_23514__va_Discount_thi_DUOC_PHEP()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenPromotionAsync(tenantId: null, kind: "FlashSale"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514

        // Đối chứng: `Discount` hợp lệ phải chèn được — nếu `CHECK` quá chặt thì nghiệp vụ mất khuyến mãi thật.
        var id = await ChenPromotionAsync(tenantId: null, kind: "Discount");
        var so = await DemAsync("select count(*) from \"ProPromotions\" where \"Id\" = $1", id);
        Assert.Equal(1L, so);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-43 (phần DB) — 3 hàng rào unique chống trùng lượt; mỗi cái có ca hợp lệ
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC43i_mot_don_1_voucher_va_2_tu_dong_KHAC_promotion__cong_don_KHONG_bi_chan()
    {
        // `D-157`: hai filter `#6` (cùng mã) và `#9` (tự động/đơn) **bù nhau** ⇒ cộng dồn HỢP LỆ
        // (1 voucher + N tự động KHÁC `PromotionId` trên cùng đơn) **KHÔNG** bị DB chặn. Nếu ai đó bỏ vế
        // `"VoucherCodeId" IS NULL`/`IS NOT NULL` khỏi một trong hai filter thì ca này bị `23505` oan.
        var orderId = Guid.NewGuid();

        var promoVoucher = await ChenPromotionAsync(tenantId: null, kind: "Voucher");
        var voucherId = await ChenVoucherAsync(null, promoVoucher);
        await ChenLuotAsync(null, promoVoucher, voucherId, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());

        foreach (var _ in new[] { 0, 1 })
        {
            var promoAuto = await ChenPromotionAsync(tenantId: null);
            await ChenLuotAsync(null, promoAuto, voucherCodeId: null, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());
        }

        var soLuot = await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"OrderId\" = $1", orderId);

        Assert.Equal(3L, soLuot);
    }

    [Fact]
    public async Task TC43a_mot_don_mot_voucher__ghi_hai_luot_cung_don_voucher_bi_chan_23505()
    {
        // `(TenantId, PromotionId, OrderId, VoucherCodeId)` — một đơn dùng **cùng mã** hai lần ⇒ bị chặn
        // (bất biến *một đơn tối đa một lượt voucher*). Trùng `(PromotionId, OrderId, VoucherCodeId)`.
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var voucherId = await ChenVoucherAsync(null, promotionId);
        var orderId = Guid.NewGuid();

        await ChenLuotAsync(null, promotionId, voucherId, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenLuotAsync(null, promotionId, voucherId, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task TC43a_ca_HOP_LE_cung_voucher_nhung_KHAC_don_thi_DUOC_PHEP()
    {
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var voucherId = await ChenVoucherAsync(null, promotionId);

        await ChenLuotAsync(null, promotionId, voucherId, Guid.NewGuid(), status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());
        await ChenLuotAsync(null, promotionId, voucherId, Guid.NewGuid(), status: 1, usageSequence: 2, customerId: null, idempotencyKey: Ma());

        var so = await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"PromotionId\" = $1 and \"VoucherCodeId\" = $2",
            promotionId, voucherId);

        Assert.Equal(2L, so);
    }

    [Fact]
    public async Task TC43b_mot_tu_dong_moi_don__ghi_hai_luot_tu_dong_cung_don_bi_chan_23505()
    {
        // `(TenantId, PromotionId, OrderId) WHERE "OrderId" IS NOT NULL AND "VoucherCodeId" IS NULL`:
        // retry lọt idempotency hoặc engine trả trùng candidate KHÔNG được nhân đôi `UsedCount`/`DiscountTotal`.
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var orderId = Guid.NewGuid();

        await ChenLuotAsync(null, promotionId, voucherCodeId: null, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenLuotAsync(null, promotionId, voucherCodeId: null, orderId, status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task TC43b_ca_HOP_LE_tu_dong_cung_promotion_nhung_KHAC_don_thi_DUOC_PHEP()
    {
        var promotionId = await ChenPromotionAsync(tenantId: null);

        await ChenLuotAsync(null, promotionId, voucherCodeId: null, Guid.NewGuid(), status: 1, usageSequence: 1, customerId: null, idempotencyKey: Ma());
        await ChenLuotAsync(null, promotionId, voucherCodeId: null, Guid.NewGuid(), status: 1, usageSequence: 2, customerId: null, idempotencyKey: Ma());

        var so = await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"PromotionId\" = $1 and \"VoucherCodeId\" is null",
            promotionId);

        Assert.Equal(2L, so);
    }

    [Fact]
    public async Task TC43c_tran_theo_khach__ghi_trung_cung_khach_va_thu_tu_con_hieu_luc_bi_chan_23505()
    {
        // `(TenantId, PromotionId, CustomerId, UsageSequence) WHERE "CustomerId" IS NOT NULL AND "Status" <> 2`:
        // hàng rào trần theo khách (`MaxUsagePerCustomer`). Hai lượt cùng khách + cùng thứ tự còn hiệu lực
        // (Status 0/1/3) ⇒ `23505`; lượt `Released` (2) nhả số ⇒ ngoài filter.
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var khachId = Guid.NewGuid();

        await ChenLuotAsync(null, promotionId, voucherCodeId: null, Guid.NewGuid(), status: 1, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenLuotAsync(null, promotionId, voucherCodeId: null, Guid.NewGuid(), status: 1, usageSequence: 1, customerId: khachId, idempotencyKey: Ma()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task TC43c_ca_HOP_LE_luot_Released_Status_2_khong_chieM_cho()
    {
        // Lượt đã NHẢ (Released = 2) nằm NGOÀI filter `"Status" <> 2` ⇒ cùng khách + cùng `UsageSequence` vẫn
        // chèn được (số thứ tự được tái sử dụng cho lượt mới) — nếu thiếu `"Status" <> 2` thì việc nhả lượt vô nghĩa.
        var promotionId = await ChenPromotionAsync(tenantId: null);
        var khachId = Guid.NewGuid();

        await ChenLuotAsync(null, promotionId, voucherCodeId: null, orderId: null, status: 2, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());
        await ChenLuotAsync(null, promotionId, voucherCodeId: null, orderId: null, status: 1, usageSequence: 1, customerId: khachId, idempotencyKey: Ma());

        var so = await DemAsync(
            "select count(*) from \"ProPromotionRedemptions\" where \"PromotionId\" = $1 and \"CustomerId\" = $2 and \"UsageSequence\" = 1",
            promotionId, khachId);

        Assert.Equal(2L, so);
    }
}

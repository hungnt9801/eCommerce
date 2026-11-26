using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// `TC-19` + `TC-30` — bất biến chống gửi trùng của `NOT-001` **chỉ chứng minh được trên PostgreSQL thật**
/// (`SRS §14.3`, `ARCH §13.1` tầng Integration).
///
/// 🚨 VÌ SAO: PostgreSQL coi `NULL` là **DISTINCT** trong unique index. Hệ thống đang chạy **một tenant**
/// (`TenantId = NULL`) ⇒ nếu index mất `NULLS NOT DISTINCT` thì hai mục cùng `IdempotencyKey` (hoặc cùng
/// nội dung trong cùng cửa sổ khử trùng) đều **LỌT** ⇒ **khách nhận HAI tin** — tốn tiền thật, mất uy tín,
/// và **không có lỗi nào** để lần ra. Đây là ca then chốt của negative control `NC-1`.
///
/// 📌 Dùng chung fixture Testcontainers (`PostgreSqlSchemaFixture`, `postgres:17-alpine`) — schema áp từ
/// `Assets/schema.sql` (sinh từ migration `Wp22_Notification_Template_Queue`). Raw SQL + tham số, không nối chuỗi.
/// </summary>
[Collection("postgres")]
public class PostgreSqlNotificationTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlNotificationTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Hex64() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    /// <summary>Chèn template + version `Published` (để thoả FK RESTRICT của `NotificationQueueItems`).</summary>
    private async Task<(Guid TemplateId, Guid VersionId)> TaoTemplateAsync(int purpose, int coalesceWindowSeconds)
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using (var cmd = _fixture.DataSource.CreateCommand("""
            insert into "NotificationTemplates"
                ("Id","TenantId","Code","Name","ChannelKind","Locale","Purpose","CoalesceWindowSeconds",
                 "Status","IsSystem","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, $2, $3, 0, 'vi', $4, $5, 1, false, '{}', $6, now(), false)
            """))
        {
            cmd.Parameters.AddWithValue(templateId);
            cmd.Parameters.AddWithValue("N" + Guid.NewGuid().ToString("N")[..12]);
            cmd.Parameters.AddWithValue("Mẫu thử " + templateId.ToString("N")[..6]);
            cmd.Parameters.AddWithValue(purpose);
            cmd.Parameters.AddWithValue(coalesceWindowSeconds);
            cmd.Parameters.AddWithValue("stamp-tpl");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = _fixture.DataSource.CreateCommand("""
            insert into "NotificationTemplateVersions"
                ("Id","TenantId","TemplateId","Version","Subject","BodyText","BodyFormat","Placeholders",
                 "ContentHash","PublishedAtUtc","CreationTime")
            values ($1, null, $2, 1, null, $3, 0, '[]'::jsonb, $4::char(64), now(), now())
            """))
        {
            cmd.Parameters.AddWithValue(versionId);
            cmd.Parameters.AddWithValue(templateId);
            cmd.Parameters.AddWithValue("Xin chào {{Ten}}");
            cmd.Parameters.AddWithValue(Hex64());
            await cmd.ExecuteNonQueryAsync();
        }

        return (templateId, versionId);
    }

    private async Task ChenMucAsync(
        Guid id, string idempotencyKey, Guid templateId, Guid versionId,
        string recipientHash, string contentFingerprint, DateTime? coalesceBucketStartUtc, int status)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "NotificationQueueItems"
                ("Id","TenantId","IdempotencyKey","ChannelKind","TemplateId","TemplateVersionId",
                 "RecipientEncrypted","RecipientHash","ContentFingerprint","CoalesceBucketStartUtc",
                 "Priority","Status","AttemptCount","ScheduledAtUtc","CorrelationId",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, $2, 0, $3, $4, 'enc', $5::char(64), $6::char(64), $7,
                    0, $8, 0, now(), 'corr', '{}', $9, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(idempotencyKey);
        cmd.Parameters.AddWithValue(templateId);
        cmd.Parameters.AddWithValue(versionId);
        cmd.Parameters.AddWithValue(recipientHash);
        cmd.Parameters.AddWithValue(contentFingerprint);
        cmd.Parameters.AddWithValue((object?)coalesceBucketStartUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue("stamp-muc");

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> DemAsync(string sql, params (string Ten, object GiaTri)[] thamSo)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(sql);
        foreach (var (ten, giaTri) in thamSo)
        {
            cmd.Parameters.AddWithValue(ten, giaTri);
        }

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // ── TC-19: unique index (TenantId, IdempotencyKey) chặn trùng khi TenantId = NULL ──────────

    [Fact]
    public async Task TC19_chan_hai_muc_cung_IdempotencyKey_khi_TenantId_NULL()
    {
        var (templateId, versionId) = await TaoTemplateAsync(purpose: 0, coalesceWindowSeconds: 0);
        var key = "TC19-" + Guid.NewGuid().ToString("N");
        var hash = Hex64();
        var fp = Hex64();

        await ChenMucAsync(Guid.NewGuid(), key, templateId, versionId, hash, fp, null, status: 0);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenMucAsync(Guid.NewGuid(), key, templateId, versionId, hash, fp, null, status: 0));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState); // 23505
    }

    // ── TC-30: partial unique index khử trùng cửa sổ (chỉ mục có bucket) ──────────────────────

    [Fact]
    public async Task TC30_partial_index_chan_hai_muc_cung_noi_dung_trong_CUNG_cua_so()
    {
        var (templateId, versionId) = await TaoTemplateAsync(purpose: 1, coalesceWindowSeconds: 300);
        var hash = Hex64();
        var fp = Hex64();
        var bucket = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        await ChenMucAsync(Guid.NewGuid(), "TC30-a-" + Guid.NewGuid().ToString("N"),
            templateId, versionId, hash, fp, bucket, status: 2);

        // Khoá idempotency KHÁC ⇒ chỉ partial index nội dung mới có thể chặn.
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenMucAsync(Guid.NewGuid(), "TC30-b-" + Guid.NewGuid().ToString("N"),
                templateId, versionId, hash, fp, bucket, status: 2));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState); // 23505
    }

    [Fact]
    public async Task TC30_ban_ghi_bi_khu_trung_bucket_NULL_KHONG_chiem_cho()
    {
        // Mục bị khử trùng ghi `CoalesceBucketStartUtc = NULL` (SRS §3 hàng 13) ⇒ ra NGOÀI partial index.
        // Nếu index mất bộ lọc `WHERE ... IS NOT NULL` thì các mục này chặn nhầm lẫn nhau (mất dấu vết).
        var (templateId, versionId) = await TaoTemplateAsync(purpose: 1, coalesceWindowSeconds: 300);
        var hash = Hex64();
        var fp = Hex64();

        await ChenMucAsync(Guid.NewGuid(), "TC30-n1-" + Guid.NewGuid().ToString("N"),
            templateId, versionId, hash, fp, coalesceBucketStartUtc: null, status: 5);
        await ChenMucAsync(Guid.NewGuid(), "TC30-n2-" + Guid.NewGuid().ToString("N"),
            templateId, versionId, hash, fp, coalesceBucketStartUtc: null, status: 5);

        var soDong = await DemAsync(
            "select count(*) from \"NotificationQueueItems\" where \"ContentFingerprint\" = @fp and \"CoalesceBucketStartUtc\" is null",
            ("fp", fp));

        Assert.Equal(2L, soDong);
    }

    [Fact]
    public async Task TC30_hai_muc_cung_noi_dung_KHAC_cua_so_thi_duoc_phep()
    {
        // Đối chứng: khử trùng là THEO CỬA SỔ, không phải "cấm trùng vĩnh viễn".
        var (templateId, versionId) = await TaoTemplateAsync(purpose: 1, coalesceWindowSeconds: 300);
        var hash = Hex64();
        var fp = Hex64();

        await ChenMucAsync(Guid.NewGuid(), "TC30-c1-" + Guid.NewGuid().ToString("N"),
            templateId, versionId, hash, fp, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), status: 2);
        await ChenMucAsync(Guid.NewGuid(), "TC30-c2-" + Guid.NewGuid().ToString("N"),
            templateId, versionId, hash, fp, new DateTime(2026, 11, 1, 0, 5, 0, DateTimeKind.Utc), status: 2);

        var soDong = await DemAsync(
            "select count(*) from \"NotificationQueueItems\" where \"ContentFingerprint\" = @fp",
            ("fp", fp));

        Assert.Equal(2L, soDong);
    }
}

using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 `S16b` — **CỘT BẰNG CHỨNG PHÁT LẠI DLQ** (`XC-002`, migration `Xc002_DlqReplay`):
/// `ReplayedAtUtc` · `ReplayCount` · `ReplayedBy`.
///
/// 🚨 **VÌ SAO PHẢI ĐO TRÊN POSTGRESQL THẬT (không chỉ unit test):** unit test chạy trên **đối tượng trong
/// bộ nhớ** — nó **không thể** phát hiện ba lỗi thật sau:
/// <list type="number">
///   <item>migration **chưa áp** (hoặc `schema.sql` quên sinh lại) ⇒ cột **không tồn tại** ⇒ API replay
///     ném lỗi SQL lúc chạy, **dù mọi unit test xanh**;</item>
///   <item>`defaultValue: 0` của `ReplayCount` **không** vào DB ⇒ dòng mới có `NULL` ⇒ cột NOT NULL chặn
///     ⇒ 📌 *"replay hỏng ở lần đầu tiên"* — đúng loại lỗi chỉ lộ khi chạy thật;</item>
///   <item>`ReplayedBy` khai `HasMaxLength(256)` nhưng cột thật là `text` (EF không áp) ⇒ ghi 1 MB **lọt**
///     ⇒ mỗi dòng DLQ phình, và 📌 **không có lỗi nào**.</item>
/// </list>
///
/// 🔒 **HAI CHIỀU:** dòng **chưa phát lại** phải có `ReplayCount = 0` + `NULL`; dòng **đã phát lại** phải
/// đọc lại **đúng** mốc/người/bộ đếm. ➕ 256 ký tự **lọt**, 257 ký tự **bị chặn**.
/// </summary>
[Collection("postgres")]
public class PostgreSqlS16bDlqReplayTests
{
    private readonly PostgreSqlSchemaFixture _fixture;

    public PostgreSqlS16bDlqReplayTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Chèn một dòng DLQ. 📌 `coDauPhatLai = false` ⇒ **KHÔNG** liệt kê 3 cột mới trong `INSERT` — đúng ca "dòng cũ/mới chưa phát lại" mà DB phải tự điền mặc định.</summary>
    private async Task ChenDongDlqAsync(
        Guid id,
        string khoa,
        bool coDauPhatLai = false,
        DateTime? replayedAtUtc = null,
        int replayCount = 0,
        string? replayedBy = null)
    {
        var danhSachCot =
            "\"Id\",\"TenantId\",\"TenSuKien\",\"KhoaNghiepVu\",\"PayloadJson\",\"PayloadHash\"," +
            "\"LyDo\",\"MoTaLyDo\",\"Loi\",\"VaoDlqLucUtc\",\"CorrelationId\"," +
            "\"DaXuLy\",\"XuLyLucUtc\",\"NguoiXuLy\",\"LyDoXuLy\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\"";

        var danhSachGiaTri = "$1, null, 'OrderStatusChangedEto', $2, '{\"OrderId\":null}', repeat('a', 64), " +
                             "0, 'probe S16b', null, now(), null, " +
                             "false, null, null, null, " +
                             "'{}', 'stamp-s16b', now(), false";

        if (coDauPhatLai)
        {
            danhSachCot += ",\"ReplayedAtUtc\",\"ReplayCount\",\"ReplayedBy\"";
            danhSachGiaTri += ", $3, $4, $5";
        }

        await using var cmd = _fixture.DataSource.CreateCommand(
            $"insert into \"MsgDeadLetters\" ({danhSachCot}) values ({danhSachGiaTri})");

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(khoa);

        if (coDauPhatLai)
        {
            cmd.Parameters.AddWithValue((object?)replayedAtUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue(replayCount);
            cmd.Parameters.AddWithValue((object?)replayedBy ?? DBNull.Value);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Đọc lại 3 cột bằng **một** câu SQL độc lập (không dùng lại tham số đã ghi).</summary>
    private async Task<(DateTime? ReplayedAtUtc, int ReplayCount, string? ReplayedBy)> DocDauPhatLaiAsync(Guid id)
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            "select \"ReplayedAtUtc\", \"ReplayCount\", \"ReplayedBy\" from \"MsgDeadLetters\" where \"Id\" = $1");

        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "Không đọc lại được dòng DLQ vừa chèn ⇒ phép đo VÔ NGHĨA.");

        var moc = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);

        return (moc, reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private async Task XoaAsync(Guid id)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("delete from \"MsgDeadLetters\" where \"Id\" = $1");
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Dong_CHUA_phat_lai__ReplayCount_mac_dinh_0_va_hai_cot_con_lai_NULL()
    {
        var id = Guid.NewGuid();
        var khoa = $"K9S16B-{Guid.NewGuid():N}";

        try
        {
            // 🚨 KHÔNG truyền 3 cột mới ⇒ đây là ca "dòng DLQ bình thường" (mọi dòng trước `S16b`).
            await ChenDongDlqAsync(id, khoa);

            var (moc, soLan, nguoi) = await DocDauPhatLaiAsync(id);

            Assert.Null(moc);
            Assert.Equal(0, soLan);
            Assert.Null(nguoi);
        }
        finally
        {
            await XoaAsync(id);
        }
    }

    [Fact]
    public async Task Dong_DA_phat_lai__doc_lai_DUNG_moc_nguoi_va_bo_dem()
    {
        var id = Guid.NewGuid();
        var khoa = $"K9S16B-{Guid.NewGuid():N}";

        // 📌 Mốc có **mili-giây** để bắt lỗi làm tròn (PostgreSQL `timestamptz` là micro-giây).
        var moc = new DateTime(2026, 10, 29, 3, 34, 2, 123, DateTimeKind.Utc).AddTicks(4560);

        try
        {
            await ChenDongDlqAsync(id, khoa, coDauPhatLai: true, replayedAtUtc: moc, replayCount: 1, replayedBy: "probe.s16b");

            var (docMoc, soLan, nguoi) = await DocDauPhatLaiAsync(id);

            Assert.NotNull(docMoc);
            Assert.Equal(moc, docMoc!.Value.ToUniversalTime());
            Assert.Equal(DateTimeKind.Utc, docMoc.Value.Kind);
            Assert.Equal(1, soLan);
            Assert.Equal("probe.s16b", nguoi);
        }
        finally
        {
            await XoaAsync(id);
        }
    }

    [Fact]
    public async Task ReplayedBy_DUNG_256_ky_tu__LOT__qua_256__BI_CHAN()
    {
        var id = Guid.NewGuid();
        var khoa = $"K9S16B-{Guid.NewGuid():N}";
        var id257 = Guid.NewGuid();

        try
        {
            // ✅ CHIỀU HỢP LỆ: đúng bằng giới hạn khai báo (`HasMaxLength(256)`).
            await ChenDongDlqAsync(id, khoa, coDauPhatLai: true, replayedAtUtc: DateTime.UtcNow, replayCount: 1,
                replayedBy: new string('n', 256));

            Assert.Equal(256, (await DocDauPhatLaiAsync(id)).ReplayedBy!.Length);

            // 🚨 CHIỀU BỊ CHẶN: 257 ký tự ⇒ phải là lỗi ĐỘ DÀI CỘT, không phải "ghi được rồi cắt".
            var loi = await Assert.ThrowsAsync<PostgresException>(
                () => ChenDongDlqAsync(id257, khoa + "-257", coDauPhatLai: true, replayedAtUtc: DateTime.UtcNow,
                    replayCount: 1, replayedBy: new string('n', 257)));

            Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, loi.SqlState);
        }
        finally
        {
            await XoaAsync(id);
            await XoaAsync(id257);
        }
    }
}

using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🆕 `S7` — **BẤT BIẾN "KHÔNG THỂ XÁC NHẬN MỘT NỬA" SỐNG TRONG POSTGRESQL**
/// (`REC-001` · `Q5` = **A**: xác nhận lệch = ĐÓNG CẢNH BÁO + audit, KHÔNG sửa tồn).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI LÀ INTEGRATION TEST (không phải unit):** `CK_ReconRuns_XacNhan_DuBo` là một ràng buộc
/// **chỉ tồn tại trong PostgreSQL**. Unit test kiểm `ReconRun.XacNhan` (đường ghi **chuẩn**), nhưng:
/// <list type="bullet">
///   <item>nó **không** chứng minh được ràng buộc DB có mặt — và nếu migration sinh ra một `CHECK` **không
///     đúng** (hoặc không sinh gì), build + unit test vẫn **xanh**;</item>
///   <item>đường ghi **TẮT** (script sửa dữ liệu, phát lại, `psql`) **không** đi qua entity ⇒ đó chính là
///     đường mà hàng rào cuối phải chặn.</item>
/// </list>
///
/// 🔒 **Kiểm CẢ HAI CHIỀU (bài học `handover` §5.2):**
/// <list type="number">
///   <item>**xác nhận ĐỦ** ⇒ **phải ghi được** (nếu không: ràng buộc quá chặt ⇒ tính năng chết mà cổng vẫn xanh);</item>
///   <item>**xác nhận THIẾU** (ba dạng: thiếu người, thiếu thời điểm, thiếu ghi chú) ⇒ **`23514`**.</item>
/// </list>
/// 📌 Mỗi ca tự dọn trong `finally` (dòng mồ côi sẽ làm `kiem-db.ps1` nhóm ① đỏ ở lượt sau —
/// `tools/README-probe.md` §D).
/// </summary>
[Collection("postgres")]
public class ReconRunXacNhanRangBuocDbTests
{
    private const string MaLoiCheck = "23514";

    /// <summary>`CK_ReconRuns_XacNhan_DuBo` — tên ràng buộc, dùng để khẳng định **đúng** ràng buộc đã chặn.</summary>
    private const string TenRangBuoc = "CK_ReconRuns_XacNhan_DuBo";

    private readonly PostgreSqlSchemaFixture _fixture;

    public ReconRunXacNhanRangBuocDbTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Chèn THẲNG một lượt đối soát — 🔑 đúng đường ghi TẮT mà `CHECK` phải chặn.</summary>
    private async Task ChenLuotAsync(Guid id, Guid? nguoiXacNhan, DateTime? lucXacNhan, string? ghiChu)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "ReconRuns"
                ("Id","TenantId","BatDauUtc","KetThucUtc","SoCapDaDo","SoLech","SoLechVuotNguong",
                 "CoVuotNguong","DonViNguong","GiaTriNguong","HanhDong","NguongHopLe","KetLuan",
                 "ConfirmedByUserId","ConfirmedAtUtc","ConfirmNote",
                 "ExtraProperties","ConcurrencyStamp","CreationTime")
            values ($1, null, now() - interval '2 seconds', now(), 10, 5, 2,
                    true, 0, 0.5, 1, true, 2,
                    $2, $3, $4,
                    '{}', $5, now())
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)nguoiXacNhan ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)lucXacNhan ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)ghiChu ?? DBNull.Value);
        cmd.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task XoaAsync(Guid id)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""delete from "ReconRuns" where "Id" = $1""");
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<bool> ConDongAsync(Guid id)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""select count(*) from "ReconRuns" where "Id" = $1""");
        cmd.Parameters.AddWithValue(id);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) == 1;
    }

    [Fact]
    public async Task XacNhan_DU_ba_truong__GHI_DUOC__neu_khong_rang_buoc_qua_chat_lan_tinh_nang()
    {
        // 🔒 CHIỀU 1 (bắt buộc phải xanh): một dòng xác nhận **đầy đủ** là dữ liệu **hợp lệ**.
        // 📌 Nếu ai đó viết `CHECK` sai (ví dụ `= false` thay vì `=`) thì ca này ĐỎ — và đó là cách duy nhất
        // phân biệt "chặn đúng" với "chặn tất cả".
        var id = Guid.NewGuid();

        try
        {
            await ChenLuotAsync(id, Guid.NewGuid(), DateTime.UtcNow, "kiểm kê khớp — test");

            Assert.True(await ConDongAsync(id));
        }
        finally
        {
            await XoaAsync(id);
        }
    }

    [Fact]
    public async Task XacNhan_THIEU_nguoi__bi_CHAN_boi_CK_ReconRuns_XacNhan_DuBo()
    {
        // 🚨 "Xác nhận một nửa": có thời điểm mà **không có ai** ⇒ bằng chứng **vô chủ** (không trả lời được
        // *ai đã đóng cảnh báo*). Đường ghi tắt phải bị DB chặn.
        var id = Guid.NewGuid();

        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ChenLuotAsync(id, nguoiXacNhan: null, DateTime.UtcNow, "có ghi chú nhưng không có người"));

            Assert.Equal(MaLoiCheck, ex.SqlState);
            Assert.Equal(TenRangBuoc, ex.ConstraintName);
            Assert.False(await ConDongAsync(id));   // ⚠️ KHÔNG được để lại dòng "nửa xác nhận"
        }
        finally
        {
            await XoaAsync(id);
        }
    }

    [Fact]
    public async Task XacNhan_THIEU_ghi_chu__bi_CHAN__vi_khong_co_ly_do_thi_khong_phai_audit()
    {
        var id = Guid.NewGuid();

        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ChenLuotAsync(id, Guid.NewGuid(), DateTime.UtcNow, ghiChu: null));

            Assert.Equal(MaLoiCheck, ex.SqlState);
            Assert.Equal(TenRangBuoc, ex.ConstraintName);
            Assert.False(await ConDongAsync(id));
        }
        finally
        {
            await XoaAsync(id);
        }
    }

    [Fact]
    public async Task Luot_CHUA_xac_nhan__ba_truong_NULL__la_TRANG_THAI_HOP_LE__khong_bi_chan()
    {
        // 🔒 CHIỀU 2 (bắt buộc phải xanh): mọi lượt quét bình thường đều có 3 trường này `NULL` ⇒ nếu `CHECK`
        // viết sai chiều (`<>` thay vì `=`) thì **JOB ĐỐI SOÁT CHẾT NGAY** — và triệu chứng sẽ là
        // "không có lượt quét nào" chứ không phải "ràng buộc sai".
        var id = Guid.NewGuid();

        try
        {
            await ChenLuotAsync(id, nguoiXacNhan: null, lucXacNhan: null, ghiChu: null);

            Assert.True(await ConDongAsync(id));
        }
        finally
        {
            await XoaAsync(id);
        }
    }
}

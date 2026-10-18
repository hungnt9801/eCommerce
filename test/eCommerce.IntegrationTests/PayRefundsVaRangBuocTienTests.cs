using Npgsql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// 🚨 **HÀNG RÀO **TIỀN** CỦA HOÀN TIỀN — CHỨNG MINH TRÊN PostgreSQL THẬT** (`S5`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI LÀ INTEGRATION TEST (không phải unit):** cả hai bất biến ở đây sống **trong PostgreSQL**, không
/// trong C#:
/// <list type="number">
///   <item>**`NULLS NOT DISTINCT`** là thuộc tính **EF Core KHÔNG mô hình hoá** ⇒ index do `CreateIndex` sinh ra
///     **không** có nó, và ở chế độ `TenantId = NULL` (hiện nay) PostgreSQL mặc định coi **mỗi `NULL` là KHÁC
///     nhau** ⇒ index **mất tác dụng IM LẶNG**. 🔑 Đây đúng lỗi đã phải chứng minh bằng `INSERT 0 2` trong
///     migration `20261013024226`; nay lặp lại cho bảng **mới** `PayRefunds`.</item>
///   <item>**`CHECK (RefundedAmount &lt;= Amount)`** — trước `S5` bất biến *"tổng hoàn ≤ đã thu"* **chỉ** sống ở
///     tầng domain (`RegisterRefund`); 📌 tức là **đường ghi tắt** (job/seed/SQL tay) **không bị chặn**. Đây là
///     bất biến **TIỀN**, xứng đáng có hàng rào cuối (`PAY-001` §5.3 + §11 #2).</item>
/// </list>
///
/// 🔒 **Kiểm CẢ HAI CHIỀU ở mọi ca:** chỉ kiểm "chặn được" thì một index **quá chặt** (chặn cả dữ liệu hợp lệ)
/// cũng XANH — và hệ quả sẽ là **không ghi được lần hoàn thứ hai hợp lệ nào**.
/// 📌 Mọi câu lệnh dùng **tham số**; mỗi ca **tự dọn** trong `finally` (nếu không, `kiem-db.ps1` sẽ đỏ ở lượt sau —
/// `tools/README-probe.md` §D).
/// </summary>
[Collection("postgres")]
public class PayRefundsVaRangBuocTienTests
{
    /// <summary>Tiền tố dành riêng cho test — dọn theo tiền tố, không theo `Id` (để không sót ca chèn hỏng).</summary>
    private const string TienTo = "PAYRF-TEST-";

    private static readonly Guid GiaoDichCoDinh = new("e0000000-0000-0000-0000-00000000a501");

    private readonly PostgreSqlSchemaFixture _fixture;

    public PayRefundsVaRangBuocTienTests(PostgreSqlSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task ChenDongHoanAsync(
        Guid id,
        Guid? tenantId,
        Guid giaoDich,
        string maYeuCau,
        decimal soTien = 100000m)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "PayRefunds"
                ("Id","TenantId","PaymentTransactionId","RefundRequestId","Amount","Reason","PerformedBy",
                 "Status","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, $5, 'ly do test', 'nguoi test', 1, '{}', $6, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(giaoDich);
        cmd.Parameters.AddWithValue(maYeuCau);
        cmd.Parameters.AddWithValue(soTien);
        cmd.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ChenGiaoDichAsync(Guid id, string maThamChieu, decimal soTien, decimal daHoan)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "PayTransactions"
                ("Id","TenantId","ProviderCode","MerchantReference","Amount","Currency","Status",
                 "RefundedAmount","WebhookReceivedCount","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, null, 'vnpay', $2, $3, 'VND', 2, $4, 0, '{}', $5, now(), false)
            """);

        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(maThamChieu);
        cmd.Parameters.AddWithValue(soTien);
        cmd.Parameters.AddWithValue(daHoan);
        cmd.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task DonDepAsync()
    {
        await using var cmd = _fixture.DataSource.CreateCommand(
            """delete from "PayRefunds" where "RefundRequestId" like $1""");

        cmd.Parameters.AddWithValue(TienTo + "%");

        await cmd.ExecuteNonQueryAsync();

        await using var cmd2 = _fixture.DataSource.CreateCommand(
            """delete from "PayTransactions" where "MerchantReference" like $1""");

        cmd2.Parameters.AddWithValue(TienTo + "%");

        await cmd2.ExecuteNonQueryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① `NULLS NOT DISTINCT` — chống ghi trùng MỘT lần hoàn tiền, KỂ CẢ khi `TenantId = NULL`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Chan_trung_MOT_lan_hoan_khi_TenantId_NULL()
    {
        await DonDepAsync();

        try
        {
            // 🔑 Ca NẶNG: `TenantId = NULL` — nếu index thiếu `NULLS NOT DISTINCT` thì **hai dòng LỌT** và
            // `RefundedAmount` có thể bị cộng hai lần cho **một** lệnh hoàn.
            await ChenDongHoanAsync(Guid.NewGuid(), tenantId: null, GiaoDichCoDinh, TienTo + "RF001");

            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ChenDongHoanAsync(Guid.NewGuid(), tenantId: null, GiaoDichCoDinh, TienTo + "RF001"));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Cho_phep_CUNG_ma_lan_hoan_nhung_KHAC_giao_dich__va_khac_so_thu_tu()
    {
        await DonDepAsync();

        try
        {
            // 🔒 **Chiều NGƯỢC** — chống index "quá chặt": mỗi giao dịch có `RF001` **riêng**, và một giao dịch có
            // `RF001` + `RF002` (hoàn hai lần) là **hợp lệ**. 📌 Nếu index mà chặn cả hai ca này thì hệ thống
            // **không hoàn được lần thứ hai** — và đó là một lỗi im lặng theo hướng ngược lại.
            await ChenDongHoanAsync(Guid.NewGuid(), null, GiaoDichCoDinh, TienTo + "RF001");
            await ChenDongHoanAsync(Guid.NewGuid(), null, GiaoDichCoDinh, TienTo + "RF002");   // cùng giao dịch, lần 2
            await ChenDongHoanAsync(Guid.NewGuid(), null, Guid.NewGuid(), TienTo + "RF001");   // giao dịch khác, lần 1

            await using var cmd = _fixture.DataSource.CreateCommand(
                """select count(*) from "PayRefunds" where "RefundRequestId" like $1""");

            cmd.Parameters.AddWithValue(TienTo + "%");

            Assert.Equal(3L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② `CHECK` — BẤT BIẾN TIỀN Ở TẦNG DB
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Hoan_VUOT_so_da_thu__bi_DB_chan_23514()
    {
        var id = Guid.NewGuid();
        var ma = TienTo + "GD-VUOT";

        await DonDepAsync();

        try
        {
            // Đã thu 100 000; thử ghi "đã hoàn 150 000" bằng **đường ghi tắt** (UPDATE thẳng, không qua domain).
            await ChenGiaoDichAsync(id, ma, 100000m, 0m);

            await using var cmd = _fixture.DataSource.CreateCommand(
                """update "PayTransactions" set "RefundedAmount" = 150000.0000 where "Id" = $1""");

            cmd.Parameters.AddWithValue(id);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
            Assert.Equal("CK_PayTransactions_RefundedAmount_KhongVuotAmount", ex.ConstraintName);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Hoan_AM__bi_DB_chan_23514()
    {
        var id = Guid.NewGuid();
        var ma = TienTo + "GD-AM";

        await DonDepAsync();

        try
        {
            // 🚨 Số âm **không** chỉ vô nghĩa: nó **GIẢM** tổng đã hoàn ⇒ **che mất** một lần hoàn đã xảy ra.
            await ChenGiaoDichAsync(id, ma, 100000m, 50000m);

            await using var cmd = _fixture.DataSource.CreateCommand(
                """update "PayTransactions" set "RefundedAmount" = -1.0000 where "Id" = $1""");

            cmd.Parameters.AddWithValue(id);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
            Assert.Equal("CK_PayTransactions_RefundedAmount_KhongAm", ex.ConstraintName);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Dong_hoan_tien_voi_so_tien_KHONG_DUONG__bi_DB_chan_23514()
    {
        await DonDepAsync();

        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ChenDongHoanAsync(Guid.NewGuid(), null, GiaoDichCoDinh, TienTo + "RF-ZERO", soTien: 0m));

            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
            Assert.Equal("CK_PayRefunds_Amount_Duong", ex.ConstraintName);
        }
        finally
        {
            await DonDepAsync();
        }
    }

    [Fact]
    public async Task Chieu_NGUOC__hoan_DUNG_BANG_so_da_thu_VA_dong_hoan_hop_le__phai_ghi_duoc()
    {
        var id = Guid.NewGuid();
        var ma = TienTo + "GD-BANG";

        await DonDepAsync();

        try
        {
            // 🔑 Nếu chỉ kiểm "DB chặn" thì một `CHECK` **quá chặt** (`<` thay vì `<=`, hoặc `Amount > 1`) cũng
            // XANH — trong khi ca **hoàn toàn phần** là ca phổ biến nhất của thực tế.
            await ChenGiaoDichAsync(id, ma, 100000m, 0m);

            await using var cmd = _fixture.DataSource.CreateCommand(
                """update "PayTransactions" set "RefundedAmount" = 100000.0000, "Status" = 5 where "Id" = $1""");

            cmd.Parameters.AddWithValue(id);

            Assert.Equal(1, await cmd.ExecuteNonQueryAsync());

            await ChenDongHoanAsync(Guid.NewGuid(), null, id, TienTo + "RF001", soTien: 100000m);

            await using var kiem = _fixture.DataSource.CreateCommand(
                """select count(*) from "PayRefunds" where "PaymentTransactionId" = $1""");

            kiem.Parameters.AddWithValue(id);

            Assert.Equal(1L, (long)(await kiem.ExecuteScalarAsync())!);
        }
        finally
        {
            await DonDepAsync();
        }
    }
}

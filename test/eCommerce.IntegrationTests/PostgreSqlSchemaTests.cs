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
    public async Task F1_du_39_index_deu_dung_NULLS_NOT_DISTINCT()
    {
        // 🔒 BẤT BIẾN (ghi ở XC-004 Q6): mọi unique index trên entity IMultiTenant PHẢI có
        // `NULLS NOT DISTINCT`. Ai tạo migration mới mà quên ⇒ test này đỏ.
        Assert.Equal(39L, await DemAsync(
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
            "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId",
            // XC-002 — hàng rào CUỐI chống trùng ở biên vào (13/10/2026).
            "IX_MsgIdempotencyRecords_TenantId_Scope_KeyValue",
            // ADDR-001 — khoá danh mục ĐVHC là (PHIÊN BẢN, CẤP, MÃ), không phải mã trần (13/10/2026).
            "IX_AddrAdministrativeUnits_TenantId_SchemaVersion_Level_Code",
            // ⚠️ BỔ SUNG 14/10/2026 — 4 index sau ĐÃ CÓ trong DB nhưng THIẾU trong danh sách đích danh.
            // 🚨 Vì sao đây là lỗ hổng thật: test khẳng định TỔNG SỐ, nên nếu ai đó THÊM một index mới
            //    và ĐỒNG THỜI làm hỏng một index khác thì tổng số KHÔNG ĐỔI ⇒ test vẫn xanh trong khi
            //    một index đã mất `NULLS NOT DISTINCT`. Danh sách đích danh là thứ duy nhất chặn được ca đó.
            "IX_AddrCarrierMappings_TenantId_CarrierCode_CarrierAddressCode",
            "IX_EInvInvoices_TenantId_KyHieuMauSo_SoHoaDon",
            "IX_SigCertificates_TenantId_SoSerial",
            "IX_SigSigningRequests_TenantId_MaTaiLieu",
            // EINV-003 — một bản thay thế/điều chỉnh chỉ gắn với MỘT hoá đơn gốc (14/10/2026).
            "IX_EInvInvoices_TenantId_HoaDonBiThayTheId",
            "IX_EInvInvoices_TenantId_HoaDonBiDieuChinhId",
            // 🚨 SAL-005 — nhật ký nhận đơn sàn (15/10/2026). Đây là hàng rào chống **tạo 2 đơn cho 1 đơn
            //    thật**; mất `NULLS NOT DISTINCT` ⇒ khi `TenantId = NULL` index mất tác dụng IM LẶNG.
            //    ⚠️ Tên đặt TƯỜNG MINH trong DbContext vì tên EF tự sinh dài 67 ký tự > 63 ⇒ bị PostgreSQL CẮT.
            "IX_SalMktIntakes_TenantId_ChannelCode_ExternalOrderId",
            // 🚨 CAT-002 — mã danh mục duy nhất trong tenant (15/10/2026). EF sinh index này **KHÔNG** kèm
            //    `NULLS NOT DISTINCT` ⇒ đã sửa tay ngay trong migration `Cat002_CategoryTree`.
            //    Mất nó ⇒ khi `TenantId = NULL` (tình trạng hiện nay) hai danh mục cùng `Code` LỌT VÀO DB **im lặng**.
            "IX_CatCategories_TenantId_Code"
        ];

        foreach (var ten in phaiCo)
        {
            var co = await DemAsync(
                $"select count(*) from pg_indexes where schemaname = 'public' and indexname like '{ten}%' and indexdef like '%NULLS NOT DISTINCT%'");

            Assert.True(co == 1L, $"Index '{ten}' KHÔNG có NULLS NOT DISTINCT ⇒ lỗi C4/F1 đã quay lại.");
        }
    }

    // ── 2b. XC-002 — chống trùng ở BIÊN VÀO (hàng rào cuối) ────────────────

    /// <summary>Chèn một bản ghi idempotency. Cặp (TenantId, Scope, KeyValue) là ràng buộc unique.</summary>
    private async Task ChenIdempotencyAsync(Guid? tenantId, string scope, string keyValue, string stamp)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "MsgIdempotencyRecords"
                ("Id","TenantId","Scope","KeyValue","PayloadHash","PayloadHashVersion","State","Attempts",
                 "ExpiresAtUtc","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, 'hash-abc', 1, 0, 1,
                    now() + interval '7 days', '{}', $5, now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(scope);
        cmd.Parameters.AddWithValue(keyValue);
        cmd.Parameters.AddWithValue(stamp);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task XC002_chan_trung_hai_ban_ghi_cung_khoa_khi_TenantId_NULL()
    {
        // 🚨 Nếu index thiếu `NULLS NOT DISTINCT` thì dòng thứ hai LỌT ⇒ webhook gửi lại sẽ được
        // xử lý hai lần (trừ kho hai lần). Đây là hàng rào CUỐI khi Redis/khoá phân tán hỏng.
        await ChenIdempotencyAsync(null, "vnpay:ipn", "GD-DUP", "idem-a");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenIdempotencyAsync(null, "vnpay:ipn", "GD-DUP", "idem-b"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task XC002_cung_gia_tri_nhung_KHAC_scope_thi_duoc_phep()
    {
        // "12345" của Shopee KHÁC "12345" của VNPay — gộp lại sẽ là lỗi im lặng.
        await ChenIdempotencyAsync(null, "shopee:order", "12345", "idem-c");
        await ChenIdempotencyAsync(null, "vnpay:ipn", "12345", "idem-d");

        var soDong = await DemAsync(
            "select count(*) from \"MsgIdempotencyRecords\" where \"KeyValue\" = '12345'");

        Assert.Equal(2L, soDong);
    }

    [Fact]
    public async Task XC002_chan_trung_khi_TenantId_CO_gia_tri()
    {
        var tenant = Guid.NewGuid();

        await ChenIdempotencyAsync(tenant, "ghtk:webhook", "VD-1", "idem-e");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenIdempotencyAsync(tenant, "ghtk:webhook", "VD-1", "idem-f"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task XC002_CHECK_constraint_Attempts_phai_lon_hon_0()
    {
        // Hàng rào DB cho bất biến "đã nhận thì phải có ít nhất 1 lần thử".
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var cmd = _fixture.DataSource.CreateCommand("""
                insert into "MsgIdempotencyRecords"
                    ("Id","TenantId","Scope","KeyValue","PayloadHash","PayloadHashVersion","State","Attempts",
                     "ExpiresAtUtc","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
                values (gen_random_uuid(), null, 'x:y', 'k', 'h', 1, 0, 0,
                        now(), '{}', 's', now(), false)
                """);
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    // ── 2c. ADDR-001 — danh mục ĐVHC: khoá là (PHIÊN BẢN, CẤP, MÃ) ──────────

    /// <summary>Chèn một đơn vị hành chính. Khoá unique là (TenantId, SchemaVersion, Level, Code).</summary>
    private async Task ChenDonViAsync(Guid? tenantId, int schemaVersion, int level, string code, string name, string? parent = null)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "AddrAdministrativeUnits"
                ("Id","TenantId","SchemaVersion","Level","Code","Name","ParentCode","IsActive",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, $5, $6, $7, true, '{}', 's', now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(schemaVersion);
        cmd.Parameters.AddWithValue(level);
        cmd.Parameters.AddWithValue(code);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue((object?)parent ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ADDR001_chan_trung_don_vi_cung_phien_ban_cap_ma_khi_TenantId_NULL()
    {
        await ChenDonViAsync(null, 2, 1, "01", "Thành phố Hà Nội");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDonViAsync(null, 2, 1, "01", "Hà Nội (bản trùng)"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task ADDR001_CHO_PHEP_cung_ma_nhung_KHAC_phien_ban_schema()
    {
        // 🚩 ĐÂY LÀ ĐIỂM CỐT LÕI CỦA ADDR-001: mã `00004` là "Phường Ba Đình" ở v2 nhưng
        // "Phường Trúc Bạch" ở v1. Hai bản ghi này **PHẢI cùng tồn tại** — nếu ràng buộc chỉ dựa trên
        // mã trần thì một trong hai bị chặn (hoặc ghi đè) ⇒ **sai đơn vị**.
        await ChenDonViAsync(null, 2, 2, "00004", "Phường Ba Đình", parent: "01");
        await ChenDonViAsync(null, 1, 2, "00004", "Phường Trúc Bạch", parent: "001");

        var soDong = await DemAsync(
            "select count(*) from \"AddrAdministrativeUnits\" where \"Code\" = '00004'");

        Assert.Equal(2L, soDong);
    }

    [Fact]
    public async Task ADDR001_CHECK_chan_phien_ban_schema_khong_ho_tro()
    {
        // Hàng rào DB: chỉ có 2 phiên bản schema được hỗ trợ (1 = 3 cấp, 2 = 2 cấp).
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDonViAsync(null, 3, 1, "01", "Phiên bản lạ"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    // ── 2d. EINV-001 — SỐ HOÁ ĐƠN không được trùng ──────────────────────────

    private async Task ChenHoaDonAsync(Guid? tenantId, string kyHieu, string so, int status, string? xmlDaKy)
    {
        await using var cmd = _fixture.DataSource.CreateCommand("""
            insert into "EInvInvoices"
                ("Id","TenantId","KyHieuMauSo","SoHoaDon","NgayLap","Status","TaxAuthorityStatus","XmlDaKy",
                 "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
            values ($1, $2, $3, $4, now(), $5, 0, $6, '{}', 's', now(), false)
            """);

        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue((object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(kyHieu);
        cmd.Parameters.AddWithValue(so);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue((object?)xmlDaKy ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task EINV001_chan_trung_SO_HOA_DON_trong_cung_ky_hieu_khi_TenantId_NULL()
    {
        // 🚨 Neu index thieu NULLS NOT DISTINCT thi hai hoa don CUNG SO lot vao DB
        // => vi pham quy dinh ve so hoa don (phai duy nhat va tang lien tuc).
        // ⚠️ Ký hiệu SINH RIÊNG cho test này: các test dùng chung một container PostgreSQL nên
        // dữ liệu của test khác có thể đụng vào (đây chính là lỗi đã gặp khi chạy lần đầu).
        var kyHieu = "DUP" + Guid.NewGuid().ToString("N")[..6];

        await ChenHoaDonAsync(null, kyHieu, "00000001", 1, "<xml/>");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenHoaDonAsync(null, kyHieu, "00000001", 1, "<xml/>"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task EINV001_CHO_PHEP_cung_so_nhung_KHAC_ky_hieu_mau_so()
    {
        // So hoa don lien tuc THEO TUNG ky hieu mau so => hai ky hieu khac nhau co the cung so.
        var ma = Guid.NewGuid().ToString("N")[..6];
        var kyHieuA = "KH" + ma + "A";
        var kyHieuB = "KH" + ma + "B";

        await ChenHoaDonAsync(null, kyHieuA, "00000001", 1, "<xml/>");
        await ChenHoaDonAsync(null, kyHieuB, "00000001", 1, "<xml/>");

        var soDong = await DemAsync(
            $"select count(*) from \"EInvInvoices\" where \"SoHoaDon\" = '00000001' and \"KyHieuMauSo\" in ('{kyHieuA}', '{kyHieuB}')");

        Assert.Equal(2L, soDong);
    }

    [Fact]
    public async Task EINV001_CHECK_chan_phat_hanh_hoa_don_CHUA_KY()
    {
        // 🚨 Hang rao DB cho bat bien PHAP LY: khong the phat hanh hoa don chua ky.
        // Status 2 = DaPhatHanh nhung XmlDaKy = null => CHECK phai chan.
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenHoaDonAsync(null, "1C26TAA", "00000099", 2, null));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    [Fact]
    public async Task EINV001_CHECK_cho_phep_trang_thai_NHAP_khong_co_XML()
    {
        // Doi chung: Nhap (0) thi KHONG can XML da ky.
        await ChenHoaDonAsync(null, "1C26TAA", "00000098", 0, null);

        var soDong = await DemAsync("select count(*) from \"EInvInvoices\" where \"SoHoaDon\" = '00000098'");

        Assert.Equal(1L, soDong);
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

    // ── 2c. 🚨 CAT-001 F7 — xoá cứng `Product` KHÔNG được Cascade xoá biến thể ─────────────

    private async Task ChenSanPhamAsync(Guid id, string code)
    {
        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"CatProducts\" " +
            "(\"Id\",\"TenantId\",\"Code\",\"Name\",\"Status\",\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @code, 'SP test', 0, '{}', 'stamp', now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("code", code);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ChenBienTheAsync(Guid id, Guid productId, string sku)
    {
        // ⚠️ Cột lấy từ **DB THẬT** (`information_schema.columns`), không đoán: `CatProductVariants` là
        // `FullAuditedEntity<Guid>` ⇒ **KHÔNG** có `ExtraProperties` và **KHÔNG** có `ConcurrencyStamp`
        // (khác `CatProducts` là aggregate root). Đã gặp lỗi 42703 khi viết test này vì đoán sai.
        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"CatProductVariants\" " +
            "(\"Id\",\"TenantId\",\"ProductId\",\"Sku\",\"Price\",\"IsActive\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @productId, @sku, 100000.0000, true, now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("sku", sku);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CAT001_F7_xoa_cung_san_pham_bi_CHAN_khi_con_bien_the()
    {
        // 🚨 VÌ SAO TEST NÀY: trước đây FK là `ON DELETE CASCADE` ⇒ xoá cứng một `Product` **XOÁ MỌI
        // BIẾN THỂ** của nó. Biến thể là thứ được **lịch sử bán** (`OrdOrderLines.ProductVariantId`) và
        // **listing sàn** (`ChannelListings.ProductVariantId`) tham chiếu ⇒ xoá cascade **phá lịch sử**
        // và để lại **tham chiếu treo**, KHÔNG có lỗi nào. Nay DB phải **CHẶN** (RESTRICT).
        var productId = Guid.NewGuid();
        var ma = "CAT001-F7-" + Guid.NewGuid().ToString("N")[..6];

        await ChenSanPhamAsync(productId, ma);
        await ChenBienTheAsync(Guid.NewGuid(), productId, "SKU-F7-" + Guid.NewGuid().ToString("N")[..6]);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => XoaCungSanPhamAsync(productId));

        // 23503 = foreign_key_violation
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);

        // Và biến thể PHẢI còn nguyên (không bị xoá theo).
        var conLai = await DemAsync(
            $"select count(*) from \"CatProductVariants\" where \"ProductId\" = '{productId}'");
        Assert.Equal(1L, conLai);
    }

    [Fact]
    public async Task CAT001_F7_rang_buoc_FK_phai_la_RESTRICT_chu_khong_CASCADE()
    {
        // 🚨 Khẳng định **CẤU HÌNH** chứ không chỉ hành vi: nếu ai đó đổi lại thành `Cascade` mà dữ liệu
        // test chưa có biến thể, test hành vi ở trên vẫn có thể xanh ⇒ đây là **hàng rào bổ sung**.
        // `pg_constraint.confdeltype`: 'r' = RESTRICT · 'c' = CASCADE · 'a' = NO ACTION.
        // ⚠️ Ép `::text` là BẮT BUỘC: cột này có kiểu `"char"` của PostgreSQL và Npgsql trả về `char`
        // ⇒ `ExecuteScalarAsync() as string` cho ra **null** (đã gặp thật khi viết test này).
        var kieu = await ChuoiAsync(
            "select confdeltype::text from pg_constraint where conname = 'FK_CatProductVariants_CatProducts_ProductId'");

        Assert.Equal("r", kieu);
    }

    private async Task XoaCungSanPhamAsync(Guid id)
    {
        await using var command = _fixture.DataSource.CreateCommand(
            "delete from \"CatProducts\" where \"Id\" = @id");
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CAT001_xoa_MEM_san_pham_van_DUOC_va_giu_bien_the()
    {
        // Đối chứng: đường HỢP LỆ là xoá MỀM (`IsDeleted = true`) — biến thể vẫn còn ⇒ lịch sử tra được.
        var productId = Guid.NewGuid();

        await ChenSanPhamAsync(productId, "CAT001-MEM-" + Guid.NewGuid().ToString("N")[..6]);
        await ChenBienTheAsync(Guid.NewGuid(), productId, "SKU-MEM-" + Guid.NewGuid().ToString("N")[..6]);

        await using (var command = _fixture.DataSource.CreateCommand(
            "update \"CatProducts\" set \"IsDeleted\" = true, \"DeletionTime\" = now() where \"Id\" = @id"))
        {
            command.Parameters.AddWithValue("id", productId);
            await command.ExecuteNonQueryAsync();
        }

        var bienTheConLai = await DemAsync(
            $"select count(*) from \"CatProductVariants\" where \"ProductId\" = '{productId}'");

        Assert.Equal(1L, bienTheConLai);
    }

    // ── 2d. 🚨 CAT-002 — hàng rào DB của CÂY DANH MỤC ────────────────────────────────────────

    private async Task ChenDanhMucAsync(Guid id, Guid? parentId, string code, string? path = null, int level = 0)
    {
        // Cột lấy từ DB THẬT (`information_schema.columns`) + migration `Cat002_CategoryTree`.
        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"CatCategories\" " +
            "(\"Id\",\"TenantId\",\"ParentId\",\"Code\",\"Name\",\"Path\",\"Level\",\"SortOrder\",\"IsActive\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @parentId, @code, 'Danh muc test', @path, @level, 0, true, '{}', 'stamp', now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("parentId", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("path", path ?? $"/{id}/");
        command.Parameters.AddWithValue("level", level);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CAT002_chan_trung_Code_khi_TenantId_NULL()
    {
        // 🚨 EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`; migration đã sửa TAY.
        // Nếu bước sửa tay bị mất thì test này ĐỎ — vì `TenantId = NULL` là ca THẬT (hệ thống 1 tenant).
        var ma = "CAT002-DUP-" + Guid.NewGuid().ToString("N")[..6];

        await ChenDanhMucAsync(Guid.NewGuid(), null, ma);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDanhMucAsync(Guid.NewGuid(), null, ma));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task CAT002_CHECK_chan_Path_sai_dinh_dang()
    {
        // `CHECK ("Path" LIKE '/%/')` — §8.1 #2 phần ĐỊNH DẠNG (phần ngữ nghĩa phải do test domain lo).
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDanhMucAsync(Guid.NewGuid(), null, "CAT002-PATH-" + Guid.NewGuid().ToString("N")[..6], "khong-co-gach"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);    // 23514
    }

    [Fact]
    public async Task CAT002_CHECK_chan_Level_vuot_do_sau()
    {
        // `CHECK ("Level" >= 0 AND "Level" <= 5)` — §8.1 #3.
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenDanhMucAsync(Guid.NewGuid(), null, "CAT002-SAU-" + Guid.NewGuid().ToString("N")[..6], null, 6));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);    // 23514
    }

    [Fact]
    public async Task CAT002_FK_RESTRICT_chan_xoa_cung_nut_cha_con_con()
    {
        // 🚨 `CAT-002` D9: `Cascade` sẽ **xoá mất cả cây con** khi xoá một nút; `Restrict` buộc đi đường
        // **xoá mềm** + guard. Nếu ai đổi lại thành `Cascade` ⇒ test này ĐỎ.
        var cha = Guid.NewGuid();
        var con = Guid.NewGuid();

        await ChenDanhMucAsync(cha, null, "CAT002-CHA-" + Guid.NewGuid().ToString("N")[..6]);
        await ChenDanhMucAsync(con, cha, "CAT002-CON-" + Guid.NewGuid().ToString("N")[..6], $"/{cha}/{con}/", 1);

        await using (var command = _fixture.DataSource.CreateCommand(
            "delete from \"CatCategories\" where \"Id\" = @id"))
        {
            command.Parameters.AddWithValue("id", cha);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);   // 23503
        }

        // Và nút con PHẢI còn nguyên (không bị xoá theo).
        var conCon = await DemAsync($"select count(*) from \"CatCategories\" where \"Id\" = '{con}'");
        Assert.Equal(1L, conCon);
    }

    [Fact]
    public async Task CAT002_FK_phai_la_RESTRICT_chu_khong_CASCADE()
    {
        // Khẳng định CẤU HÌNH (không chỉ hành vi) — `pg_constraint.confdeltype`: 'r' = RESTRICT.
        // ⚠️ Ép `::text`: cột kiểu `"char"` ⇒ Npgsql trả `char`, `as string` cho ra null (đã gặp thật).
        var kieu = await ChuoiAsync(
            "select confdeltype::text from pg_constraint where conname = 'FK_CatCategories_CatCategories_ParentId'");

        Assert.Equal("r", kieu);
    }

    // ── 2e. 🚨 CAT-002 (lát cắt 3) — LIÊN KẾT SẢN PHẨM ↔ DANH MỤC ──────────────────────────────

    private async Task ChenLienKetAsync(Guid id, Guid productId, Guid categoryId, bool laChinh, string ma)
    {
        // Cột lấy từ DB THẬT + migration `Cat002b_ProductCategory`. FK là `Restrict` ⇒ sản phẩm + danh mục
        // phải tồn tại trước.
        // ⚠️ CHỈ chèn khi CHƯA có: test "nhiều danh mục KHÔNG chính" gọi helper này 3 lần với **cùng**
        // `productId` ⇒ chèn lại sẽ đụng `PK_CatProducts` (23505) và làm test đỏ vì **lý do SAI**.
        // (Không thêm `ON CONFLICT` vào `ChenSanPhamAsync`/`ChenDanhMucAsync` vì test
        //  `CAT002_chan_trung_Code_khi_TenantId_NULL` đang **mong đợi** lỗi trùng phát ra TỪ helper đó.)
        if (await DemAsync($"select count(*) from \"CatProducts\" where \"Id\" = '{productId}'") == 0)
        {
            await ChenSanPhamAsync(productId, $"LK-SP-{ma}");
        }

        if (await DemAsync($"select count(*) from \"CatCategories\" where \"Id\" = '{categoryId}'") == 0)
        {
            await ChenDanhMucAsync(categoryId, null, $"LK-DM-{ma}");
        }

        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"CatProductCategories\" " +
            "(\"Id\",\"TenantId\",\"ProductId\",\"CategoryId\",\"IsPrimary\",\"SortOrder\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @productId, @categoryId, @laChinh, 0, '{}', 'stamp', now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("categoryId", categoryId);
        command.Parameters.AddWithValue("laChinh", laChinh);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CAT002b_chan_GAN_TRUNG_cung_cap_sanpham_danhmuc_khi_TenantId_NULL()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var sanPham = Guid.NewGuid();
        var danhMuc = Guid.NewGuid();

        await ChenLienKetAsync(Guid.NewGuid(), sanPham, danhMuc, false, duoi);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenLienKetAsync(Guid.NewGuid(), sanPham, danhMuc, false, duoi));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task CAT002b_chan_HAI_danh_muc_CHINH_cho_cung_san_pham__BR006()
    {
        // 🚨 Đây là bất biến mà CHỈ unique index CÓ FILTER mới diễn đạt được (BR-006).
        // Mất `NULLS NOT DISTINCT` ⇒ hai dòng "chính" cùng `TenantId = NULL` đều LỌT ⇒ hai breadcrumb/URL SEO.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var sanPham = Guid.NewGuid();

        await ChenLienKetAsync(Guid.NewGuid(), sanPham, Guid.NewGuid(), true, duoi + "a");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenLienKetAsync(Guid.NewGuid(), sanPham, Guid.NewGuid(), true, duoi + "b"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task CAT002b_DOI_CHUNG_nhieu_danh_muc_KHONG_chinh_thi_DUOC_PHEP()
    {
        // 🚨 ĐỐI CHỨNG BẮT BUỘC: nếu index có filter bị viết SAI (bỏ `WHERE "IsPrimary"`) thì nó chặn luôn
        // việc một sản phẩm nằm trong NHIỀU danh mục — điều **hoàn toàn hợp lệ** và rất cần cho storefront.
        // Không có test này thì lỗi đó sẽ chỉ lộ ra khi khách không tìm thấy sản phẩm trong danh mục thứ hai.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var sanPham = Guid.NewGuid();

        await ChenLienKetAsync(Guid.NewGuid(), sanPham, Guid.NewGuid(), false, duoi + "a");
        await ChenLienKetAsync(Guid.NewGuid(), sanPham, Guid.NewGuid(), false, duoi + "b");
        await ChenLienKetAsync(Guid.NewGuid(), sanPham, Guid.NewGuid(), false, duoi + "c");

        var soLienKet = await DemAsync(
            $"select count(*) from \"CatProductCategories\" where \"ProductId\" = '{sanPham}'");

        Assert.Equal(3L, soLienKet);
    }

    [Fact]
    public async Task CAT002b_FK_RESTRICT_chan_xoa_cung_danh_muc_con_san_pham()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var sanPham = Guid.NewGuid();
        var danhMuc = Guid.NewGuid();

        await ChenLienKetAsync(Guid.NewGuid(), sanPham, danhMuc, false, duoi);

        await using var command = _fixture.DataSource.CreateCommand(
            "delete from \"CatCategories\" where \"Id\" = @id");
        command.Parameters.AddWithValue("id", danhMuc);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);   // 23503
    }

    // ── 2f. 🚨 PRC-002 — HÀNG RÀO DB CHO ADR-02 ("hai bậc cùng MinQuantity ⇒ NÉM") ─────────────

    private async Task ChenBoBacAsync(Guid id, string code)
    {
        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"PrcTierPriceSets\" " +
            "(\"Id\",\"TenantId\",\"Code\",\"Name\",\"Scope\",\"Currency\",\"EffectiveFrom\",\"Priority\",\"Status\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @code, 'Bo bac test', 0, 'VND', now(), 0, 0, '{}', 'stamp', now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("code", code);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ChenBacAsync(Guid id, Guid setId, string sku, int minQty)
    {
        await using var command = _fixture.DataSource.CreateCommand(
            "insert into \"PrcTierPrices\" " +
            "(\"Id\",\"TenantId\",\"TierPriceSetId\",\"Sku\",\"MinQuantity\",\"AmountValue\",\"CurrencyCode\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            "values (@id, null, @setId, @sku, @minQty, 90000, 'VND', '{}', 'stamp', now(), false)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("setId", setId);
        command.Parameters.AddWithValue("sku", sku);
        command.Parameters.AddWithValue("minQty", minQty);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task PRC002_chan_HAI_bac_CUNG_MinQuantity_khi_TenantId_NULL()
    {
        // 🚨 Hàng rào DB cho ADR-02. Mất `NULLS NOT DISTINCT` ⇒ hai bậc trùng LỌT ⇒ `ChonBacThang` NÉM `0008`
        // ⇒ **checkout chết** cho SKU đó (không phải bán sai giá, nhưng vẫn hỏng đường bán).
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var setId = Guid.NewGuid();
        var sku = $"PRC002-SKU-{duoi}";

        await ChenBoBacAsync(setId, $"PRC002-BT-{duoi}");
        await ChenBacAsync(Guid.NewGuid(), setId, sku, 10);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => ChenBacAsync(Guid.NewGuid(), setId, sku, 10));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task PRC002_DOI_CHUNG_cung_MinQuantity_nhung_KHAC_Sku_thi_DUOC_PHEP()
    {
        // Đối chứng: index phải là `(set, SKU, MinQuantity)` — nếu ai đó bỏ `Sku` khỏi index thì
        // **một bộ bậc chỉ chứa được MỘT SKU** ⇒ mọi SKU khác mất bậc thang mà không có lỗi nào.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var setId = Guid.NewGuid();

        await ChenBoBacAsync(setId, $"PRC002-BT2-{duoi}");
        await ChenBacAsync(Guid.NewGuid(), setId, $"PRC002-A-{duoi}", 10);
        await ChenBacAsync(Guid.NewGuid(), setId, $"PRC002-B-{duoi}", 10);

        var soBac = await DemAsync($"select count(*) from \"PrcTierPrices\" where \"TierPriceSetId\" = '{setId}'");

        Assert.Equal(2L, soBac);
    }

    // ── 2g. 🚨 INV-001 — RÀNG BUỘC DB CHO TỒN KHO (ADR-03) + CHỐNG GHI TRÙNG SỔ CÁI (ADR-05) ──────

    private async Task ChayAsync(string sql)
    {
        await using var command = _fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task INV001_CHECK_chan_OnHand_AM__hang_rao_cuoi_cho_ban_thua_hang()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var id = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChayAsync(
            "insert into \"InvItems\" (\"Id\",\"TenantId\",\"WarehouseId\",\"Sku\",\"OnHand\",\"Reserved\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            $"values ('{id}', null, '{Guid.NewGuid()}', 'INV-CHK-{duoi}', -1, 0, '{{}}', 'x', now(), false)"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);   // 23514
    }

    [Fact]
    public async Task INV001_CHECK_chan_Reserved_VUOT_OnHand__Available_AM()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var id = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChayAsync(
            "insert into \"InvItems\" (\"Id\",\"TenantId\",\"WarehouseId\",\"Sku\",\"OnHand\",\"Reserved\"," +
            "\"ExtraProperties\",\"ConcurrencyStamp\",\"CreationTime\",\"IsDeleted\") " +
            $"values ('{id}', null, '{Guid.NewGuid()}', 'INV-CHK2-{duoi}', 1, 2, '{{}}', 'x', now(), false)"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task INV001_unique_chan_GHI_TRUNG_so_cai_theo_ReferenceLineId()
    {
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var lineId = Guid.NewGuid();
        var kho = Guid.NewGuid();

        string Lenh(Guid id) =>
            "insert into \"InvStockLedger\" (\"Id\",\"TenantId\",\"WarehouseId\",\"Sku\",\"MovementType\"," +
            "\"QuantityDelta\",\"BalanceAfter\",\"ReferenceLineId\",\"OccurredAtUtc\"," +
            $"\"CreationTime\",\"IsDeleted\") values ('{id}', null, '{kho}', 'INV-LED-{duoi}', " +
            $"0, 5, 5, '{lineId}', now(), now(), false)";

        await ChayAsync(Lenh(Guid.NewGuid()));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ChayAsync(Lenh(Guid.NewGuid())));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);   // 23505
    }

    [Fact]
    public async Task INV001_nhieu_but_toan_KHONG_tham_chieu_van_DUOC__nho_bo_loc_IS_NOT_NULL()
    {
        // 🚨 ĐỐI CHỨNG BẮT BUỘC: bút toán điều chỉnh tay KHÔNG có dòng chứng từ (ReferenceLineId = NULL).
        // Nếu unique index KHÔNG có bộ lọc `IS NOT NULL` thì toàn hệ thống chỉ được phép có **MỘT** bút toán
        // không tham chiếu ⇒ **khoá cứng nghiệp vụ điều chỉnh** mà triệu chứng là lỗi unique rất khó hiểu.
        var duoi = Guid.NewGuid().ToString("N")[..6];
        var kho = Guid.NewGuid();

        string Lenh(Guid id, int delta) =>
            "insert into \"InvStockLedger\" (\"Id\",\"TenantId\",\"WarehouseId\",\"Sku\",\"MovementType\"," +
            "\"QuantityDelta\",\"BalanceAfter\",\"ReferenceLineId\",\"OccurredAtUtc\"," +
            $"\"CreationTime\",\"IsDeleted\") values ('{id}', null, '{kho}', 'INV-NUL-{duoi}', " +
            $"0, {delta}, 5, null, now(), now(), false)";

        await ChayAsync(Lenh(Guid.NewGuid(), 1));
        await ChayAsync(Lenh(Guid.NewGuid(), 2));

        var so = await DemAsync($"select count(*) from \"InvStockLedger\" where \"Sku\" = 'INV-NUL-{duoi}'");

        Assert.Equal(2L, so);
    }
}

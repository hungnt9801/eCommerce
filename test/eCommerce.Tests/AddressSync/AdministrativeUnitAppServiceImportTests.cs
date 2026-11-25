using eCommerce.Address;
using eCommerce.Address.Caching;
using eCommerce.Address.Events;
using eCommerce.Tests.AddressSync.HaTang;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` BR-009 + BR-014 + `D-78`/`D-81`/`D-82`/`D-241`** — đường nạp danh mục qua
/// `AdministrativeUnitAppService.ImportDatasetAsync`:
///   • **TC-21(a)**: `SourceName` rỗng / `schemaVersion` sai ⇒ NÉM **trước** khoá, **0** dòng lô;
///   • **TC-21(b/c)**: lượt hợp lệ trả `CatalogSyncRunDto` của lượt mình; nạp lại cùng dữ liệu ⇒ **2** dòng lô
///     **cùng** `SourceHash`, lượt 2 = 0 thay đổi (không early-return);
///   • **TC-21(f) / TC-24(e)**: nguồn đổi `ParentCode` ⇒ `0008`; **dòng từ chối (`BiChan`) vẫn đọc được** sau
///     rollback, ghi ở UoW **riêng** `requiresNew: true`, và **0** đơn vị danh mục bị đổi;
///   • **TC-14**: nạp lô ⇒ xoá cache **kể cả parent vừa biến mất**;
///   • **TC-25**: khoá bận ⇒ NÉM `eCommerce:Lock:0001` (**không** `TimeoutException`), **0** dòng lô.
///
/// 📌 Dựng app service bằng `new` + fake in-memory (`HaTangDiaChi`) — **không** host ABP, **không** PostgreSQL.
/// </summary>
public class AdministrativeUnitAppServiceImportTests
{
    private const string MaTinh = "01";

    // ── Dữ liệu nguồn hợp lệ: 1 tỉnh + 9 xã = 10 đơn vị (qua hàng rào 2 "quá ít") ─────────────
    private static ImportDatasetInput Nguon(
        string source = "NQ 60-NQ/TW",
        int schemaVersion = 2,
        string? parentCuaXa1 = null,
        string? tenXa1 = null)
    {
        var rows = new List<ImportUnitRow>
        {
            new(AdministrativeLevel.Tinh, MaTinh, "Thành phố Hà Nội", null)
        };

        for (var i = 1; i <= 9; i++)
        {
            var ma = $"0000{i}";
            var ten = i == 1 && tenXa1 is not null ? tenXa1 : $"Phường {i}";
            var cha = i == 1 && parentCuaXa1 is not null ? parentCuaXa1 : MaTinh;
            rows.Add(new ImportUnitRow(AdministrativeLevel.Xa, ma, ten, cha));
        }

        return new ImportDatasetInput(schemaVersion, source, rows);
    }

    /// <summary>Seed repo bằng đúng danh mục nguồn (10 đơn vị) + tuỳ chọn thêm đơn vị cũ.</summary>
    private static void SeedDanhMuc(
        RepoDiaChiGia<AdministrativeUnit> repo,
        ImportDatasetInput nguon,
        params (AdministrativeLevel Cap, string Ma, string Ten, string? Cha)[] them)
    {
        foreach (var row in nguon.Rows)
        {
            repo.Them(AdministrativeUnit.TaoMoi(
                AdministrativeSchemaVersion.V2, row.Level, row.Code, row.Name, row.ParentCode));
        }

        foreach (var (cap, ma, ten, cha) in them)
        {
            repo.Them(AdministrativeUnit.TaoMoi(AdministrativeSchemaVersion.V2, cap, ma, ten, cha));
        }
    }

    // ── TC-21(b/c): lượt hợp lệ trả DTO + nạp lại cùng dữ liệu ⇒ 2 dòng lô cùng hash ──────────

    [Fact]
    public async Task TC21b_import_hop_le_tra_CatalogSyncRunDto_cua_luot_minh()
    {
        var (sut, bo) = HaTangDiaChi.Dung();
        var nguon = Nguon();

        var kq = await sut.ImportDatasetAsync(nguon);

        Assert.Equal(2, kq.SchemaVersion);
        Assert.Equal(CatalogSyncTrigger.Import, kq.Trigger);
        Assert.Equal("NQ 60-NQ/TW", kq.SourceName);
        Assert.Equal(64, kq.SourceHash!.Length);
        Assert.Equal(CatalogSyncOutcome.DaApDung, kq.Outcome);
        Assert.Null(kq.ErrorCode);
        Assert.Equal(10, kq.SoThemMoi);                       // 1 tỉnh + 9 xã
        Assert.StartsWith("user:", kq.ImportedBy, StringComparison.Ordinal);
        Assert.Equal(10, bo.DonVi.TatCa.Count);
    }

    [Fact]
    public async Task TC21c_import_HAI_lan_cung_du_lieu_thi_HAI_dong_lo_cung_SourceHash()
    {
        var (sut, bo) = HaTangDiaChi.Dung();
        var nguon = Nguon();

        var lan1 = await sut.ImportDatasetAsync(nguon);
        var lan2 = await sut.ImportDatasetAsync(nguon);

        Assert.Equal(2, bo.LoNap.TatCa.Count);               // BR-009: 0 thay đổi VẪN ghi dòng lô
        Assert.Equal(lan1.SourceHash, lan2.SourceHash);       // hash ổn định (không Guid.NewGuid)
        Assert.Equal(10, lan1.SoThemMoi);
        Assert.Equal(0, lan2.SoThemMoi);                      // lượt 2 không thêm gì
        Assert.Equal(CatalogSyncOutcome.DaApDung, lan2.Outcome);
    }

    // ── TC-14: nạp lô ⇒ cache sạch, KỂ CẢ parent vừa biến mất ─────────────────────────────────

    [Fact]
    public async Task TC14_import_thi_xoa_cache_ca_parent_VUA_BIEN_MAT()
    {
        var (sut, bo) = HaTangDiaChi.Dung();
        var nguon = Nguon();

        // Danh mục hiện có = đúng nguồn + 1 xã cũ thuộc tỉnh "02" (tỉnh 02 sắp biến mất khỏi danh mục).
        SeedDanhMuc(bo.DonVi, nguon, (AdministrativeLevel.Xa, "00099", "Phường cũ", "02"));

        bo.CachePhanTan.DuLieu["v2:*"] = new AddressChildrenCacheItem();
        bo.CachePhanTan.DuLieu["v2:01"] = new AddressChildrenCacheItem();
        bo.CachePhanTan.DuLieu["v2:02"] = new AddressChildrenCacheItem();

        await sut.ImportDatasetAsync(nguon);

        // Tập xoá = hợp {null} ∪ parent mới ({01}) ∪ parent hiện có trước commit ({01, 02}).
        Assert.DoesNotContain("v2:*", bo.CachePhanTan.DuLieu.Keys);
        Assert.DoesNotContain("v2:01", bo.CachePhanTan.DuLieu.Keys);
        Assert.DoesNotContain("v2:02", bo.CachePhanTan.DuLieu.Keys);

        // ETO mang đủ tập parentCodes (để instance khác xoá cache của chúng).
        var eto = Assert.IsType<AddressCacheInvalidatedEto>(Assert.Single(bo.Bus.DaPhat).DuLieu);
        Assert.True(Assert.Single(bo.Bus.DaPhat).UseOutbox, "ETO xoá cache PHẢI phát qua outbox (D-242).");
        Assert.Contains("02", eto.ParentCodes);
    }

    // ── TC-21(f) / TC-24(e): đổi ParentCode ⇒ 0008, dòng từ chối CÒN ĐỌC ĐƯỢC ─────────────────

    [Fact]
    public async Task TC21f_TC24e_doi_ParentCode_thi_NEM_0008_va_dong_tu_choi_CON_DOC_DUOC()
    {
        var (sut, bo) = HaTangDiaChi.Dung();
        var nguonCu = Nguon();
        SeedDanhMuc(bo.DonVi, nguonCu);

        // Nguồn đổi cha xã 00001 từ "01" sang "02" (danh tính đổi).
        var nguonMoi = Nguon(parentCuaXa1: "02");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.ImportDatasetAsync(nguonMoi));

        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, ex.Code);   // eCommerce:Address:0008
        Assert.Equal(1, (int)ex.Data["soDoiCha"]!);

        // 🔑 0 thay đổi danh mục: ParentCode entity GIỮ NGUYÊN (lô bị rollback toàn bộ).
        Assert.Equal(0, bo.DonVi.SoLanChen);
        Assert.Equal(0, bo.DonVi.SoLanSua);
        Assert.Equal("01", bo.DonVi.TatCa.Single(x => x.Code == "00001").ParentCode);

        // 🔑 Dòng lô TỪ CHỐI vẫn đọc được SAU rollback — ghi ở UoW RIÊNG `requiresNew: true`.
        var lo = Assert.Single(bo.LoNap.TatCa);
        Assert.Equal(CatalogSyncOutcome.BiChan, lo.Outcome);
        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, lo.ErrorCode);
        Assert.Equal(CatalogSyncTrigger.Import, lo.Trigger);
        Assert.Equal(64, lo.SourceHash!.Length);
        Assert.Equal(0, lo.SoThemMoi);                        // 4 số đếm = 0 cho lượt bị chặn

        var begin = Assert.Single(bo.Uow.LanBegin);
        Assert.True(begin.RequiresNew, "Dòng lô từ chối PHẢI ghi ở UoW riêng (requiresNew: true) — D-78/BR-009.");
        Assert.True(begin.IsTransactional);
        Assert.True(bo.Uow.Uows.Single().IsCompleted);
    }

    [Fact]
    public async Task TC24e_doi_ParentCode_qua_import_cung_bi_0008_nhu_job()
    {
        // Cùng hàng rào 5 cho CẢ HAI đường (D-81): import không có luật riêng.
        var (sut, bo) = HaTangDiaChi.Dung();
        SeedDanhMuc(bo.DonVi, Nguon());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.ImportDatasetAsync(Nguon(parentCuaXa1: "79")));

        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, ex.Code);
        Assert.Equal(CatalogSyncOutcome.BiChan, Assert.Single(bo.LoNap.TatCa).Outcome);
    }

    // ── TC-21(a): validate TRƯỚC khoá ⇒ 0 dòng lô, không chiếm khoá ────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TC21a_SourceName_rong_thi_NEM_0002_TRUOC_khoa_0_dong_lo(string? source)
    {
        var (sut, bo) = HaTangDiaChi.Dung();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.ImportDatasetAsync(Nguon(source: source!)));

        Assert.Equal(AddressErrorCodes.MaKhongHopLe, ex.Code);   // eCommerce:Address:0002
        Assert.Empty(bo.LoNap.TatCa);
        Assert.Empty(bo.Uow.LanBegin);
        Assert.Empty(bo.Khoa.TenDaXin);                          // chưa chiếm khoá
    }

    [Fact]
    public async Task TC21a_schemaVersion_sai_thi_NEM_0005_TRUOC_khoa_0_dong_lo()
    {
        var (sut, bo) = HaTangDiaChi.Dung();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.ImportDatasetAsync(Nguon(schemaVersion: 3)));

        Assert.Equal(AddressErrorCodes.PhienBanKhongHopLe, ex.Code);   // eCommerce:Address:0005
        Assert.Empty(bo.LoNap.TatCa);
        Assert.Empty(bo.Uow.LanBegin);
        Assert.Empty(bo.Khoa.TenDaXin);
    }

    // ── P2 #2 (`D-252`): SourceName > 512 ⇒ 0002 TRƯỚC khoá, 0 dòng lô — biên đúng 512 vẫn nạp ───

    [Fact]
    public async Task P2_2_SourceName_513_ky_tu_thi_NEM_0002_TRUOC_khoa_0_dong_lo()
    {
        var (sut, bo) = HaTangDiaChi.Dung();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.ImportDatasetAsync(Nguon(source: new string('x', 513))));

        Assert.Equal(AddressErrorCodes.MaKhongHopLe, ex.Code);          // eCommerce:Address:0002
        Assert.Equal("SourceName", ex.Data["truong"]);
        Assert.Empty(bo.LoNap.TatCa);                                   // 0 dòng lô
        Assert.Empty(bo.Uow.LanBegin);                                  // chưa mở UoW
        Assert.Empty(bo.Khoa.TenDaXin);                                 // chưa chiếm khoá
    }

    [Fact]
    public async Task P2_2_SourceName_dung_512_ky_tu_van_nap_duoc()
    {
        var (sut, bo) = HaTangDiaChi.Dung();
        var source = new string('x', 512);

        var kq = await sut.ImportDatasetAsync(Nguon(source: source));

        Assert.Equal(CatalogSyncOutcome.DaApDung, kq.Outcome);
        Assert.Equal(512, kq.SourceName!.Length);
        Assert.Single(bo.LoNap.TatCa);
    }

    // ── TC-25: khoá bận ⇒ NÉM eCommerce:Lock:0001 (KHÔNG TimeoutException), 0 dòng lô ──────────

    [Fact]
    public async Task TC25_khoa_ban_thi_NEM_Lock_0001_khong_phai_TimeoutException()
    {
        var (sut, bo) = HaTangDiaChi.Dung(luonBanKhoa: true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.ImportDatasetAsync(Nguon()));

        Assert.Equal("eCommerce:Lock:0001", ex.Code);
        Assert.NotEqual("eCommerce:Address:0001", ex.Code);
        Assert.Empty(bo.LoNap.TatCa);
        Assert.Single(bo.Khoa.TenDaXin);
        Assert.Equal(AddressLockKeys.CatalogImport, bo.Khoa.TenDaXin[0]);
    }

    // ── Nhánh "lỗi không mong đợi" (Outcome = Loi) cũng ghi dòng lô, rồi ném lại ────────────────

    [Fact]
    public async Task TC21_loi_khong_mong_doi_thi_ghi_dong_lo_Loi_roi_nem_lai()
    {
        var (sut, bo) = HaTangDiaChi.Dung();

        // Làm hỏng đường đọc danh mục hiện có ⇒ ném lỗi KHÔNG thuộc hàng rào (nên phân loại `Loi`).
        bo.DonVi.HongGetList = true;

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.ImportDatasetAsync(Nguon()));

        var lo = Assert.Single(bo.LoNap.TatCa);
        Assert.Equal(CatalogSyncOutcome.Loi, lo.Outcome);
        Assert.Null(lo.ErrorCode);
        Assert.Equal(typeof(NotSupportedException).FullName, lo.ErrorType);
        Assert.True(Assert.Single(bo.Uow.LanBegin).RequiresNew);
    }
}

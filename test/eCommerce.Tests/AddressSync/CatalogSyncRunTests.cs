using eCommerce.Address;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` `D-252` P2 #2** — hàng rào độ dài `SourceName` ở tầng **domain**:
/// cột `AddrCatalogSyncRuns.SourceName` là `character varying(512)` ⇒ `CatalogSyncRun.Ghi` phải **NÉM**
/// khi vượt 512 (không cắt im lặng, không để lỗi 500 lúc commit UoW ở nhánh worker/job).
///
/// 📌 Hàng rào app service (`AdministrativeUnitAppServiceImportTests`) chặn **trước khoá** cho đường import;
/// ca này khoá hàng rào **chung** ở factory để mọi đường ghi (worker/job) đều fail-closed.
/// </summary>
public class CatalogSyncRunTests
{
    private static readonly DateTime BatDau = new(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);

    private static CatalogSyncRun Ghi(string? sourceName)
        => CatalogSyncRun.Ghi(
            schemaVersion: 2,
            trigger: CatalogSyncTrigger.Job,
            sourceName: sourceName,
            sourceHash: null,
            startedAtUtc: BatDau,
            importedAtUtc: BatDau.AddSeconds(1),
            importedBy: "user:qa",
            outcome: CatalogSyncOutcome.DaApDung,
            errorCode: null,
            errorType: null,
            soThemMoi: 0,
            soDoiTen: 0,
            soMoLai: 0,
            soDong: 0);

    // ── P2 #2 (`D-252`): SourceName 513 ký tự ⇒ ArgumentException, KHÔNG trả entity ─────────────

    [Fact]
    public void P2_2_Ghi_sourceName_513_ky_tu_thi_NEM_ArgumentException_khong_tra_entity()
    {
        var ex = Assert.Throws<ArgumentException>(() => Ghi(new string('x', 513)));

        Assert.Equal("sourceName", ex.ParamName);
    }
}

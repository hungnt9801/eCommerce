using eCommerce.Notification;
using Volo.Abp;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-07`/`TC-08`/`TC-42` — **version đã publish là BẤT BIẾN** + mục **ghim version** (`SRS §5.2`/`§3.2`),
/// và `TC-25` — **người tạo lô không được tự duyệt** (`SRS §3.3`). Tất cả ở **tầng Domain thuần, không DB**.
/// </summary>
public class NotificationTemplateAndBatchTests
{
    // ── TC-07: version đã Published là bất biến ───────────────────────────────────────────────

    [Fact]
    public void TC07_sua_version_da_publish_thi_NEM_0016_va_hash_khong_doi()
    {
        var template = NotificationTestData.Template();
        var version = NotificationTestData.Version(template.Id, subject: "A", bodyText: "B {{Ten}}");
        var hashTruoc = version.ContentHash;

        var ex = Assert.Throws<BusinessException>(
            () => version.SuaNoiDung("Nội dung mới", "Thân mới {{Ten}}", []));

        Assert.Equal(NotificationErrorCodes.PhienBanBatBien, ex.Code);
        Assert.Equal(hashTruoc, version.ContentHash);
        Assert.Equal("A", version.Subject);
        Assert.Equal("B {{Ten}}", version.BodyText);
    }

    [Fact]
    public void TC07_ContentHash_tai_lap_va_khac_khi_noi_dung_khac()
    {
        var placeholders = new[] { new NotificationPlaceholder("Ten", NotificationPlaceholderKind.Text, true) };
        var h1 = NotificationTemplateVersion.TinhContentHash("A", "B", placeholders);
        var h2 = NotificationTemplateVersion.TinhContentHash("A", "B", placeholders);
        var h3 = NotificationTemplateVersion.TinhContentHash("A", "B khác", placeholders);

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
        Assert.True(NotificationHash.LaHex64(h1));
    }

    [Fact]
    public void TC07_thu_tu_placeholder_khac_nhau_van_cung_hash_canonical()
    {
        // ContentHash canonical sắp `Ordinal` theo tên ⇒ đổi thứ tự khai KHÔNG đổi hash.
        var a = new NotificationPlaceholder("A", NotificationPlaceholderKind.Text, true);
        var b = new NotificationPlaceholder("B", NotificationPlaceholderKind.Text, false);

        var h1 = NotificationTemplateVersion.TinhContentHash("s", "body", [a, b]);
        var h2 = NotificationTemplateVersion.TinhContentHash("s", "body", [b, a]);

        Assert.Equal(h1, h2);
    }

    // ── TC-08: mục ghim version — publish v2 không đổi nội dung mục cũ ────────────────────────

    [Fact]
    public void TC08_publish_version_moi_khong_doi_noi_dung_muc_ghim_v1()
    {
        var template = NotificationTestData.Template();
        var v1 = NotificationTestData.Version(template.Id, subject: "V1", bodyText: "Thân V1");
        template.PublishVersion(v1.Id);

        // Mục ghim v1.
        var muc = NotificationTestData.QueueItem(templateVersionId: v1.Id);
        Assert.Equal(v1.Id, muc.TemplateVersionId);

        // Publish v2.
        var v2 = NotificationTestData.Version(template.Id, subject: "V2", bodyText: "Thân V2", version: 2);
        template.PublishVersion(v2.Id);

        // Mục vẫn ghim v1 và đọc v1 theo Id ra ĐÚNG nội dung cũ (không bị v2 ghi đè).
        Assert.Equal(v1.Id, muc.TemplateVersionId);
        Assert.Equal("V1", v1.Subject);
        Assert.Equal("Thân V1", v1.BodyText);
        Assert.Equal("V2", v2.Subject);
        Assert.Equal(v2.Id, template.CurrentPublishedVersionId);
    }

    [Fact]
    public void TC08_version_archived_van_doc_duoc_theo_Id()
    {
        var template = NotificationTestData.Template();
        var v1 = NotificationTestData.Version(template.Id, subject: "V1", bodyText: "Thân V1");
        template.PublishVersion(v1.Id);
        template.Archive();

        // Archived ở template KHÔNG xoá version; đọc theo Id vẫn trả nội dung cũ.
        Assert.Equal("Thân V1", v1.BodyText);
        Assert.Equal(NotificationTemplateStatus.Archived, template.Status);
    }

    // ── TC-42: GĐ3 chỉ Text — Html bị từ chối TRƯỚC khi ghi ──────────────────────────────────

    [Fact]
    public void TC42_version_Html_thi_NEM_0020()
    {
        var ex = Assert.Throws<BusinessException>(
            () => NotificationTestData.Version(Guid.NewGuid(), bodyFormat: NotificationBodyFormat.Html));

        Assert.Equal(NotificationErrorCodes.BodyFormatKhongHoTro, ex.Code);
    }

    [Fact]
    public void TC42_version_hop_le_co_BodyFormat_Text()
    {
        var version = NotificationTestData.Version(Guid.NewGuid());

        Assert.Equal(NotificationBodyFormat.Text, version.BodyFormat);
    }

    [Fact]
    public void TaoMoi_version_so_thu_tu_khong_hop_le_thi_NEM_0013()
    {
        var ex = Assert.Throws<BusinessException>(
            () => NotificationTestData.Version(Guid.NewGuid(), version: 0));

        Assert.Equal(NotificationErrorCodes.ThieuTruongBatBuoc, ex.Code);
    }

    // ── Template: publish/archive guard ───────────────────────────────────────────────────────

    [Fact]
    public void Template_archive_hai_lan_thi_NEM_0011()
    {
        var template = NotificationTestData.Template();
        template.Archive();

        var ex = Assert.Throws<BusinessException>(() => template.Archive());
        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void Template_publish_sau_archive_thi_NEM_0011()
    {
        var template = NotificationTestData.Template();
        template.Archive();

        var ex = Assert.Throws<BusinessException>(() => template.PublishVersion(Guid.NewGuid()));
        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    // ── TC-25: tách nhiệm vụ — người tạo lô KHÔNG được tự duyệt ───────────────────────────────

    [Fact]
    public void TC25_nguoi_tao_lo_khong_duoc_tu_duyet()
    {
        var nguoiTao = Guid.NewGuid();
        var lo = NotificationBatch.TaoMoi(
            Guid.NewGuid(), "lo-1", Guid.NewGuid(), Guid.NewGuid(), "idem-lo-1", 10,
            createdByUserId: nguoiTao, correlationId: "corr", tenantId: null);

        var ex = Assert.Throws<BusinessException>(() => lo.Duyet(nguoiTao, NotificationTestData.Now));

        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
        Assert.Equal(NotificationBatchStatus.ChoDuyet, lo.Status);
    }

    [Fact]
    public void TC25_nguoi_khac_duyet_duoc_va_lo_sang_DaDuyet()
    {
        var nguoiTao = Guid.NewGuid();
        var nguoiDuyet = Guid.NewGuid();
        var lo = NotificationBatch.TaoMoi(
            Guid.NewGuid(), "lo-2", Guid.NewGuid(), Guid.NewGuid(), "idem-lo-2", 10,
            createdByUserId: nguoiTao, correlationId: "corr", tenantId: null);

        lo.Duyet(nguoiDuyet, NotificationTestData.Now);

        Assert.Equal(NotificationBatchStatus.DaDuyet, lo.Status);
        Assert.Equal(nguoiDuyet, lo.ApprovedByUserId);
        Assert.Equal(NotificationTestData.Now, lo.ApprovedAtUtc);
    }

    [Fact]
    public void Batch_duyet_lan_hai_thi_NEM_0011()
    {
        var lo = NotificationBatch.TaoMoi(
            Guid.NewGuid(), "lo-3", Guid.NewGuid(), Guid.NewGuid(), "idem-lo-3", 1,
            createdByUserId: Guid.NewGuid(), correlationId: "corr", tenantId: null);
        lo.Duyet(Guid.NewGuid(), NotificationTestData.Now);

        var ex = Assert.Throws<BusinessException>(() => lo.Duyet(Guid.NewGuid(), NotificationTestData.Now));
        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void Batch_huy_lan_hai_thi_NEM_0011()
    {
        var lo = NotificationBatch.TaoMoi(
            Guid.NewGuid(), "lo-4", Guid.NewGuid(), Guid.NewGuid(), "idem-lo-4", 1,
            createdByUserId: Guid.NewGuid(), correlationId: "corr", tenantId: null);
        lo.Huy("ly-do");

        var ex = Assert.Throws<BusinessException>(() => lo.Huy("lan-nua"));
        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }
}

using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;

// 📌 Gộp DTO + module + app service vào MỘT file để giữ số file nhỏ; tách ra khi module lớn lên.
namespace eCommerce.EInvoice.Application;

// ─────────────────────────── DTO ───────────────────────────

public sealed record EInvoiceDto(
    Guid Id,
    string KyHieuMauSo,
    string SoHoaDon,
    DateTime NgayLap,
    EInvoiceStatus Status,
    TaxAuthorityStatus TaxAuthorityStatus,
    string? MaCQT,
    string? LoiCQT)
{
    public static EInvoiceDto Tu(EInvoice hd) => new(
        hd.Id, hd.KyHieuMauSo, hd.SoHoaDon, hd.NgayLap, hd.Status, hd.TaxAuthorityStatus, hd.MaCQT, hd.LoiCQT);
}

public sealed record TaoHoaDonRequest(string KyHieuMauSo, DateTime NgayLap);

public sealed record KyHoaDonRequest(string XmlDaKy);

public sealed record DanhDauKetQuaCqtRequest(string PhanHoiCqt);

// ─────────────────────────── Module ABP ───────────────────────────

/// <summary>
/// Module tầng Application của `EInvoice`.
/// 🚨 BẮT BUỘC có class này VÀ phải được khai trong `[DependsOn]` của Host — thiếu thì assembly
/// không nằm trong đồ thị module ⇒ ABP không quét app service ⇒ API **`404` body rỗng** (bài học C8/C16,
/// đã trả giá 2 lần trong dự án này).
/// </summary>
[DependsOn(
    typeof(EInvoiceDomainModule),
    typeof(AbpDddApplicationModule)
)]
public class EInvoiceApplicationModule : AbpModule
{
    public override async Task OnApplicationInitializationAsync(Volo.Abp.ApplicationInitializationContext context)
    {
        // 🔒 Worker CỐ ĐỊNH (không phải dynamic) ⇒ không có nguy cơ "không bao giờ chạy" như lỗi C10.
        await context.AddBackgroundWorkerAsync<EInvoiceResubmissionWorker>();
    }
}

// ─────────────────────────── App service ───────────────────────────

/// <summary>
/// API **phát hành hoá đơn điện tử** (`EINV-001` + `EINV-002`).
///
/// 🔒 `[Authorize]` ở **CẤP CLASS** (quy ước #6 — test reflection `TC-01` của `XC-004` sẽ đỏ nếu thiếu).
///
/// 🎯 VÌ SAO APP SERVICE NÀY MỎNG: toàn bộ luật nghiệp vụ đã nằm ở tầng Domain đã kiểm chứng
/// (state machine 2 trục, cấp số an toàn, parser `OK:`/`ERR:`). App service **chỉ** nạp entity, gọi
/// đúng một hàm domain, rồi lưu. Nếu nhồi thêm luật vào đây thì luật sẽ **không được test** ở tầng có
/// test tốt nhất (Domain) — và lại sinh ra hai nguồn sự thật.
/// </summary>
[Authorize]
public class EInvoiceAppService : ApplicationService
{
    private readonly IRepository<EInvoice, Guid> _hoaDon;
    private readonly EInvoiceNumberIssuer _capSo;

    public EInvoiceAppService(IRepository<EInvoice, Guid> hoaDon, EInvoiceNumberIssuer capSo)
    {
        _hoaDon = hoaDon;
        _capSo = capSo;
    }

    /// <summary>Tạo hoá đơn nháp. **Số hoá đơn do hệ thống cấp** (không nhận từ client).</summary>
    public async Task<EInvoiceDto> TaoHoaDonAsync(TaoHoaDonRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        // Cấp số nằm trong khoá phân tán (đã kiểm chứng) — ném lỗi nếu không giành được khoá.
        var soHoaDon = await _capSo.CapSoMoiAsync(yeuCau.KyHieuMauSo);

        var hoaDon = EInvoice.TaoMoi(yeuCau.KyHieuMauSo, soHoaDon, yeuCau.NgayLap);

        await _hoaDon.InsertAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    public async Task<EInvoiceDto> KyAsync(Guid id, KyHoaDonRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var hoaDon = await _hoaDon.GetAsync(id);
        hoaDon.Ky(yeuCau.XmlDaKy);          // state machine ở Domain ném lỗi nếu sai
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    /// <summary>Phát hành. Domain **bắt buộc đã ký** (`eCommerce:EInvoice:0102`).</summary>
    public async Task<EInvoiceDto> PhatHanhAsync(Guid id)
    {
        var hoaDon = await _hoaDon.GetAsync(id);
        hoaDon.PhatHanh();
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    /// <summary>
    /// Ghi nhận **phản hồi của cơ quan thuế** đã nhận được (do job hoặc người dùng dán vào).
    /// 🔒 Việc phân tích `OK:`/`ERR:` do `EInvoiceResponseParser` quyết định (**fail-closed**) — app service
    /// **không** tự đoán thành công.
    /// </summary>
    public async Task<EInvoiceDto> DanhDauKetQuaCqtAsync(Guid id, DanhDauKetQuaCqtRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var ketQua = EInvoiceResponseParser.PhanTich(yeuCau.PhanHoiCqt);

        var hoaDon = await _hoaDon.GetAsync(id);

        if (ketQua.ThanhCong)
        {
            hoaDon.NhanKetQuaCQT(ketQua.MaCQT!);
        }
        else
        {
            hoaDon.BiTuChoiCQT(ketQua.MaLoi ?? "khong-ro");
        }

        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    public async Task<EInvoiceDto> GuiCqtAsync(Guid id)
    {
        var hoaDon = await _hoaDon.GetAsync(id);
        hoaDon.GuiCQT(Clock.Now);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    public async Task<EInvoiceDto> HuyAsync(Guid id, string lyDo)
    {
        var hoaDon = await _hoaDon.GetAsync(id);
        hoaDon.Huy(lyDo);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    public async Task<EInvoiceDto> LayAsync(Guid id) => EInvoiceDto.Tu(await _hoaDon.GetAsync(id));

    /// <summary>Danh sách hoá đơn theo trạng thái CQT (cho màn hình xử lý hoá đơn bị từ chối).</summary>
    public async Task<IReadOnlyList<EInvoiceDto>> LayTheoTrangThaiCqtAsync(TaxAuthorityStatus trangThai)
    {
        var ds = await _hoaDon.GetListAsync(x => x.TaxAuthorityStatus == trangThai);

        return ds.OrderBy(x => x.SoHoaDon, StringComparer.Ordinal).Select(EInvoiceDto.Tu).ToList();
    }
}
